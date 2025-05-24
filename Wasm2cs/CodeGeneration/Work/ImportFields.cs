using Wasm2cs.CodeGeneration.Extensions;
using WebAssembly;

namespace Wasm2cs.CodeGeneration.Work;

internal class GlobalImportField(Import.Global global)
    : IWorkItem
{
    public Task Emit(IndentedTextWriter writer, Module module)
    {
        throw new NotImplementedException();
    }
}

internal class MemoryImportField(Import.Memory memory)
    : IWorkItem
{
    public async Task Emit(IndentedTextWriter writer, Module module)
    {
        await writer.AppendLine($"private readonly Memory {memory.BackingFieldName()};");
    }
}

internal class FuncImportField(Import.Function function)
    : IWorkItem
{
    public async Task Emit(IndentedTextWriter writer, Module module)
    {
        var type = module.Types[(int)function.TypeIndex].FunctionObjectTypeSignature();
        await writer.AppendLine($"private readonly {type} {function.BackingFieldName()};");
    }
}

internal class TableImportField(Import.Table table)
    : IWorkItem
{
    public Task Emit(IndentedTextWriter writer, Module module)
    {
        throw new NotImplementedException();
    }
}