using WebAssembly;

namespace Wasm2cs.CodeGeneration.Extensions;

internal static class ImportExtensions
{
    public static string BackingFieldName(this Import import)
    {
        return $"_import_{import.Field}";
    }
}