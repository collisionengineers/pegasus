using System.Diagnostics;
using System.Text.Json;
using CollisionRenderer.Core;

namespace CollisionRenderer.Cli;

/// <summary>
/// Command-line front end. Every command is a thin wrapper over the shared Core
/// pipeline (<see cref="CollisionRendererFactory"/>) so the CLI and the desktop
/// app expose identical features.
/// </summary>
public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 0 || IsHelp(args[0]))
        {
            PrintUsage();
            return args.Length == 0 ? 1 : 0;
        }

        var command = args[0].ToLowerInvariant();
        var rest = args.Skip(1).ToArray();

        try
        {
            return command switch
            {
                "list" or "templates" => ListTemplates(),
                "forms" => Forms(rest),
                "validate" => Validate(rest),
                "render" => await RenderAsync(rest),
                "batch" => await BatchAsync(rest),
                "install-browser" => InstallBrowser(),
                "version" => PrintVersion(),
                _ => Unknown(command),
            };
        }
        catch (RenderValidationException ex)
        {
            Error(ex.Message);
            return 2;
        }
        catch (Exception ex)
        {
            Error(ex.Message);
            return 1;
        }
    }

    // ----------------------------------------------------------------- commands

    private static int ListTemplates()
    {
        Console.WriteLine("Available document templates:\n");
        foreach (var t in CollisionRendererFactory.Catalog.List())
        {
            Console.WriteLine($"  {t.Id,-26} {t.Name}");
            Console.WriteLine($"  {new string(' ', 26)} {t.Description}\n");
        }

        Console.WriteLine("Render with:  collisionrenderer render --template <id> --data <file.json> --out <file.pdf>");
        return 0;
    }


    private static int Forms(string[] args)
    {
        if (args.Length == 0 || IsHelp(args[0]))
        {
            PrintFormsUsage();
            return args.Length == 0 ? 1 : 0;
        }

        var subcommand = args[0].ToLowerInvariant();
        var rest = args.Skip(1).ToArray();

        return subcommand switch
        {
            "list" => ListForms(),
            "blank" => WriteBlank(rest),
            "schema" or "form" => WriteFormSchema(rest),
            "starter" => WriteStarter(rest),
            _ => Unknown($"forms {subcommand}"),
        };
    }

    private static int ListForms()
    {
        Console.WriteLine("Available blank authoring templates:\n");
        foreach (var t in CollisionRendererFactory.AuthoringCatalog.List())
        {
            Console.WriteLine($"  {t.Id,-36} {t.Name}");
            Console.WriteLine($"  {new string(' ', 36)} {t.Category} -> renders as {t.RenderTemplateId}");
            Console.WriteLine($"  {new string(' ', 36)} {t.Description}\n");
        }

        Console.WriteLine("Create a draft with:  collisionrenderer forms blank --template <id> --out draft.json");
        return 0;
    }

    private static int WriteBlank(string[] args)
    {
        var opts = Options.Parse(args);
        var id = opts.Require("template", "t");
        var json = CollisionRendererFactory.AuthoringCatalog.GetBlankJson(id);
        WriteTextOrFile(json, opts.Get("out", "o"), $"Wrote blank draft for '{id}'");
        return 0;
    }

    private static int WriteStarter(string[] args)
    {
        var opts = Options.Parse(args);
        var id = opts.Require("template", "t");
        var json = CollisionRendererFactory.AuthoringCatalog.GetStarterJson(id);
        WriteTextOrFile(json, opts.Get("out", "o"), $"Wrote starter draft for '{id}'");
        return 0;
    }

    private static int WriteFormSchema(string[] args)
    {
        var opts = Options.Parse(args);
        var id = opts.Require("template", "t");
        var form = CollisionRendererFactory.AuthoringCatalog.GetForm(id);
        var json = JsonSerializer.Serialize(form, CrJson.Options);
        WriteTextOrFile(json, opts.Get("out", "o"), $"Wrote form schema for '{id}'");
        return 0;
    }

    private static int Validate(string[] args)
    {
        var opts = Options.Parse(args);
        var id = opts.Require("template", "t");
        var json = ReadData(opts.Require("data", "d"));

        var descriptor = CollisionRendererFactory.Catalog.Get(id);
        object model;
        try
        {
            model = JsonSerializer.Deserialize(json, descriptor.ModelType, CrJson.Options)
                    ?? throw new RenderValidationException(new[] { "Payload deserialised to null." });
        }
        catch (JsonException ex)
        {
            Error($"Invalid JSON: {ex.Message}");
            return 2;
        }

        var result = new PayloadValidator().Validate(id, model);
        foreach (var w in result.Warnings)
        {
            Console.WriteLine($"warning: {w}");
        }

        if (result.Ok)
        {
            Console.WriteLine($"OK — '{id}' payload is valid.");
            return 0;
        }

        foreach (var e in result.Errors)
        {
            Error(e);
        }

        return 2;
    }

    private static async Task<int> RenderAsync(string[] args)
    {
        var opts = Options.Parse(args);
        var id = opts.Require("template", "t");
        var json = ReadData(opts.Require("data", "d"));
        var density = ParseDensity(opts.Get("density") ?? "auto");

        var request = new RenderRequest
        {
            TemplateId = id,
            Json = json,
            Options = density,
        };

        await using var renderer = CollisionRendererFactory.CreateRenderer();
        var result = await renderer.RenderAsync(request);

        var outPath = opts.Get("out", "o");
        if (string.IsNullOrWhiteSpace(outPath))
        {
            outPath = Path.Combine(Directory.GetCurrentDirectory(), result.SuggestedFileName);
        }

        var fullDir = Path.GetDirectoryName(Path.GetFullPath(outPath));
        if (!string.IsNullOrEmpty(fullDir))
        {
            Directory.CreateDirectory(fullDir);
        }

        await File.WriteAllBytesAsync(outPath, result.Pdf);

        Console.WriteLine($"Rendered {id} -> {outPath}");
        Console.WriteLine($"  pages:   {result.PageCount}");
        Console.WriteLine($"  density: {result.Density}");
        Console.WriteLine($"  sha256:  {result.Sha256}");
        Console.WriteLine($"  engine:  {result.EngineVersion}");
        foreach (var w in result.Warnings)
        {
            Console.WriteLine($"  warning: {w}");
        }

        if (opts.Has("open"))
        {
            OpenFile(outPath);
        }

        return 0;
    }

    private static async Task<int> BatchAsync(string[] args)
    {
        var opts = Options.Parse(args);
        var manifestPath = opts.Require("manifest", "m");
        var outDir = opts.Get("out", "o") ?? Directory.GetCurrentDirectory();

        if (!File.Exists(manifestPath))
        {
            throw new FileNotFoundException($"Batch manifest not found: {manifestPath}");
        }

        var manifestJson = await File.ReadAllTextAsync(manifestPath);
        var manifest = JsonSerializer.Deserialize<BatchCliManifest>(manifestJson, CrJson.Options)
            ?? throw new RenderValidationException(new[] { "Batch manifest deserialised to null." });

        if (manifest.Items.Count == 0)
        {
            throw new RenderValidationException(new[] { "Batch manifest must contain at least one item." });
        }

        Directory.CreateDirectory(outDir);
        var failures = 0;

        await using var renderer = CollisionRendererFactory.CreateRenderer();
        for (var i = 0; i < manifest.Items.Count; i++)
        {
            var item = manifest.Items[i];
            try
            {
                var data = ResolveBatchItemJson(item, manifestPath);
                var result = await renderer.RenderAsync(new RenderRequest
                {
                    TemplateId = item.TemplateId,
                    Json = data,
                    Options = ParseDensity(item.Density ?? "auto"),
                });

                var outPath = ResolveBatchOutputPath(outDir, item.Out, result.SuggestedFileName);
                var fullDir = Path.GetDirectoryName(Path.GetFullPath(outPath));
                if (!string.IsNullOrEmpty(fullDir))
                {
                    Directory.CreateDirectory(fullDir);
                }

                await File.WriteAllBytesAsync(outPath, result.Pdf);
                Console.WriteLine($"[{i + 1}/{manifest.Items.Count}] rendered {item.TemplateId} -> {outPath}");
            }
            catch (Exception ex) when (ex is RenderValidationException or KeyNotFoundException or FileNotFoundException or JsonException)
            {
                failures++;
                Error($"batch item {i + 1} ({item.TemplateId}): {ex.Message}");
            }
        }

        Console.WriteLine(failures == 0
            ? $"Batch complete: {manifest.Items.Count} rendered."
            : $"Batch complete: {manifest.Items.Count - failures} rendered, {failures} failed.");

        return failures == 0 ? 0 : 2;
    }

    private static int InstallBrowser()
    {
        Console.WriteLine("Installing the Chromium engine for Playwright...");
        var exit = Microsoft.Playwright.Program.Main(new[] { "install", "chromium" });
        Console.WriteLine(exit == 0 ? "Chromium installed." : $"Installer exited with code {exit}.");
        return exit;
    }

    private static int PrintVersion()
    {
        var version = typeof(CollisionRendererFactory).Assembly.GetName().Version?.ToString() ?? "0.0.0";
        Console.WriteLine($"Collision Renderer {version}");
        return 0;
    }

    // ------------------------------------------------------------------ helpers

    private static RenderOptions ParseDensity(string value) => value.ToLowerInvariant() switch
    {
        "auto" => new RenderOptions { Fit = DensityFit.Auto, Density = Density.Normal },
        "normal" => new RenderOptions { Fit = DensityFit.Fixed, Density = Density.Normal },
        "compact" => new RenderOptions { Fit = DensityFit.Fixed, Density = Density.Compact },
        "ultra" or "ultra-compact" => new RenderOptions { Fit = DensityFit.Fixed, Density = Density.UltraCompact },
        _ => throw new RenderValidationException(new[] { $"Unknown density '{value}'. Use auto|normal|compact|ultra." }),
    };

    private static string ReadData(string source)
    {
        if (source == "-")
        {
            return Console.In.ReadToEnd();
        }

        if (!File.Exists(source))
        {
            throw new FileNotFoundException($"Data file not found: {source}");
        }

        return File.ReadAllText(source);
    }

    private static void OpenFile(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo(Path.GetFullPath(path)) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  (could not open automatically: {ex.Message})");
        }
    }

    private static bool IsHelp(string arg) =>
        arg is "-h" or "--help" or "help" or "/?";

    private static int Unknown(string command)
    {
        Error($"Unknown command '{command}'.");
        PrintUsage();
        return 1;
    }

    private static void Error(string message)
    {
        var prev = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine($"error: {message}");
        Console.ForegroundColor = prev;
    }

    private static void PrintUsage()
    {
        Console.WriteLine(
            """
            Collision Renderer — branded PDF documents for Collision Engineers.

            Usage:
              collisionrenderer <command> [options]

            Commands:
              list                       List the available document templates.
              forms list                 List blank authoring templates.
              forms blank --template <id>
                                         Print a blank draft payload.
              forms starter --template <id>
                                         Print a draft with overwriteable prompts.
              forms schema --template <id>
                                         Print the Core-owned form schema.
              validate --template <id> --data <file>
                                         Check a payload without rendering.
              render   --template <id> --data <file> [--out <file.pdf>]
                                         Render a document to PDF.
              batch    --manifest <file> [--out <folder>]
                                         Render multiple manifest items.
              install-browser            Download the Chromium engine (first-time setup).
              version                    Show the version.

            Render options:
              --out, -o <path>           Output PDF path (default: <REG>_<type>.pdf in the current folder).
              --density <mode>           auto (default) | normal | compact | ultra.
              --open                     Open the PDF when finished.
              --data, -d <path|->        JSON payload file, or '-' to read from stdin.
              --manifest, -m <path>       Batch manifest JSON file.

            Examples:
              collisionrenderer list
              collisionrenderer forms blank --template total-loss-report --out draft.json
              collisionrenderer forms starter --template fee-note --out fee.json
              collisionrenderer render --template market-valuation-evidence --data val.json --out val.pdf --open
              collisionrenderer batch --manifest batch.json --out artifacts/batch
            """);
    }

    private static void PrintFormsUsage()
    {
        Console.WriteLine(
            """
            Collision Renderer forms - document authoring templates.

            Usage:
              collisionrenderer forms list
              collisionrenderer forms blank   --template <id> [--out draft.json]
              collisionrenderer forms starter --template <id> [--out draft.json]
              collisionrenderer forms schema  --template <id> [--out schema.json]
            """);
    }

    private static void WriteTextOrFile(string text, string? outPath, string message)
    {
        if (string.IsNullOrWhiteSpace(outPath))
        {
            Console.WriteLine(text);
            return;
        }

        var fullDir = Path.GetDirectoryName(Path.GetFullPath(outPath));
        if (!string.IsNullOrEmpty(fullDir))
        {
            Directory.CreateDirectory(fullDir);
        }

        File.WriteAllText(outPath, text);
        Console.WriteLine($"{message} to {outPath}");
    }

    private static string ResolveBatchItemJson(BatchCliItem item, string manifestPath)
    {
        if (!string.IsNullOrWhiteSpace(item.DataPath))
        {
            var baseDir = Path.GetDirectoryName(Path.GetFullPath(manifestPath)) ?? Directory.GetCurrentDirectory();
            var path = Path.IsPathRooted(item.DataPath)
                ? item.DataPath
                : Path.Combine(baseDir, item.DataPath);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Batch data file not found: {path}");
            }

            return File.ReadAllText(path);
        }

        if (item.Data.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            throw new RenderValidationException(new[] { "Batch item requires either data or dataPath." });
        }

        return item.Data.GetRawText();
    }

    private static string ResolveBatchOutputPath(string outDir, string? itemOut, string suggestedFileName)
    {
        if (string.IsNullOrWhiteSpace(itemOut))
        {
            return Path.Combine(outDir, suggestedFileName);
        }

        return Path.IsPathRooted(itemOut)
            ? itemOut
            : Path.Combine(outDir, itemOut);
    }
}

