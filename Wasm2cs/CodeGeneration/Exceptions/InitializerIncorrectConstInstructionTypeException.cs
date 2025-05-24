using WebAssembly;

namespace Wasm2cs.CodeGeneration.Exceptions;

public class InitializerIncorrectConstInstructionTypeException(OpCode OpCode)
    : Exception
{
    
}