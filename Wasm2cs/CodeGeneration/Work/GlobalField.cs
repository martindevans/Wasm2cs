using Wacs.Core;
using Wacs.Core.Types;
using Wasm2cs.CodeGeneration.Extensions;

namespace Wasm2cs.CodeGeneration.Work;

internal class GlobalField(Module.Global Global, bool Construct)
    : IWorkItem
{
    public async Task Emit(IndentedTextWriter writer, Module module)
    {
        var name = NameConventions.Global(Global.Id);
        var dotnetType = Global.Type.ContentType.ToDotnetType().Name;

        //todo: global init

        var suffix = "default";
        var @readonly = Global.Type.Mutability == Mutability.Mutable ? "" : "readonly ";
        await writer.AppendLine($"private {@readonly}{dotnetType} {name} = {suffix};");
    }
}