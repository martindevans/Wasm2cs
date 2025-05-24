using WebAssembly;

namespace Wasm2cs.CodeGeneration.Exceptions;

public class InitializerIncorrectConstInstructionTypeException
    : Exception
{
    public OpCode OpCode { get; }

    public InitializerIncorrectConstInstructionTypeException(OpCode opCode)
    {
        OpCode = opCode;
    }
}