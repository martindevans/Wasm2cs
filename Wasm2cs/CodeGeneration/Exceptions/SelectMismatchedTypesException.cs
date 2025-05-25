using WebAssembly;

namespace Wasm2cs.CodeGeneration.Exceptions;

public class SelectMismatchedTypesException
    : Exception
{
    public WebAssemblyValueType AType { get; }
    public WebAssemblyValueType BType { get; }

    public SelectMismatchedTypesException(WebAssemblyValueType aType, WebAssemblyValueType bType)
        : base($"'Select' instruction should pop two values with same type, but stack contained: [{aType}, {bType}]")
    {
        AType = aType;
        BType = bType;
    }
}