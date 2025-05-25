using WebAssembly;

namespace Wasm2cs.CodeGeneration.Extensions;

internal static class WebAssemblyValueTypeExtensions
{
    public static Type ToDotnetType(this WebAssemblyValueType type)
    {
        return type switch
        {
            WebAssemblyValueType.Int32 => typeof(int),
            WebAssemblyValueType.Int64 => typeof(long),
            WebAssemblyValueType.Float32 => typeof(float),
            WebAssemblyValueType.Float64 => typeof(double),

            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
    }

    public static string FunctionObjectTypeSignature(this WebAssemblyType type)
    {
        return FunctionObjectTypeSignature(type.Parameters, type.Returns);
    }

    private static string FunctionObjectTypeSignature(IList<WebAssemblyValueType> parameters, IList<WebAssemblyValueType> returns)
    {
        var inputs = string.Join(", ", parameters.Select((a, _) => a.ToDotnetType().Name));
        var outputs = ReturnType(returns);

        if (returns.Count == 0)
        {
            return parameters.Count == 0
                ? "Action"
                : $"Action<{inputs}>";
        }

        return parameters.Count == 0
            ? $"Func<{outputs}>"
            : $"Func<{inputs}, {outputs}>";
    }

    public static string ReturnType(this IList<WebAssemblyValueType> types)
    {
        return types.Count switch
        {
            0 => "void",
            1 => types[0].ToDotnetType().Name,
            _ => $"({string.Join(", ", types.Select(a => a.ToDotnetType().Name))})",
        };
    }

    public static string[] ParameterList(this IList<WebAssemblyValueType> types)
    {
        var parameters = from item in types.Select((type, index) => new { type, index })
                         let dotnet = item.type.ToDotnetType()
                         let name = NameConventions.FunctionArg((uint)item.index)
                         select name;

        return parameters.ToArray();
    }
}