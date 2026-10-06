namespace Zand.Rendering;

// Whether CellRenderer fills its pixel buffer in a single thread or splits the rows across threads.
// Should only affect how quickly the frame is prepared, never what is drawn.
public enum RenderParallelism
{
    Sequential,
    Parallel
}
