using Wacs.Core.Types;

namespace Wasm2cs.CodeGeneration.Exceptions;

public class CannotSetImmutableGlobal
    : Exception
{
    public GlobalIdx Index { get; }

    public CannotSetImmutableGlobal(GlobalIdx index)
        : base($"Cannot set an immutable global at index {index}")
    {
        Index = index;
    }
}