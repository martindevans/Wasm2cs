using System.Globalization;
using Wasm2cs.CodeGeneration.Exceptions;
using Wasm2cs.CodeGeneration.Extensions;
using WebAssembly;
using WebAssembly.Instructions;

namespace Wasm2cs.CodeGeneration.Work;

internal class FuncExport(Export function)
    : IWorkItem
{
    public async Task Emit(IndentedTextWriter writer, Module module)
    {
        var type = module.Types[(int)module.GetModuleFuncTypeIndex(function.Index)];

        var @return = type.Returns.Count > 0 ? "return" : "";

        var paramsTypes = MethodParameterList(type.Parameters);
        var paramsArgs = paramsTypes.Select((t, i) => $"{t} _param{i}").ToList();
        var callArgs = string.Join(", ", paramsTypes.Select((_, i) => $"_param{i}").ToList());

        await using (await writer.Method(function.Name, args: paramsArgs, returns: type.Returns.ReturnType()))
        {
            await writer.AppendLine($"{@return} Function{function.Index}({callArgs});");
        }
        await writer.AppendLine();
    }

    private static List<string> MethodParameterList(IList<WebAssemblyValueType> types)
    {
        return types.Select(a => a.ToDotnetType().Name).ToList();
    }
}

internal class TableExport(Export table)
    : IWorkItem
{
    public Task Emit(IndentedTextWriter writer, Module module)
    {
        throw new NotImplementedException();
    }
}

internal class GlobalExport(Export export)
    : IWorkItem
{
    public async Task Emit(IndentedTextWriter writer, Module module)
    {
        var global = module.Globals[(int)export.Index];

        var type = global.ContentType.ToDotnetType();

        var initName = $"InitGlobal_{export.Name}";
        await writer.Property(type, export.Name, true, global.IsMutable, initName);

        await using (await writer.Method(initName, true, false, returns: type.FullName))
            await writer.AppendLine($"return {GetGlobalInitializerValue(global.ContentType, global.InitializerExpression.ToList())};");

        await writer.AppendLine();
    }

    private static string GetGlobalInitializerValue(WebAssemblyValueType type, IReadOnlyList<Instruction> instructions)
    {
        if (instructions[^1] is not End)
            throw new FunctionDoesNotEndException();

        switch (type)
        {
            case WebAssemblyValueType.Int32:
            {
                if (instructions[0] is not Int32Constant i32c)
                    throw new InitializerIncorrectConstInstructionTypeException(instructions[0].OpCode);
                return i32c.Value.ToString();
            }

            case WebAssemblyValueType.Int64:
            {
                if (instructions[0] is not Int64Constant i64c)
                    throw new InitializerIncorrectConstInstructionTypeException(instructions[0].OpCode);
                return i64c.Value.ToString();
            }

            case WebAssemblyValueType.Float32:
            {
                if (instructions[0] is not Float32Constant f32c)
                    throw new InitializerIncorrectConstInstructionTypeException(instructions[0].OpCode);
                return f32c.Value.ToString(CultureInfo.InvariantCulture);
            }

            case WebAssemblyValueType.Float64:
            {
                if (instructions[0] is not Float64Constant f64c)
                    throw new InitializerIncorrectConstInstructionTypeException(instructions[0].OpCode);
                return f64c.Value.ToString(CultureInfo.InvariantCulture);
            }

            default:
                throw new InitializerIncorrectConstInstructionTypeException(instructions[0].OpCode);
        }
    }
}

internal class MemoryExport(Export export)
    : IWorkItem
{
    public async Task Emit(IndentedTextWriter writer, Module module)
    {
        var memory = module.Memories[(int)export.Index];

        //todo: can this memory be imported? if so, need to handle that instead of just always constructing it here
        await writer.AppendLine($"private readonly Memory _memory_{export.Name} = new Memory({memory.ResizableLimits.Minimum}, {memory.ResizableLimits.Maximum ?? uint.MaxValue});");
        await writer.AppendLine($"public Memory {export.Name} => _memory_{export.Name};");
        await writer.AppendLine();
    }
}