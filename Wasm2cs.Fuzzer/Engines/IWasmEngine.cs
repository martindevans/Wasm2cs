using System.Security.Cryptography;

namespace Wasm2cs.Fuzzer.Engines;

public interface IWasmEngine
{
    Task<RunResult> Run(byte[] program, int input);
}

public class RunResult
    : IEquatable<RunResult>
{
    public bool IndeterminateResult { get; init; }
    public int? TrapResult { get; init; }

    public bool MissingMain { get; init; }

    public int ReturnValue { get; init; }
    public int MemoryHash { get; init; }

    public bool Equals(RunResult? other)
    {
        if (ReferenceEquals(null, other))
            return false;
        if (ReferenceEquals(this, other))
            return true;
        return MissingMain == other.MissingMain && ReturnValue == other.ReturnValue && MemoryHash == other.MemoryHash;
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj))
            return false;
        if (ReferenceEquals(this, obj))
            return true;
        if (obj.GetType() != this.GetType())
            return false;
        return Equals((RunResult)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(MissingMain, ReturnValue, MemoryHash);
    }

    public static int CalculateMemoryHash(Span<byte> memory)
    {
        //todo:memory hashing
        return 0;

        Span<byte> sha512 = stackalloc byte[64];
        SHA512.HashData(memory, sha512);

        // Combine large hash into 32 bits
        var final = 0;
        for (var i = 0; i < sha512.Length; i++)
        {
            unchecked
            {
                final += sha512[i];
            }
        }

        return final;
    }
}