using Wacs.Core;
using Wasm2cs.CodeGeneration.Extensions;

namespace Wasm2cs.CodeGeneration.Work;

internal class Instantiation
    : IWorkItem
{
    private readonly string _className;
    private readonly Module _module;

    public Instantiation(string className, Module module)
    {
        _className = className;
        _module = module;
    }

    public async Task Emit(IndentedTextWriter writer, Module module)
    {
        await using (await writer.Region("Instantiation"))
        {
            // Factory method
            var (argsParams, argsNames) = InstantiateArgs(_module);
            await using (await writer.Method("Instantiate", @static: true, args: argsParams, returns: _className))
                await writer.AppendLine($"return new {_className}({string.Join(", ", argsNames)});");

            await writer.AppendLine();

            // Constructor
            await using (await writer.Constructor(_className, args: argsParams, @public: false))
            {
                foreach (var import in _module.Imports)
                    await writer.AppendLine($"{NameConventions.ImportBackingField(import)} = {import.Name};");
            }
        }
    }

    private static (List<string> @params, List<string> names) InstantiateArgs(Module module)
    {
        var @params = new List<string>();
        var names = new List<string>();

        foreach (var moduleImport in module.Imports)
        {
            var desc = moduleImport.Desc;

            var name = $"{moduleImport.Name}";
            switch (desc)
            {
                case Module.ImportDesc.FuncDesc func:
                    {
                        var type = module.Types[func.TypeIndex.Value];
                        var typeSignature = type.FunctionObjectTypeSignature();
                        @params.Add(typeSignature + " " + name);
                        names.Add(name);
                        break;
                    }

                case Module.ImportDesc.GlobalDesc global:
                    throw new NotImplementedException("global import");

                case Module.ImportDesc.MemDesc memory:
                    throw new NotImplementedException("memory import");

                case Module.ImportDesc.TableDesc table:
                    throw new NotImplementedException("table import");

                default:
                    throw new NotSupportedException($"Unkown import type: {moduleImport}");
            }
        }

        return (@params, names);
    }
}