using System.Text;
using CollisionDocNet.Extraction;
using CollisionDocNet.Model;

namespace CollisionDocNet.Security.Tests;

[TestClass]
public sealed class DeterministicHostileRegressionTests
{
    [TestMethod]
    public async Task ExtractAsync_DeterministicMutationSeeds_NeverThrowAndRepeatCanonically()
    {
        (byte[] Bytes, string FileName)[] seeds =
        [
            (SyntheticDocuments.Pdf("BT (seed) Tj ET"), "seed.pdf"),
            (SyntheticDocuments.CompoundSignature("WordDocument"), "seed.doc"),
            (SyntheticDocuments.MinimalDocx("seed"), "seed.docx"),
            (SyntheticDocuments.CompoundSignature("__properties_version1.0"), "seed.msg"),
            (SyntheticDocuments.Eml("seed", "text/plain"), "seed.eml"),
        ];

        foreach ((byte[] original, string fileName) in seeds)
        {
            for (int mutation = 0; mutation < 16; mutation++)
            {
                byte[] input = Mutate(original, mutation);
                string identity = $"security-fuzz-{fileName}-{mutation}";

                ExtractionResult first = await DocumentExtractor.ExtractAsync(input, identity, fileName);
                ExtractionResult retry = await DocumentExtractor.ExtractAsync(input, identity, fileName);

                CollectionAssert.AreEqual(
                    ExtractionResultJson.SerializeToUtf8Bytes(first),
                    ExtractionResultJson.SerializeToUtf8Bytes(retry),
                    $"Canonical output changed for {fileName} mutation {mutation}.");
                Assert.IsTrue(Enum.IsDefined(first.Outcome));
            }
        }
    }

    [TestMethod]
    public async Task ExtractAsync_DeterministicArbitraryByteSeeds_ReturnUnsupportedOrBoundedFailure()
    {
        var random = new Random(0x5ec001);
        for (int index = 0; index < 64; index++)
        {
            byte[] input = new byte[index * 7 % 513];
            random.NextBytes(input);

            ExtractionResult result = await DocumentExtractor.ExtractAsync(input, $"security-arbitrary-{index}", "opaque.bin");

            Assert.IsTrue(result.Outcome is
                ExtractionOutcome.UnsupportedFormat or
                ExtractionOutcome.UnsupportedFeature or
                ExtractionOutcome.Corrupt or
                ExtractionOutcome.ResourceLimitExceeded);
            Assert.IsFalse(result.Issues.Any(static issue => issue.Message.Contains("System.", StringComparison.Ordinal)));
        }
    }

    private static byte[] Mutate(byte[] source, int mutation)
    {
        byte[] result = source.ToArray();
        if (result.Length == 0)
        {
            return [(byte)mutation];
        }

        int offset = unchecked((mutation * 7919) % result.Length);
        result[offset] ^= (byte)(1 << (mutation & 7));
        if ((mutation & 3) == 3 && result.Length > 8)
        {
            Array.Resize(ref result, result.Length - (mutation % 7) - 1);
        }

        return result;
    }
}
