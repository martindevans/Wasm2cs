namespace Wasm2cs.Runtime;

public class Memory
{
    public int Minimum { get; }
    public uint Maximum { get; }

    public int Size { get; }

    public Memory(int minimum, uint maximum)
    {
        Minimum = minimum;
        Maximum = maximum;

        Size = 0;
    }

    /// <summary>
    /// Grow memory by a number of pages
    /// </summary>
    /// <returns>The previous size, or -1 if this operation failed</returns>
    public int Grow(int pages)
    {
        return -1;
    }
}