using JetBrains.Annotations;

namespace Wasm2cs.Runtime;

public abstract class TrapException
    : Exception
{
    protected TrapException(string message)
        : base(message)
    {
        
    }
}

[UsedImplicitly]
public sealed class UnreachableTrapException
    : TrapException
{
    public UnreachableTrapException()
        : base("Executed 'Unreachable' instruction")
    {
        
    }
}