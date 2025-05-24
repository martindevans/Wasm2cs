namespace Wasm2cs.Runtime;

public abstract class TrapException
    : Exception
{
    public TrapException(string message)
        : base(message)
    {
        
    }
}

public sealed class UnreachableTrapException
    : TrapException
{
    public UnreachableTrapException()
        : base("Executed 'Unreachable' instruction")
    {
        
    }
}