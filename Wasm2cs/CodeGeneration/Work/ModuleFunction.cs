using System.Globalization;
using System.Threading;
using Wasm2cs.CodeGeneration.Exceptions;
using Wasm2cs.CodeGeneration.Extensions;
using WebAssembly;
using WebAssembly.Instructions;

namespace Wasm2cs.CodeGeneration.Work;

internal class ModuleFunction(Function Function, FunctionBody Body, int Index)
    : IWorkItem
{
    public async Task Emit(IndentedTextWriter writer, Module module)
    {
        var funcType = module.Types[(int)Function.Type];
        var name = $"Function{Index}";

        await using (await writer.Method(
                         name,
                         @static: false,
                         @public: false,
                         funcType.Parameters.ParameterList(),
                         funcType.Returns.ReturnType()
                     ))
        {
            // Create all locals
            var localIdx = 0;
            var locals = new List<(string localName, WebAssemblyValueType Type)>();
            foreach (var local in Body.Locals)
            {
                var localType = local.Type.ToDotnetType();
                for (var i = 0; i < local.Count; i++)
                {
                    var localName = $"local_{localIdx++}";
                    locals.Add((localName, local.Type));
                    await writer.AppendLine($"{localType} {localName} = default");
                }
            }

            // Emit instructions
            var stack = new StackBuilder(writer);
            var scope = new ScopeChecker("implicit_func_start");
            foreach (var instruction in Body.Code)
            {
                switch (instruction.OpCode)
                {
                    case OpCode.NoOperation:
                    {
                        await writer.AppendLine("// nop");
                        break;
                    }

                    case OpCode.Return:
                    {
                        await EmitReturn(stack);
                        break;
                    }

                    case OpCode.End:
                    {
                        scope.Pop();
                        break;
                    }

                    #region int32
                    case OpCode.Int32Constant:
                    {
                        var ci32 = (Int32Constant)instruction;
                        await stack.Push(ci32.Value);
                        break;
                    }

                    case OpCode.Int32EqualZero:
                    {
                        var v = stack.Pop(WebAssemblyValueType.Int32);
                        var expr = $"{v} == 0 ? 1 : 0";
                        await stack.Push(WebAssemblyValueType.Int32, expr);
                        break;  
                    }
                    #endregion

                    #region int64
                    case OpCode.Int64Constant:
                    {
                        var ci64 = (Int64Constant)instruction;
                        await stack.Push(ci64.Value);
                        break;
                    }

                    case OpCode.Int64EqualZero:
                    {
                        var v = stack.Pop(WebAssemblyValueType.Int64);
                        var expr = $"{v} == 0 ? 1 : 0";
                        await stack.Push(WebAssemblyValueType.Int32, expr);
                        break;
                    }
                    #endregion

                    #region float32
                    case OpCode.Float32Constant:
                    {
                        var f32 = (Float32Constant)instruction;
                        await stack.Push(f32.Value);
                        break;
                    }

                    case OpCode.Float32Absolute:
                    {
                        await EmitPrefixUnaryFunction(stack, WebAssemblyValueType.Float32, "MathF.Abs");
                        break;
                    }

                    case OpCode.Float32Negate:
                    {
                        await EmitPrefixUnaryFunction(stack, WebAssemblyValueType.Float32, "-");
                        break;
                    }

                    case OpCode.Float32Ceiling:
                    {
                        await EmitPrefixUnaryFunction(stack, WebAssemblyValueType.Float64, "MathF.Ceiling");
                        break;
                    }

                    case OpCode.Float32Floor:
                    {
                        await EmitPrefixUnaryFunction(stack, WebAssemblyValueType.Float32, "MathF.Floor");
                        break;
                    }

                    case OpCode.Float32Truncate:
                    {
                        await EmitPrefixUnaryFunction(stack, WebAssemblyValueType.Float32, "MathF.Truncate");
                        break;
                    }

                    case OpCode.Float32Nearest:
                    {
                        await EmitUnaryFunction(stack, WebAssemblyValueType.Float32, "MathF.Round({0}, MidpointRounding.ToEven)");
                        break;
                    }

                    case OpCode.Float32SquareRoot:
                    {
                        await EmitPrefixUnaryFunction(stack, WebAssemblyValueType.Float32, "MathF.Sqrt");
                        break;
                    }

                    case OpCode.Float32Add:
                    {
                        await EmitBinaryFunction(stack, WebAssemblyValueType.Float32, "{0} + {1}");
                        break;
                    }

                    case OpCode.Float32Subtract:
                    {
                        await EmitBinaryFunction(stack, WebAssemblyValueType.Float32, "{0} - {1}");
                        break;
                    }

                    case OpCode.Float32Multiply:
                    {
                        await EmitBinaryFunction(stack, WebAssemblyValueType.Float32, "{0} * {1}");
                        break;
                    }

                    case OpCode.Float32Divide:
                    {
                        await EmitBinaryFunction(stack, WebAssemblyValueType.Float32, "{0} / {1}");
                        break;
                    }

                    case OpCode.Float32Minimum:
                    {
                        await EmitBinaryFunction(stack, WebAssemblyValueType.Float32, "MathF.Min({0}, {1})");
                        break;
                    }

                    case OpCode.Float32Maximum:
                    {
                        await EmitBinaryFunction(stack, WebAssemblyValueType.Float32, "MathF.Max({0}, {1})");
                        break;
                    }

                    case OpCode.Float32CopySign:
                    {
                        await EmitBinaryFunction(stack, WebAssemblyValueType.Float32, "MathF.CopySign({0}, {1})");
                        break;
                    }

                    case OpCode.Float32Equal:
                    {
                        await EmitBinaryTransform(stack, WebAssemblyValueType.Float32, WebAssemblyValueType.Int32, "({0} == {1}) ? 1 : 0");
                        break;
                    }

                    case OpCode.Float32NotEqual:
                    {
                        await EmitBinaryTransform(stack, WebAssemblyValueType.Float32, WebAssemblyValueType.Int32, "({0} != {1}) ? 1 : 0");
                        break;
                    }

                    case OpCode.Float32LessThan:
                    {
                        await EmitBinaryTransform(stack, WebAssemblyValueType.Float32, WebAssemblyValueType.Int32, "({0} < {1}) ? 1 : 0");
                        break;
                    }

                    case OpCode.Float32GreaterThan:
                    {
                        await EmitBinaryTransform(stack, WebAssemblyValueType.Float32, WebAssemblyValueType.Int32, "({0} > {1}) ? 1 : 0");
                        break;
                    }

                    case OpCode.Float32LessThanOrEqual:
                    {
                        await EmitBinaryTransform(stack, WebAssemblyValueType.Float32, WebAssemblyValueType.Int32, "({0} <= {1}) ? 1 : 0");
                        break;
                    }

                    case OpCode.Float32GreaterThanOrEqual:
                    {
                        await EmitBinaryTransform(stack, WebAssemblyValueType.Float32, WebAssemblyValueType.Int32, "({0} >= {1}) ? 1 : 0");
                        break;
                    }
                    #endregion

                    #region float64
                    case OpCode.Float64Constant:
                    {
                        var f64 = (Float64Constant)instruction;
                        await stack.Push(f64.Value);
                        break;
                    }

                    case OpCode.Float64Absolute:
                    {
                        await EmitPrefixUnaryFunction(stack, WebAssemblyValueType.Float64, "Math.Abs");
                        break;
                    }

                    case OpCode.Float64Negate:
                    {
                        await EmitPrefixUnaryFunction(stack, WebAssemblyValueType.Float64, "-");
                        break;
                    }

                    case OpCode.Float64Ceiling:
                    {
                        await EmitPrefixUnaryFunction(stack, WebAssemblyValueType.Float64, "Math.Ceiling");
                        break;
                    }

                    case OpCode.Float64Floor:
                    {
                        await EmitPrefixUnaryFunction(stack, WebAssemblyValueType.Float64, "Math.Floor");
                        break;
                    }

                    case OpCode.Float64Truncate:
                    {
                        await EmitPrefixUnaryFunction(stack, WebAssemblyValueType.Float64, "Math.Truncate");
                        break;
                    }

                    case OpCode.Float64Nearest:
                    {
                        await EmitUnaryFunction(stack, WebAssemblyValueType.Float64, "Math.Round({0}, MidpointRounding.ToEven)");
                        break;
                    }

                    case OpCode.Float64SquareRoot:
                    {
                        await EmitPrefixUnaryFunction(stack, WebAssemblyValueType.Float64, "Math.Sqrt");
                        break;
                    }

                    case OpCode.Float64Add:
                    {
                        await EmitBinaryFunction(stack, WebAssemblyValueType.Float64, "{0} + {1}");
                        break;
                    }

                    case OpCode.Float64Subtract:
                    {
                        await EmitBinaryFunction(stack, WebAssemblyValueType.Float64, "{0} - {1}");
                        break;
                    }

                    case OpCode.Float64Multiply:
                    {
                        await EmitBinaryFunction(stack, WebAssemblyValueType.Float64, "{0} * {1}");
                        break;
                    }

                    case OpCode.Float64Divide:
                    {
                        await EmitBinaryFunction(stack, WebAssemblyValueType.Float64, "{0} / {1}");
                        break;
                    }

                    case OpCode.Float64Minimum:
                    {
                        await EmitBinaryFunction(stack, WebAssemblyValueType.Float64, "Math.Min({0}, {1})");
                        break;
                    }

                    case OpCode.Float64Maximum:
                    {
                        await EmitBinaryFunction(stack, WebAssemblyValueType.Float64, "Math.Max({0}, {1})");
                        break;
                    }

                    case OpCode.Float64CopySign:
                    {
                        await EmitBinaryFunction(stack, WebAssemblyValueType.Float64, "Math.CopySign({0}, {1})");
                        break;
                    }

                    case OpCode.Float64Equal:
                    {
                        await EmitBinaryTransform(stack, WebAssemblyValueType.Float64, WebAssemblyValueType.Int32, "({0} == {1}) ? 1 : 0");
                        break;
                    }

                    case OpCode.Float64NotEqual:
                    {
                        await EmitBinaryTransform(stack, WebAssemblyValueType.Float64, WebAssemblyValueType.Int32, "({0} != {1}) ? 1 : 0");
                        break;
                    }

                    case OpCode.Float64LessThan:
                    {
                        await EmitBinaryTransform(stack, WebAssemblyValueType.Float64, WebAssemblyValueType.Int32, "({0} < {1}) ? 1 : 0");
                        break;
                    }

                    case OpCode.Float64GreaterThan:
                    {
                        await EmitBinaryTransform(stack, WebAssemblyValueType.Float64, WebAssemblyValueType.Int32, "({0} > {1}) ? 1 : 0");
                        break;
                    }

                    case OpCode.Float64LessThanOrEqual:
                    {
                        await EmitBinaryTransform(stack, WebAssemblyValueType.Float64, WebAssemblyValueType.Int32, "({0} <= {1}) ? 1 : 0");
                        break;
                    }

                    case OpCode.Float64GreaterThanOrEqual:
                    {
                        await EmitBinaryTransform(stack, WebAssemblyValueType.Float64, WebAssemblyValueType.Int32, "({0} >= {1}) ? 1 : 0");
                        break;
                    }
                    #endregion

                    case OpCode.Call:
                    {
                        var call = (Call)instruction;
                        var type = module.Types[(int)module.GetModuleFuncTypeIndex(call.Index)];

                        var inputs = (from parameter in type.Parameters
                                      let localName = stack.Pop(parameter)
                                      select localName).ToArray();

                        var callName = $"Function{call.Index}";
                        var parameters = string.Join(", ", inputs);
                        await writer.AppendLine($"{callName}({parameters});");
                        break;
                    }

                    case OpCode.Unreachable:
                    {
                        await writer.AppendLine("throw new UnreachableTrapException();");
                        break;
                    }

                    case OpCode.Drop:
                    {
                        stack.Pop(out _);
                        break;
                    }

                    case OpCode.Select:
                    {
                        // Pop 2 values of same type
                        var a = stack.Pop(out var aType);
                        var b = stack.Pop(out var bType);
                        if (aType != bType)
                            throw new SelectMismatchedTypesException(aType, bType);

                        // Pop discriminator
                        var c = stack.Pop(WebAssemblyValueType.Int32);

                        // Select based on discriminator
                        var expr = $"({c} != 0 ? {a} : {b})";
                        await stack.Push(aType, expr);
                        break;
                    }

                    #region locals
                    case OpCode.LocalGet:
                    {
                        var localGet = (LocalGet)instruction;
                        var local = locals[(int)localGet.Index];
                        await stack.Push(local.Type, local.localName);
                        break;
                    }

                    case OpCode.LocalSet:
                    {
                        var localSet = (LocalSet)instruction;
                        var local = locals[(int)localSet.Index];
                        var stackName = stack.Pop(local.Type);
                        await writer.AppendLine($"{stackName} = {local.localName};");
                        break;
                    }

                    case OpCode.LocalTee:
                    {
                        var localSet = (LocalSet)instruction;
                        var local = locals[(int)localSet.Index];
                        var stackName = stack.Pop(local.Type);
                        await writer.AppendLine($"{stackName} = {local.localName};");
                        await stack.Push(local.Type, local.localName);
                        break;
                    }
                    #endregion

                    #region globals
                    case OpCode.GlobalGet:
                    {
                        var globalGet = (GlobalGet)instruction;
                        var global = module.Globals[(int)globalGet.Index];
                        await stack.Push(global.ContentType, $"_global_{globalGet.Index}");
                        break;
                    }

                    case OpCode.GlobalSet:
                    {
                        var globalGet = (GlobalGet)instruction;
                        var global = module.Globals[(int)globalGet.Index];
                        if (!global.IsMutable)
                            throw new CannotSetImmutableGlobal(globalGet.Index);

                        var v = stack.Pop(global.ContentType);
                        await writer.AppendLine($"_global_{globalGet.Index} = {v};");

                        break;
                    }
                    #endregion

                    case OpCode.Block:
                    case OpCode.Loop:
                    case OpCode.If:
                    case OpCode.Else:
                    case OpCode.Branch:
                    case OpCode.BranchIf:
                    case OpCode.BranchTable:
                    case OpCode.CallIndirect:
                    case OpCode.Int32Load:
                    case OpCode.Int64Load:
                    case OpCode.Float32Load:
                    case OpCode.Float64Load:
                    case OpCode.Int32Load8Signed:
                    case OpCode.Int32Load8Unsigned:
                    case OpCode.Int32Load16Signed:
                    case OpCode.Int32Load16Unsigned:
                    case OpCode.Int64Load8Signed:
                    case OpCode.Int64Load8Unsigned:
                    case OpCode.Int64Load16Signed:
                    case OpCode.Int64Load16Unsigned:
                    case OpCode.Int64Load32Signed:
                    case OpCode.Int64Load32Unsigned:
                    case OpCode.Int32Store:
                    case OpCode.Int64Store:
                    case OpCode.Float32Store:
                    case OpCode.Float64Store:
                    case OpCode.Int32Store8:
                    case OpCode.Int32Store16:
                    case OpCode.Int64Store8:
                    case OpCode.Int64Store16:
                    case OpCode.Int64Store32:
                    case OpCode.MemorySize:
                    case OpCode.MemoryGrow:
                    case OpCode.Int32Equal:
                    case OpCode.Int32NotEqual:
                    case OpCode.Int32LessThanSigned:
                    case OpCode.Int32LessThanUnsigned:
                    case OpCode.Int32GreaterThanSigned:
                    case OpCode.Int32GreaterThanUnsigned:
                    case OpCode.Int32LessThanOrEqualSigned:
                    case OpCode.Int32LessThanOrEqualUnsigned:
                    case OpCode.Int32GreaterThanOrEqualSigned:
                    case OpCode.Int32GreaterThanOrEqualUnsigned:
                    case OpCode.Int64Equal:
                    case OpCode.Int64NotEqual:
                    case OpCode.Int64LessThanSigned:
                    case OpCode.Int64LessThanUnsigned:
                    case OpCode.Int64GreaterThanSigned:
                    case OpCode.Int64GreaterThanUnsigned:
                    case OpCode.Int64LessThanOrEqualSigned:
                    case OpCode.Int64LessThanOrEqualUnsigned:
                    case OpCode.Int64GreaterThanOrEqualSigned:
                    case OpCode.Int64GreaterThanOrEqualUnsigned:
                    case OpCode.Int32CountLeadingZeroes:
                    case OpCode.Int32CountTrailingZeroes:
                    case OpCode.Int32CountOneBits:
                    case OpCode.Int32Add:
                    case OpCode.Int32Subtract:
                    case OpCode.Int32Multiply:
                    case OpCode.Int32DivideSigned:
                    case OpCode.Int32DivideUnsigned:
                    case OpCode.Int32RemainderSigned:
                    case OpCode.Int32RemainderUnsigned:
                    case OpCode.Int32And:
                    case OpCode.Int32Or:
                    case OpCode.Int32ExclusiveOr:
                    case OpCode.Int32ShiftLeft:
                    case OpCode.Int32ShiftRightSigned:
                    case OpCode.Int32ShiftRightUnsigned:
                    case OpCode.Int32RotateLeft:
                    case OpCode.Int32RotateRight:
                    case OpCode.Int64CountLeadingZeroes:
                    case OpCode.Int64CountTrailingZeroes:
                    case OpCode.Int64CountOneBits:
                    case OpCode.Int64Add:
                    case OpCode.Int64Subtract:
                    case OpCode.Int64Multiply:
                    case OpCode.Int64DivideSigned:
                    case OpCode.Int64DivideUnsigned:
                    case OpCode.Int64RemainderSigned:
                    case OpCode.Int64RemainderUnsigned:
                    case OpCode.Int64And:
                    case OpCode.Int64Or:
                    case OpCode.Int64ExclusiveOr:
                    case OpCode.Int64ShiftLeft:
                    case OpCode.Int64ShiftRightSigned:
                    case OpCode.Int64ShiftRightUnsigned:
                    case OpCode.Int64RotateLeft:
                    case OpCode.Int64RotateRight:
                    case OpCode.Int32WrapInt64:
                    case OpCode.Int32TruncateFloat32Signed:
                    case OpCode.Int32TruncateFloat32Unsigned:
                    case OpCode.Int32TruncateFloat64Signed:
                    case OpCode.Int32TruncateFloat64Unsigned:
                    case OpCode.Int64ExtendInt32Signed:
                    case OpCode.Int64ExtendInt32Unsigned:
                    case OpCode.Int64TruncateFloat32Signed:
                    case OpCode.Int64TruncateFloat32Unsigned:
                    case OpCode.Int64TruncateFloat64Signed:
                    case OpCode.Int64TruncateFloat64Unsigned:
                    case OpCode.Float32ConvertInt32Signed:
                    case OpCode.Float32ConvertInt32Unsigned:
                    case OpCode.Float32ConvertInt64Signed:
                    case OpCode.Float32ConvertInt64Unsigned:
                    case OpCode.Float32DemoteFloat64:
                    case OpCode.Float64ConvertInt32Signed:
                    case OpCode.Float64ConvertInt32Unsigned:
                    case OpCode.Float64ConvertInt64Signed:
                    case OpCode.Float64ConvertInt64Unsigned:
                    case OpCode.Float64PromoteFloat32:
                    case OpCode.Int32ReinterpretFloat32:
                    case OpCode.Int64ReinterpretFloat64:
                    case OpCode.Float32ReinterpretInt32:
                    case OpCode.Float64ReinterpretInt64:
                    case OpCode.Int32Extend8Signed:
                    case OpCode.Int32Extend16Signed:
                    case OpCode.Int64Extend8Signed:
                    case OpCode.Int64Extend16Signed:
                    case OpCode.Int64Extend32Signed:
                    case OpCode.MiscellaneousOperationPrefix:
                    default:
                        throw new NotSupportedException($"Unknown OpCode: '{instruction.OpCode}'");
                }
            }

            await EmitReturn(stack);
            scope.CheckEmpty();
        }

        await writer.AppendLine();

        async Task EmitReturn(StackBuilder stack)
        {
            var returns = (from @return in funcType.Returns
                           let localName = stack.Pop(@return)
                           select localName).ToArray();
            if (returns.Length != 0)
                await writer.AppendLine($"return ({string.Join(", ", returns)});");
        }

        async Task EmitPrefixUnaryFunction(StackBuilder stack, WebAssemblyValueType type, string func)
        {
            await EmitPrefixUnaryTransform(stack, type, type, func);
        }

        async Task EmitPrefixUnaryTransform(StackBuilder stack, WebAssemblyValueType typeIn, WebAssemblyValueType typeOut, string func)
        {
            var v = stack.Pop(typeIn);
            var expr = $"{func}({v})";
            await stack.Push(typeOut, expr);
        }

        async Task EmitUnaryFunction(StackBuilder stack, WebAssemblyValueType type, string funcFormat)
        {
            await EmitUnaryTransform(stack, type, type, funcFormat);
        }

        async Task EmitUnaryTransform(StackBuilder stack, WebAssemblyValueType typeIn, WebAssemblyValueType typeOut, string funcFormat)
        {
            var v = stack.Pop(typeIn);
            var expr = string.Format(funcFormat, v);
            await stack.Push(typeOut, expr);
        }

        async Task EmitBinaryFunction(StackBuilder stack, WebAssemblyValueType type, string funcFormat)
        {
            await EmitBinaryTransform(stack, type, type, funcFormat);
        }

        async Task EmitBinaryTransform(StackBuilder stack, WebAssemblyValueType typeIn, WebAssemblyValueType typeOut, string funcFormat)
        {
            var a = stack.Pop(typeIn);
            var b = stack.Pop(typeIn);
            var expr = string.Format(funcFormat, a, b);
            await stack.Push(typeOut, expr);
        }
    }

    private class StackBuilder(IndentedTextWriter Writer)
    {
        private int _index;
        private readonly Stack<(WebAssemblyValueType, string)> _stack = [ ];

        #region push
        public async Task Push(WebAssemblyValueType type, string value, bool @const = false)
        {
            var name = $"stack{_index++}";
            await Writer.AppendLine($"{(@const ? "const " : "")}{type.ToDotnetType()} {name} = ({value});");
            _stack.Push((type, name));
        }

        public async Task Push(int value)
        {
            await Push(WebAssemblyValueType.Int32, value.ToString(), @const:true);
        }

        public async Task Push(long value)
        {
            await Push(WebAssemblyValueType.Int64, value.ToString(), @const: true);
        }

        public async Task Push(float value)
        {
            await Push(WebAssemblyValueType.Float32, value.ToString(CultureInfo.InvariantCulture), @const: true);
        }

        public async Task Push(double value)
        {
            await Push(WebAssemblyValueType.Float64, value.ToString(CultureInfo.InvariantCulture), @const: true);
        }
        #endregion

        public string Pop(WebAssemblyValueType type)
        {
            var (t, n) = _stack.Pop();
            if (t != type)
                throw new InvalidOperationException($"Tried to pop '{type}' but found '{t}'");
            return n;
        }

        public string Pop(out WebAssemblyValueType type)
        {
            var (t, n) = _stack.Pop();
            type = t;
            return n;
        }
    }

    private class ScopeChecker
    {
        private readonly Stack<string> _scopes = [ ];

        public ScopeChecker(string? scope = null)
        {
            if (scope != null)
                Push(scope);
        }

        public void Push(string scope)
        {
            _scopes.Push(scope);
        }

        public void Pop()
        {
            _scopes.Pop();
        }

        public void CheckEmpty()
        {
            if (_scopes.Count != 0)
                throw new InvalidOperationException($"Scopes stack is not empty: [{string.Join(", ", _scopes)}]");
        }
    }
}