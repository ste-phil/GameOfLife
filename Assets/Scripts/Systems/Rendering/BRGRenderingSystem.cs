using Unity.Entities;
using Unity.Burst;
using UnityEngine;
using Unity.Mathematics;
using System;
using UnityEngine.Rendering;
using UnityEngine.XR;
using Unity.Collections;
using Unity.Jobs;
using Unity.Collections.LowLevel.Unsafe;

namespace GameOfLife
{

    /// <summary>
    /// Not working as of now. This was supposed to run wiht my own shader, but turns out running with shader graph works great too.
    /// </summary>
    [BurstCompile]
    public unsafe partial class BRGREnderingSystemSystem : SystemBase
    {
        private BatchRendererGroup m_BatchRendererGroup; // BRG object
        private GraphicsBuffer m_graphicsBuffer; // GPU raw buffer (could be SSBO or UBO)
        private int m_alignedGPUWindowSize;
        private int m_maxInstancePerWindow;
        private int m_windowCount;
        private int m_totalGpuBufferSize;

        private int m_instanceCount;

        private BatchMeshID m_meshID;
        private BatchMaterialID m_materialID;
        private BatchID[] m_batchIds;

        private bool initialized = false;

        protected override void OnCreate()
        {
            RequireForUpdate<Config>();
            RequireForUpdate<SimulationState>();
            RequireForUpdate<BRGInstancedRenderingMode>();
        }


        protected override void OnStartRunning()
        {
            var simulationState = SystemAPI.GetSingleton<SimulationState>();
            var grid = simulationState.Grid;

            var mesh = MeshUtils.CreateQuad();
            var shader = Shader.Find("GameOfLife/QuadColorShaderDOTS");
            var mat = new Material(shader);
            mat.enableInstancing = true;

            m_BatchRendererGroup = new(this.OnPerformCulling, IntPtr.Zero);
            m_meshID = m_BatchRendererGroup.RegisterMesh(mesh);
            m_materialID = m_BatchRendererGroup.RegisterMaterial(mat);

            var instanceSize = 2; // size of the instance data in bytes
            m_alignedGPUWindowSize = (grid.CellCountWithoutGhostCells * instanceSize + 15) & (-16);
            m_maxInstancePerWindow = grid.CellCountWithoutGhostCells;
            m_windowCount = 1;
            m_totalGpuBufferSize = m_windowCount * m_alignedGPUWindowSize;
            m_graphicsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Raw, m_totalGpuBufferSize / 4, 4);

            var batchMetadata = new NativeArray<MetadataValue>(2, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

            int cellStatesId = Shader.PropertyToID("_CellStates");
            int cellIndicesId = Shader.PropertyToID("_CellIndices");

            m_batchIds = new BatchID[m_windowCount];
            for (int i = 0; i < m_windowCount; i++)
            {
                batchMetadata[0] = CreateMetadataValue(cellStatesId, 0, true);
                batchMetadata[1] = CreateMetadataValue(cellIndicesId, m_maxInstancePerWindow * 1, true);
                
                int offset = i * m_alignedGPUWindowSize;
                m_batchIds[i] = m_BatchRendererGroup.AddBatch(
                    batchMetadata, 
                    m_graphicsBuffer.bufferHandle, 
                    (uint)offset, 
                    0
                );
            }

            var bounds = new Bounds(new Vector3(0, 0, 0), new Vector3(1048576.0f, 1048576.0f, 1048576.0f));
            m_BatchRendererGroup.SetGlobalBounds(bounds);
            
            initialized = true;
        }

        protected override void OnDestroy()
        {
            if (initialized)
            {
                for (uint i = 0; i < m_windowCount; i++)
                    m_BatchRendererGroup.RemoveBatch(m_batchIds[i]);

                m_BatchRendererGroup.UnregisterMaterial(m_materialID);
                m_BatchRendererGroup.UnregisterMesh(m_meshID);
                m_BatchRendererGroup.Dispose();
                m_graphicsBuffer.Dispose();
            }
        }


