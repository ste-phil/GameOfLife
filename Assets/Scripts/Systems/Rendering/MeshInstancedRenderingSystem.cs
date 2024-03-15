using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering.Universal;

namespace GameOfLife
{
    [BurstCompile]
    public partial class MeshInstancedRenderingSystem : SystemBase
    {
        public class BatchRenderingData
        {
            public MaterialPropertyBlock PropertyBlock;
            public ComputeBuffer ComputeBuffer;
            public Matrix4x4[] Matrices;
        }

        private Mesh quadMesh;
        private Material material;
        //private ComputeBuffer cellStateBuffer;
        private List<BatchRenderingData> instanceBatchRenderingData;

        public const int MAX_INSTANCES_PER_DRAW = 416;

        private static readonly ProfilerMarker renderInstancesMarker = new ProfilerMarker("RenderInstances");
        private static readonly ProfilerMarker fillComputeBufferMarker = new ProfilerMarker("FillComputeBuffer");

        protected override void OnCreate()
        {
            RequireForUpdate<Config>();
            RequireForUpdate<SimulationState>();
            RequireForUpdate<MeshInstacedRenderingMode>();

            quadMesh = MeshUtils.CreateQuad();
            var shader = Shader.Find("GameOfLife/QuadColorShader");
            material = new Material(shader);
            material.enableInstancing = true;
        }


        protected override void OnStartRunning()
        {
            var simulationState = SystemAPI.GetSingleton<SimulationState>();
            var grid = simulationState.Grid;

            var drawCalls = GetRequiredDrawCalls(simulationState.Grid.CellCountWithoutGhostCells);
            instanceBatchRenderingData = new();
            for (int i = 0; i < drawCalls; i++)
            {
                var offset = MAX_INSTANCES_PER_DRAW * i;
                var count = MAX_INSTANCES_PER_DRAW;

                var matrices = new Matrix4x4[count];
                var centerOffset = grid.AxisSize / 2.0f - .5f;
                for (int j = offset; j < offset + count; j++)
                {
                    var xPos = (j % grid.AxisSize) - centerOffset;
                    var yPos = (j / grid.AxisSize) - centerOffset;

                    matrices[j - offset] = new float4x4(
                        quaternion.RotateY(math.PI),
                        new float3(xPos, yPos, 0)
                    );
                }

                var computeBuffer = new ComputeBuffer(MAX_INSTANCES_PER_DRAW, sizeof(int), ComputeBufferType.Structured, ComputeBufferMode.SubUpdates);

                var props = new MaterialPropertyBlock();
                props.SetBuffer("_CellStates", computeBuffer);

                instanceBatchRenderingData.Add(new BatchRenderingData
                {
                    ComputeBuffer = computeBuffer,
                    Matrices = matrices,
                    PropertyBlock = props
                });
            }
        }

        protected override void OnStopRunning()
        {
            for (int i = 0; i < instanceBatchRenderingData.Count; i++)
            {
                instanceBatchRenderingData[i].ComputeBuffer.Dispose();
            }
        }

        protected override void OnUpdate()
        {
            var simulationState = SystemAPI.GetSingleton<SimulationState>();
            var grid = simulationState.Grid;

            var fullDrawCalls = grid.CellCountWithoutGhostCells / MAX_INSTANCES_PER_DRAW;
            var lastDrawCallAmount = grid.CellCountWithoutGhostCells % MAX_INSTANCES_PER_DRAW;
            for (int i = 0; i < fullDrawCalls; i++)
            {
                RenderInstances(ref grid, instanceBatchRenderingData[i], i * MAX_INSTANCES_PER_DRAW, MAX_INSTANCES_PER_DRAW);
            }

            if (lastDrawCallAmount > 0)
                RenderInstances(ref grid, instanceBatchRenderingData[^1], fullDrawCalls * MAX_INSTANCES_PER_DRAW, lastDrawCallAmount);
        }

        private int GetRequiredDrawCalls(int cellCount)
        {
            return cellCount / MAX_INSTANCES_PER_DRAW + (cellCount % MAX_INSTANCES_PER_DRAW == 0 ? 0 : 1);
        }


        /// <summary>
        /// This is required since there is an upper limit of 1023 instances per draw call.
        /// Only 511 instances in this case since  objectToWorld and worldToObject matrices are used
        /// See https://docs.unity3d.com/2023.3/Documentation/ScriptReference/Graphics.RenderMeshInstanced.html
        /// </summary>
        /// <param name="state"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        private void RenderInstances(ref SimulationGrid grid, BatchRenderingData renderingData, int offset, int count)
        {
            FillComputeBuffer2(ref grid, renderingData.ComputeBuffer, offset, count);

            renderInstancesMarker.Begin();
            Graphics.DrawMeshInstanced(
                quadMesh,
                0,
                material,
                renderingData.Matrices,
                renderingData.Matrices.Length,
                renderingData.PropertyBlock,
                UnityEngine.Rendering.ShadowCastingMode.Off,
                false,
                0
            );
            renderInstancesMarker.End();
        }

        private static void FillComputeBuffer(ref SimulationGrid grid, ComputeBuffer buffer, int offset, int count)
        {
            fillComputeBufferMarker.Begin();
            var arr = buffer.BeginWrite<int>(0, count);
            for (int i = offset; i < offset + count; i++)
            {
                int x = i % grid.AxisSize;
                int y = i / grid.AxisSize;

                arr[i - offset] = (int)grid.GetCell(x + 1, y + 1) != 0 ? 1 : 0;
            }

            //CheckComputeBuffer(arr, offset, count);

            buffer.EndWrite<int>(count);
            fillComputeBufferMarker.End();
        }

        private static void FillComputeBuffer2(ref SimulationGrid grid, ComputeBuffer buffer, int offset, int count)
        {
            var arr = buffer.BeginWrite<int>(0, count);
            FillBuffer(ref grid, ref arr, offset, count);
            buffer.EndWrite<int>(count);
        }

        [BurstCompile]
        private static void FillBuffer(ref SimulationGrid grid, ref NativeArray<int> arr, int offset, int count)
        {
            fillComputeBufferMarker.Begin();
            for (int i = offset; i < offset + count; i++)
            {
                int x = i % grid.AxisSize;
                int y = i / grid.AxisSize;

                arr[i - offset] = (int)(grid.GetCell(x + 1, y + 1) != 0 ? 1 : 0);
            }

            //CheckComputeBuffer(arr, offset, count);
            fillComputeBufferMarker.End();
        }


        private void CheckComputeBuffer(NativeArray<int> arr, int offset, int count)
        {
            int xc = 0;
            for (int i = 0; i < arr.Length; i++)
            {
                if (arr[i] == 1)
                    xc++;
            }
            Debug.Log($"Count: [{offset},{offset + count}]: " + xc);
        }
    }
}