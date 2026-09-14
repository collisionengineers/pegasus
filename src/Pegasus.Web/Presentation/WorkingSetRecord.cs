namespace Pegasus.Web.Presentation;

/// <summary>
/// How a page that is a record announces itself to the working-set strip
/// (v26 § Working set). The layout writes it onto <c>main</c> as
/// <c>data-record-href</c>, <c>data-record-kind</c>, <c>data-record-ref</c>,
/// <c>data-record-reg</c> and <c>data-record-glyph</c>; site.js keeps the set
/// per browser under <c>pegasus.workingSet</c>. A page sets
/// <c>ViewData["WorkingSetRecord"]</c> to one of these; every other page renders
/// a plain <c>main</c> and joins nothing.
/// </summary>
/// <param name="Href">The record's own route, the identity of its tab.</param>
/// <param name="Kind">One of <see cref="Kinds"/>; it chooses the tab glyph.</param>
/// <param name="Reference">The bold part of the tab: the Case/PO, "Triage", the U-reference, the image reference or the message subject.</param>
/// <param name="Registration">The mono part: the registration, or null when the record has none (a message shows its subject as the reference).</param>
/// <param name="Glyph">A state glyph, one of <see cref="Glyphs"/>, or null.</param>
public sealed record WorkingSetRecord(
    string Href,
    string Kind,
    string Reference,
    string? Registration = null,
    string? Glyph = null)
{
    public static class Kinds
    {
        public const string Case = "case";
        public const string Triage = "triage";
        public const string Unidentified = "unidentified";
        public const string Image = "image";
        public const string Message = "message";
    }

    public static class Glyphs
    {
        /// <summary>A Glass's session is open on the record.</summary>
        public const string GlassOpen = "glass-open";

        /// <summary>A colleague holds the record's edit lease.</summary>
        public const string LeaseColleague = "lease-colleague";
    }
}
