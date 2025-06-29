using Wacs.Core;
using Wasm2cs.CodeGeneration;
using Wasm2cs.CodeGeneration.Work;

namespace Wasm2cs;

public static class WasmConverter
{
    public static async Task Convert(string className, string @namespace, Stream input, Stream output)
    {
        await using var o = new StreamWriter(output);
        await Convert(className, @namespace, input, o);
    }

    public static async Task Convert(string className, string @namespace, Stream input, TextWriter output)
    {
        className = className
           .Replace(".", "_");

        var module = BinaryModuleParser.ParseWasm(input);
        var builder = new IndentedTextWriter(output);

        List<IWorkItem> work =
        [
            // Fields for storing Imports
            ..ImportFields(module),
            ..MemoryFields(module),
            ..GlobalFields(module),

            // Factory method & Constructor
            new Instantiation(className, module),

            // Exported things
            ..Exports(module),

            // Internal functions to called indexed functions
            ..IndexedFunctions(module),
        ];

        await builder.Using("Wasm2cs.Runtime");
        await builder.AppendLine();
        await builder.Namespace(@namespace);
        await builder.AppendLine();

        await using (await builder.Class(className))
        {
            foreach (var item in work)
            {
                try
                {
                    await item.Emit(builder, module);
                }
                catch (Exception ex)
                {
                    await Console.Error.WriteLineAsync($"Method conversion failed: {ex.Message}");
                }

                await builder.AppendLine();
            }
        }
    }

    private static IEnumerable<IWorkItem> GlobalFields(Module module)
    {
        foreach (var global in module.Globals)
        {
            var imported = module.ImportedGlobals.Any(a => a.Id == global.Id);
            yield return new GlobalField(global, !imported);
        }
    }

    private static IEnumerable<IWorkItem> ImportFields(Module module)
    {
        for (var i = 0; i < module.Imports.Length; i++)
        {
            var import = module.Imports[i];

            switch (import.Desc)
            {
                case Module.ImportDesc.FuncDesc:
                    yield return new FuncImportField(import);
                    break;

                case Module.ImportDesc.TableDesc:
                    yield return new TableImportField(import);
                    break;

                case Module.ImportDesc.MemDesc:
                    yield return new MemoryImportField(import);
                    break;

                case Module.ImportDesc.GlobalDesc:
                    yield return new GlobalImportField(import);
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        yield break;
    }

    private static IEnumerable<IWorkItem> MemoryFields(Module module)
    {
        foreach (var memory in module.Memories)
        {
            var imported = module.ImportedMems.Any(a => a.Id == memory.Id);
            yield return new MemoryField(memory, !imported);
        }
    }

    private static IEnumerable<IWorkItem> Exports(Module module)
    {
        for (var i = 0; i < module.Exports.Length; i++)
        {
            var export = module.Exports[i];

            switch (export.Desc)
            {
                case Module.ExportDesc.FuncDesc:
                    yield return new FuncExport(export);
                    break;

                case Module.ExportDesc.TableDesc:
                    yield return new TableExport(export);
                    break;

                case Module.ExportDesc.MemDesc:
                    yield return new MemoryExport(export);
                    break;

                case Module.ExportDesc.GlobalDesc:
                    yield return new GlobalExport(export);
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    private static IEnumerable<IWorkItem> IndexedFunctions(Module module)
    {
        // Functions are indexed starting from zero. Imports first, then explicitly defined functions.
        var funcIndex = 0u;

        // Find all function imports
        foreach (var importedFunc in module.Imports.Where(a => a.Desc is Module.ImportDesc.FuncDesc))
            yield return new ImportedFuncWrapper(importedFunc);

        // Now handle the explicit functions
        for (var i = 0; i < module.Funcs.Count; i++)
            yield return new ModuleFunction(module.Funcs[i]);
    }
}