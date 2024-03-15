using Unity.Entities;
using Unity.Burst;
using UnityEngine;
using Unity.Mathematics;
using System;
using UnityEngine.Rendering;
using Unity.Collections;
using Unity.Jobs;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Profiling;

namespace GameOfLife
{
    /// <summary>
    /// Renders the grid using a single quad for 32 cells in a 8x4 subgrid
    /// </summary>
    [BurstCompile]
    public unsafe partial class BRGMultiCellGraphInstancedRenderingSystem : SystemBase
    {
        public unsafe struct RenderingData
        {
            public BatchMeshID MeshId;
            public BatchMaterialID MaterialId;
            public BatchID BatchId;
            public int InstanceCount;
            public int CellsPerInstance;
            public bool Initialized;
            public NativeArray<int> VisibleInstances;
        }

        private static readonly ProfilerMarker fillBufferMarker = new ProfilerMarker("FillBuffer");
        private static readonly ProfilerMarker uploadDataMarker = new ProfilerMarker("UploadData");
        private static readonly ProfilerMarker fillVisibleInstancesMarker = new ProfilerMarker("FillVisibleInstancesMarker");

        private BatchRendererGroup m_BatchRendererGroup;
        private GraphicsBuffer m_graphicsBuffer;
        private int m_alignedGPUBufferSize;

        private int uploadCount = 0;

        private RenderingData* renderingData;

        protected override void OnCreate()
        {
            RequireForUpdate<Config>();
            RequireForUpdate<ChunkSimulationState>();
            RequireForUpdate<BRGMultiCellGraphInstancedRenderingMode>();

            renderingData = Malloc<RenderingData>(1, Allocator.Persistent);
            renderingData->Initialized = false;
        }


        protected override void OnStartRunning()
        {
            var simulationState = SystemAPI.GetSingleton<ChunkSimulationState>();
            var grid = simulationState.Grid;

            var mesh = MeshUtils.CreateQuad();
            var shader = Shader.Find("Shader Graphs/QuadColorGraphMultiCellDOTS");
            var mat = new Material(shader);


            m_BatchRendererGroup = new(OnPerformCulling, new IntPtr(renderingData));

            //16 bytes for float4
            //3 float4 for localToWorld
            //3 float4 for worldToLocal
            //1 float for cellStates

            var cellsPerInstance = 32; // 4 bytes -> 32 bits -> we need 1 bit per cell!
            var instanceSize = 2 * 3 * 16 + 4; // size of the instance data in bytes
            var instanceCount = grid.CellCountWithoutGhostCells / cellsPerInstance;

            if (grid.CellCountWithoutGhostCells % cellsPerInstance != 0)
                throw new InvalidOperationException("Cell count must be a multiple of 32");

            Debug.Log("InstanceCount: " + instanceCount);

            m_alignedGPUBufferSize = (instanceCount * instanceSize + 15) & (-16);
            m_graphicsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Raw, m_alignedGPUBufferSize / 4, 4); //Stride must be a multiple of 4

            var batchMetadata = new NativeArray<MetadataValue>(3, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

            int shaderIdObjectToWorld = Shader.PropertyToID("unity_ObjectToWorld");
            int shaderIdWorldToObject = Shader.PropertyToID("unity_WorldToObject");
            int shaderIdCellStates = Shader.PropertyToID("_CellStatesX");

            batchMetadata[0] = CreateMetadataValue(shaderIdObjectToWorld, 0, true);
            batchMetadata[1] = CreateMetadataValue(shaderIdWorldToObject, instanceCount * 3 * 16, true);
            batchMetadata[2] = CreateMetadataValue(shaderIdCellStates, instanceCount * 3 * 16 * 2, true);

            var batchId = m_BatchRendererGroup.AddBatch(batchMetadata, m_graphicsBuffer.bufferHandle, 0, 0);

            //Set very large bounds so that the culling system doesn't cull anything
            var bounds = new Bounds(new Vector3(0, 0, 0), new Vector3(1048576.0f, 1048576.0f, 1048576.0f));
            m_BatchRendererGroup.SetGlobalBounds(bounds);


            renderingData->InstanceCount = instanceCount;
            renderingData->CellsPerInstance = cellsPerInstance;
            renderingData->MaterialId = m_BatchRendererGroup.RegisterMaterial(mat);
            renderingData->MeshId = m_BatchRendererGroup.RegisterMesh(mesh);
            renderingData->BatchId = batchId;

            renderingData->VisibleInstances = new NativeArray<int>(instanceCount, Allocator.Persistent);
            new FillVisibleInstances
            {
                VisibleInstances = renderingData->VisibleInstances
            }.Schedule(instanceCount, 256).Complete();

            
            UploadStaticGpuData(ref grid);

            renderingData->Initialized = true;
        }

