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
            var localsNames = new List<string>();
            foreach (var locals in Body.Locals)
            {
                var localType = locals.Type.ToDotnetType();
                for (var i = 0; i < locals.Count; i++)
                {
                    var localName = $"local_{localIdx++}";
                    localsNames.Add(localName);
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

                    default:
                        throw new NotSupportedException($"Unknown OpCode: '{instruction.OpCode}'");
                }
            }

            // Pop function returns
            var returns = (from @return in funcType.Returns
                           let localName = stack.Pop(@return)
                           select localName).ToArray();
            if (returns.Length != 0)
                await writer.AppendLine($"return ({string.Join(", ", returns)});");

            scope.CheckEmpty();
        }

        await writer.AppendLine();
    }

    private class StackBuilder(IndentedTextWriter Writer)
    {
        private int _index;
        private readonly Stack<(WebAssemblyValueType, string)> _stack = [ ];

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

        public string Pop(WebAssemblyValueType type)
        {
            var (t, n) = _stack.Pop();
            if (t != type)
                throw new InvalidOperationException($"Tried to pop '{type}' but found '{t}'");
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