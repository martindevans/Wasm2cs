using Wacs.Core.Types;
using Wacs.Core.Types.Defs;

namespace Wasm2cs.CodeGeneration.Extensions;

internal static class RecursiveTypeExtensions
{
    public static string FunctionObjectTypeSignature(this RecursiveType type)
    {
        var funcType = (FunctionType)type.SubTypes.Single().Body;
        var parameters = funcType.ParameterTypes.Types;
        var returns = funcType.ResultType.Types;

        var inputs = string.Join(", ", parameters.Select((a, _) => a.ToDotnetType().Name));
        var outputs = ReturnType(returns);

        if (returns.Length == 0)
        {
            return parameters.Length == 0
                ? "Action"
                : $"Action<{inputs}>";
        }

        return parameters.Length == 0
            ? $"Func<{outputs}>"
            : $"Func<{inputs}, {outputs}>";
    }

    public static Type ToDotnetType(this ValType type)
    {
        return type switch
        {
            ValType.I32 => typeof(int),
            ValType.I64 => typeof(long),
            ValType.F32 => typeof(float),
            ValType.F64 => typeof(double),

            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
    }

    public static string ReturnType(this IList<ValType> types)
    {
        return types.Count switch
        {
            0 => "void",
            1 => types[0].ToDotnetType().Name,
            _ => $"({string.Join(", ", types.Select(a => a.ToDotnetType().Name))})",
        };
    }

    public static string[] ParameterList(this IList<ValType> types)
    {
        var parameters = from item in types.Select((type, index) => new { type, index })
                         let dotnet = item.type.ToDotnetType()
                         let name = NameConventions.FunctionArg((uint)item.index)
                         select $"{dotnet.Name} {name}";

        return parameters.ToArray();
    }
}