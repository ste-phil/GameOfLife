using Unity.Entities;
using Unity.Burst;
using Unity.Jobs;
using UnityEngine;

namespace GameOfLife
{
	[BurstCompile]
	public partial struct ChunkSimulationSystem : ISystem
	{
        private float elapsedTime;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Config>();
            state.RequireForUpdate<ActiveSimulation>();
            state.RequireForUpdate<ChunkSimulationState>();
        }

        [BurstCompile]
		public void OnUpdate(ref SystemState state)
		{
            ref var simState = ref SystemAPI.GetSingletonRW<ChunkSimulationState>().ValueRW;
            var config = SystemAPI.GetSingleton<Config>();

            elapsedTime += SystemAPI.Time.DeltaTime;
            if (elapsedTime < 1f / config.UpdatesPerSecond)
                return;
            elapsedTime = 0.0f;

            var handle = new SimulationJob
            {
                Grid = simState.Grid,
                NextGrid = simState.NextGrid
            }.Schedule(simState.Grid.AxisSize * simState.Grid.AxisSize, 256);
            handle.Complete();

            //Swap buffers
            var temp = simState.Grid;
            simState.Grid = simState.NextGrid;
            simState.NextGrid = temp;
        }


        [BurstCompile]
        public struct SimulationJob : IJobParallelFor
        {
            public ChunkSimulationGrid Grid;
            public ChunkSimulationGrid NextGrid;

            public void Execute(int idx)
            {
                int x = (idx % Grid.AxisSize);
                int y = (idx / Grid.AxisSize);

                var aliveNeighbours = 0;
                aliveNeighbours += Grid.GetCell(x + 1, y);
                aliveNeighbours += Grid.GetCell(x - 1, y);
                aliveNeighbours += Grid.GetCell(x, y - 1);
                aliveNeighbours += Grid.GetCell(x, y + 1);

                aliveNeighbours += Grid.GetCell(x + 1, y + 1);
                aliveNeighbours += Grid.GetCell(x - 1, y + 1);
                aliveNeighbours += Grid.GetCell(x + 1, y - 1);
                aliveNeighbours += Grid.GetCell(x - 1, y - 1);

                var cellState = Grid.GetCell(x, y);
                var newCellState = aliveNeighbours == 3 || (cellState == 1 && aliveNeighbours == 2);
                NextGrid.SetCell(x, y, newCellState);
            }
        }
    }
}