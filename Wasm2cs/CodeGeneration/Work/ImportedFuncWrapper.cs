using Wasm2cs.CodeGeneration.Extensions;
using WebAssembly;

namespace Wasm2cs.CodeGeneration.Work;

internal class ImportedFuncWrapper(Import.Function Import, uint Index)
    : IWorkItem
{
    public async Task Emit(IndentedTextWriter writer, Module module)
    {
        var type = module.Types[(int)Import.TypeIndex];

        var @return = type.Returns.Count > 0 ? "return " : "";

        var paramsTypes = type.Parameters.Select(a => a.ToDotnetType()).ToArray();
        var paramsArgs = paramsTypes.Select((t, i) => $"{t} _param{i}").ToList();
        var callArgs = string.Join(", ", paramsTypes.Select((_, i) => $"_param{i}").ToList());

        var backingField = Import.BackingFieldName();

        var funcName = NameConventions.Function(Index);
        await using (await writer.Method(funcName, @public:false, args: paramsArgs, returns: type.Returns.ReturnType()))
        {
            await writer.AppendLine($"{@return} {backingField}({callArgs});");
        }
        await writer.AppendLine();
    }
}