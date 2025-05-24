using WebAssembly;

namespace Wasm2cs.CodeGeneration.Extensions;

internal static class ModuleExtensions
{
    public static uint GetModuleFuncTypeIndex(this Module module, uint index)
    {
        var importedFunctions = from import in module.Imports
                                where import.Kind == ExternalKind.Function
                                let fi = (Import.Function)import
                                select fi.TypeIndex;

        var otherFunctions = from function in module.Functions
                             select function.Type;

        return importedFunctions.Concat(otherFunctions).ElementAt((int)index);
    }
}