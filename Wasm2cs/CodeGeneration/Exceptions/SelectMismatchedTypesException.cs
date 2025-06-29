using Wacs.Core.Types.Defs;

namespace Wasm2cs.CodeGeneration.Exceptions;

public class SelectMismatchedTypesException
    : Exception
{
    public ValType AType { get; }
    public ValType BType { get; }

    public SelectMismatchedTypesException(ValType aType, ValType bType)
        : base($"'Select' instruction should pop two values with same type, but stack contained: [{aType}, {bType}]")
    {
        AType = aType;
        BType = bType;
    }
}