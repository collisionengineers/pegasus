using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json.Nodes;
using CollisionRenderer.Mcp.Valuation;
using ModelContextProtocol.Server;

namespace CollisionRenderer.Mcp.Tools;

/// <summary>
/// Delivery-time access to the finished valuation PDFs. <c>render_valuation_outputs</c> writes the
/// report + advert evidence pack under <c>%LOCALAPPDATA%\CollisionRenderer\output</c> and returns
/// <c>file://</c> artifact URIs; a non-technical engineer just sees raw, un-clickable links. This
/// tool opens those PDFs in the OS default viewer (<c>mode:"open"</c>) or reveals them in File
/// Explorer (<c>mode:"reveal"</c>). The MCP server runs as a child of Claude Desktop in the user's
/// interactive session, so the launch lands on their desktop — mirroring the CLI's <c>OpenFile</c>
/// (<c>Process.Start</c> with <c>UseShellExecute = true</c>).
///
/// SECURITY: only a file whose resolved absolute path is UNDER <see cref="ArtifactOutput.OutputRoot"/>
/// is ever launched. A prompt-injected call naming an arbitrary path (e.g. an executable in System32)
/// is pushed into <c>errors[]</c> and never opened.
/// </summary>
[McpServerToolType]
public static class OutputAccessTools
{
    [McpServerTool(Name = "open_valuation_output", Title = "Open or reveal valuation PDFs",
        ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description(
        "Open the finished valuation PDFs in the OS default viewer, or reveal them in File Explorer " +
        "(pass the artifacts[].uri from render_valuation_outputs). mode:\"open\" (default) launches each " +
        "PDF in the engineer's default viewer; mode:\"reveal\" selects each file in File Explorer to save " +
        "or attach. Only files under the renderer's own output directory are opened — anything else is " +
        "returned in errors[], never launched. Returns { opened, errors, mode }.")]
    public static JsonNode OpenValuationOutput(
        [Description("The artifacts[].uri values from render_valuation_outputs (file:// URIs, or plain absolute paths).")]
        string[] uris,
        [Description("\"open\" to launch in the default PDF viewer (default), or \"reveal\" to select in File Explorer.")]
        string mode = "open")
    {
        var reveal = string.Equals(mode, "reveal", StringComparison.OrdinalIgnoreCase);
        var opened = new JsonArray();
        var errors = new JsonArray();

        foreach (var raw in uris ?? Array.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                errors.Add("empty uri");
                continue;
            }

            string path;
            try
            {
                path = ToLocalPath(raw);
            }
            catch (Exception ex)
            {
                errors.Add($"{raw}: could not parse as a file path ({ex.Message})");
                continue;
            }

            // Guard BEFORE any launch: refuse anything not under our own output directory, so a
            // prompt-injected uri cannot open an arbitrary file on the engineer's machine.
            if (!IsUnderOutputRoot(path))
            {
                errors.Add($"{raw}: refused — not under the renderer output directory");
                continue;
            }

            if (!File.Exists(path))
            {
                errors.Add($"{raw}: file not found");
                continue;
            }

            try
            {
                if (reveal)
                {
                    // explorer.exe /select is finicky: it needs the fully-resolved backslash path, quoted.
                    Process.Start("explorer.exe", $"/select,\"{path}\"");
                }
                else
                {
                    // UseShellExecute launches the file's default handler in the user's interactive
                    // session — mirrors Cli/Program.cs OpenFile.
                    Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                }

                opened.Add(path);
            }
            catch (Exception ex)
            {
                errors.Add($"{raw}: could not {(reveal ? "reveal" : "open")} ({ex.Message})");
            }
        }

        return new JsonObject
        {
            ["opened"] = opened,
            ["errors"] = errors,
            ["mode"] = reveal ? "reveal" : "open",
        };
    }

    /// <summary>
    /// Resolve a render artifact uri to a local absolute path. Accepts a <c>file://</c> URI
    /// (parsed via <see cref="Uri.LocalPath"/>) or a plain absolute path. <see cref="Path.GetFullPath(string)"/>
    /// normalises it so the guard compares real paths (collapses any <c>..\</c> and unifies separators).
    /// </summary>
    internal static string ToLocalPath(string raw)
    {
        var trimmed = raw.Trim();
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) && uri.IsFile)
        {
            return Path.GetFullPath(uri.LocalPath);
        }

        return Path.GetFullPath(trimmed);
    }

    /// <summary>
    /// True only when <paramref name="path"/> resolves to a location UNDER
    /// <see cref="ArtifactOutput.OutputRoot"/>. The root is normalised with a trailing
    /// <see cref="Path.DirectorySeparatorChar"/> so a sibling directory that merely shares the prefix
    /// (e.g. <c>…\output-evil</c>) cannot slip through. Comparison is case-insensitive (Windows paths).
    /// </summary>
    internal static bool IsUnderOutputRoot(string path)
    {
        var full = Path.GetFullPath(path);
        var root = Path.GetFullPath(ArtifactOutput.OutputRoot);
        if (!root.EndsWith(Path.DirectorySeparatorChar))
        {
            root += Path.DirectorySeparatorChar;
        }

        return full.StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }
}
