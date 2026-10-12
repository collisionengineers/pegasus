using System.Diagnostics.CodeAnalysis;

namespace Pegasus.Core.ReferenceData;

public static class ReferenceDataPolicy
{
    public static bool IsCanonicalDomainSuffix(string? domainSuffix)
    {
        if (string.IsNullOrEmpty(domainSuffix) ||
            domainSuffix.Length > 254 ||
            domainSuffix[0] != '@')
        {
            return false;
        }

        var domain = domainSuffix.AsSpan(1);
        if (domain.Length == 0 || domain.IndexOf('@') >= 0)
        {
            return false;
        }

        var labelCount = 0;
        var labelStart = 0;
        for (var index = 0; index <= domain.Length; index++)
        {
            if (index != domain.Length && domain[index] != '.')
            {
                continue;
            }

            var label = domain[labelStart..index];
            if (!IsCanonicalDomainLabel(label))
            {
                return false;
            }

            labelCount++;
            labelStart = index + 1;
        }

        return labelCount >= 2;
    }

    public static bool TryExtractDomainSuffix(
        string? emailAddress,
        [NotNullWhen(true)] out string? domainSuffix)
    {
        domainSuffix = null;
        if (string.IsNullOrWhiteSpace(emailAddress))
        {
            return false;
        }

        var trimmed = emailAddress.Trim();
        var separator = trimmed.LastIndexOf('@');
        if (separator <= 0 || separator == trimmed.Length - 1)
        {
            return false;
        }

        var candidate = $"@{trimmed[(separator + 1)..].ToLowerInvariant()}";
        if (!IsCanonicalDomainSuffix(candidate))
        {
            return false;
        }

        domainSuffix = candidate;
        return true;
    }

    private static bool IsCanonicalDomainLabel(ReadOnlySpan<char> label)
    {
        if (label.Length is < 1 or > 63 || label[0] == '-' || label[^1] == '-')
        {
            return false;
        }

        foreach (var character in label)
        {
            if (character == '-' ||
                character is >= 'a' and <= 'z' ||
                character is >= '0' and <= '9')
            {
                continue;
            }

            return false;
        }

        return true;
    }
}
