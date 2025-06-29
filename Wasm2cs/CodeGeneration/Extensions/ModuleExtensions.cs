using Wacs.Core;
using Wacs.Core.Types;

namespace Wasm2cs.CodeGeneration.Extensions;

internal static class ModuleExtensions
{
    public static Module.Function GetFunction(this Module module, FuncIdx idx)
    {
        var index = (int)idx.Value;

        if (index < module.ImportedFunctions.Count)
            return module.ImportedFunctions[(int)index];

        index -= module.ImportedFunctions.Count;
        return module.Funcs[index];
    }
}