namespace Wasm2cs.CodeGeneration.Exceptions;

public class CannotSetImmutableGlobal
    : Exception
{
    public uint Index { get; }

    public CannotSetImmutableGlobal(uint index)
        : base($"Cannot set an immutable global at index {index}")
    {
        Index = index;
    }
}