using System.Reflection;
using Wasmtime;
using Wazzy.Extensions;
using Wazzy.WasiSnapshotPreview1.Clock;
using Wazzy.WasiSnapshotPreview1.Environment;
using Wazzy.WasiSnapshotPreview1.FileSystem.Implementations;
using Wazzy.WasiSnapshotPreview1.Process;
using Wazzy.WasiSnapshotPreview1.Random;
using Module = Wasmtime.Module;

namespace Wasm2cs.Fuzzer.Mutation;

public sealed class Mutator
    : IDisposable
{
    private readonly ulong _fuel;
    private readonly Engine _engine;
    private readonly Module _module;

    public Mutator(ulong fuel = 10_000_000_000)
    {
        _fuel = fuel;
        _engine = new Engine(new Config()
           .WithFuelConsumption(true)
        );
        _module = Module.FromStream(_engine, "mutate", Assembly.GetExecutingAssembly().GetManifestResourceStream("Wasm2cs.Fuzzer.Mutation.mutate_wasm.wasm")!);
    }

    public byte[]? Mutate(int seed, ReadOnlySpan<byte> source)
    {
        using var store = new Store(_engine);
        store.Fuel = _fuel;
        store.SetWasiConfiguration(new WasiConfiguration());

        using var linker = new Linker(_engine);
        linker.DefineFeature(new SeededRandomSource(seed));
        linker.DefineFeature(new ManualClock(DateTime.UnixEpoch, TimeSpan.FromMilliseconds(1)));
        linker.DefineFeature(new WriteToConsoleFilesystem());
        linker.DefineFeature(new BasicEnvironment());
        linker.DefineFeature(new ThrowExitProcess());

        var mutatorInstance = linker.Instantiate(store, _module);
        var mutatorMemory = mutatorInstance.GetMemory("memory")!;

        try
        {
            var buffer_alloc = mutatorInstance.GetFunction<int, int>("buffer_alloc")!;
            var buffer_get_ptr = mutatorInstance.GetFunction<int, int>("buffer_get_ptr")!;
            var buffer_get_size = mutatorInstance.GetFunction<int, int>("buffer_get_size")!;
            var mutate_wasm = mutatorInstance.GetFunction<int, long, int>("mutate_wasm")!;

            // Create a buffer and copy code into it
            var code = buffer_alloc(source.Length);
            source.CopyTo(mutatorMemory.GetSpan(buffer_get_ptr(code), buffer_get_size(code)));

            // Run mutation
            var resultBuffer = mutate_wasm(code, seed);
            var resultSpan = mutatorMemory.GetSpan(buffer_get_ptr(resultBuffer), buffer_get_size(resultBuffer));

            var err = Module.Validate(_engine, resultSpan);
            if (err != null)
                return null;

            return resultSpan.ToArray();
        }
        catch (TrapException)
        {
            return null;
        }
        catch (WasmtimeException)
        {
            return null;
        }
    }

    public void Dispose()
    {
        _engine.Dispose();
        _module.Dispose();
    }
}