using System.Text.Json.Nodes;
using Xunit;

namespace CollisionRenderer.Core.Tests;

public class JsonPathTests
{
    // ----------------------------------------------------------------- Parse

    [Fact]
    public void Parse_splits_property_chain()
    {
        var segments = JsonPath.Parse("a.b.c");
        Assert.Equal(
            new[] { new JsonPath.Segment("a", null), new JsonPath.Segment("b", null), new JsonPath.Segment("c", null) },
            segments);
    }

    [Fact]
    public void Parse_handles_property_then_index()
    {
        var segments = JsonPath.Parse("a[0].b");
        Assert.Equal(
            new[] { new JsonPath.Segment("a", null), new JsonPath.Segment(null, 0), new JsonPath.Segment("b", null) },
            segments);
    }

    [Fact]
    public void Parse_handles_consecutive_indices()
    {
        var segments = JsonPath.Parse("a[0][1]");
        Assert.Equal(
            new[] { new JsonPath.Segment("a", null), new JsonPath.Segment(null, 0), new JsonPath.Segment(null, 1) },
            segments);
    }

    [Fact]
    public void Parse_skips_the_dollar_token()
    {
        Assert.Empty(JsonPath.Parse("$"));

        var segments = JsonPath.Parse("a.$.b");
        Assert.Equal(
            new[] { new JsonPath.Segment("a", null), new JsonPath.Segment("b", null) },
            segments);
    }

    [Theory]
    [InlineData("a..b")]
    [InlineData(" a . b ")]
    public void Parse_trims_and_drops_empty_parts(string path)
    {
        var segments = JsonPath.Parse(path);
        Assert.Equal(
            new[] { new JsonPath.Segment("a", null), new JsonPath.Segment("b", null) },
            segments);
    }

    [Theory]
    [InlineData("a[x].b")] // non-numeric index
    [InlineData("a[0")]    // unclosed bracket
    public void Parse_stops_at_a_malformed_bracket(string path)
    {
        // The clean prefix is kept; the bad bracket and anything after it is dropped.
        Assert.Equal(new[] { new JsonPath.Segment("a", null) }, JsonPath.Parse(path));
    }

    // ------------------------------------------------------------------- Set

    [Fact]
    public void Set_creates_nested_objects()
    {
        var root = new JsonObject();
        JsonPath.Set(root, "a.b", "x");
        Assert.Equal("x", (string?)root["a"]?["b"]);
    }

    [Fact]
    public void Set_creates_array_with_null_fill()
    {
        var root = new JsonObject();
        JsonPath.Set(root, "a[2]", "x");

        var arr = Assert.IsType<JsonArray>(root["a"]);
        Assert.Equal(3, arr.Count);
        Assert.Null(arr[0]);
        Assert.Null(arr[1]);
        Assert.Equal("x", (string?)arr[2]);
    }

    [Fact]
    public void Set_overwrites_an_existing_leaf()
    {
        var root = new JsonObject { ["a"] = "old" };
        JsonPath.Set(root, "a", "new");
        Assert.Equal("new", (string?)root["a"]);
    }

    [Fact]
    public void Set_replaces_a_wrong_typed_intermediate()
    {
        var root = new JsonObject { ["a"] = "scalar" };
        JsonPath.Set(root, "a.b", "x");
        Assert.Equal("x", (string?)root["a"]?["b"]);
    }

    [Fact]
    public void Set_creates_an_object_inside_an_array()
    {
        var root = new JsonObject();
        JsonPath.Set(root, "a[0].b", "x");

        var arr = Assert.IsType<JsonArray>(root["a"]);
        Assert.Equal("x", (string?)arr[0]?["b"]);
    }

    [Fact]
    public void Set_writes_a_coerced_scalar()
    {
        var root = new JsonObject();
        JsonPath.Set(root, "flags.active", true);
        Assert.True((bool)root["flags"]!["active"]!);
    }

    [Fact]
    public void Set_stores_a_clone_not_an_alias()
    {
        var root = new JsonObject();
        var payload = new JsonObject { ["k"] = "v" };
        JsonPath.Set(root, "a", payload);

        payload["k"] = "changed";
        Assert.Equal("v", (string?)root["a"]?["k"]); // tree holds the clone, not the original
    }

    [Fact]
    public void Set_empty_path_is_a_noop()
    {
        var root = new JsonObject { ["a"] = "1" };
        JsonPath.Set(root, "$", "ignored");

        Assert.Single(root);
        Assert.Equal("1", (string?)root["a"]);
    }

    [Fact]
    public void Set_property_on_an_array_root_is_a_silent_noop()
    {
        var root = new JsonArray();
        JsonPath.Set(root, "a", "x");
        Assert.Empty(root);
    }

    // -------------------------------------------------------------- Navigate

    [Fact]
    public void Navigate_reads_a_nested_value()
    {
        var root = new JsonObject { ["a"] = new JsonObject { ["b"] = "x" } };
        Assert.Equal("x", (string?)JsonPath.Navigate(root, "a.b"));
    }

    [Fact]
    public void Navigate_reads_an_array_element()
    {
        var root = new JsonObject { ["a"] = new JsonArray("zero", "one") };
        Assert.Equal("one", (string?)JsonPath.Navigate(root, "a[1]"));
    }

    [Theory]
    [InlineData("a.missing")] // missing property (a is an array)
    [InlineData("a[5]")]      // index out of range
    [InlineData("missing.b")] // missing property mid-path
    public void Navigate_returns_null_when_a_step_is_absent(string path)
    {
        var root = new JsonObject { ["a"] = new JsonArray("only") };
        Assert.Null(JsonPath.Navigate(root, path));
    }

    [Fact]
    public void Navigate_returns_null_for_a_null_root() =>
        Assert.Null(JsonPath.Navigate(null, "a.b"));

    // --------------------------------------------------------------- Combine

    [Fact]
    public void Combine_treats_dollar_as_the_parent() =>
        Assert.Equal("a[0]", JsonPath.Combine("a[0]", "$"));

    [Fact]
    public void Combine_joins_with_a_dot() =>
        Assert.Equal("a[0].label", JsonPath.Combine("a[0]", "label"));

    // ---------------------------------------------------------------- ToNode

    [Fact]
    public void ToNode_maps_clr_scalars()
    {
        Assert.Null(JsonPath.ToNode(null));
        Assert.True(JsonPath.ToNode(true)!.GetValue<bool>());
        Assert.Equal(7, JsonPath.ToNode(7)!.GetValue<int>());
        Assert.Equal(12.5m, JsonPath.ToNode(12.5m)!.GetValue<decimal>());
        Assert.Equal(3.5d, JsonPath.ToNode(3.5d)!.GetValue<double>());
        Assert.Equal("text", JsonPath.ToNode("text")!.GetValue<string>());
    }

    [Fact]
    public void ToNode_deep_clones_an_existing_node()
    {
        var original = new JsonObject { ["k"] = "v" };
        var cloned = JsonPath.ToNode(original);

        Assert.NotSame(original, cloned);
        original["k"] = "changed";
        Assert.Equal("v", (string?)cloned!["k"]); // clone is unaffected
    }
}
