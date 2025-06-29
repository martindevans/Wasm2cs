using Wacs.Core;
using Wacs.Core.Types;

namespace Wasm2cs.CodeGeneration;

internal static class NameConventions
{
    public static string Memory(string id)
    {
        return $"Memory_{id}";
    }

    public static string Global(GlobalIdx id)
    {
        return $"Global_{id.Value}";
    }

    public static string Global(string id)
    {
        return $"Global_{id}";
    }

    public static string Function(FuncIdx index)
    {
        return $"Function_{index.Value}";
    }

    public static string Function(string index)
    {
        return $"Function_{index}";
    }

    public static string FunctionArg(uint index)
    {
        return $"arg{index}";
    }

    public static string Local(uint index)
    {
        return $"local_{index}";
    }

    public static string BlockLabel(uint index)
    {
        return $"block_label_{index}";
    }

    public static string ImportBackingField(Module.Import import)
    {
        return $"_import_{import.ModuleName}_{import.Name}";
    }
}