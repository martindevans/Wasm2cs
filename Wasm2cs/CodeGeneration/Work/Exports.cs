using Wacs.Core;
using Wacs.Core.Types;
using Wasm2cs.CodeGeneration.Extensions;

namespace Wasm2cs.CodeGeneration.Work;

internal class FuncExport(Module.Export function)
    : IWorkItem
{
    public async Task Emit(IndentedTextWriter writer, Module module)
    {
        var desc = (Module.ExportDesc.FuncDesc)function.Desc;
        var func = module.GetFunction(desc.FunctionIndex);
        var type = (FunctionType)module.Types[func.TypeIndex.Value].SubTypes.Single().Body;
        var name = NameConventions.Function(desc.FunctionIndex);

        var @return = type.ResultType.Arity > 0 ? "return" : "";

        var paramsTypes = type.ParameterTypes.Types.Select(a => a.ToDotnetType().Name).ToList();
        var paramsArgs = paramsTypes.Select((t, i) => $"{t} _param{i}").ToList();
        var callArgs = string.Join(", ", paramsTypes.Select((_, i) => $"_param{i}").ToList());

        await using (await writer.Method(function.Name, args: paramsArgs, returns: type.ResultType.Types.ReturnType()))
        {
            await writer.AppendLine($"{@return} {name}({callArgs});");
        }
        await writer.AppendLine();
    }
}

internal class TableExport(Module.Export table)
    : IWorkItem
{
    public Task Emit(IndentedTextWriter writer, Module module)
    {
        throw new NotImplementedException();
    }
}

internal class GlobalExport(Module.Export export)
    : IWorkItem
{
    public async Task Emit(IndentedTextWriter writer, Module module)
    {
        var desc = (Module.ExportDesc.GlobalDesc)export.Desc;
        var global = module.Globals[(int)desc.GlobalIndex.Value];
        var dotnetType = global.Type.ContentType.ToDotnetType().Name;
        var name = NameConventions.Global(global.Id);

        await writer.AppendLine($"public {dotnetType} {export.Name}");
        await using (await writer.Braces())
        {
            await writer.AppendLine($"get => {name};");
            if (global.Type.Mutability == Mutability.Mutable)
                await writer.AppendLine($"set => {name} = value;");
        }
        await writer.AppendLine();
    }
}

internal class MemoryExport(Module.Export export)
    : IWorkItem
{
    public async Task Emit(IndentedTextWriter writer, Module module)
    {
        var desc = (Module.ExportDesc.MemDesc)export.Desc;
        var memory = module.Memories[(int)desc.MemoryIndex.Value];

        var name = NameConventions.Memory(memory.Id);

        await writer.AppendLine($"public Memory {export.Name} => {name};");
        await writer.AppendLine();
    }
}