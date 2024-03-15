using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace GameOfLife
{
    public enum CellSize
    {
        ByteSizeCell, BitSizeCell,
    }

    public enum RenderingMode
    {
        MeshInstanced, BRGInstanced, BRGGraphInstanced, BRGMultiCellGraphInstanced
    }


    public struct Config : IComponentData
    {
        public int GridSize;
        public float UpdatesPerSecond;

        public RenderingMode RenderingMode;
        public bool UseRandomInitialization;
        public float OrthographicSize;
    }

    public struct InitialAliveCells : IBufferElementData
    {
        public int2 Value;
    }

    public struct BRGInstancedRenderingMode : IComponentData { }
    public struct BRGGraphInstancedRenderingMode : IComponentData { }
    public struct BRGMultiCellGraphInstancedRenderingMode : IComponentData { }
    public struct MeshInstacedRenderingMode : IComponentData { }

}