        protected override void OnStopRunning()
        {
            if (renderingData->Initialized)
            {
                m_BatchRendererGroup.RemoveBatch(renderingData->BatchId);

                m_BatchRendererGroup.UnregisterMaterial(renderingData->MaterialId);
                m_BatchRendererGroup.UnregisterMesh(renderingData->MeshId);
                m_BatchRendererGroup.Dispose();
                m_graphicsBuffer.Dispose();

                renderingData->VisibleInstances.Dispose();
            }

            UnsafeUtility.Free(renderingData, Allocator.Persistent);
        }

        protected override void OnDestroy()
        {
            if (renderingData->Initialized)
            {
                m_BatchRendererGroup.RemoveBatch(renderingData->BatchId);

                m_BatchRendererGroup.UnregisterMaterial(renderingData->MaterialId);
                m_BatchRendererGroup.UnregisterMesh(renderingData->MeshId);
                m_BatchRendererGroup.Dispose();
                m_graphicsBuffer.Dispose();

                renderingData->VisibleInstances.Dispose();
            }

            UnsafeUtility.Free(renderingData, Allocator.Persistent);
        }


        private static JobHandle OnPerformCulling(BatchRendererGroup rendererGroup, BatchCullingContext cullingContext, BatchCullingOutput cullingOutput, IntPtr userContext)
        {
            var data = (RenderingData*)userContext.ToPointer();

            if (!data->Initialized)
                return new JobHandle();

            BatchCullingOutputDrawCommands drawCommands = new BatchCullingOutputDrawCommands();

            // calculate the amount of draw commands we need in case of UBO mode (one draw command per window)
            int drawCommandCount = 1;

            var instanceCount = (uint)data->InstanceCount;
            drawCommands.drawCommandCount = drawCommandCount;

            // Allocate a single BatchDrawRange. (all our draw commands will refer to this BatchDrawRange)
            drawCommands.drawRangeCount = 1;
            drawCommands.drawRanges = Malloc<BatchDrawRange>(1);
            drawCommands.drawRanges[0] = new BatchDrawRange
            {
                drawCommandsBegin = 0,
                drawCommandsCount = (uint)drawCommandCount,
                filterSettings = new BatchFilterSettings
                {
                    renderingLayerMask = 1,
                    layer = 0,
                    motionMode = MotionVectorGenerationMode.Camera,
                    shadowCastingMode = ShadowCastingMode.Off,
                    receiveShadows = false,
                    staticShadowCaster = false,
                    allDepthSorted = false
                }
            };

            if (drawCommands.drawCommandCount > 0)
            {
                // as we don't need culling, the visibility int array buffer will always be {0,1,2,3,...} for each draw command
                fillVisibleInstancesMarker.Begin();

                drawCommands.visibleInstances = Malloc<int>((uint)instanceCount);
                UnsafeUtility.MemCpy(drawCommands.visibleInstances, data->VisibleInstances.GetUnsafePtr(), instanceCount * sizeof(int));

                fillVisibleInstancesMarker.End();

                drawCommands.drawCommands = Malloc<BatchDrawCommand>((uint)drawCommandCount);
                drawCommands.drawCommands[0] = new BatchDrawCommand
                {
                    visibleOffset = (uint)0,
                    visibleCount = (uint)instanceCount,
                    batchID = data->BatchId,
                    materialID = data->MaterialId,
                    meshID = data->MeshId,
                    submeshIndex = 0,
                    splitVisibilityMask = 0xff,
                    flags = BatchDrawCommandFlags.None,
                    sortingPosition = 0
                };
            }

            cullingOutput.drawCommands[0] = drawCommands;
            drawCommands.instanceSortingPositions = null;
            drawCommands.instanceSortingPositionFloatCount = 0;

            return new JobHandle();
        }

        protected override void OnUpdate()
        {
            if (!renderingData->Initialized)
                return;

            var simulationState = SystemAPI.GetSingleton<ChunkSimulationState>();
            var grid = simulationState.Grid;

            //if (uploadCount == 1)
            //    return;

            var cellStateSize = renderingData->InstanceCount;
            var arr = new NativeArray<uint>(cellStateSize, Allocator.Temp);
            
            FillBuffer(ref grid, ref arr);

            //arr[0] = 0b1111_1111__1111_1111__1111_1111__1111_1111;
            //arr[1] = 0b1000_0000__0000_0000__0000_0000__0000_0011;
            //arr[2] = 0;
            //arr[3] = 0;
            UploadGpuData(ref arr);

            uploadCount++;
            arr.Dispose();
        }


