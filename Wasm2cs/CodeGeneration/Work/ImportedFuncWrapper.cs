using Wacs.Core;
using Wacs.Core.Types;
using Wasm2cs.CodeGeneration.Extensions;

namespace Wasm2cs.CodeGeneration.Work;

internal class ImportedFuncWrapper(Module.Import Import)
    : IWorkItem
{
    public async Task Emit(IndentedTextWriter writer, Module module)
    {
        var desc = (Module.ImportDesc.FuncDesc)Import.Desc;
        var type = (FunctionType)module.Types[(int)desc.TypeIndex.Value].SubTypes.Single().Body;

        var @return = type.ResultType.Arity > 0 ? "return " : "";

        var paramsTypes = type.ParameterTypes.Types.Select(a => a.ToDotnetType().Name).ToArray();
        var paramsArgs = paramsTypes.Select((t, i) => $"{t} _param{i}").ToList();
        var callArgs = string.Join(", ", paramsTypes.Select((_, i) => $"_param{i}").ToList());

        var backingField = NameConventions.ImportBackingField(Import);

        var funcName = NameConventions.Function(desc.Id);
        await using (await writer.Method(funcName, @public: false, args: paramsArgs, returns: type.ResultType.Types.ReturnType()))
        {
            await writer.AppendLine($"{@return} {backingField}({callArgs});");
        }
        await writer.AppendLine();
    }
}