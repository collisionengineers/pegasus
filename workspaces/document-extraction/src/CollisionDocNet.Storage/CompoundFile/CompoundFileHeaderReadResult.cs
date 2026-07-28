namespace CollisionDocNet.Storage.CompoundFile;

public readonly record struct CompoundFileHeaderReadResult
{
    private CompoundFileHeaderReadResult(
        CompoundFileHeader? header,
        CompoundFileHeaderReadError error)
    {
        Header = header;
        Error = error;
    }

    public CompoundFileHeader? Header { get; }

    public CompoundFileHeaderReadError Error { get; }

    public bool IsSuccess =>
        Error == CompoundFileHeaderReadError.None && Header is not null;

    internal static CompoundFileHeaderReadResult Success(CompoundFileHeader header) =>
        new(header, CompoundFileHeaderReadError.None);

    internal static CompoundFileHeaderReadResult Failure(CompoundFileHeaderReadError error) =>
        new(null, error);
}
