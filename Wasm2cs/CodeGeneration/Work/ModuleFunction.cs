using System.Globalization;
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

                    case OpCode.Int32Constant:
                    {
                        var ci32 = (Int32Constant)instruction;
                        await stack.Push(ci32.Value);
                        break;
                    }

                    case OpCode.Int64Constant:
                    {
                        var ci64 = (Int64Constant)instruction;
                        await stack.Push(ci64.Value);
                        break;
                    }

                    case OpCode.Float32Constant:
                    {
                        var f32 = (Float32Constant)instruction;
                        await stack.Push(f32.Value);
                        break;
                    }

                    case OpCode.Float64Constant:
                    {
                        var f64 = (Float64Constant)instruction;
                        await stack.Push(f64.Value);
                        break;
                    }

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

                    #region locals
                    case OpCode.LocalGet:
                    {
                        var localGet = (LocalGet)instruction;
                        var local = locals[(int)localGet.Index];
                        await stack.Push(local.Type, local.localName, false);
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
                        await stack.Push(local.Type, local.localName, false);
                        break;
                    }
                    #endregion

                    #region globals
                    case OpCode.GlobalGet:
                    {
                        var globalGet = (GlobalGet)instruction;
                        var global = module.Globals[(int)globalGet.Index];
                        await stack.Push(global.ContentType, $"_global_{globalGet.Index}", false);
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
                        await stack.Push(aType, expr, false);
                        break;
                    }

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
    }

    private class StackBuilder(IndentedTextWriter Writer)
    {
        private int _index;
        private readonly Stack<(WebAssemblyValueType, string)> _stack = [ ];

        #region push
        public async Task Push(WebAssemblyValueType type, string value, bool @const)
        {
            var name = $"stack{_index++}";
            await Writer.AppendLine($"{(@const ? "const " : "")}{type.ToDotnetType()} {name} = {value};");
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