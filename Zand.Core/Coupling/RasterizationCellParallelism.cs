namespace Zand.Core.Coupling;

// Controls whether ShapeRasterizer processes a single body's footprint concurrently in row-bands,
// as opposed to iterating every row individually.
public enum RasterizationCellParallelism
{
    Sequential,
    Parallel
}
