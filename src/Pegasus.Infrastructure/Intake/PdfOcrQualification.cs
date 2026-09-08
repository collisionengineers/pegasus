using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Graphics.Operations;
using UglyToad.PdfPig.Graphics.Operations.SpecialGraphicsState;
using UglyToad.PdfPig.Graphics.Operations.TextShowing;
using UglyToad.PdfPig.Graphics.Operations.TextState;
using UglyToad.PdfPig.Parser.Parts;
using UglyToad.PdfPig.Tokens;

namespace Pegasus.Infrastructure.Intake;

/// <summary>
/// Positive evidence of the anonymous Type3 character-map fault seen in retained
/// estimate PDFs. This is not the scan-page rule or a fallback for parser failure.
/// </summary>
public static class PdfOcrQualification
{
    public static bool HasUnusableTextMap(PdfDocument document, Page page)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(page);

        var resources = FindResources(document, page.Dictionary);
        var fonts = ReadDictionary(document, resources, NameToken.Font);
        var states = new Stack<(bool UnusableFont, double Size, TextRenderingMode Mode)>();
        var state = (UnusableFont: false, Size: 0d, Mode: TextRenderingMode.Fill);
        foreach (var operation in page.Operations)
        {
            switch (operation)
            {
                case Push:
                    states.Push(state);
                    break;
                case Pop:
                    if (!states.TryPop(out state))
                        return false;
                    break;
                case SetFontAndSize font:
                    state.UnusableFont = IsAnonymousType3(document, ReadDictionary(document, fonts, font.Font));
                    state.Size = font.Size;
                    break;
                case SetTextRenderingMode rendering:
                    state.Mode = rendering.Mode;
                    break;
                case SetGraphicsStateParametersFromDictionary graphics:
                    var parameters = ReadDictionary(document,
                        ReadDictionary(document, resources, NameToken.ExtGState), graphics.Name);
                    if (parameters is not null && parameters.TryGet(NameToken.Font, out var fontToken)
                        && DirectObjectFinder.TryGet<ArrayToken>(fontToken, document.Structure.TokenScanner, out var fontArray)
                        && fontArray.Length == 2)
                    {
                        state.UnusableFont = DirectObjectFinder.TryGet<DictionaryToken>(
                            fontArray[0], document.Structure.TokenScanner, out var fontDictionary)
                            && IsAnonymousType3(document, fontDictionary);
                        state.Size = fontArray[1] is NumericToken size ? size.Double : 0;
                    }
                    break;
                default:
                    if (state.UnusableFont && state.Size != 0
                        && state.Mode is not (TextRenderingMode.Neither or TextRenderingMode.NeitherClip)
                        && ShowsText(operation))
                        return true;
                    break;
            }
        }
        return false;
    }

    private static bool IsAnonymousType3(PdfDocument document, DictionaryToken? font)
    {
        if (font is null
            || !font.TryGet<NameToken>(NameToken.Subtype, out var subtype)
            || subtype != NameToken.Type3
            || font.TryGet(NameToken.ToUnicode, out _))
            return false;

        var encoding = ReadDictionary(document, font, NameToken.Encoding);
        if (encoding is null || !encoding.TryGet(NameToken.Differences, out var differencesToken)
            || !DirectObjectFinder.TryGet<ArrayToken>(differencesToken, document.Structure.TokenScanner, out var differences))
            return false;

        var hasAnonymousGlyph = false;
        foreach (var item in differences.Data)
        {
            if (item is NumericToken)
                continue;
            if (item is not NameToken name)
                return false;
            if (name.Data == ".notdef")
                continue;
            if (name.Data.Length == 0 || !name.Data.All(char.IsAsciiDigit))
                return false;
            hasAnonymousGlyph = true;
        }
        return hasAnonymousGlyph;
    }

    private static bool ShowsText(IGraphicsStateOperation operation) => operation switch
    {
        ShowText text => text.Bytes.Length > 0 || !string.IsNullOrEmpty(text.Text),
        MoveToNextLineShowText text => text.Bytes.Length > 0 || !string.IsNullOrEmpty(text.Text),
        MoveToNextLineShowTextWithSpacing text => text.Bytes.Length > 0 || !string.IsNullOrEmpty(text.Text),
        ShowTextsWithPositioning text => text.Array.Any(token => token is StringToken { Data.Length: > 0 }
            or HexToken { Memory.Length: > 0 }),
        _ => false
    };

    private static DictionaryToken? FindResources(PdfDocument document, DictionaryToken page)
    {
        var visited = new HashSet<DictionaryToken>();
        DictionaryToken? current = page;
        while (current is not null && visited.Add(current))
        {
            if (current.TryGet(NameToken.Resources, out var resources))
                return DirectObjectFinder.TryGet<DictionaryToken>(resources, document.Structure.TokenScanner, out var dictionary)
                    ? dictionary : null;
            current = ReadDictionary(document, current, NameToken.Parent);
        }
        return null;
    }

    private static DictionaryToken? ReadDictionary(PdfDocument document, DictionaryToken? owner, NameToken key) =>
        owner is not null && owner.TryGet(key, out var value)
        && DirectObjectFinder.TryGet<DictionaryToken>(value, document.Structure.TokenScanner, out var dictionary)
            ? dictionary : null;
}
