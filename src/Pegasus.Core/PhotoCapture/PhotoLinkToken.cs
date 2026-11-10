using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Pegasus.Core.PhotoCapture;

/// <summary>
/// A raw photo link token, which exists only in the issuing response, the
/// message that carries it and the phone that opened it. Its
/// <see cref="ToString"/> is redacted so it can never reach a log or a trace
/// by accident; <see cref="Value"/> is read deliberately where the link is
/// written out.
/// </summary>
public readonly record struct CaptureLinkSecret(string Value)
{
    public override string ToString() => "[REDACTED]";
}

/// <summary>
/// The one owner of what a photo link token is (FRD-28, ADR-0090): 256 bits
/// from the platform's random source, written as 43 base64url characters,
/// and kept only as its SHA-256 digest. The same rule carried the removed
/// request-scoped upload links (INT-31) and is kept rather than re-decided.
/// A standing link's slug is not a secret: it lives on WhatsApp and in a QR
/// code, and it can only start a capture, so it is shorter and stored as it is.
/// </summary>
public static class PhotoLinkToken
{
    public const int TokenBytes = 32;
    public const int TokenLength = 43;
    public const int SlugLength = 12;
    public const int MinimumSlugLength = 6;
    public const int MaximumSlugLength = 60;

    private const string CrockfordAlphabet = "0123456789abcdefghjkmnpqrstvwxyz";

    public static CaptureLinkSecret Create()
    {
        Span<byte> bytes = stackalloc byte[TokenBytes];
        RandomNumberGenerator.Fill(bytes);
        return new(Base64Url.EncodeToString(bytes));
    }

    /// <summary>The lowercase hexadecimal SHA-256 of the token's ASCII, the only form a store holds.</summary>
    public static string Digest(CaptureLinkSecret secret) => Digest(secret.Value);

    public static string Digest(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.ASCII.GetBytes(token)));
    }

    /// <summary>
    /// Whether a presented string has the shape of a token at all: exactly
    /// 43 base64url characters. Anything else is refused before any store is
    /// read, so a malformed value costs no lookup and gets the same answer as
    /// an unknown one.
    /// </summary>
    public static bool HasTokenShape(string? presented)
    {
        if (presented is null || presented.Length != TokenLength)
        {
            return false;
        }

        foreach (var character in presented)
        {
            if (!char.IsAsciiLetterOrDigit(character) && character is not ('-' or '_'))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>A contact's slug: 12 Crockford base32 characters (60 bits), no vowels that read as digits.</summary>
    public static string CreateSlug()
    {
        Span<byte> bytes = stackalloc byte[SlugLength];
        RandomNumberGenerator.Fill(bytes);
        var characters = new char[SlugLength];
        for (var index = 0; index < SlugLength; index++)
        {
            characters[index] = CrockfordAlphabet[bytes[index] & 0x1F];
        }

        return new string(characters);
    }

    /// <summary>
    /// Whether a presented slug has the shape of one: 6 to 60 lowercase
    /// letters, digits and hyphens. The website link may carry a readable
    /// slug the operator chose; a contact's is always <see cref="CreateSlug"/>'s.
    /// </summary>
    public static bool HasSlugShape(string? presented)
    {
        if (presented is null
            || presented.Length is < MinimumSlugLength or > MaximumSlugLength
            || presented[0] == '-'
            || presented[^1] == '-')
        {
            return false;
        }

        foreach (var character in presented)
        {
            if (!(char.IsAsciiLetterLower(character) || char.IsAsciiDigit(character) || character == '-'))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>The slug as stored: trimmed and lowercased, or null when it has no slug shape.</summary>
    public static string? NormalizeSlug(string? presented)
    {
        var value = presented?.Trim().ToLowerInvariant();
        return HasSlugShape(value) ? value : null;
    }
}
