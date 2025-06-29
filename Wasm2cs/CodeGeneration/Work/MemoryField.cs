using Wacs.Core;
using Wacs.Core.Types;

namespace Wasm2cs.CodeGeneration.Work;

internal class MemoryField(MemoryType Memory, bool Construct)
    : IWorkItem
{
    public async Task Emit(IndentedTextWriter writer, Module module)
    {
        var name = NameConventions.Memory(Memory.Id);

        var suffix = "default";
        if (Construct)
            suffix = $"new Memory({Memory.Limits.Minimum}, {Memory.Limits.Maximum?.ToString() ?? "null"})";

        await writer.AppendLine($"private readonly Memory {name} = {suffix};");
    }
}