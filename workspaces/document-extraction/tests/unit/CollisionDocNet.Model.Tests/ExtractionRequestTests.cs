using CollisionDocNet.Model;

namespace CollisionDocNet.Model.Tests;

[TestClass]
public sealed class ExtractionRequestTests
{
    [TestMethod]
    public void FromBytes_CopiesCallerBuffer()
    {
        byte[] source = [1, 2, 3];
        ExtractionInput input = ExtractionInput.FromBytes(source);

        source[0] = 9;

        Assert.AreEqual(ExtractionInputKind.Bytes, input.Kind);
        Assert.AreEqual((byte)1, input.Bytes[0]);
        Assert.IsNull(input.Stream);
    }

    [TestMethod]
    public void FromStream_PreservesReadableCallerOwnedStream()
    {
        using var stream = new MemoryStream([1]);

        ExtractionInput input = ExtractionInput.FromStream(stream);

        Assert.AreEqual(ExtractionInputKind.Stream, input.Kind);
        Assert.AreSame(stream, input.Stream);
        Assert.IsTrue(stream.CanRead);
    }

    [TestMethod]
    public void FromStream_UnreadableStream_Throws()
    {
        using var stream = new MemoryStream();
        stream.Close();

        Assert.ThrowsExactly<ArgumentException>(() => ExtractionInput.FromStream(stream));
    }

    [TestMethod]
    public void Request_PreservesUntrustedHintsAsMetadataOnly()
    {
        var request = new ExtractionRequest(
            ExtractionInput.FromBytes([1]),
            "source-1",
            "..\\untrusted.doc",
            "application/msword",
            ExtractionPolicy.CreateDefault());

        Assert.AreEqual("source-1", request.SourceIdentity);
        Assert.AreEqual("..\\untrusted.doc", request.FileName);
        Assert.AreEqual("application/msword", request.DeclaredMediaType);
        Assert.AreEqual(ExtractionPolicy.DefaultPolicyId, request.Policy.PolicyId);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("   ")]
    public void Request_EmptySourceIdentity_Throws(string sourceIdentity)
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            new ExtractionRequest(
                ExtractionInput.FromBytes([]),
                sourceIdentity,
                null,
                null,
                ExtractionPolicy.CreateDefault()));
    }
}
