using System.Security.Cryptography;
using System.Text;

namespace CollisionDocNet.Core;

public readonly record struct Sha256Digest
{
    private const int HexLength = 64;

    private Sha256Digest(string hex) => Hex = hex;

    public string Hex { get; }

    public static Sha256Digest Compute(ReadOnlySpan<byte> bytes) =>
        new(Convert.ToHexStringLower(SHA256.HashData(bytes)));

    public static bool TryParse(string? value, out Sha256Digest digest)
    {
        digest = default;
        if (value is null || value.Length != HexLength)
        {
            return false;
        }

        foreach (char character in value)
        {
            bool isHex = character is >= '0' and <= '9' or >= 'a' and <= 'f';
            if (!isHex)
            {
                return false;
            }
        }

        digest = new Sha256Digest(value);
        return true;
    }

    public override string ToString() => Hex ?? string.Empty;
}

public static class StableIdentity
{
    public const string PolicyId = "sha256-length-prefixed/1";

    public static string Create(string domain, params ReadOnlySpan<string> components)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domain);
        if (domain.Length > 32
            || domain[0] is not (>= 'a' and <= 'z')
            || !IsSafeDomain(domain))
        {
            throw new ArgumentException(
                "The identity domain must be 1-32 lowercase ASCII letters, digits or hyphens and start with a letter.",
                nameof(domain));
        }

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Append(hash, domain);
        foreach (string component in components)
        {
            ArgumentNullException.ThrowIfNull(component);
            Append(hash, component);
        }

        return $"{domain}-{Convert.ToHexStringLower(hash.GetHashAndReset())}";
    }

    private static bool IsSafeDomain(string domain)
    {
        foreach (char character in domain)
        {
            if (character is not (>= 'a' and <= 'z')
                && character is not (>= '0' and <= '9')
                && character != '-')
            {
                return false;
            }
        }

        return true;
    }

    private static void Append(IncrementalHash hash, string value)
    {
        int byteCount = Encoding.UTF8.GetByteCount(value);
        Span<byte> length = stackalloc byte[sizeof(int)];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(length, byteCount);
        hash.AppendData(length);

        if (byteCount == 0)
        {
            return;
        }

        byte[] bytes = Encoding.UTF8.GetBytes(value);
        hash.AppendData(bytes);
    }
}
