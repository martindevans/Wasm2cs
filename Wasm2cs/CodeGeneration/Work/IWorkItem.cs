using WebAssembly;

namespace Wasm2cs.CodeGeneration.Work;

internal interface IWorkItem
{
    public Task Emit(IndentedTextWriter writer, Module module);
}