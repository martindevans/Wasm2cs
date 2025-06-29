using System;
using System.Globalization;
using System.Numerics;
using Wacs.Core;
using Wacs.Core.Instructions;
using Wacs.Core.Instructions.Memory;
using Wacs.Core.Instructions.Numeric;
using Wacs.Core.OpCodes;
using Wacs.Core.Types;
using Wacs.Core.Types.Defs;
using Wasm2cs.CodeGeneration.Exceptions;
using Wasm2cs.CodeGeneration.Extensions;

namespace Wasm2cs.CodeGeneration.Work;

internal class ModuleFunction(Module.Function Function)
    : IWorkItem
{
    public async Task Emit(IndentedTextWriter writer, Module module)
    {
        var funcType = (FunctionType)module.Types[Function.TypeIndex.Value].SubTypes.Single().Body;
        var name = NameConventions.Function(Function.Index);

        await using (await writer.Method(
                         name,
                         @static: false,
                         @public: false,
                         funcType.ParameterTypes.Types.ParameterList(),
                         funcType.ResultType.Types.ReturnType()
                     ))
        {
            // Suppress some warnings
            await writer.AppendLine("#pragma warning disable IDE0059 // Unnecessary assignment of a value");
            await writer.AppendLine("// ReSharper disable RedundantNameQualifier");
            await writer.AppendLine("// ReSharper disable BuiltInTypeReferenceStyle");
            await writer.AppendLine("// ReSharper disable SuggestVarOrType_BuiltInTypes");
            await writer.AppendLine("// ReSharper disable InlineTemporaryVariable");
            await writer.AppendLine("// ReSharper disable ConvertToConstant.Local");
            await writer.AppendLine("");

            // Create all locals
            var localIdx = 0u;
            var locals = new List<(string localName, ValType Type)>();

            // Parameters
            foreach (var paramType in funcType.ParameterTypes.Types)
            {
                var argName = NameConventions.FunctionArg(localIdx);
                var localName = NameConventions.Local(localIdx++);
                locals.Add((localName, paramType));
                await writer.AppendLine($"{paramType.ToDotnetType().Name} {localName} = {argName};");
            }

            // Explicit locals
            foreach (var local in Function.Locals)
            {
                var localName = NameConventions.Local(localIdx++);
                locals.Add((localName, local));
                await writer.AppendLine($"{local.ToDotnetType().Name} {localName} = default;");
            }

            // Some counters
            var tmpVarIdx = 0u;
            var blockIdx = 0u;

            // Emit instructions
            var stack = new StackBuilder(writer);
            var scope = new ScopeChecker();
            var instructions = Function.Body.Flatten().ToArray();
            for (var i = 0; i < instructions.Length; i++)
            {
                var instruction = instructions[i];
            
                await writer.AppendLine($"// {instruction.Op.x00}");

                switch (instruction.Op.x00)
                {
                    case OpCode.Nop:
                        {
                            await writer.AppendLine("// nop");
                            break;
                        }

                    case OpCode.Return:
                        {
                            await EmitReturn(stack);
                            break;
                        }

                    #region control flow
                    case OpCode.End:
                        {
                            await scope.Pop(writer);
                            break;
                        }

                    case OpCode.Block:
                        {
                            await scope.EnterBlock(writer, NameConventions.BlockLabel(blockIdx++));
                            break;
                        }

                    case OpCode.Loop:
                        {
                            await scope.EnterLoop(writer, NameConventions.BlockLabel(blockIdx++));
                            break;
                        }
                    #endregion

                    #region int32
                    case OpCode.I32Const:
                    {
                        var ci32 = (InstI32Const)instruction;
                        await stack.Push(ci32.Value);
                        break;
                    }

                    case OpCode.I32WrapI64:
                    {
                        var v = stack.Pop(ValType.I64);
                        var expr = $"unchecked((int){v})";
                        await stack.Push(ValType.I32, expr);
                        break;
                    }

                    case OpCode.I32Eq:
                    {
                        await EmitInequality(stack, "==", unsigned: true);
                        break;
                    }

                    case OpCode.I32Eqz:
                    {
                        var v = stack.Pop(ValType.I32);
                        var expr = $"{v} == 0 ? 1 : 0";
                        await stack.Push(ValType.I32, expr);
                        break;
                    }

                    case OpCode.I32Ne:
                    {
                        await EmitInequality(stack, "!=", unsigned: true);
                        break;
                    }

                    case OpCode.I32LtS:
                    {
                        await EmitBinaryTransform(stack, ValType.I32, ValType.I32, "({0} < {1} ? 1 : 0)");
                        break;
                    }

                    case OpCode.I32LtU:
                    {
                        await EmitBinaryTransform(stack, ValType.I32, ValType.I32, "((uint){0} < (uint){1} ? 1 : 0)");
                        break;
                    }

                    case OpCode.I32GtS:
                    {
                        await EmitBinaryTransform(stack, ValType.I32, ValType.I32, "({0} > {1} ? 1 : 0)");
                        break;
                    }

                    case OpCode.I32GtU:
                    {
                        await EmitBinaryTransform(stack, ValType.I32, ValType.I32, "((uint){0} > (uint){1} ? 1 : 0)");
                        break;
                    }

                    case OpCode.I32LeS:
                    {
                        await EmitBinaryTransform(stack, ValType.I32, ValType.I32, "({0} <= {1} ? 1 : 0)");
                        break;
                    }

                    case OpCode.I32LeU:
                    {
                        await EmitBinaryTransform(stack, ValType.I32, ValType.I32, "((uint){0} <= (uint){1} ? 1 : 0)");
                        break;
                    }

                    case OpCode.I32GeS:
                    {
                        await EmitBinaryTransform(stack, ValType.I32, ValType.I32, "({0} >= {1} ? 1 : 0)");
                        break;
                    }

                    case OpCode.I32GeU:
                    {
                        await EmitBinaryTransform(stack, ValType.I32, ValType.I32, "((uint){0} >= (uint){1} ? 1 : 0)");
                        break;
                    }

                    case OpCode.I32And:
                    {
                        await EmitBinaryUnsignedInt32Operator(stack, "&");
                        break;
                    }

                    case OpCode.I32Or:
                    {
                        await EmitBinaryUnsignedInt32Operator(stack, "|");
                        break;
                    }

                    case OpCode.I32Xor:
                    {
                        await EmitBinaryUnsignedInt32Operator(stack, "^");
                        break;
                    }

                    case OpCode.I32Add:
                    {
                        await EmitBinarySignedInt32Operator(stack, "+");
                        break;
                    }

                    case OpCode.I32Sub:
                    {
                        await EmitBinarySignedInt32Operator(stack, "-");
                        break;
                    }

                    case OpCode.I32Mul:
                    {
                        await EmitBinaryFunction(stack, ValType.I32, "unchecked({0} * {1})");
                        break;
                    }

                    case OpCode.I32Shl:
                    {
                        await EmitBinarySignedInt32Operator(stack, "<<");
                        break;
                    }

                    case OpCode.I32ShrU:
                    {
                        await EmitBinaryFunction(
                            stack,
                            ValType.I32,
                            "unchecked((int)((uint){0}) >> ((int){1}))"
                        );
                        break;
                    }

                    case OpCode.I32ShrS:
                    {
                        await EmitBinarySignedInt32Operator(stack, ">>>");
                        break;
                    }

                    case OpCode.I32Rotl:
                    {
                        await EmitBinaryTransform(stack, ValType.I32, ValType.I32, "BitOperations.RotateLeft({0}, {1})");
                        break;
                    }

                    case OpCode.I32Rotr:
                    {
                        await EmitBinaryTransform(stack, ValType.I32, ValType.I32, "BitOperations.RotateRight({0}, {1})");
                        break;
                    }

                    case OpCode.I32Clz:
                    {
                        await EmitUnaryTransform(stack, ValType.I32, ValType.I32, "BitOperations.LeadingZeroCount(unchecked((uint){0}))");
                        break;
                    }

                    case OpCode.I32Ctz:
                    {
                        await EmitUnaryTransform(stack, ValType.I32, ValType.I32, "BitOperations.TrailingZeroCount(unchecked((uint){0}))");
                        break;
                    }

                    case OpCode.I32Popcnt:
                    {
                        await EmitUnaryTransform(stack, ValType.I32, ValType.I32, "BitOperations.PopCount(unchecked((uint){0}))");
                        break;
                    }
                    #endregion

                    #region int64
                    case OpCode.I64Const:
                    {
                        var ci64 = (InstI64Const)instruction;
                        await stack.Push(ci64.GetValue());
                        break;
                    }

                    case OpCode.I64Eqz:
                    {
                        var v = stack.Pop(ValType.I64);
                        var expr = $"{v} == 0L ? 1 : 0";
                        await stack.Push(ValType.I32, expr);
                        break;
                    }

                    case OpCode.I64Eq:
                    {
                        await EmitBinaryTransform(stack, ValType.I64, ValType.I32, "({0} == {1} ? 1 : 0)");
                        break;
                    }

                    case OpCode.I64Ne:
                    {
                        await EmitBinaryTransform(stack, ValType.I64, ValType.I32, "({0} != {1} ? 1 : 0)");
                        break;
                    }

                    case OpCode.I64LtS:
                    {
                        await EmitBinaryTransform(stack, ValType.I64, ValType.I32, "({0} < {1} ? 1 : 0)");
                        break;
                    }

                    case OpCode.I64LtU:
                    {
                        await EmitBinaryTransform(stack, ValType.I64, ValType.I32, "((ulong){0} < (ulong){1} ? 1 : 0)");
                        break;
                    }

                    case OpCode.I64GtS:
                    {
                        await EmitBinaryTransform(stack, ValType.I64, ValType.I32, "({0} > {1} ? 1 : 0)");
                        break;
                    }

                    case OpCode.I64GtU:
                    {
                        await EmitBinaryTransform(stack, ValType.I64, ValType.I32, "((ulong){0} > (ulong){1} ? 1 : 0)");
                        break;
                    }

                    case OpCode.I64LeS:
                    {
                        await EmitBinaryTransform(stack, ValType.I64, ValType.I32, "({0} <= {1} ? 1 : 0)");
                        break;
                    }

                    case OpCode.I64LeU:
                    {
                        await EmitBinaryTransform(stack, ValType.I64, ValType.I32, "((ulong){0} <= (ulong){1} ? 1 : 0)");
                        break;
                    }

                    case OpCode.I64GeS:
                    {
                        await EmitBinaryTransform(stack, ValType.I64, ValType.I32, "({0} >= {1} ? 1 : 0)");
                        break;
                    }

                    case OpCode.I64GeU:
                    {
                        await EmitBinaryTransform(stack, ValType.I64, ValType.I32, "((ulong){0} >= (ulong){1} ? 1 : 0)");
                        break;
                    }

                    case OpCode.I64Clz:
                    {
                        await EmitUnaryTransform(stack, ValType.I64, ValType.I64, "BitOperations.LeadingZeroCount({0})");
                        break;
                    }

                    case OpCode.I64Ctz:
                    {
                        await EmitUnaryTransform(stack, ValType.I64, ValType.I64, "BitOperations.TrailingZeroCount({0})");
                        break;
                    }

                    case OpCode.I64Popcnt:
                    {
                        await EmitUnaryTransform(stack, ValType.I64, ValType.I64, "BitOperations.PopCount({0})");
                        break;
                    }

                    case OpCode.I64Add:
                    {
                        await EmitBinaryFunction(stack, ValType.I64, "unchecked({0} + {1})");
                        break;
                    }

                    case OpCode.I64Sub:
                    {
                        await EmitBinaryFunction(stack, ValType.I64, "unchecked({0} - {1})");
                        break;
                    }

                    case OpCode.I64Mul:
                    {
                        await EmitBinaryFunction(stack, ValType.I64, "unchecked({0} * {1})");
                        break;
                    }

                    case OpCode.I64Rotl:
                    {
                        await EmitBinaryTransform(stack, ValType.I64, ValType.I64, "BitOperations.RotateLeft({0}, {1})");
                        break;
                    }

                    case OpCode.I64Rotr:
                    {
                        await EmitBinaryTransform(stack, ValType.I64, ValType.I64, "BitOperations.RotateRight({0}, {1})");
                        break;
                    }

                    case OpCode.I64And:
                    {
                        await EmitBinaryFunction(stack, ValType.I64, "{0} & {1}");
                        break;
                    }

                    case OpCode.I64Or:
                    {
                        await EmitBinaryFunction(stack, ValType.I64, "{0} | {1}");
                        break;
                    }

                    case OpCode.I64Xor:
                    {
                        await EmitBinaryFunction(stack, ValType.I64, "{0} ^ {1}");
                        break;
                    }

                    case OpCode.I64Shl:
                    {
                        await EmitBinaryFunction(stack, ValType.I64, "{0} << (int)({1} & 63)");
                        break;
                    }

                    case OpCode.I64ShrS:
                    {
                        await EmitBinaryFunction(stack, ValType.I64, "{0} >> (int)({1} & 63)");
                        break;
                    }

                    case OpCode.I64ShrU:
                    {
                        await EmitBinaryFunction(
                            stack,
                            ValType.I64,
                            "unchecked((long)((ulong){0} >> (int)({1} & 63)))"
                        );
                        break;
                    }
                    #endregion

                    #region float32
                    case OpCode.F32Const:
                        {
                            var f32 = (InstF32Const)instruction;
                            await stack.Push(f32.GetValue());
                            break;
                        }

                    case OpCode.F32Eq:
                        {
                            await EmitBinaryTransform(stack, ValType.F32, ValType.I32, "({0} == {1} ? 1 : 0)");
                            break;
                        }

                    case OpCode.F32Ne:
                        {
                            await EmitBinaryTransform(stack, ValType.F32, ValType.I32, "({0} != {1} ? 1 : 0)");
                            break;
                        }

                    case OpCode.F32Lt:
                        {
                            await EmitBinaryTransform(stack, ValType.F32, ValType.I32, "({0} < {1} ? 1 : 0)");
                            break;
                        }

                    case OpCode.F32Gt:
                        {
                            await EmitBinaryTransform(stack, ValType.F32, ValType.I32, "({0} > {1} ? 1 : 0)");
                            break;
                        }

                    case OpCode.F32Le:
                        {
                            await EmitBinaryTransform(stack, ValType.F32, ValType.I32, "({0} <= {1} ? 1 : 0)");
                            break;
                        }

                    case OpCode.F32Ge:
                        {
                            await EmitBinaryTransform(stack, ValType.F32, ValType.I32, "({0} >= {1} ? 1 : 0)");
                            break;
                        }

                    case OpCode.F32Abs:
                        {
                            await EmitPrefixUnaryTransform(stack, ValType.F32, ValType.F32, "MathF.Abs");
                            break;
                        }

                    case OpCode.F32Neg:
                        {
                            await EmitPrefixUnaryTransform(stack, ValType.F32, ValType.F32, "-");
                            break;
                        }

                    case OpCode.F32Ceil:
                        {
                            await EmitPrefixUnaryTransform(stack, ValType.F32, ValType.F32, "MathF.Ceiling");
                            break;
                        }

                    case OpCode.F32Floor:
                        {
                            await EmitPrefixUnaryTransform(stack, ValType.F32, ValType.F32, "MathF.Floor");
                            break;
                        }

                    case OpCode.F32Trunc:
                        {
                            await EmitPrefixUnaryTransform(stack, ValType.F32, ValType.F32, "MathF.Truncate");
                            break;
                        }

                    case OpCode.F32Nearest:
                        {
                            await EmitUnaryTransform(stack, ValType.F32, ValType.F32, "MathF.Round({0}, MidpointRounding.ToEven)");
                            break;
                        }

                    case OpCode.F32Sqrt:
                        {
                            await EmitPrefixUnaryTransform(stack, ValType.F32, ValType.F32, "MathF.Sqrt");
                            break;
                        }

                    case OpCode.F32Add:
                        {
                            await EmitBinaryFunction(stack, ValType.F32, "{0} + {1}");
                            break;
                        }

                    case OpCode.F32Sub:
                        {
                            await EmitBinaryFunction(stack, ValType.F32, "{0} - {1}");
                            break;
                        }

                    case OpCode.F32Mul:
                        {
                            await EmitBinaryFunction(stack, ValType.F32, "{0} * {1}");
                            break;
                        }

                    case OpCode.F32Div:
                        {
                            await EmitBinaryFunction(stack, ValType.F32, "{0} / {1}");
                            break;
                        }

                    case OpCode.F32Min:
                        {
                            await EmitBinaryFunction(stack, ValType.F32, "MathF.Min({0}, {1})");
                            break;
                        }

                    case OpCode.F32Max:
                        {
                            await EmitBinaryFunction(stack, ValType.F32, "MathF.Max({0}, {1})");
                            break;
                        }

                    case OpCode.F32Copysign:
                        {
                            await EmitBinaryFunction(stack, ValType.F32, "MathF.CopySign({0}, {1})");
                            break;
                        }
                    #endregion

                    #region float64
                    case OpCode.F64Const:
                        {
                            var f64 = (InstF64Const)instruction;
                            await stack.Push(f64.GetValue());
                            break;
                        }

                    case OpCode.F64Eq:
                        {
                            await EmitBinaryTransform(stack, ValType.F64, ValType.I32, "({0} == {1} ? 1 : 0)");
                            break;
                        }

                    case OpCode.F64Ne:
                        {
                            await EmitBinaryTransform(stack, ValType.F64, ValType.I32, "({0} != {1} ? 1 : 0)");
                            break;
                        }

                    case OpCode.F64Lt:
                        {
                            await EmitBinaryTransform(stack, ValType.F64, ValType.I32, "({0} < {1} ? 1 : 0)");
                            break;
                        }

                    case OpCode.F64Gt:
                        {
                            await EmitBinaryTransform(stack, ValType.F64, ValType.I32, "({0} > {1} ? 1 : 0)");
                            break;
                        }

                    case OpCode.F64Le:
                        {
                            await EmitBinaryTransform(stack, ValType.F64, ValType.I32, "({0} <= {1} ? 1 : 0)");
                            break;
                        }

                    case OpCode.F64Ge:
                        {
                            await EmitBinaryTransform(stack, ValType.F64, ValType.I32, "({0} >= {1} ? 1 : 0)");
                            break;
                        }

                    case OpCode.F64Abs:
                        {
                            await EmitPrefixUnaryTransform(stack, ValType.F64, ValType.F64, "Math.Abs");
                            break;
                        }

                    case OpCode.F64Neg:
                        {
                            await EmitPrefixUnaryTransform(stack, ValType.F64, ValType.F64, "-");
                            break;
                        }

                    case OpCode.F64Ceil:
                        {
                            await EmitPrefixUnaryTransform(stack, ValType.F64, ValType.F64, "Math.Ceiling");
                            break;
                        }

                    case OpCode.F64Floor:
                        {
                            await EmitPrefixUnaryTransform(stack, ValType.F64, ValType.F64, "Math.Floor");
                            break;
                        }

                    case OpCode.F64Trunc:
                        {
                            await EmitPrefixUnaryTransform(stack, ValType.F64, ValType.F64, "Math.Truncate");
                            break;
                        }

                    case OpCode.F64Nearest:
                        {
                            await EmitUnaryTransform(stack, ValType.F64, ValType.F64, "Math.Round({0}, MidpointRounding.ToEven)");
                            break;
                        }

                    case OpCode.F64Sqrt:
                        {
                            await EmitPrefixUnaryTransform(stack, ValType.F64, ValType.F64, "Math.Sqrt");
                            break;
                        }

                    case OpCode.F64Add:
                        {
                            await EmitBinaryFunction(stack, ValType.F64, "{0} + {1}");
                            break;
                        }

                    case OpCode.F64Sub:
                        {
                            await EmitBinaryFunction(stack, ValType.F64, "{0} - {1}");
                            break;
                        }

                    case OpCode.F64Mul:
                        {
                            await EmitBinaryFunction(stack, ValType.F64, "{0} * {1}");
                            break;
                        }

                    case OpCode.F64Div:
                        {
                            await EmitBinaryFunction(stack, ValType.F64, "{0} / {1}");
                            break;
                        }

                    case OpCode.F64Min:
                        {
                            await EmitBinaryFunction(stack, ValType.F64, "Math.Min({0}, {1})");
                            break;
                        }

                    case OpCode.F64Max:
                        {
                            await EmitBinaryFunction(stack, ValType.F64, "Math.Max({0}, {1})");
                            break;
                        }

                    case OpCode.F64Copysign:
                        {
                            await EmitBinaryFunction(stack, ValType.F64, "Math.CopySign({0}, {1})");
                            break;
                        }
                    #endregion

                    case OpCode.Call:
                        {
                            var call = (InstCall)instruction;

                            var func = module.GetFunction(call.X);
                            var type = (FunctionType)module.Types[func.TypeIndex.Value].SubTypes.Single().Body;
                            //var type = (FunctionType)module.Types[(int)call.X.Value].SubTypes.Single().Body;

                            var inputs = (from parameter in type.ParameterTypes.Types
                                          let localName = stack.Pop(parameter)
                                          select localName).ToArray();

                            var callName = NameConventions.Function(call.X);
                            var parameters = string.Join(", ", inputs);
                            var expr = $"{callName}({parameters})";

                            if (type.ResultType.Arity == 0)
                            {
                                await writer.AppendLine($"{expr};");
                            }
                            else if (type.ResultType.Arity == 1)
                            {
                                await stack.Push(type.ResultType.Types.Single(), expr);
                            }
                            else
                            {
                                // Make call, assigning results to temps
                                var tmps = type.ResultType.Types.Select(type => (type, name: $"call_return_tmp{tmpVarIdx++}")).ToArray();
                                var tuple = string.Join(", ", tmps.Select(a => $"{a.type} {a.name}"));
                                await writer.AppendLine($"{tuple} = {expr};");

                                // Push returned results to stack
                                foreach (var item in tmps)
                                    await stack.Push(item.type, item.name);
                            }

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
                            var c = stack.Pop(ValType.I32);

                            // Select based on discriminator
                            var expr = $"({c} != 0 ? {a} : {b})";
                            await stack.Push(aType, expr);
                            break;
                        }

                    #region locals
                    case OpCode.LocalGet:
                        {
                            var localGet = (InstLocalGet)instruction;
                            var local = locals[localGet.GetIndex()];
                            await stack.Push(local.Type, local.localName);
                            break;
                        }

                    case OpCode.LocalSet:
                        {
                            var localSet = (InstLocalSet)instruction;
                            var local = locals[localSet.GetIndex()];
                            var stackName = stack.Pop(local.Type);
                            await writer.AppendLine($"{stackName} = {local.localName};");
                            break;
                        }

                    case OpCode.LocalTee:
                        {
                            var localTee = (InstLocalTee)instruction;
                            var local = locals[localTee.GetIndex()];
                            var stackName = stack.Pop(local.Type);
                            await writer.AppendLine($"{stackName} = {local.localName};");
                            await stack.Push(local.Type, local.localName);
                            break;
                        }
                    #endregion

                    #region globals
                    case OpCode.GlobalGet:
                        {
                            var globalGet = (InstGlobalGet)instruction;
                            var globalIdx = globalGet.GetIndex();
                            var global = module.Globals[(int)globalIdx.Value];

                            await stack.Push(global.Type.ContentType, NameConventions.Global(globalIdx));
                            break;
                        }

                    case OpCode.GlobalSet:
                        {
                            var globalSet = (InstGlobalSet)instruction;
                            var globalIdx = globalSet.GetIndex();
                            var global = module.Globals[(int)globalIdx.Value];

                            if (global.Type.Mutability != Mutability.Mutable)
                                throw new CannotSetImmutableGlobal(globalIdx);

                            var v = stack.Pop(global.Type.ContentType);
                            await writer.AppendLine($"{NameConventions.Global(globalIdx)} = {v};");

                            break;
                        }
                    #endregion

                    #region memory
                    case OpCode.MemorySize:
                    {
                        var n = NameConventions.Memory("0");
                        await stack.Push(ValType.I32, $"{n}.Size");
                        break;
                    }

                    case OpCode.MemoryGrow:
                    {
                        var n = NameConventions.Memory("0");
                        var pages = stack.Pop(ValType.I32);
                        var expr = $"{n}.Grow({pages})";
                        await stack.Push(ValType.I32, expr);
                        break;
                    }
                    #endregion

                    #region Memory I32
                    case OpCode.I32Load:
                    {
                        await EmitMemoryLoad(stack, instruction, "ReadI32", ValType.I32);
                        break;
                    }

                    case OpCode.I32Store:
                    {
                        await EmitMemoryStore(stack, writer, instruction, "WriteI32", ValType.I32);
                        break;
                    }

                    case OpCode.I32Load8S:
                    {
                        await EmitMemoryLoad(stack, instruction, "ReadI8", ValType.I32);
                        break;
                    }

                    case OpCode.I32Load8U:
                    {
                        await EmitMemoryLoad(stack, instruction, "ReadU8", ValType.I32);
                        break;
                    }

                    case OpCode.I32Load16S:
                    {
                        await EmitMemoryLoad(stack, instruction, "ReadI16", ValType.I32);
                        break;
                    }

                    case OpCode.I32Load16U:
                    {
                        await EmitMemoryLoad(stack, instruction, "ReadU16", ValType.I32);
                        break;
                    }
                    #endregion

                    #region Memory I64
                    case OpCode.I64Load:
                    {
                        await EmitMemoryLoad(stack, instruction, "ReadI64", ValType.I64);
                        break;
                    }

                    case OpCode.I64Store:
                    {
                        await EmitMemoryStore(stack, writer, instruction, "WriteI64", ValType.I64);
                        break;
                    }

                    case OpCode.I64Load8S:
                    {
                        await EmitMemoryLoad(stack, instruction, "ReadI8", ValType.I64);
                        break;
                    }

                    case OpCode.I64Load8U:
                    {
                        await EmitMemoryLoad(stack, instruction, "ReadU8", ValType.I64);
                        break;
                    }

                    case OpCode.I64Load16S:
                    {
                        await EmitMemoryLoad(stack, instruction, "ReadI16", ValType.I64);
                        break;
                    }

                    case OpCode.I64Load16U:
                    {
                        await EmitMemoryLoad(stack, instruction, "ReadU16", ValType.I64);
                        break;
                    }

                    case OpCode.I64Load32S:
                    {
                        await EmitMemoryLoad(stack, instruction, "ReadI32", ValType.I64);
                        break;
                    }

                    case OpCode.I64Load32U:
                    {
                        await EmitMemoryLoad(stack, instruction, "ReadU32", ValType.I64);
                        break;
                    }
                    #endregion

                    #region Memory F32
                    case OpCode.F32Load:
                    {
                        await EmitMemoryLoad(stack, instruction, "ReadF32", ValType.F32);
                        break;
                    }
                    case OpCode.F32Store:
                    {
                        await EmitMemoryStore(stack, writer, instruction, "WriteF32", ValType.F32);
                        break;
                    }
                    #endregion

                    #region Memory F64
                    case OpCode.F64Load:
                    {
                        await EmitMemoryLoad(stack, instruction, "ReadF64", ValType.F64);
                        break;
                    }
                    case OpCode.F64Store:
                    {
                        await EmitMemoryStore(stack, writer, instruction, "WriteF64", ValType.F64);
                        break;
                    }
                    #endregion

                    case OpCode.If:
                    case OpCode.Else:
                    case OpCode.Br:
                    case OpCode.BrIf:
                    case OpCode.BrTable:
                    case OpCode.CallIndirect:
                    case OpCode.ReturnCall:
                    case OpCode.ReturnCallIndirect:
                    case OpCode.CallRef:
                    case OpCode.ReturnCallRef:
                    case OpCode.TryTable:
                    case OpCode.Throw:
                    case OpCode.ThrowRef:
                    case OpCode.RefNull:
                    case OpCode.RefIsNull:
                    case OpCode.RefFunc:
                    case OpCode.RefEq:
                    case OpCode.RefAsNonNull:
                    case OpCode.BrOnNull:
                    case OpCode.BrOnNonNull:
                    case OpCode.SelectT:
                    case OpCode.TableGet:
                    case OpCode.TableSet:
                    case OpCode.I32Store8:
                    case OpCode.I32Store16:
                    case OpCode.I64Store8:
                    case OpCode.I64Store16:
                    case OpCode.I64Store32:
                    case OpCode.I32DivS:
                    case OpCode.I32DivU:
                    case OpCode.I32RemS:
                    case OpCode.I32RemU:
                    case OpCode.I64DivS:
                    case OpCode.I64DivU:
                    case OpCode.I64RemS:
                    case OpCode.I64RemU:
                    case OpCode.I32TruncF32S:
                    case OpCode.I32TruncF32U:
                    case OpCode.I32TruncF64S:
                    case OpCode.I32TruncF64U:
                    case OpCode.I64ExtendI32S:
                    case OpCode.I64ExtendI32U:
                    case OpCode.I64TruncF32S:
                    case OpCode.I64TruncF32U:
                    case OpCode.I64TruncF64S:
                    case OpCode.I64TruncF64U:
                    case OpCode.F32ConvertI32S:
                    case OpCode.F32ConvertI32U:
                    case OpCode.F32ConvertI64S:
                    case OpCode.F32ConvertI64U:
                    case OpCode.F32DemoteF64:
                    case OpCode.F64ConvertI32S:
                    case OpCode.F64ConvertI32U:
                    case OpCode.F64ConvertI64S:
                    case OpCode.F64ConvertI64U:
                    case OpCode.F64PromoteF32:
                    case OpCode.I32ReinterpretF32:
                    case OpCode.I64ReinterpretF64:
                    case OpCode.F32ReinterpretI32:
                    case OpCode.F64ReinterpretI64:
                    case OpCode.I32Extend8S:
                    case OpCode.I32Extend16S:
                    case OpCode.I64Extend8S:
                    case OpCode.I64Extend16S:
                    case OpCode.I64Extend32S:
                        await writer.AppendLine($"throw new NotImplementedException(\"todo: {instruction.Op.x00}\");");
                        Console.WriteLine($"todo: {instruction.Op.x00}");
                        break;

                    default:
                        throw new UnsupportedWasmInstructionException(instruction.Op.x00);
                }
            }

            await EmitReturn(stack);
            scope.CheckEmpty();
        }

        await writer.AppendLine();

        #region emitters
        async Task EmitReturn(StackBuilder stack)
        {
            var returns = (from @return in funcType.ResultType.Types
                           let localName = stack.Pop(@return)
                           select localName).ToArray();
            if (returns.Length != 0)
                await writer.AppendLine($"return ({string.Join(", ", returns)});");
        }

        //async Task EmitPrefixUnaryFunction(StackBuilder stack, ValType type, string func)
        //{
        //    await EmitPrefixUnaryTransform(stack, type, type, func);
        //}

        async Task EmitPrefixUnaryTransform(StackBuilder stack, ValType typeIn, ValType typeOut, string func)
        {
            var v = stack.Pop(typeIn);
            var expr = $"{func}({v})";
            await stack.Push(typeOut, expr);
        }

        //async Task EmitUnaryFunction(StackBuilder stack, ValType type, string funcFormat)
        //{
        //    await EmitUnaryTransform(stack, type, type, funcFormat);
        //}

        async Task EmitUnaryTransform(StackBuilder stack, ValType typeIn, ValType typeOut, string funcFormat)
        {
            var v = stack.Pop(typeIn);
            var expr = string.Format(funcFormat, v);
            await stack.Push(typeOut, expr);
        }

        async Task EmitBinaryFunction(StackBuilder stack, ValType type, string funcFormat)
        {
            await EmitBinaryTransform(stack, type, type, funcFormat);
        }

        async Task EmitBinaryUnsignedInt32Operator(StackBuilder stack, string @operator, string prefix = "")
        {
            var fmt = $"{prefix}(unchecked((int)(((uint){{0}}) {@operator} unchecked((uint){{1}}))))";
            await EmitBinaryFunction(stack, ValType.I32, fmt);
        }

        async Task EmitBinarySignedInt32Operator(StackBuilder stack, string @operator, string prefix = "")
        {
            var fmt = $"{prefix}(unchecked((int){{0}}) {@operator} unchecked((int){{1}}))";
            await EmitBinaryFunction(stack, ValType.I32, fmt);
        }

        async Task EmitBinarySignedInt64Operator(StackBuilder stack, string @operator, string prefix = "")
        {
            var fmt = $"{prefix}(unchecked((long){{0}}) {@operator} unchecked((long){{1}}))";
            await EmitBinaryFunction(stack, ValType.I32, fmt);
        }

        async Task EmitInequality(StackBuilder stack, string @operator, bool unsigned)
        {
            var fmt = unsigned
                    ? $"Convert.ToInt32(unchecked(((uint){{0}})) {@operator} unchecked(((uint){{1}})))"
                    : $"Convert.ToInt32(unchecked(((int){{0}})) {@operator} unchecked(((int){{1}})))";

            await EmitBinaryFunction(stack, ValType.I32, fmt);
        }

        async Task EmitBinaryTransform(StackBuilder stack, ValType typeIn, ValType typeOut, string funcFormat)
        {
            var a = stack.Pop(typeIn);
            var b = stack.Pop(typeIn);
            var expr = string.Format(funcFormat, a, b);
            await stack.Push(typeOut, expr);
        }

        async Task EmitMemoryLoad(StackBuilder stack, InstructionBase instr, string readMethod, ValType valType)
        {
            var instruction = (InstMemoryLoad)instr;
            var dotnet = valType.ToDotnetType().Name;

            var m = instruction.GetMemArg();
            var n = NameConventions.Memory($"{m.M.Value}");
            var addr = stack.Pop(ValType.I32);

            await stack.Push(valType, $"({dotnet}){n}.{readMethod}({addr} + {m.Offset})");
        }

        async Task EmitMemoryStore(StackBuilder stack, IndentedTextWriter writer, InstructionBase instr, string writeMethod, ValType valType)
        {
            var instruction = (InstMemoryStore)instr;

            var m = instruction.GetMemArg();
            var n = NameConventions.Memory($"{m.M.Value}");
            var addr = stack.Pop(ValType.I32);
            var val = stack.Pop(valType);

            await writer.AppendLine($"{n}.{writeMethod}({val}, {addr} + {m.Offset});");
        }
        #endregion
    }

    private class StackBuilder(IndentedTextWriter Writer)
    {
        private int _index;
        private readonly Stack<(ValType, string)> _stack = [];

        #region push
        public async Task Push(ValType type, string value, bool @const = false)
        {
            var name = $"stack{_index++}";
            await Writer.AppendLine($"{(@const ? "const " : "")}{type.ToDotnetType().Name} {name} = ({value});");
            _stack.Push((type, name));
        }

        public async Task Push(int value)
        {
            await Push(ValType.I32, value.ToString(), @const: true);
        }

        public async Task Push(long value)
        {
            await Push(ValType.I64, value.ToString(), @const: true);
        }

        public async Task Push(float value)
        {
            await Push(ValType.F32, value.ToString(CultureInfo.InvariantCulture), @const: true);
        }

        public async Task Push(double value)
        {
            await Push(ValType.F64, value.ToString(CultureInfo.InvariantCulture), @const: true);
        }
        #endregion

        public string Pop(ValType type)
        {
            var (t, n) = _stack.Pop();
            if (t != type)
                throw new InvalidOperationException($"Tried to pop '{type}' but found '{t}'");
            return n;
        }

        public string Pop(out ValType type)
        {
            var (t, n) = _stack.Pop();
            type = t;
            return n;
        }
    }

    private class ScopeChecker
    {
        private readonly Stack<(bool, string)> _scopes = [];

        public ScopeChecker()
        {
            _scopes.Push((false, ""));
        }

        public Task EnterBlock(IndentedTextWriter writer, string endLabel)
        {
            _scopes.Push((true, endLabel));
            return Task.CompletedTask;
        }

        public async Task EnterLoop(IndentedTextWriter writer, string startLabel)
        {
            _scopes.Push((false, startLabel));
            await writer.AppendLine($"{startLabel}: ;");
        }

        public async Task Pop(IndentedTextWriter writer)
        {
            var (needsWriting, label) = _scopes.Pop();
            if (needsWriting)
                await writer.AppendLine($"{label}: ;");
        }

        public void CheckEmpty()
        {
            if (_scopes.Count != 0)
                throw new InvalidOperationException($"Scopes stack is not empty: [{string.Join(", ", _scopes)}]");
        }
    }
}