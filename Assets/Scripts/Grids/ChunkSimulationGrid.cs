using GameOfLife;
using System;
using System.Linq;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities.UniversalDelegates;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine.UI;
using static UnityEngine.Rendering.DebugUI;

//Memory layout
/* . .  0  1  2  3 *** . .  4  5  6  7 <-- columns
*  . .  .  .  .  . *** . .  .  .  .  .
*  0 .  1  2  3  4 *** 0 . 17 18 19 20
*  1 .  5  6  7  8 *** 1 . 21 22 23 24
*  2 .  9 10 11 12 *** 2 . 25 26 27 28
*  3 . 13 14 15 16 *** 3 . 29 30 31 32
*/

//1 bit per cell

public unsafe struct ChunkSimulationGrid : IDisposable
{
    private static readonly ProfilerMarker markerGetData = new ProfilerMarker("UnsafeBitArray Get");
    private static readonly ProfilerMarker makerCalcIdx = new ProfilerMarker("Calculate Idx");

    private const int GHOST_LAYER_OFFSET = 8;

    private int gridSize;
    [NativeDisableParallelForRestriction]
    private NativeArray<int> cells;

    public readonly static int2 ChunkSize = new int2(8, 4);
    public const int ChunkSizeBytes = 4;
    public const int CellSize_Bits = 1;
    private const int ChunkSizeBits = 32;

    public int AxisSize => gridSize;
    public int CellCountWithoutGhostCells => gridSize * gridSize;
    public int MemoryConsumption => cells.Length / 32;
    public int* GetUnsafePtr => (int*)cells.GetUnsafePtr();

    public ChunkSimulationGrid(int gridSize)
    {
        this.gridSize = gridSize;

        //put left and right one byte padding
        //shift the whole index access by 7 bit to the right so that the ghost cells have one bit for themselfs
        // | 0 1 2 3 4 5 6 7 | 08 09 10 11 12 13 14 15 | 16 17 18 19 20 21 22 23 | 24 25 26 27 28 29 30 31 |
        // | * * * * * * * P |  C  C  C  C  C  C  C  C |  C  C  C  C  C  C  C  C |  P  *  *  *  *  *  *  * |
        // This is required for BRGMultiCellGraphInstanced rendering mode

        cells = new NativeArray<int>(((gridSize) / 8 + 2) * ((gridSize) / 4 + 2), Allocator.Persistent);
    }

    public void Dispose()
    {
        cells.Dispose();
    }

    public int GetCellByte(int x, int y)
    {
        makerCalcIdx.Begin();

        var cellCoord = new int2(x, y);

        var chunkIdx = cellCoord / ChunkSize + 1;
        var chunkOffset = cellCoord % ChunkSize;
        var chunkXCount = gridSize / ChunkSize.x + 2;

        var chunkStartIdx = chunkIdx.y * chunkXCount + chunkIdx.x;
        var indexInChunk = chunkOffset.x + chunkOffset.y * ChunkSize.x;
        var bitOffset = chunkStartIdx * ChunkSizeBits + indexInChunk;
        makerCalcIdx.End();

        markerGetData.Begin();
        //var data = cells.GetBits(bitOffset, 1);
        var data = cells[chunkStartIdx];
        markerGetData.End();

        return data;
    }

    public int GetCell(int x, int y)
    {
        //makerCalcIdx.Begin();

        var cellCoord = new int2(x, y);

        var chunkIdx = cellCoord / ChunkSize + 1;
        var chunkOffset = cellCoord % ChunkSize;
        var chunkXCount = gridSize / ChunkSize.x + 2;

        var chunkStartIdx = chunkIdx.y * chunkXCount + chunkIdx.x;
        var indexInChunk = chunkOffset.x + chunkOffset.y * ChunkSize.x;
        //var bitOffset = chunkStartIdx * ChunkSizeBits + indexInChunk;
        //makerCalcIdx.End();

        //markerGetData.Begin();
        var data = cells[chunkStartIdx];
        data = (data >> indexInChunk) & 0x1;
        //markerGetData.End();

        return data;
    }

    public void SetCell(int x, int y, bool value)
    {
        var cellCoord = new int2(x, y);

        var chunkIdx = cellCoord / ChunkSize + 1;
        var chunkOffset = cellCoord % ChunkSize;
        var chunkXCount = gridSize / ChunkSize.x + 2;

        var chunkStartIdx = chunkIdx.y * chunkXCount + chunkIdx.x;
        var indexInChunk = chunkOffset.x + chunkOffset.y * ChunkSize.x;
        //var bitOffset = chunkStartIdx * ChunkSizeBits + indexInChunk;

        int mask = (value ? 1 : 0) << indexInChunk;
        var chunkData = cells[chunkStartIdx];
        var newData = (chunkData & ~(1 << indexInChunk)) | mask;

        cells[chunkStartIdx] = newData;
    }
}
