using Wasm2cs.CodeGeneration;
using Wasm2cs.CodeGeneration.Work;
using WebAssembly;

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
        var module = Module.ReadFromBinary(input);
        var builder = new IndentedTextWriter(output);

        List<IWorkItem> work =
        [
            // Fields for storing Imports
            ..ImportFields(module),

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
                await item.Emit(builder, module);
                await builder.AppendLine();
            }
        }
    }

    private static IEnumerable<IWorkItem> ImportFields(Module module)
    {
        for (var i = 0; i < module.Imports.Count; i++)
        {
            var import = module.Imports[i];

            switch (import.Kind)
            {
                case ExternalKind.Function:
                    yield return new FuncImportField((Import.Function)import);
                    break;

                case ExternalKind.Table:
                    yield return new TableImportField((Import.Table)import);
                    break;

                case ExternalKind.Memory:
                    yield return new MemoryImportField((Import.Memory)import);
                    break;

                case ExternalKind.Global:
                    yield return new GlobalImportField((Import.Global)import);
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    private static IEnumerable<IWorkItem> Exports(Module module)
    {
        foreach (var moduleExport in module.Exports)
        {
            switch (moduleExport.Kind)
            {
                case ExternalKind.Function:
                    yield return new FuncExport(moduleExport);
                    break;
                case ExternalKind.Table:
                    yield return new TableExport(moduleExport);
                    break;
                case ExternalKind.Memory:
                    yield return new MemoryExport(moduleExport);
                    break;
                case ExternalKind.Global:
                    yield return new GlobalExport(moduleExport);
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
        foreach (var importedFunc in module.Imports.OfType<Import.Function>())
            yield return new ImportedFuncWrapper(importedFunc, funcIndex++);

        // Now handle the explicit functions
        for (var i = 0; i < module.Functions.Count; i++)
            yield return new ModuleFunction(module.Functions[i], module.Codes[i], funcIndex++);
    }
}