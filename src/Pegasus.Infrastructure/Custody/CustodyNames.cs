namespace Pegasus.Infrastructure.Custody;

/// <summary>
/// The one safe-name mapping for custody folder and file names. Every custody
/// adapter (Box source custody, Box managed content, local managed content)
/// must resolve the same Case/PO to the same name, so the mapping lives in one
/// place with a fixed character set: <c>Path.GetInvalidFileNameChars()</c>
/// differs between Windows and Linux, and a remote store's path must never
/// depend on which host computed it. The set below is the Windows-invalid
/// superset, so it is identical on every platform and valid on all of them.
/// </summary>
internal static class CustodyNames
{
    private static readonly HashSet<char> InvalidCharacters = BuildInvalidCharacters();

    internal static string SafeName(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var result = new string(value.Trim()
            .Select(character => InvalidCharacters.Contains(character) ? '_' : character)
            .ToArray());
        if (string.IsNullOrWhiteSpace(result) || result.Length > 180)
        {
            throw new ArgumentException("The custody name is invalid.", nameof(value));
        }

        return result;
    }

    private static HashSet<char> BuildInvalidCharacters()
    {
        var characters = new HashSet<char> { '"', '<', '>', '|', ':', '*', '?', '\\', '/' };
        for (var control = (char)0; control < (char)32; control++)
        {
            characters.Add(control);
        }

        return characters;
    }
}
