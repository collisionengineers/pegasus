namespace Pegasus.Infrastructure.Intake.DocumentExtraction.Cfb;

internal readonly record struct CompoundFileReadResult
{
    private CompoundFileReadResult(
        CompoundFile? file,
        CompoundFileReadError error,
        uint? location)
    {
        File = file;
        Error = error;
        Location = location;
    }

    public CompoundFile? File { get; }

    public CompoundFileReadError Error { get; }

    public uint? Location { get; }

    public bool IsSuccess => Error == CompoundFileReadError.None && File is not null;

    internal static CompoundFileReadResult Success(CompoundFile file) =>
        new(file, CompoundFileReadError.None, null);

    internal static CompoundFileReadResult Failure(
        CompoundFileReadError error,
        uint? location = null) =>
        new(null, error, location);
}
