using GameOfLife;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace GameOfLife
{
    public struct ActiveSimulation : IComponentData {}

    public unsafe struct SimulationState : IComponentData
    {
        public SimulationGrid Grid;
        public SimulationGrid NextGrid;
    }

    public unsafe struct ChunkSimulationState : IComponentData
    {
        public ChunkSimulationGrid Grid;
        public ChunkSimulationGrid NextGrid;
    }
}