internal sealed record BatchCliManifest
{
    public List<BatchCliItem> Items { get; init; } = new();
}

internal sealed record BatchCliItem
{
    public string TemplateId { get; init; } = "";
    public JsonElement Data { get; init; }
    public string? DataPath { get; init; }
    public string? Density { get; init; }
    public string? Out { get; init; }
}

/// <summary>Minimal, dependency-free option parser ("--key value", "-k value", "--flag").</summary>
internal sealed class Options
{
    private readonly Dictionary<string, string?> _values = new(StringComparer.OrdinalIgnoreCase);

    public static Options Parse(string[] args)
    {
        var o = new Options();
        for (var i = 0; i < args.Length; i++)
        {
            var token = args[i];
            if (!token.StartsWith('-'))
            {
                continue;
            }

            var key = token.TrimStart('-');
            if (i + 1 < args.Length && !args[i + 1].StartsWith('-'))
            {
                o._values[key] = args[++i];
            }
            else
            {
                o._values[key] = null; // flag
            }
        }

        return o;
    }

    public string? Get(params string[] names)
    {
        foreach (var n in names)
        {
            if (_values.TryGetValue(n, out var v) && v is not null)
            {
                return v;
            }
        }

        return null;
    }

    public bool Has(params string[] names) => names.Any(n => _values.ContainsKey(n));

    public string Require(params string[] names) =>
        Get(names) ?? throw new RenderValidationException(
            new[] { $"Missing required option --{names[0]}." });
}
