namespace Pegasus.Infrastructure.Intake.DocumentExtraction;

/// <summary>
/// Replaces unpaired UTF-16 surrogates so extracted text is always valid
/// Unicode before it reaches persistence or serialization.
/// </summary>
internal static class TextSanitation
{
    internal static string ReplaceLoneSurrogates(string value, out bool replaced)
    {
        replaced = false;
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

            replaced = true;
            break;
        }

        if (!replaced)
        {
            return value;
        }

        var characters = value.ToCharArray();
        for (var index = 0; index < characters.Length; index++)
        {
            if (!char.IsSurrogate(characters[index]))
            {
                continue;
            }

            if (char.IsHighSurrogate(characters[index])
                && index + 1 < characters.Length
                && char.IsLowSurrogate(characters[index + 1]))
            {
                index++;
                continue;
            }

            characters[index] = '�';
        }

        return new string(characters);
    }
}
