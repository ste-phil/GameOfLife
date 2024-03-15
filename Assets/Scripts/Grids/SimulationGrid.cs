using GameOfLife;
using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Profiling;

//Memory layout
/* . .  0  1  2  3 *** . .  4  5  6  7 <-- columns
*  . .  .  .  .  . *** . .  .  .  .  .
*  0 .  1  2  3  4 *** 0 .  5  6  7  8
*  1 .  9 10 11 12 *** 1 . 13 14 15 16
*  2 . 17 18 19 20 *** 2 . 21 22 23 24
*  3 . 25 26 27 28 *** 3 . 29 30 31 32
*/
public unsafe struct SimulationGrid : IDisposable
{
    private static readonly ProfilerMarker markerGetCellXY = new ProfilerMarker("GetCellXY");
    private static readonly ProfilerMarker markerGetCell = new ProfilerMarker("GetCell");

    private const int GHOST_LAYER_OFFSET = 1;

    private int gridSize;
    private int cellSize_Bits;
    private UnsafeBitArray cells;

    public int AxisSize => gridSize;
    public int AxisSizeWithGhostCells => gridSize + 2;
    public int CellCount => cells.Length / CellSize_Bits;
    public int CellCountWithoutGhostCells => gridSize * gridSize;
    public int CellSize_Bits => cellSize_Bits;
    public int MemoryConsumption => cells.Length / 8;
    public int* GetUnsafePtr => (int*)cells.Ptr;

    public SimulationGrid(int gridSize, int cellSize_Bits)
    {
        this.gridSize = gridSize;
        this.cellSize_Bits = cellSize_Bits;

        var paddedGridSize = gridSize + 2;
        cells = new UnsafeBitArray((paddedGridSize) * (paddedGridSize) * cellSize_Bits, Allocator.Persistent);
    }

    public void Dispose()
    {
        cells.Dispose();
    }

    public ulong GetCell(int x, int y)
    {
        //markerGetCellXY.Begin();

        var xIdxBits = (x + GHOST_LAYER_OFFSET) * CellSize_Bits;
        var yIdxBits = (y + GHOST_LAYER_OFFSET) * CellSize_Bits;

        var data = cells.GetBits(xIdxBits + yIdxBits * AxisSizeWithGhostCells, 1);
        //markerGetCellXY.End();

        return data;
    }


    public void SetCell(int x, int y, bool value)
    {
        //var old = GetCell(idx) != 0;
        //if (value != old)
        //{
        //    AliveCells += value && !old ? 1 : -1;
        //}

        var xIdxBits = (x + GHOST_LAYER_OFFSET) * CellSize_Bits;
        var yIdxBits = (y + GHOST_LAYER_OFFSET) * CellSize_Bits;
        var idx = xIdxBits + yIdxBits * AxisSizeWithGhostCells;
        cells.SetBits(idx, value, 1);
    }
}
