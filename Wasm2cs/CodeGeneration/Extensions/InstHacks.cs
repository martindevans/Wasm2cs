using System.Reflection;
using Wacs.Core.Instructions;
using Wacs.Core.Instructions.Numeric;
using Wacs.Core.Types;

namespace Wasm2cs.CodeGeneration.Extensions;

internal static class InstHacks
{
    public static GlobalIdx GetIndex(this InstGlobalGet get)
    {
        var field = get.GetType().GetField("Index", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (GlobalIdx)field.GetValue(get)!;
    }
    
    public static GlobalIdx GetIndex(this InstGlobalSet get)
    {
        var field = get.GetType().GetField("Index", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (GlobalIdx)field.GetValue(get)!;
    }

    public static MemArg GetMemArg(this InstMemoryLoad load)
    {
        var field = load.GetType().GetField("M", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (MemArg)field.GetValue(load)!;
    }

    public static MemArg GetMemArg(this InstMemoryStore store)
    {
        var field = store.GetType().GetField("M", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (MemArg)field.GetValue(store)!;
    }

    public static long GetValue(this InstI64Const instr)
    {
        var field = instr.GetType().GetField("Value", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (long)field.GetValue(instr)!;
    }

    public static float GetValue(this InstF32Const instr)
    {
        var field = instr.GetType().GetField("Value", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (float)field.GetValue(instr)!;
    }

    public static double GetValue(this InstF64Const instr)
    {
        var field = instr.GetType().GetField("Value", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (double)field.GetValue(instr)!;
    }
}