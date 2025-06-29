

using Wacs.Core;
using Wasm2cs.CodeGeneration.Extensions;

namespace Wasm2cs.CodeGeneration.Work;

internal class GlobalImportField(Module.Import global)
    : IWorkItem
{
    public Task Emit(IndentedTextWriter writer, Module module)
    {
        return Task.CompletedTask;
    }
}

internal class MemoryImportField(Module.Import memory)
    : IWorkItem
{
    public Task Emit(IndentedTextWriter writer, Module module)
    {
        return Task.CompletedTask;
    }
}

internal class FuncImportField(Module.Import function)
    : IWorkItem
{
    public async Task Emit(IndentedTextWriter writer, Module module)
    {
        var desc = (Module.ImportDesc.FuncDesc)function.Desc;

        var type = module.Types[desc.TypeIndex.Value].FunctionObjectTypeSignature();
        await writer.AppendLine($"private readonly {type} {NameConventions.ImportBackingField(function)};");
    }
}

internal class TableImportField(Module.Import table)
    : IWorkItem
{
    public Task Emit(IndentedTextWriter writer, Module module)
    {
        return Task.CompletedTask;
    }
}