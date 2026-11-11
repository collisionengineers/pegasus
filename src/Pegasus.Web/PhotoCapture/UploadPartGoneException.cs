using Pegasus.Core.Documents;

namespace Pegasus.Web.PhotoCapture;

/// <summary>
/// A staged part listed before the read was gone when it was opened: a commit
/// that kept the item, or the sweep, cleared it.
/// </summary>
public sealed class UploadPartGoneException : Exception
{
    public UploadPartGoneException()
        : base(DocumentUploadRefusals.Unavailable)
    {
    }

    public UploadPartGoneException(string message)
        : base(message)
    {
    }

    public UploadPartGoneException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
