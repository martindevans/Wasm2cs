namespace Wasm2cs.Runtime;

public class Memory
{
    public int Minimum { get; }
    public uint Maximum { get; }

    public Memory(int minimum, uint maximum)
    {
        Minimum = minimum;
        Maximum = maximum;
    }
}