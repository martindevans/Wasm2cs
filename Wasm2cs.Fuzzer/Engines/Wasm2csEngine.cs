using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Text;
using Wasm2cs.CodeGeneration.Exceptions;

namespace Wasm2cs.Fuzzer.Engines;

public class Wasm2csEngine
    : IWasmEngine
{
    public async Task<RunResult> Run(byte[] program, int input)
    {
        // Convert wasm to csharp
        string code;
        try
        {
            var output = new StringBuilder();
            await using (var writer = new StringWriter())
                await WasmConverter.Convert("ConvertedClass", "FuzzCompile", new MemoryStream(program), writer);
            code = output.ToString();
        }
        catch (UnsupportedWasmInstructionException ex)
        {
            Console.WriteLine($"Unsupported: {ex.OpCode}");
            return new RunResult { IndeterminateResult = true };
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return new RunResult { IndeterminateResult = true };
        }

        // Compile it
        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        var references = new[] {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location)
        };

        var compilation = CSharpCompilation.Create(
            "DynamicAssembly",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var ms = new MemoryStream();
        var result = compilation.Emit(ms);

        if (!result.Success)
        {
            foreach (var diag in result.Diagnostics)
                Console.WriteLine(diag.ToString());
            return new RunResult { IndeterminateResult = true };
        }

        ms.Seek(0, SeekOrigin.Begin);
        var assembly = Assembly.Load(ms.ToArray());
        var type = assembly.GetType("ConvertedClass")!;
        var obj = Activator.CreateInstance(type);
        var method = type.GetMethod("main")!;
        var intResult = (int)method.Invoke(obj, [input])!;

        return new RunResult
        {
            ReturnValue = intResult,
        };
    }
}