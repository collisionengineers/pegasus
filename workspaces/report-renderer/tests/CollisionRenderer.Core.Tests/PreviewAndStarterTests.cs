using System.Text.Json;
using CollisionRenderer.Core.Design;
using CollisionRenderer.Core.Models;
using CollisionRenderer.Core.Templating;
using Xunit;

namespace CollisionRenderer.Core.Tests;

public class PlaceholderScannerTests
{
    [Fact]
    public void Detects_guillemet_placeholders()
    {
        var json = $"{{\"make\":\"{PlaceholderScanner.Open}Make{PlaceholderScanner.Close}\"}}";
        var scan = PlaceholderScanner.Scan(json);

        Assert.True(scan.Any);
        Assert.Equal(1, scan.Count);
        Assert.Single(scan.Samples);
    }

    [Fact]
    public void Ignores_genuine_content()
    {
        Assert.False(PlaceholderScanner.ContainsPlaceholders("{\"make\":\"BMW\",\"model\":\"3 Series\"}"));
        Assert.False(PlaceholderScanner.ContainsPlaceholders(""));
        Assert.False(PlaceholderScanner.ContainsPlaceholders(null));
    }

    [Fact]
    public void Counts_and_dedupes_samples()
    {
        var o = PlaceholderScanner.Open;
        var c = PlaceholderScanner.Close;
        var json = $"[\"{o}Make{c}\",\"{o}Make{c}\",\"{o}Model{c}\"]";
        var scan = PlaceholderScanner.Scan(json);

        Assert.Equal(3, scan.Count);
        Assert.Equal(2, scan.Samples.Count); // distinct
    }
}

public class StarterTests
{
    [Theory]
    [MemberData(nameof(AuthoringIds))]
    public void Starter_has_placeholders_and_still_deserialises(string id)
    {
        var authoring = CollisionRendererFactory.AuthoringCatalog;
        var starter = authoring.GetStarterJson(id);

        Assert.True(PlaceholderScanner.ContainsPlaceholders(starter),
            $"starter for '{id}' should contain placeholders");

        // Type-preserving wash: the starter must still bind to the typed model.
        var renderDescriptor = CollisionRendererFactory.Catalog.Get(authoring.Get(id).RenderTemplateId);
        var model = JsonSerializer.Deserialize(starter, renderDescriptor.ModelType, CrJson.Options);
        Assert.NotNull(model);
    }

    [Fact]
    public void Fee_note_starter_keeps_numeric_fields_numeric()
    {
        // Money/Number fields must not be replaced with a guillemet string.
        var starter = CollisionRendererFactory.AuthoringCatalog.GetStarterJson("fee-note");
        var model = JsonSerializer.Deserialize<FeeNoteDocument>(starter, CrJson.Options);

        Assert.NotNull(model);
        Assert.NotEmpty(model!.Items); // still binds; amount stays a decimal default
    }

    public static IEnumerable<object[]> AuthoringIds() =>
        CollisionRendererFactory.AuthoringCatalog.List().Select(t => new object[] { t.Id });
}

public class BlankLetterheadTests
{
    [Fact]
    public void Is_registered_in_both_catalogs()
    {
        Assert.Contains("blank-letterhead", CollisionRendererFactory.Catalog.List().Select(t => t.Id));
        Assert.Contains("blank-letterhead", CollisionRendererFactory.AuthoringCatalog.List().Select(t => t.Id));
    }

    [Fact]
    public void Validates_without_a_title_or_sections()
    {
        var model = new ExpertReportDocument { Title = "", Intro = { "Free-text body." } };
        var result = new PayloadValidator().Validate("blank-letterhead", model);
        Assert.True(result.Ok, string.Join("; ", result.Errors));
    }

    [Fact]
    public void Composes_letterhead_body_without_a_heading()
    {
        var descriptor = TemplateCatalog.Default.Get("blank-letterhead");
        var composer = new HtmlComposer(BrandAssets.Default, TemplateCatalog.Default);
        var model = new ExpertReportDocument { Title = "", Intro = { "A free-text letterhead body." } };

        var composed = composer.Compose(descriptor, model, Density.Normal);

        Assert.Contains("A free-text letterhead body.", composed.Html);
        Assert.DoesNotContain("<h1", composed.Html); // empty title renders no heading
    }
}

public class PreviewComposerTests
{
    private static readonly IPreviewComposer Preview = CollisionRendererFactory.CreatePreviewComposer();

    [Fact]
    public void Empty_draft_returns_best_effort_placeholder()
    {
        var result = Preview.ComposePreview("fee-note", "", Density.Normal);
        Assert.True(result.IsBestEffort);
        Assert.Contains("<html", result.Html);
    }

    [Fact]
    public void Invalid_json_returns_best_effort_without_throwing()
    {
        var result = Preview.ComposePreview("fee-note", "{ not valid json", Density.Normal);
        Assert.True(result.IsBestEffort);
        Assert.Contains("<html", result.Html);
    }

    [Fact]
    public void Unknown_template_returns_best_effort()
    {
        var result = Preview.ComposePreview("does-not-exist", "{}", Density.Normal);
        Assert.True(result.IsBestEffort);
    }

    [Fact]
    public void Valid_sample_renders_faithful_html()
    {
        var json = CollisionRendererFactory.Catalog.GetSampleJson("market-valuation-evidence");
        var result = Preview.ComposePreview("market-valuation-evidence", json, Density.Normal);

        Assert.False(result.IsBestEffort);
        Assert.Contains("MARKET VALUATION EVIDENCE", result.Html);
    }

    [Theory]
    [MemberData(nameof(AuthoringIds))]
    public void Every_starter_previews_without_throwing(string id)
    {
        var authoring = CollisionRendererFactory.AuthoringCatalog;
        var starter = authoring.GetStarterJson(id);
        var renderId = authoring.Get(id).RenderTemplateId;

        var result = Preview.ComposePreview(renderId, starter, Density.Normal);
        Assert.Contains("<html", result.Html);
    }

    public static IEnumerable<object[]> AuthoringIds() =>
        CollisionRendererFactory.AuthoringCatalog.List().Select(t => new object[] { t.Id });
}
