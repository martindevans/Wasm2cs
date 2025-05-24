namespace Wasm2cs.CodeGeneration;

internal class IndentedTextWriter
{
    private readonly TextWriter _dest;
    private int _indent;

    private bool _allowUsings = true;
    private bool _allowNamespace = true;

    public IndentedTextWriter(TextWriter dest)
    {
        _dest = dest;
    }

    public async Task AppendLine(string input)
    {
        var lines = input.Split([ "\r\n", "\r", "\n" ], StringSplitOptions.None);
        foreach (var line in lines)
            await _dest.WriteLineAsync(new string(' ', _indent * 4) + line);
    }

    public async Task AppendLine()
    {
        await _dest.WriteLineAsync();
    }

    public async Task<IAsyncDisposable> Braces()
    {
        await AppendLine("{");
        _indent++;

        return new IndentScope(this);
    }

    private class IndentScope
        : IAsyncDisposable
    {
        private readonly IndentedTextWriter _parent;

        public IndentScope(IndentedTextWriter parent)
        {
            _parent = parent;
        }

        public async ValueTask DisposeAsync()
        {
            _parent._indent--;
            await _parent.AppendLine("}");
        }
    }

    public async Task Using(string @namespace)
    {
        if (!_allowUsings)
            throw new InvalidOperationException("Cannot emit using statement");

        await AppendLine($"using {@namespace};");
    }

    public async Task Namespace(string @namespace)
    {
        if (!_allowNamespace)
            throw new InvalidOperationException("Cannot emit namespace statement");

        await AppendLine($"namespace {@namespace};");
        await AppendLine();

        _allowNamespace = false;
        _allowUsings = false;
    }

    public async Task<IAsyncDisposable> Class(string name)
    {
        await AppendLine($"class {name}");
        return await Braces();
    }

    public async Task<IAsyncDisposable> Method(string name, bool @static = false, bool @public = true, IReadOnlyList<string>? args = null, string? returns = null)
    {
        await AppendLine($"{(@public ? "public" : "private")} {(@static ? "static " : "")}{returns ?? "void"} {name}({string.Join(", ", args ?? [])})");
        return await Braces();
    }

    public async Task<IAsyncDisposable> Constructor(string name, bool @public = true, IReadOnlyList<string>? args = null)
    {
        await AppendLine($"{(@public ? "public" : "private")} {name}({string.Join(", ", args ?? [])})");
        return await Braces();
    }

    public async Task Property(Type type, string name, bool publicGet, bool publicSet, string? initMethod = null)
    {
        var backingField = $"backing_{name}";
        await AppendLine($"private {type} {backingField}{(initMethod != null ? $" = {initMethod}();" : "")}");

        await AppendLine($"public {type} {name}");
        await using (await Braces())
        {
            await AppendLine($"{(publicGet ? "private " : "")}set => {backingField} = value;");
            await AppendLine($"{(publicSet ? "private " : "")}get => {backingField};");
        }

        await AppendLine();
    }

    public async Task<IAsyncDisposable> Region(string name)
    {
        await AppendLine($"#region {name}");
        return new RegionScope(this);
    }

    private class RegionScope
        : IAsyncDisposable
    {
        private readonly IndentedTextWriter _parent;

        public RegionScope(IndentedTextWriter parent)
        {
            _parent = parent;
        }

        public async ValueTask DisposeAsync()
        {
            await _parent.AppendLine("#endregion");
            await _parent.AppendLine();
        }
    }
}