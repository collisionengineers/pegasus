namespace Pegasus.Web.Pages.Intake;

/// <summary>
/// The file name a retained intake download is saved under: the leaf of the
/// stored name without control characters, quotes, semicolons or characters
/// the host's file system does not allow, or <paramref name="fallback"/> when
/// nothing is left.
/// </summary>
internal static class IntakeDownloadFileName
{
    public static string Safe(string fileName, string fallback)
    {
        var name = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(name))
        {
            return fallback;
        }

        var invalid = Path.GetInvalidFileNameChars();
        var safe = string.Concat(name.Where(character =>
            !char.IsControl(character)
            && character != '"'
            && character != '\''
            && character != ';'
            && !invalid.Contains(character)));
        return string.IsNullOrWhiteSpace(safe) ? fallback : safe;
    }
}
