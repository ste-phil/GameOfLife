using Unity.Entities;
using Unity.Burst;
using Unity.Jobs;
using UnityEngine;

namespace GameOfLife
{
	[BurstCompile]
	public partial struct SimulationSystem : ISystem
	{
        private float elapsedTime;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Config>();
            state.RequireForUpdate<ActiveSimulation>();
            state.RequireForUpdate<SimulationState>();
        }

        [BurstCompile]
		public void OnUpdate(ref SystemState state)
		{
            ref var simState = ref SystemAPI.GetSingletonRW<SimulationState>().ValueRW;
            var config = SystemAPI.GetSingleton<Config>();

            elapsedTime += SystemAPI.Time.DeltaTime;
            if (elapsedTime < 1f / config.UpdatesPerSecond)
                return;
            elapsedTime = 0.0f;

            //for (int y = 1; y < simState.Grid.AxisSizeWithGhostCells - 1; y++)
            //{
            //    for (var x = 1; x < simState.Grid.AxisSizeWithGhostCells - 1; x++)
            //    {
            //        var idx = x + y * simState.Grid.AxisSizeWithGhostCells;

            //        var aliveNeighbours = 0;

            //        //horizontal and vertical
            //        aliveNeighbours += (int)simState.Grid.GetCell(idx + 1);
            //        aliveNeighbours += (int)simState.Grid.GetCell(idx - 1);
            //        aliveNeighbours += (int)simState.Grid.GetCell(idx + simState.Grid.AxisSizeWithGhostCells);
            //        aliveNeighbours += (int)simState.Grid.GetCell(idx - simState.Grid.AxisSizeWithGhostCells);

            //        //diagonal
            //        aliveNeighbours += (int)simState.Grid.GetCell(idx + 1 + simState.Grid.AxisSizeWithGhostCells);
            //        aliveNeighbours += (int)simState.Grid.GetCell(idx - 1 - simState.Grid.AxisSizeWithGhostCells);
            //        aliveNeighbours += (int)simState.Grid.GetCell(idx - 1 + simState.Grid.AxisSizeWithGhostCells);
            //        aliveNeighbours += (int)simState.Grid.GetCell(idx + 1 - simState.Grid.AxisSizeWithGhostCells);

            //        var cellState = (int)simState.Grid.GetCell(idx);

            //        var newCellState = aliveNeighbours == 3 || (cellState == 1 && aliveNeighbours == 2);
            //        simState.NextGrid.SetCell(idx, newCellState);
            //    }
            //}

            //var size = simState.Grid.AxisSize * simState.Grid.AxisSize;
            //for (int i = 0; i < size; i++)
            //{
            //    int x = (i % simState.Grid.AxisSize);
            //    int y = (i / simState.Grid.AxisSize);

            //    var aliveNeighbours = 0;
            //    aliveNeighbours += (int)simState.Grid.GetCell(x + 1, y);
            //    aliveNeighbours += (int)simState.Grid.GetCell(x - 1, y);
            //    aliveNeighbours += (int)simState.Grid.GetCell(x, y + 1);
            //    aliveNeighbours += (int)simState.Grid.GetCell(x, y - 1);

            //    aliveNeighbours += (int)simState.Grid.GetCell(x + 1, y + 1);
            //    aliveNeighbours += (int)simState.Grid.GetCell(x - 1, y + 1);
            //    aliveNeighbours += (int)simState.Grid.GetCell(x + 1, y - 1);
            //    aliveNeighbours += (int)simState.Grid.GetCell(x - 1, y - 1);

            //    var cellState = (int)simState.Grid.GetCell(x, y);
            //    var newCellState = aliveNeighbours == 3 || (cellState == 1 && aliveNeighbours == 2);
            //    simState.NextGrid.SetCell(x, y, newCellState);
            //}

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
            public SimulationGrid Grid;
            public SimulationGrid NextGrid;

            public void Execute(int idx)
            {
                int x = (idx % Grid.AxisSize);
                int y = (idx / Grid.AxisSize);

                var aliveNeighbours = 0;
                aliveNeighbours += (int) Grid.GetCell(x + 1, y);
                aliveNeighbours += (int) Grid.GetCell(x - 1, y);
                aliveNeighbours += (int) Grid.GetCell(x, y - 1);
                aliveNeighbours += (int) Grid.GetCell(x, y + 1);

                aliveNeighbours += (int) Grid.GetCell(x + 1, y + 1);
                aliveNeighbours += (int) Grid.GetCell(x - 1, y + 1);
                aliveNeighbours += (int) Grid.GetCell(x + 1, y - 1);
                aliveNeighbours += (int) Grid.GetCell(x - 1, y - 1);

                var cellState = Grid.GetCell(x, y);
                var newCellState = aliveNeighbours == 3 || (cellState == 1 && aliveNeighbours == 2);
                NextGrid.SetCell(x, y, newCellState);
            }
        }
    }
}