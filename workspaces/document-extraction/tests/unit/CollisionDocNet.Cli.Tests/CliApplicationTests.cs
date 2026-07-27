using System.Text;
using System.Text.Json;
using CollisionDocNet.Model;

namespace CollisionDocNet.Cli.Tests;

[TestClass]
public sealed class CliApplicationTests
{
    [TestMethod]
    public async Task RunAsync_DetectFromStandardInput_WritesOneSafeDetectionDocument()
    {
        await using var input = MessageStream();
        using var output = new StringWriter();
        using var error = new StringWriter();

        int exitCode = await CliApplication.RunAsync(["detect", "--input", "-", "--name", "message.eml"], input, output, error);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(1, output.ToString().Count(static character => character == '\n'));
        Assert.Contains("\"detectedFormat\":\"InternetMessage\"", output.ToString());
        Assert.AreEqual(string.Empty, error.ToString());
        Assert.DoesNotContain("sender@example.test", output.ToString());
        Assert.DoesNotContain("evidence body", output.ToString());
    }

    [TestMethod]
    public async Task RunAsync_FileAndStandardInput_DetectionIsEquivalent()
    {
        string root = CreateTemporaryDirectory();
        string path = Path.Combine(root, "message.eml");
        await File.WriteAllBytesAsync(path, Message());
        try
        {
            using var fileOutput = new StringWriter();
            using var stdinOutput = new StringWriter();
            using var error = new StringWriter();
            await using var stdin = MessageStream();

            int fileExit = await CliApplication.RunAsync(["detect", "--input", path], Stream.Null, fileOutput, error);
            int stdinExit = await CliApplication.RunAsync(["detect", "--input", "-", "--name", "message.eml"], stdin, stdinOutput, error);

            Assert.AreEqual(fileExit, stdinExit);
            Assert.AreEqual(fileOutput.ToString(), stdinOutput.ToString());
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [TestMethod]
    public async Task RunAsync_Extract_CreatesVerifiedBundleAndCompletionEnvelope()
    {
        string root = CreateTemporaryDirectory();
        try
        {
            string outputPath = Path.Combine(root, "bundle");
            await using var input = AttachmentMessageStream();
            using var output = new StringWriter();
            using var error = new StringWriter();

            int exitCode = await CliApplication.RunAsync(["extract", "--input", "-", "--name", "message.eml", "--output", outputPath], input, output, error);

            Assert.AreEqual(0, exitCode);
            Assert.Contains("\"resultPath\":\"result.json\"", output.ToString());
            Assert.DoesNotContain("evidence body", output.ToString());
            Assert.AreEqual(string.Empty, error.ToString());
            string resultPath = Path.Combine(outputPath, "result.json");
            Assert.IsTrue(File.Exists(resultPath));
            using JsonDocument bundle = JsonDocument.Parse(await File.ReadAllBytesAsync(resultPath));
            JsonElement asset = Assert.ContainsSingle(bundle.RootElement.GetProperty("assetFiles").EnumerateArray().ToArray());
            string relative = asset.GetProperty("path").GetString()!;
            Assert.EndsWith(".png", relative);
            byte[] content = await File.ReadAllBytesAsync(Path.Combine(outputPath, relative.Replace('/', Path.DirectorySeparatorChar)));
            Assert.AreEqual(Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(content)), asset.GetProperty("sha256").GetString());
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [TestMethod]
    public async Task RunAsync_Extract_NonImageAttachmentWritesDescriptorButNoAssetFile()
    {
        string root = CreateTemporaryDirectory();
        try
        {
            string outputPath = Path.Combine(root, "bundle");
            await using var input = NonImageAttachmentMessageStream();
            using var output = new StringWriter();
            using var error = new StringWriter();

            int exitCode = await CliApplication.RunAsync(
                ["extract", "--input", "-", "--name", "message.eml", "--output", outputPath], input, output, error);

            Assert.AreEqual(0, exitCode);
            using JsonDocument bundle = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(outputPath, "result.json")));
            Assert.IsEmpty(bundle.RootElement.GetProperty("assetFiles").EnumerateArray().ToArray());
            Assert.ContainsSingle(bundle.RootElement.GetProperty("result").GetProperty("metadata").EnumerateArray()
                .Where(static item => item.GetProperty("name").GetString() == "nonPayload.binary"));
            Assert.IsFalse(Directory.Exists(Path.Combine(outputPath, "assets")));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [TestMethod]
    public async Task RunAsync_ExistingOutput_ReturnsTechnicalFailureAndPreservesDirectory()
    {
        string root = CreateTemporaryDirectory();
        string outputPath = Path.Combine(root, "bundle");
        Directory.CreateDirectory(outputPath);
        File.WriteAllText(Path.Combine(outputPath, "sentinel"), "keep");
        try
        {
            await using var input = MessageStream();
            using var output = new StringWriter();
            using var error = new StringWriter();

            int exitCode = await CliApplication.RunAsync(["extract", "--input", "-", "--name", "message.eml", "--output", outputPath], input, output, error);

            Assert.AreEqual(70, exitCode);
            Assert.Contains("\"outcome\":\"TechnicalFailure\"", output.ToString());
            Assert.AreEqual("keep", File.ReadAllText(Path.Combine(outputPath, "sentinel")));
            Assert.DoesNotContain("evidence body", error.ToString());
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [TestMethod]
    public async Task RunAsync_CancelledWrite_RemovesOwnedStagingAndReturnsCancelledEnvelope()
    {
        string root = CreateTemporaryDirectory();
        string outputPath = Path.Combine(root, "bundle");
        var fileSystem = new CancellingFileSystem(PhysicalCliFileSystem.Instance);
        try
        {
            await using var input = MessageStream();
            using var output = new StringWriter();
            using var error = new StringWriter();

            int exitCode = await CliApplication.RunAsync(
                ["extract", "--input", "-", "--name", "message.eml", "--output", outputPath],
                input, output, error, fileSystem, CancellationToken.None);

            Assert.AreEqual(25, exitCode);
            Assert.Contains("\"resultPath\":null", output.ToString());
            Assert.IsFalse(Directory.Exists(outputPath));
            Assert.IsEmpty(Directory.GetDirectories(root));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [TestMethod]
    public async Task RunAsync_ReparseInput_IsDeniedBeforeOpen()
    {
        string root = CreateTemporaryDirectory();
        string path = Path.Combine(root, "message.eml");
        await File.WriteAllBytesAsync(path, Message());
        var fileSystem = new ReparseFileSystem(PhysicalCliFileSystem.Instance, path);
        try
        {
            using var output = new StringWriter();
            using var error = new StringWriter();

            int exitCode = await CliApplication.RunAsync(["detect", "--input", path], Stream.Null, output, error, fileSystem, CancellationToken.None);

            Assert.AreEqual(70, exitCode);
            Assert.Contains("\"outcome\":\"TechnicalFailure\"", output.ToString());
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [TestMethod]
    [DataRow("https://example.test/evidence.pdf")]
    [DataRow("file:///C:/evidence.pdf")]
    [DataRow("\\\\server\\share\\evidence.pdf")]
    [DataRow("\\\\?\\C:\\evidence.pdf")]
    [DataRow("\\\\.\\C:\\evidence.pdf")]
    public async Task RunAsync_RemoteUriAndDeviceInputs_AreDenied(string path)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        int exitCode = await CliApplication.RunAsync(["detect", "--input", path, "--quiet"], Stream.Null, output, error);

        Assert.AreEqual(70, exitCode);
        Assert.Contains("\"outcome\":\"TechnicalFailure\"", output.ToString());
        Assert.IsNotEmpty(error.ToString());
    }

    [TestMethod]
    public async Task RunAsync_ConcurrentOutputCollision_PublishesOneCompleteBundleAndLeaksNoStaging()
    {
        string root = CreateTemporaryDirectory();
        string outputPath = Path.Combine(root, "bundle");
        try
        {
            using var output1 = new StringWriter();
            using var output2 = new StringWriter();
            using var error1 = new StringWriter();
            using var error2 = new StringWriter();
            await using var input1 = MessageStream();
            await using var input2 = MessageStream();

            Task<int> first = CliApplication.RunAsync(["extract", "--input", "-", "--name", "message.eml", "--output", outputPath], input1, output1, error1);
            Task<int> second = CliApplication.RunAsync(["extract", "--input", "-", "--name", "message.eml", "--output", outputPath], input2, output2, error2);
            int[] exits = [await first, await second];
            Array.Sort(exits);

            Assert.AreEqual(0, exits[0]);
            Assert.AreEqual(70, exits[1]);
            Assert.IsTrue(File.Exists(Path.Combine(outputPath, "result.json")));
            Assert.HasCount(1, Directory.GetDirectories(root));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [TestMethod]
    public async Task RunAsync_Retry_WritesByteIdenticalLogicalBundles()
    {
        string root = CreateTemporaryDirectory();
        string firstPath = Path.Combine(root, "first");
        string secondPath = Path.Combine(root, "second");
        try
        {
            using var output1 = new StringWriter();
            using var output2 = new StringWriter();
            using var error = new StringWriter();
            await using var input1 = AttachmentMessageStream();
            await using var input2 = AttachmentMessageStream();

            int first = await CliApplication.RunAsync(["extract", "--input", "-", "--name", "message.eml", "--output", firstPath], input1, output1, error);
            int second = await CliApplication.RunAsync(["extract", "--input", "-", "--name", "message.eml", "--output", secondPath], input2, output2, error);

            Assert.AreEqual(0, first);
            Assert.AreEqual(0, second);
            CollectionAssert.AreEqual(await File.ReadAllBytesAsync(Path.Combine(firstPath, "result.json")),
                await File.ReadAllBytesAsync(Path.Combine(secondPath, "result.json")));
            string firstAsset = Assert.ContainsSingle(Directory.GetFiles(Path.Combine(firstPath, "assets")));
            string secondAsset = Assert.ContainsSingle(Directory.GetFiles(Path.Combine(secondPath, "assets")));
            Assert.AreEqual(Path.GetFileName(firstAsset), Path.GetFileName(secondAsset));
            CollectionAssert.AreEqual(await File.ReadAllBytesAsync(firstAsset), await File.ReadAllBytesAsync(secondAsset));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [TestMethod]
    public async Task RunAsync_RaisedLimit_IsUsageError()
    {
        await using var input = MessageStream();
        using var output = new StringWriter();
        using var error = new StringWriter();

        int exitCode = await CliApplication.RunAsync(["detect", "--input", "-", "--name", "message.eml", "--max-input-bytes", "10485761"], input, output, error);

        Assert.AreEqual(CliApplication.UsageExitCode, exitCode);
        Assert.AreEqual(string.Empty, output.ToString());
    }

    [TestMethod]
    public async Task RunAsync_LoweredInputLimit_ReturnsResourceLimitExit()
    {
        await using var input = MessageStream();
        using var output = new StringWriter();
        using var error = new StringWriter();

        int exitCode = await CliApplication.RunAsync(["detect", "--input", "-", "--name", "message.eml", "--max-input-bytes", "1"], input, output, error);

        Assert.AreEqual(24, exitCode);
        Assert.Contains("\"outcome\":\"ResourceLimitExceeded\"", output.ToString());
    }

    [TestMethod]
    public async Task RunAsync_UsageError_DoesNotEchoSensitiveOptionValue()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        int exitCode = await CliApplication.RunAsync(["extract", "--input", "sensitive-name.eml", "--unknown", "secret"], Stream.Null, output, error);

        Assert.AreEqual(CliApplication.UsageExitCode, exitCode);
        Assert.DoesNotContain("sensitive-name", error.ToString());
        Assert.DoesNotContain("secret", error.ToString());
    }

    [TestMethod]
    [DataRow(ExtractionOutcome.Complete, 0)]
    [DataRow(ExtractionOutcome.Partial, 10)]
    [DataRow(ExtractionOutcome.UnsupportedFormat, 20)]
    [DataRow(ExtractionOutcome.UnsupportedFeature, 21)]
    [DataRow(ExtractionOutcome.Encrypted, 22)]
    [DataRow(ExtractionOutcome.Corrupt, 23)]
    [DataRow(ExtractionOutcome.ResourceLimitExceeded, 24)]
    [DataRow(ExtractionOutcome.Cancelled, 25)]
    [DataRow(ExtractionOutcome.TimedOut, 26)]
    [DataRow(ExtractionOutcome.TechnicalFailure, 70)]
    public void ExitCode_EveryOutcome_UsesDocumentedMapping(ExtractionOutcome outcome, int expected) =>
        Assert.AreEqual(expected, CliApplication.ExitCode(outcome));

    [TestMethod]
    public async Task RunAsync_HelpAndVersion_AreDeterministicJson()
    {
        using var help = new StringWriter();
        using var version = new StringWriter();
        using var error = new StringWriter();

        int helpExit = await CliApplication.RunAsync(["help"], Stream.Null, help, error);
        int versionExit = await CliApplication.RunAsync(["version"], Stream.Null, version, error);

        Assert.AreEqual(0, helpExit);
        Assert.AreEqual(0, versionExit);
        _ = JsonDocument.Parse(help.ToString());
        _ = JsonDocument.Parse(version.ToString());
        Assert.AreEqual(string.Empty, error.ToString());
    }

    private static byte[] Message() => Encoding.ASCII.GetBytes(
        "From: sender@example.test\r\nTo: receiver@example.test\r\nSubject: evidence\r\nContent-Type: text/plain\r\n\r\nevidence body\r\n");

    private static MemoryStream MessageStream() => new(Message(), writable: false);

    private static MemoryStream AttachmentMessageStream() => new(Encoding.ASCII.GetBytes(
        "From: sender@example.test\r\nTo: receiver@example.test\r\nSubject: evidence\r\nMIME-Version: 1.0\r\nContent-Type: multipart/mixed; boundary=x\r\n\r\n--x\r\nContent-Type: text/plain\r\n\r\nevidence body\r\n--x\r\nContent-Type: image/png; name=a.png\r\nContent-Disposition: attachment; filename=a.png\r\nContent-Transfer-Encoding: base64\r\n\r\niVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Y9ZKXcAAAAASUVORK5CYII=\r\n--x--\r\n"), writable: false);

    private static MemoryStream NonImageAttachmentMessageStream() => new(Encoding.ASCII.GetBytes(
        "From: sender@example.test\r\nTo: receiver@example.test\r\nSubject: evidence\r\nMIME-Version: 1.0\r\nContent-Type: multipart/mixed; boundary=x\r\n\r\n--x\r\nContent-Type: text/plain\r\n\r\nevidence body\r\n--x\r\nContent-Type: application/octet-stream\r\nContent-Disposition: attachment; filename=a.bin\r\nContent-Transfer-Encoding: base64\r\n\r\nAQIDBA==\r\n--x--\r\n"), writable: false);

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"collisiondocnet-cli-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private class DelegatingFileSystem(ICliFileSystem inner) : ICliFileSystem
    {
        protected ICliFileSystem Inner { get; } = inner;
        public virtual Stream OpenRead(string path) => Inner.OpenRead(path);
        public virtual Stream CreateNewFile(string path) => Inner.CreateNewFile(path);
        public virtual bool FileExists(string path) => Inner.FileExists(path);
        public virtual bool DirectoryExists(string path) => Inner.DirectoryExists(path);
        public virtual bool IsReparsePoint(string path) => Inner.IsReparsePoint(path);
        public virtual void CreateDirectory(string path) => Inner.CreateDirectory(path);
        public virtual void MoveDirectory(string source, string destination) => Inner.MoveDirectory(source, destination);
        public virtual void DeleteDirectory(string path, bool recursive) => Inner.DeleteDirectory(path, recursive);
        public virtual string GetFullPath(string path) => Inner.GetFullPath(path);
        public virtual string GetPathRoot(string path) => Inner.GetPathRoot(path);
        public virtual string GetFileName(string path) => Inner.GetFileName(path);
        public virtual string? GetDirectoryName(string path) => Inner.GetDirectoryName(path);
        public virtual string GetRandomFileName() => Inner.GetRandomFileName();
        public virtual string Combine(string first, string second) => Inner.Combine(first, second);
    }

    private sealed class ReparseFileSystem(ICliFileSystem inner, string reparsePath) : DelegatingFileSystem(inner)
    {
        public override bool IsReparsePoint(string path) => string.Equals(GetFullPath(path), GetFullPath(reparsePath), StringComparison.OrdinalIgnoreCase) || base.IsReparsePoint(path);
    }

    private sealed class CancellingFileSystem(ICliFileSystem inner) : DelegatingFileSystem(inner)
    {
        private bool _cancelled;
        public override Stream CreateNewFile(string path)
        {
            if (!_cancelled)
            {
                _cancelled = true;
                return new CancellingWriteStream(base.CreateNewFile(path));
            }
            return base.CreateNewFile(path);
        }
    }

    private sealed class CancellingWriteStream(Stream inner) : Stream
    {
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => inner.Length;
        public override long Position { get => inner.Position; set => throw new NotSupportedException(); }
        public override void Flush() => inner.Flush();
        public override Task FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => inner.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => throw new OperationCanceledException();
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => ValueTask.FromCanceled(CancelledToken());
        protected override void Dispose(bool disposing) { if (disposing) inner.Dispose(); base.Dispose(disposing); }
        public override async ValueTask DisposeAsync() { await inner.DisposeAsync(); await base.DisposeAsync(); GC.SuppressFinalize(this); }
        private static CancellationToken CancelledToken() { var source = new CancellationTokenSource(); source.Cancel(); return source.Token; }
    }
}
