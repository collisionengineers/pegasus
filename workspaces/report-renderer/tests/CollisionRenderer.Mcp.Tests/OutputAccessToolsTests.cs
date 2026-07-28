using System;
using System.IO;
using System.Text.Json.Nodes;
using CollisionRenderer.Mcp.Tools;
using CollisionRenderer.Mcp.Valuation;
using Xunit;

namespace CollisionRenderer.Mcp.Tests;

/// <summary>
/// The security guard for <c>open_valuation_output</c>: it must open ONLY files under the
/// renderer's own output directory, so a prompt-injected call naming an arbitrary path is refused
/// into <c>errors[]</c> and never launched. These tests exercise the parse/guard helpers directly
/// (and the tool's reject path, which returns before any launch) — no process is ever started, so
/// they are safe and fast in CI.
/// </summary>
public class OutputAccessToolsTests
{
    [Fact]
    public void IsUnderOutputRoot_accepts_a_file_written_under_the_output_root()
    {
        Directory.CreateDirectory(ArtifactOutput.OutputRoot);
        var path = Path.Combine(ArtifactOutput.OutputRoot, $"guard-probe-{Guid.NewGuid():N}.pdf");
        File.WriteAllText(path, "probe");
        try
        {
            Assert.True(OutputAccessTools.IsUnderOutputRoot(path));
            Assert.True(File.Exists(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(@"C:\Windows\System32\notepad.exe")]
    [InlineData(@"C:\Windows\System32\calc.exe")]
    public void IsUnderOutputRoot_rejects_a_path_outside_the_output_root(string outside)
    {
        Assert.False(OutputAccessTools.IsUnderOutputRoot(outside));
    }

    [Fact]
    public void IsUnderOutputRoot_rejects_a_sibling_that_only_shares_the_prefix()
    {
        // …\output-evil must NOT count as under …\output — that is what the trailing-separator
        // normalisation defends against.
        var sibling = ArtifactOutput.OutputRoot + "-evil" + Path.DirectorySeparatorChar + "x.pdf";
        Assert.False(OutputAccessTools.IsUnderOutputRoot(sibling));
    }

    [Fact]
    public void Open_refuses_a_path_outside_the_output_root_into_errors_without_launching()
    {
        // notepad.exe exists but is outside the output root, so it is rejected at the guard —
        // before File.Exists and before any Process.Start.
        var result = (JsonObject)OutputAccessTools.OpenValuationOutput(new[] { @"C:\Windows\System32\notepad.exe" });

        Assert.Empty((JsonArray)result["opened"]!);
        Assert.NotEmpty((JsonArray)result["errors"]!);
        Assert.Equal("open", result["mode"]!.GetValue<string>());
    }

    [Fact]
    public void Open_refuses_a_file_uri_to_outside_the_output_root_into_errors()
    {
        var result = (JsonObject)OutputAccessTools.OpenValuationOutput(
            new[] { "file:///C:/Windows/System32/notepad.exe" }, mode: "open");

        Assert.Empty((JsonArray)result["opened"]!);
        Assert.NotEmpty((JsonArray)result["errors"]!);
    }

    [Fact]
    public void Open_reports_missing_file_under_the_root_without_launching()
    {
        // Under the root but non-existent → File.Exists guard rejects it into errors, no launch.
        var missing = Path.Combine(ArtifactOutput.OutputRoot, $"nope-{Guid.NewGuid():N}.pdf");
        var result = (JsonObject)OutputAccessTools.OpenValuationOutput(new[] { missing });

        Assert.Empty((JsonArray)result["opened"]!);
        Assert.NotEmpty((JsonArray)result["errors"]!);
    }

    [Fact]
    public void Reveal_mode_is_echoed_in_the_result_mode()
    {
        // An outside path is refused (no Explorer launch), but the echoed mode still says "reveal".
        var result = (JsonObject)OutputAccessTools.OpenValuationOutput(
            new[] { @"C:\Windows\System32\notepad.exe" }, mode: "reveal");

        Assert.Equal("reveal", result["mode"]!.GetValue<string>());
        Assert.Empty((JsonArray)result["opened"]!);
    }

    [Fact]
    public void ToLocalPath_round_trips_a_file_uri_under_the_output_root()
    {
        var path = Path.Combine(ArtifactOutput.OutputRoot, "DF73VSA_market_valuation_evidence.pdf");
        var uri = new Uri(path).AbsoluteUri; // file:///C:/…/output/DF73VSA_market_valuation_evidence.pdf

        Assert.Equal(Path.GetFullPath(path), OutputAccessTools.ToLocalPath(uri));
    }

    [Fact]
    public void ToLocalPath_accepts_a_plain_absolute_path()
    {
        var path = Path.Combine(ArtifactOutput.OutputRoot, "DF73VSA_advert_evidence_pack.pdf");

        Assert.Equal(Path.GetFullPath(path), OutputAccessTools.ToLocalPath(path));
    }
}
