using System.Reflection;
using Wasm2cs.Fuzzer.Engines;
using Wasm2cs.Fuzzer.Mutation;

namespace Wasm2cs.Fuzzer.Fuzzing;

public class OracleFuzzer
{
    private readonly IWasmEngine _test;
    private readonly IWasmEngine _oracle;
    private readonly Mutator _mutator;

    private byte[] _seed;

    /// <summary>
    /// Create a new fuzzer
    /// </summary>
    /// <param name="test">Engine under test</param>
    /// <param name="oracle">"Oracle" engine which is always correct</param>
    /// <param name="mutator">Mutator to mutate wasm programs</param>
    /// <param name="seed"></param>
    public OracleFuzzer(IWasmEngine test, IWasmEngine oracle, Mutator mutator, int seed)
    {
        _test = test;
        _oracle = oracle;
        _mutator = mutator;

        _seed = [ ];
        Reset(seed);
    }

    public async Task<FuzzResult> RunOnce(int id)
    {
        var expectedTask = Task.Run(() => _oracle.Run(_seed, id));
        var actualTask = Task.Run(() => _test.Run(_seed, id));

        var expected = await expectedTask;
        if (expected.IndeterminateResult)
        {
            Reset(id);
            return FuzzResult.Indeterminate;
        }

        var actual = await actualTask;
        if (actual.IndeterminateResult)
        {
            Reset(id);
            return FuzzResult.Indeterminate;
        }

        if (!expected.Equals(actual))
        {
            throw new NotImplementedException();
        }

        var mutated = _mutator.Mutate(id + 1, _seed);
        if (mutated == null)
            Reset(id);
        else
            _seed = mutated;

        return FuzzResult.Success;
    }

    private void Reset(int seed)
    {
        // Choose a seed program from the "Seeds" folder
        var rng = new Random(seed);
        var seeds = Assembly.GetExecutingAssembly().GetManifestResourceNames().Where(a => a.StartsWith("Wasm2cs.Fuzzer.Mutation.Seeds.")).ToArray();
        var name = seeds[rng.Next(0, seeds.Length)];

        // Load an seed
        using var reader = new BinaryReader(Assembly.GetExecutingAssembly().GetManifestResourceStream(name)!);
        var selectedSeed = reader.ReadBytes(checked((int)reader.BaseStream.Length));

        // Mutate it
        var mutated = _mutator.Mutate(seed + 1, selectedSeed);
        if (mutated == null)
            Reset(seed + 1);
        else
            _seed = mutated;
    }

    public enum FuzzResult
    {
        Success,
        Failure,
        Indeterminate
    }
}