        private unsafe static void FillBuffer(ref ChunkSimulationGrid grid, ref NativeArray<uint> arr)
        {
            var x = (byte*)arr.GetUnsafePtr();

            var chunksPerAxis = grid.AxisSize / ChunkSimulationGrid.ChunkSize;

            fillBufferMarker.Begin();

            var dst = (byte*)arr.GetUnsafePtr();
            var src = (byte*)grid.GetUnsafePtr;

            //skip first ghost layer row (2 * 4 byte padding (=2 chunks) + 1 byte row ghost data)
            src += (2 + chunksPerAxis.x) * ChunkSimulationGrid.ChunkSizeBytes;
            //skip first ghost layer column
            src += ChunkSimulationGrid.ChunkSizeBytes;         
            for (int i = 0; i < chunksPerAxis.y; i++)
            {
                UnsafeUtility.MemCpy(dst, src, chunksPerAxis.x * ChunkSimulationGrid.ChunkSizeBytes);
                dst += chunksPerAxis.x * ChunkSimulationGrid.ChunkSizeBytes;
                src += (2 + chunksPerAxis.x) * ChunkSimulationGrid.ChunkSizeBytes;
            }

            fillBufferMarker.End();
        }



        /// <summary>
        /// The cell state data that changes every frame
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="cellStates"></param>
        public void UploadGpuData<T>(ref NativeArray<T> cellStates) where T : unmanaged
        {
            var instanceCount = renderingData->InstanceCount;


            uploadDataMarker.Begin();

            //Offset is a byte size of mulitples of sizeof(T)
            m_graphicsBuffer.SetData(cellStates, 0, instanceCount * 3 * 2 * 4, instanceCount);

            uploadDataMarker.End();
        }

        /// <summary>
        /// GPU data that is static and doesn't change every frame
        /// </summary>
        /// <param name="grid"></param>
        private void UploadStaticGpuData(ref ChunkSimulationGrid grid)
        {
            var instanceCount = renderingData->InstanceCount;
            var instaceAxisCount = grid.AxisSize / new int2(8, 4);

            float2 centerOffset = instaceAxisCount / new float2(2.0f) - .5f;
            
            var localToWorlds = new NativeArray<float4>(instanceCount * 3, Allocator.TempJob);
            new FillLocalToWorldMatrix
            {
                AxisSize = instaceAxisCount.x,
                CenterOffset = centerOffset,
                LocalToWorldArray = localToWorlds
            }.Schedule(instanceCount, 256).Complete();

            //shader graph matrices
            m_graphicsBuffer.SetData(localToWorlds, 0, 0, instanceCount * 3 /*3 x float4*/);
            m_graphicsBuffer.SetData(localToWorlds, 0, instanceCount * 3, instanceCount * 3 /*3 x float4*/);

            localToWorlds.Dispose();
        }


        [BurstCompile]
        private partial struct FillLocalToWorldMatrix : IJobParallelFor
        {
            public int AxisSize;
            public float2 CenterOffset;

            [NativeDisableParallelForRestriction] public NativeArray<float4> LocalToWorldArray;

            public void Execute(int i)
            {
                var xPos = (i % AxisSize) - CenterOffset.x;
                var yPos = (i / AxisSize) - CenterOffset.y;

                //See https://blog.unity.com/engine-platform/batchrenderergroup-sample-high-frame-rate-on-budget-devices
                // on how to set the unity_ObjectToWorld data that unity's shader graph expects

                var arrayIdx = i * 3;
                LocalToWorldArray[arrayIdx + 0] = new float4(1, 0, 0, 0);
                LocalToWorldArray[arrayIdx + 1] = new float4(.5f, 0, 0, 0);
                LocalToWorldArray[arrayIdx + 2] = new float4(1, xPos, yPos * .5f, 0);
            }
        }

        [BurstCompile]
        private partial struct FillBufferJob : IJobParallelFor
        {
            public int AxisSize;
            [NativeDisableParallelForRestriction] public NativeArray<float> CellStates;
            [NativeDisableParallelForRestriction] public SimulationGrid Grid;

            public void Execute(int i)
            {
                int x = i % AxisSize;
                int y = i / AxisSize;

                CellStates[i] = (float)Grid.GetCell(x + 1, y + 1) != 0 ? 1 : 0;
            }
        }

        [BurstCompile]
        private partial struct FillVisibleInstances : IJobParallelFor
        {
            [NativeDisableParallelForRestriction] public NativeArray<int> VisibleInstances;

            public void Execute(int i)
            {
                VisibleInstances[i] = i;
            }
        }

        private static T* Malloc<T>(uint count, Allocator allocator = Allocator.TempJob) where T : unmanaged
        {
            return (T*)UnsafeUtility.Malloc(
                UnsafeUtility.SizeOf<T>() * count,
                UnsafeUtility.AlignOf<T>(),
                allocator
            );
        }

        static MetadataValue CreateMetadataValue(int nameID, int gpuOffset, bool isPerInstance)
        {
            const uint kIsPerInstanceBit = 0x80000000;
            return new MetadataValue
            {
                NameID = nameID,
                Value = (uint)gpuOffset | (isPerInstance ? (kIsPerInstanceBit) : 0),
            };
        }
    }
}