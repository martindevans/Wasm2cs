namespace Wasm2cs.CodeGeneration;

internal static class NameConventions
{
    public static string Memory(int index)
    {
        return $"Memory_{index}";
    }

    public static string Function(uint index)
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
}