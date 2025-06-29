namespace Wasm2cs.Runtime;

public class Memory
{
    public uint Minimum { get; }
    public uint? Maximum { get; }

    public int Size { get; }

    public Memory(uint minimum, uint? maximum)
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

    public int ReadI32(int addr)
    {
        throw new NotImplementedException();
    }

    public int WriteI32(int value, int addr)
    {
        throw new NotImplementedException();
    }

    public byte ReadI8(int addr)
    {
        throw new NotImplementedException();
    }

    public byte ReadU8(int addr)
    {
        throw new NotImplementedException();
    }

    public byte ReadI16(int addr)
    {
        throw new NotImplementedException();
    }

    public byte ReadU16(int addr)
    {
        throw new NotImplementedException();
    }
}