        [BurstCompile]
        private JobHandle OnPerformCulling(BatchRendererGroup rendererGroup, BatchCullingContext cullingContext, BatchCullingOutput cullingOutput, IntPtr userContext)
        {
            if (!initialized)
                return new JobHandle();

            BatchCullingOutputDrawCommands drawCommands = new BatchCullingOutputDrawCommands();

            // calculate the amount of draw commands we need in case of UBO mode (one draw command per window)
            int drawCommandCount = (m_instanceCount + m_maxInstancePerWindow - 1) / m_maxInstancePerWindow;
            int maxInstancePerDrawCommand = m_maxInstancePerWindow;
            drawCommands.drawCommandCount = drawCommandCount;

            // Allocate a single BatchDrawRange. ( all our draw commands will refer to this BatchDrawRange)
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
                    receiveShadows = true,
                    staticShadowCaster = false,
                    allDepthSorted = false
                }
            };

            if (drawCommands.drawCommandCount > 0)
            {
                // as we don't need culling, the visibility int array buffer will always be {0,1,2,3,...} for each draw command
                // so we just allocate maxInstancePerDrawCommand and fill it
                int visibilityArraySize = maxInstancePerDrawCommand;
                if (m_instanceCount < visibilityArraySize)
                    visibilityArraySize = m_instanceCount;

                drawCommands.visibleInstances = Malloc<int>((uint)visibilityArraySize);

                // As we don't need any frustum culling in our context, we fill the visibility array with {0,1,2,3,...}
                for (int i = 0; i < visibilityArraySize; i++)
                    drawCommands.visibleInstances[i] = i;

                // Allocate the BatchDrawCommand array (drawCommandCount entries)
                // In SSBO mode, drawCommandCount will be just 1
                drawCommands.drawCommands = Malloc<BatchDrawCommand>((uint)drawCommandCount);
                int left = m_instanceCount;
                for (int i = 0; i < drawCommandCount; i++)
                {
                    int inBatchCount = left > maxInstancePerDrawCommand ? maxInstancePerDrawCommand : left;
                    drawCommands.drawCommands[i] = new BatchDrawCommand
                    {
                        visibleOffset = (uint)0,    // all draw command is using the same {0,1,2,3...} visibility int array
                        visibleCount = (uint)inBatchCount,
                        batchID = m_batchIds[i],
                        materialID = m_materialID,
                        meshID = m_meshID,
                        submeshIndex = 0,
                        splitVisibilityMask = 0xff,
                        flags = BatchDrawCommandFlags.None,
                        sortingPosition = 0
                    };
                    left -= inBatchCount;
                }
            }

            cullingOutput.drawCommands[0] = drawCommands;
            drawCommands.instanceSortingPositions = null;
            drawCommands.instanceSortingPositionFloatCount = 0;

            return new JobHandle();
        }

        protected override void OnUpdate()
        {
            var simulationState = SystemAPI.GetSingleton<SimulationState>();
            var grid = simulationState.Grid;

            UploadGpuData(ref grid, grid.CellCountWithoutGhostCells);
        }

        [BurstCompile]
        public bool UploadGpuData(ref SimulationGrid grid, int instanceCount)
        {
            if ((uint)instanceCount > (uint)m_maxInstancePerWindow)
                return false;

            m_instanceCount = instanceCount;
            int completeWindows = m_instanceCount / m_maxInstancePerWindow;

            var cellStates = new NativeArray<byte>(instanceCount, Allocator.Temp);
            for (int i = 0; i < grid.CellCountWithoutGhostCells; i++)
            {
                int x = i % grid.AxisSize;
                int y = i / grid.AxisSize;

                cellStates[i] = (byte)(grid.GetCell(x + 1, y + 1) != 0 ? 1 : 0);
            }


            //Means that we can only have 255 instances!!!!
            var cellIndices = new NativeArray<byte>(instanceCount, Allocator.Temp);
            for (int i = 0; i < grid.CellCountWithoutGhostCells; i++)
            {
                cellIndices[i] = (byte) i;
            }


            //Cell states
            m_graphicsBuffer.SetData(cellStates, 0, 0, m_maxInstancePerWindow);
            
            //Cell indices
            m_graphicsBuffer.SetData(cellIndices, 0, m_maxInstancePerWindow * 1, m_maxInstancePerWindow);

            return true;
        }


        private static T* Malloc<T>(uint count) where T : unmanaged
        {
            return (T*)UnsafeUtility.Malloc(
                UnsafeUtility.SizeOf<T>() * count,
                UnsafeUtility.AlignOf<T>(),
                Allocator.TempJob);
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