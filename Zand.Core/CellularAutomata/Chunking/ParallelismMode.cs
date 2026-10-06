namespace Zand.Core.CellularAutomata.Chunking;

// If the chunks in the CA grid are processed one at a time (Sequential), across various threads (Parallel),
// or across various threads, attempting to prioritize the most timely to consume first (ParallelLargestFirst)
// Only meaningful when Chunking is enabled
public enum ParallelismMode
{
    Sequential,
    Parallel,
    ParallelLargestFirst
}
