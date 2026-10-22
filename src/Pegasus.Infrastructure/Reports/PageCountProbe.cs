using QuestPDF.Elements;
using QuestPDF.Infrastructure;

namespace Pegasus.Infrastructure.Reports;

/// <summary>
/// Learns a document's page count from the layout that produced it, so the
/// finished PDF is not opened again to count its pages. It draws nothing: in
/// the running footer it is composed on each page in each of QuestPDF's two
/// layout phases, and the total is only known in the second, so it records
/// the total when there is one. One probe serves one render and is never
/// shared.
/// </summary>
internal sealed class PageCountProbe : IDynamicComponent
{
    private int pages;

    /// <summary>Whether the layout reported a total.</summary>
    internal bool Counted => Volatile.Read(ref pages) > 0;

    /// <summary>The pages the layout reported.</summary>
    internal int Count => Counted
        ? Volatile.Read(ref pages)
        : throw new InvalidOperationException("The layout did not report a page count.");

    public DynamicComponentComposeResult Compose(DynamicContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.TotalPages != int.MaxValue)
        {
            Volatile.Write(ref pages, context.TotalPages);
        }

        return new DynamicComponentComposeResult
        {
            Content = context.CreateElement(_ => { }),
            HasMoreContent = false,
        };
    }
}
