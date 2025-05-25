using Wasm2cs.Fuzzer.Engines;
using Wasm2cs.Fuzzer.Fuzzing;
using Wasm2cs.Fuzzer.Mutation;
using Wasmtime;

var fuzzer = new OracleFuzzer(
    new Wasm2csEngine(),
    new WasmtimeEngine(new Engine(new Config().WithFuelConsumption(true))),
    new Mutator(),
    123
);

for (var i = 0; i < 100; i++)
{
    var result = await fuzzer.RunOnce(i);
    if (result == OracleFuzzer.FuzzResult.Indeterminate)
        continue;

    Console.WriteLine($"{i}: {result}");
}