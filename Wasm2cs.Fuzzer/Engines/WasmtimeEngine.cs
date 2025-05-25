using Wasmtime;

namespace Wasm2cs.Fuzzer.Engines;

public class WasmtimeEngine
    : IWasmEngine
{
    private readonly Engine _engine;

    public WasmtimeEngine(Engine engine)
    {
        _engine = engine;
    }

    public async Task<RunResult> Run(byte[] program, int id)
    {
        try
        {
            using var store = new Store(_engine);
            store.Fuel = 10_000_000;

            using var module = Module.FromBytes(_engine, "module", program);
            var instance = new Instance(store, module);

            var func = instance.GetFunction<int, int>("main");
            if (func == null)
                return new RunResult { MissingMain = true };

            int result;
            try
            {
                result = func.Invoke(id);
            }
            catch (TrapException ex)
            {
                return new RunResult { TrapResult = (int)ex.Type };
            }
            catch (WasmtimeException ex)
            {
                return new RunResult { IndeterminateResult = true };
            }

            return new RunResult
            {
                ReturnValue = result,
                MemoryHash = Hash(instance.GetMemory("memory")),
            };
        }
        catch (Exception ex)
        {
            return new RunResult { IndeterminateResult = true };
        }
    }

    private static int Hash(Memory? memory)
    {
        if (memory == null)
            return 0;

        return RunResult.CalculateMemoryHash(memory.GetSpan(0, (int)Math.Min(int.MaxValue, memory.GetLength())));
    }
}