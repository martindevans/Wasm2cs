using WebAssembly;

namespace Wasm2cs.CodeGeneration.Exceptions;

public class UnsupportedWasmInstructionException
    : NotSupportedException
{
    public OpCode OpCode { get; }

    public UnsupportedWasmInstructionException(OpCode opCode)
        : base($"Unsupported WASM opcode '{opCode}'")
    {
        OpCode = opCode;
    }
}