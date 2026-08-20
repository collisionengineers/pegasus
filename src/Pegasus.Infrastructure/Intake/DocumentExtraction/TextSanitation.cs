namespace Pegasus.Infrastructure.Intake.DocumentExtraction;

/// <summary>
/// Replaces unpaired UTF-16 surrogates so extracted text is always valid
/// Unicode before it reaches persistence or serialization.
/// </summary>
internal static class TextSanitation
{
    internal static string ReplaceLoneSurrogates(string value, out bool replaced)
    {
        char[]? characters = null;
        for (var index = 0; index < value.Length; index++)
        {
            if (!char.IsSurrogate(value[index]))
            {
                continue;
            }

            if (char.IsHighSurrogate(value[index])
                && index + 1 < value.Length
                && char.IsLowSurrogate(value[index + 1]))
            {
                index++;
                continue;
            }

            characters ??= value.ToCharArray();
            characters[index] = '�';
        }

        if (characters is null)
        {
            replaced = false;
            return value;
        }

        replaced = true;
        return new string(characters);
    }
}
