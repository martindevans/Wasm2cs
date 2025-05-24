using Wasm2cs.CodeGeneration.Extensions;
using WebAssembly;

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
                    await writer.AppendLine($"{import.BackingFieldName()} = {import.Field};");
            }
        }
    }

    private static (List<string> @params, List<string> names) InstantiateArgs(Module module)
    {
        var @params = new List<string>();
        var names = new List<string>();

        foreach (var moduleImport in module.Imports)
        {
            var name = $"{moduleImport.Field}";
            switch (moduleImport)
            {
                case Import.Function func:
                {
                    var type = module.Types[(int)func.TypeIndex];
                    var typeSignature = type.FunctionObjectTypeSignature();
                    @params.Add(typeSignature + " " + name);
                    names.Add(name);
                    break;
                }

                case Import.Global global:
                    throw new NotImplementedException();

                case Import.Memory memory:
                    throw new NotImplementedException();

                case Import.Table table:
                    throw new NotImplementedException();

                default:
                    throw new NotImplementedException($"Unkown import type {moduleImport.Kind}");
            }
        }

        return (@params, names);
    }
}