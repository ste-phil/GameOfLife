using Unity.Entities;
using Unity.Burst;
using GameOfLife;
using UnityEngine;
using Unity.Mathematics;
using Unity.Jobs;
using System;
using System.Linq;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs.LowLevel.Unsafe;

namespace GameOfLife
{
	[BurstCompile]
	public partial struct InitializationSystem : ISystem
	{
		[BurstCompile]
		public void OnCreate(ref SystemState state)
		{
            state.RequireForUpdate<Config>();
		}

		//[BurstCompile]
		public void OnUpdate(ref SystemState state)
		{
            var query = SystemAPI.QueryBuilder().WithAll<Config, InitialAliveCells>().Build();
            if (query.CalculateEntityCountWithoutFiltering() == 0)
                return;

            ref var config = ref query.GetSingletonRW<Config>().ValueRW;

          


            var initialized = SystemAPI.QueryBuilder().WithAll<ActiveSimulation>().Build().CalculateEntityCount() > 0;
            if (!initialized)
            {
                var seed = (uint)UnityEngine.Random.Range(1, 100000);
                var rnds = new NativeArray<Unity.Mathematics.Random>(JobsUtility.MaxJobThreadCount, Allocator.TempJob);
                for (int i = 0; i < rnds.Length; i++)
                {
                    rnds[i] = new Unity.Mathematics.Random(seed *= 3);
                }
                Debug.Log("Seed: " + seed);

                switch (config.RenderingMode)
                {
                    case RenderingMode.BRGMultiCellGraphInstanced:
                        {
                            Camera.main.orthographicSize = config.GridSize / 16.0f;
                            config.OrthographicSize = Camera.main.orthographicSize;

                            var newSimState = new ChunkSimulationState();
                            newSimState.Grid = new ChunkSimulationGrid(config.GridSize);
                            newSimState.NextGrid = new ChunkSimulationGrid(config.GridSize);

                            if (config.UseRandomInitialization)
                            {
                                new InitChunkJob
                                {
                                    Random = rnds,
                                    GridSize = config.GridSize,
                                    Grid = newSimState.Grid
                                }.Schedule(config.GridSize * config.GridSize, 256).Complete();
                                rnds.Dispose();
                            }
                            else
                            {
                                var initCells = query.GetSingletonBuffer<InitialAliveCells>();
                                for (var i = 0; i < initCells.Length; i++)
                                {
                                    newSimState.Grid.SetCell(initCells[i].Value.x, initCells[i].Value.y, true);
                                }

                            }


                            Debug.Log("Required memory: " + BitsToHumanReadable(2 * newSimState.Grid.MemoryConsumption));

                            var e = state.EntityManager.CreateEntity(typeof(ChunkSimulationState), typeof(ActiveSimulation));
                            state.EntityManager.SetComponentData(e, newSimState);
                            state.EntityManager.AddComponentData(e, new BRGMultiCellGraphInstancedRenderingMode());
                                   
                            break;
                        }
                    case RenderingMode.BRGGraphInstanced:
                    case RenderingMode.MeshInstanced:
                    case RenderingMode.BRGInstanced:
                        {
                            Camera.main.orthographicSize = config.GridSize / 2.0f;
                            config.OrthographicSize = Camera.main.orthographicSize;

                            var newSimState = new SimulationState();
                            newSimState.Grid = new SimulationGrid(config.GridSize, 8);
                            newSimState.NextGrid = new SimulationGrid(config.GridSize, 8);

                            if (config.UseRandomInitialization)
                            {
                                new InitJob
                                {
                                    Random = rnds,
                                    GridSize = config.GridSize,
                                    Grid = newSimState.Grid
                                }.Schedule(config.GridSize * config.GridSize, 256).Complete();
                                rnds.Dispose();
                            }
                            else
                            {
                                var initCells = query.GetSingletonBuffer<InitialAliveCells>();
                                for (var i = 0; i < initCells.Length; i++)
                                {
                                    newSimState.Grid.SetCell(initCells[i].Value.x, initCells[i].Value.y, true);
                                }

                            }

                            Debug.Log("Required memory: " + BitsToHumanReadable(2 * newSimState.Grid.MemoryConsumption));

                            var e = state.EntityManager.CreateEntity(typeof(SimulationState), typeof(ActiveSimulation));
                            state.EntityManager.SetComponentData(e, newSimState);

                            switch (config.RenderingMode)
                            {
                                case RenderingMode.MeshInstanced:
                                    state.EntityManager.AddComponentData(e, new MeshInstacedRenderingMode());
                                    break;
                                case RenderingMode.BRGInstanced:
                                    state.EntityManager.AddComponentData(e, new BRGInstancedRenderingMode());
                                    break;
                                case RenderingMode.BRGGraphInstanced:
                                    state.EntityManager.AddComponentData(e, new BRGGraphInstancedRenderingMode());
                                    break;
                                default:
                                    break;
                            }
                            break;
                        }
                    default:
                        throw new System.Exception("Not implemented");
                }
            }
        }

        [BurstCompile]
        struct InitChunkJob : IJobParallelFor
        {
            public int GridSize;
            public ChunkSimulationGrid Grid;

            [NativeDisableParallelForRestriction]
            public NativeArray<Unity.Mathematics.Random> Random;

            [NativeSetThreadIndex]
            private int threadIdx;

            public void Execute(int idx)
            {
                int x = (idx % Grid.AxisSize);
                int y = (idx / Grid.AxisSize);

                var rnd = Random[threadIdx];
                var alive = rnd.NextBool();

                Random[threadIdx] = rnd;
                Grid.SetCell(x, y, alive);
            }
        }

        [BurstCompile]
        struct InitJob : IJobParallelFor
        {
            public int GridSize;
            public SimulationGrid Grid;

            [NativeDisableParallelForRestriction]
            public NativeArray<Unity.Mathematics.Random> Random;

            [NativeSetThreadIndex]
            private int threadIdx;

            public void Execute(int idx)
            {
                int x = (idx % Grid.AxisSize);
                int y = (idx / Grid.AxisSize);

                var rnd = Random[threadIdx];
                var alive = rnd.NextBool();

                Random[threadIdx] = rnd;
                Grid.SetCell(x, y, alive);
            }
        }


        public static string BitsToHumanReadable(long bits)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double size = bits / 8;
            int index = 0;

            while (size >= 1024 && index < sizes.Length - 1)
            {
                size /= 1024;
                index++;
            }

            return $"{size:0.##} {sizes[index]}";
        }

        public void OnDestroy(ref SystemState state)
        {
            var query = SystemAPI.QueryBuilder().WithAll<SimulationState>().Build();
            state.EntityManager.DestroyEntity(query);
        }
	}
}