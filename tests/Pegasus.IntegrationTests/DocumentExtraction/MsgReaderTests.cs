using Xunit;
using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using Pegasus.Infrastructure.Intake.DocumentExtraction.Msg;
using Pegasus.Infrastructure.Intake.DocumentExtraction.Cfb;

namespace Pegasus.IntegrationTests.DocumentExtraction;

public sealed class MsgReaderTests
{
    [Fact]
    public void ReadMailWithRecipientBodiesAndAttachmentProjectsEvidence()
    {
        var fixture = new MsgFixture();
        fixture.AddRootProperties(
            Variable(0x001A, 0x001F, "IPM.Note"),
            Variable(0x0037, 0x001F, "Synthetic subject"),
            Variable(0x1000, 0x001F, "Plain body"),
            Variable(0x1013, 0x0102, Encoding.UTF8.GetBytes("<p>HTML body</p>")),
            Fixed(0x3FDE, 0x0003, 65001));
        fixture.AddRecipient(5, 1, "Recipient", "recipient@example.invalid");
        fixture.AddByValueAttachment(9, 3, "evidence.bin", [1, 2, 3]);

        MsgDocument document = MsgReader.Read(fixture.Build());

        Assert.Equal(MsgItemKind.Mail, document.Projection.Kind);
        Assert.Equal("Synthetic subject", document.Projection.Fields["subject"]);
        Assert.Single(document.Recipients);
        Assert.Equal("To", document.Recipients[0].Role);
        Assert.Equal("Plain body", document.Bodies.CanonicalText);
        Assert.Equal("plain", document.Bodies.CanonicalSource);
        Assert.Single(document.Attachments);
        Assert.Equal(new byte[] { 1, 2, 3 }, document.Attachments[0].Content.ToArray());
    }

    [Fact]
    public void ReadFixedAndMultiValuedPropertiesPreservesDecodedAndRawValues()
    {
        var fixture = new MsgFixture();
        fixture.AddRootProperties(
            Variable(0x001A, 0x001F, "IPM.Note"),
            Fixed(0x4001, 0x0002, (short)-7),
            Fixed(0x4002, 0x000B, true),
            Fixed(0x4003, 0x0040, DateTimeOffset.UnixEpoch.ToFileTime()),
            MultiFixed(0x4004, 0x0003, 1, 2, 3),
            MultiVariable(0x4005, 0x001F, "one", "two"),
            FixedRaw(0x4006, 0x0777, [9, 8, 7, 6, 5, 4, 3, 2]));

        MsgDocument document = MsgReader.Read(fixture.Build());

        Assert.Equal((short)-7, Find(document, 0x4001).Values[0].Decoded);
        Assert.True((bool)Find(document, 0x4002).Values[0].Decoded!);
        Assert.Equal(3, Find(document, 0x4004).Values.Length);
        Assert.Equal("two", Find(document, 0x4005).Values[1].Decoded);
        Assert.Equal(MapiValueKind.Raw, Find(document, 0x4006).Values[0].Kind);
        Assert.Contains("MSG_PROPERTY_TYPE_UNSUPPORTED", document.Issues.Select(static issue => issue.Code));
    }

    [Fact]
    public void ReadSingleGuidUsesDedicatedValueStreamAndPreservesRawBytes()
    {
        var fixture = new MsgFixture();
        Guid expected = new("ffeeddcc-bbaa-9988-7766-554433221100");
        fixture.AddRootProperties(
            Variable(0x001A, 0x001F, "IPM.Note"),
            Variable(0x4007, 0x0048, expected.ToByteArray()));

        MsgDocument document = MsgReader.Read(fixture.Build());
        MapiValue value = Assert.Single(Find(document, 0x4007).Values);

        Assert.Equal(MapiValueKind.Identifier, value.Kind);
        Assert.Equal(expected, value.Decoded);
        Assert.Equal(expected.ToByteArray(), value.RawBytes.ToArray());
        Assert.DoesNotContain("MSG_PROPERTY_VALUE_INLINE_INVALID", document.Issues.Select(static issue => issue.Code));
    }

    [Fact]
    public void ReadMultipleGuidsUsesContiguousFixedWidthValueStream()
    {
        var fixture = new MsgFixture();
        Guid first = new("00112233-4455-6677-8899-aabbccddeeff");
        Guid second = new("ffeeddcc-bbaa-9988-7766-554433221100");
        fixture.AddRootProperties(
            Variable(0x001A, 0x001F, "IPM.Note"),
            MultiGuid(0x4008, first, second));

        MsgDocument document = MsgReader.Read(fixture.Build());
        ImmutableArray<MapiValue> values = Find(document, 0x4008).Values;

        Assert.Equal(2, values.Length);
        Assert.Equal(first, values[0].Decoded);
        Assert.Equal(second, values[1].Decoded);
    }

    [Fact]
    public void ReadSingleGuidWithoutValueStreamRetainsExplicitMissingEvidence()
    {
        var fixture = new MsgFixture();
        fixture.AddRootProperties(
            Variable(0x001A, 0x001F, "IPM.Note"),
            new PropertySpec(0x4009, 0x0048, new byte[8], []));

        MsgDocument document = MsgReader.Read(fixture.Build());
        MapiValue value = Assert.Single(Find(document, 0x4009).Values);

        Assert.Equal(MapiValueKind.Raw, value.Kind);
        Assert.Empty(value.RawBytes);
        Assert.Contains("MSG_PROPERTY_VALUE_STREAM_MISSING", document.Issues.Select(static issue => issue.Code));
        Assert.Equal(MsgReadOutcome.Partial, document.Outcome);
    }

    [Fact]
    public void ReadNamedPropertiesResolvesNumericAndStringNames()
    {
        var fixture = new MsgFixture();
        fixture.AddRootProperties(
            Variable(0x001A, 0x001F, "IPM.Note"),
            Fixed(0x8000, 0x0003, 42),
            Variable(0x8001, 0x001F, "named value"));
        fixture.AddNamedPropertyMap();

        MsgDocument document = MsgReader.Read(fixture.Build());

        NamedPropertyIdentity? numericCandidate = Find(document, 0x8000).NamedIdentity;
        Assert.NotNull(numericCandidate);
        NamedPropertyIdentity numeric = numericCandidate;
        Assert.Equal((uint)0x1234, numeric.NumericName);
        NamedPropertyIdentity? textualCandidate = Find(document, 0x8001).NamedIdentity;
        Assert.NotNull(textualCandidate);
        NamedPropertyIdentity textual = textualCandidate;
        Assert.Equal("SyntheticName", textual.StringName);
    }

    [Fact]
    public void ReadExternalAndOleAttachmentsRecordsPassiveIssuesWithoutRetrieval()
    {
        var fixture = new MsgFixture();
        fixture.AddRootProperties(Variable(0x001A, 0x001F, "IPM.Note"));
        fixture.AddReferenceAttachment(2, 7, "https://example.invalid/passive");
        fixture.AddOleAttachment(3, 6);

        MsgDocument document = MsgReader.Read(fixture.Build());

        Assert.Contains("MSG_ATTACHMENT_REFERENCE_PASSIVE", document.Issues.Select(static issue => issue.Code));
        Assert.Contains("MSG_ATTACHMENT_OLE_PASSIVE", document.Issues.Select(static issue => issue.Code));
        Assert.Equal("https://example.invalid/passive", document.Attachments[0].PassiveReference);
        Assert.Empty(document.Attachments[0].Content);
    }

    [Fact]
    public void ReadEmbeddedMessageRecursesWithinLimit()
    {
        var fixture = new MsgFixture();
        fixture.AddRootProperties(Variable(0x001A, 0x001F, "IPM.Note"));
        fixture.AddEmbeddedMessage(4, "IPM.Contact");

        MsgDocument document = MsgReader.Read(fixture.Build());

        MsgDocument? embeddedCandidate = document.Attachments[0].EmbeddedMessage;
        Assert.NotNull(embeddedCandidate);
        MsgDocument embedded = embeddedCandidate;
        Assert.Equal(MsgItemKind.Contact, embedded.Projection.Kind);

        var limits = MsgReadLimits.Default with { MaximumNestingDepth = 0 };
        MsgDocument limited = MsgReader.Read(fixture.Build(), limits);
        Assert.Equal(MsgReadOutcome.ResourceLimitExceeded, limited.Outcome);
    }

    [Theory]
    [InlineData("IPM.Appointment", (int)MsgItemKind.Calendar)]
    [InlineData("IPM.Schedule.Meeting.Request", (int)MsgItemKind.Meeting)]
    [InlineData("IPM.Contact", (int)MsgItemKind.Contact)]
    [InlineData("IPM.DistList", (int)MsgItemKind.DistributionList)]
    [InlineData("IPM.Task", (int)MsgItemKind.Task)]
    [InlineData("IPM.StickyNote", (int)MsgItemKind.Note)]
    [InlineData("IPM.Activity", (int)MsgItemKind.Generic)]
    [InlineData("REPORT.IPM.Note.NDR", (int)MsgItemKind.Report)]
    public void ReadMessageClassProjectsDeclaredSemanticKind(string messageClass, int expected)
    {
        var fixture = new MsgFixture();
        fixture.AddRootProperties(Variable(0x001A, 0x001F, messageClass));

        MsgDocument document = MsgReader.Read(fixture.Build());

        Assert.Equal((MsgItemKind)expected, document.Projection.Kind);
    }

    [Fact]
    public void ReadProtectedClassReturnsEncryptedWithoutDecryption()
    {
        var fixture = new MsgFixture();
        fixture.AddRootProperties(Variable(0x001A, 0x001F, "IPM.Note.SMIME"));

        MsgDocument document = MsgReader.Read(fixture.Build());

        Assert.Equal(MsgReadOutcome.Encrypted, document.Outcome);
        Assert.Contains("MSG_PROTECTED_CONTENT", document.Issues.Select(static issue => issue.Code));
    }

    [Fact]
    public void ReadCancelledTokenReturnsCancelled()
    {
        var fixture = new MsgFixture();
        fixture.AddRootProperties(Variable(0x001A, 0x001F, "IPM.Note"));
        using var source = new CancellationTokenSource();
        source.Cancel();

        MsgDocument document = MsgReader.Read(fixture.Build(), cancellationToken: source.Token);

        Assert.Equal(MsgReadOutcome.Cancelled, document.Outcome);
    }

    [Fact]
    public void ReadString8CodePageDeclaredAfterValueDecodesOnceWithoutFallback()
    {
        var fixture = new MsgFixture();
        fixture.AddRootProperties(
            Variable(0x001A, 0x001F, "IPM.Note"),
            VariableBytes(0x0037, 0x001E, [0x80, 0]),
            Fixed(0x3FFD, 0x0003, 1252));
        var limits = MsgReadLimits.Default with { MaximumProperties = 3 };

        MsgDocument document = MsgReader.Read(fixture.Build(), limits);

        Assert.Equal("€", document.Projection.Fields["subject"]);
        Assert.Equal(MsgReadOutcome.Complete, document.Outcome);
        Assert.DoesNotContain("MSG_CODEPAGE_FALLBACK", document.Issues.Select(static issue => issue.Code));
    }

    [Fact]
    public void ReadConflictingCodePagesUsesFirstAndReportsConflict()
    {
        var fixture = new MsgFixture();
        fixture.AddRootProperties(
            Fixed(0x3FFD, 0x0003, 1252),
            VariableBytes(0x001A, 0x001E, Encoding.ASCII.GetBytes("IPM.Note\0")),
            Fixed(0x3FFD, 0x0003, 65001));

        MsgDocument document = MsgReader.Read(fixture.Build());

        Assert.Equal("IPM.Note", document.Projection.MessageClass);
        Assert.Contains("MSG_CODEPAGE_CONFLICT", document.Issues.Select(static issue => issue.Code));
        Assert.Single(document.Issues.Where(static issue => issue.Code == "MSG_CODEPAGE_CONFLICT"));
    }

    [Fact]
    public void ReadVariableMultiValueZeroOneAndManyUsesFourByteLengthRecords()
    {
        var fixture = new MsgFixture();
        fixture.AddRootProperties(
            Variable(0x001A, 0x001F, "IPM.Note"),
            MultiVariableBytes(0x4100, 0x0102, Array.Empty<byte>()),
            MultiVariableBytes(0x4101, 0x0102, [1]),
            MultiVariableBytes(0x4102, 0x0102, [2], [3, 4]));

        MsgDocument document = MsgReader.Read(fixture.Build());

        Assert.Single(Find(document, 0x4100).Values);
        Assert.Empty(Find(document, 0x4100).Values[0].RawBytes);
        Assert.Single(Find(document, 0x4101).Values);
        Assert.Equal(2, Find(document, 0x4102).Values.Length);
    }

    [Fact]
    public void ReadMalformedVariableMultiValueRetainsActualAndTrailingRawBytes()
    {
        var fixture = new MsgFixture();
        fixture.AddRootProperties(
            Variable(0x001A, 0x001F, "IPM.Note"),
            MultiVariableMalformed(0x4103, 7, [9, 8], [0xAA]));

        MsgDocument document = MsgReader.Read(fixture.Build());
        MapiProperty property = Find(document, 0x4103);

        Assert.Equal(2, property.Values.Length);
        Assert.Equal(MapiValueKind.Raw, property.Values[0].Kind);
        Assert.Equal(new byte[] { 9, 8 }, property.Values[0].RawBytes.ToArray());
        Assert.Equal(new byte[] { 0xAA }, property.Values[1].RawBytes.ToArray());
        Assert.Contains("MSG_MULTIVALUE_ITEM_INVALID", document.Issues.Select(static issue => issue.Code));
        Assert.Contains("MSG_MULTIVALUE_TABLE_INVALID", document.Issues.Select(static issue => issue.Code));
    }

    [Fact]
    public void ReadPropertyLimitPreservesEarlierEvidenceAndReturnsResourceOutcome()
    {
        var fixture = new MsgFixture();
        fixture.AddRootProperties(
            Variable(0x001A, 0x001F, "IPM.Note"),
            Variable(0x0037, 0x001F, "retained"),
            Variable(0x1000, 0x001F, "omitted"));
        var limits = MsgReadLimits.Default with { MaximumProperties = 2 };

        MsgDocument document = MsgReader.Read(fixture.Build(), limits);

        Assert.Equal(MsgReadOutcome.ResourceLimitExceeded, document.Outcome);
        Assert.Equal("retained", document.Projection.Fields["subject"]);
        Assert.Equal(2, document.Properties.Length);
    }

    [Fact]
    public void ReadPartiallyProjectedClassAndGenericClassNeverReturnComplete()
    {
        var report = new MsgFixture();
        report.AddRootProperties(Variable(0x001A, 0x001F, "REPORT.IPM.Note.NDR"));
        var generic = new MsgFixture();
        generic.AddRootProperties(Variable(0x001A, 0x001F, "IPM.Custom"));

        MsgDocument reportDocument = MsgReader.Read(report.Build());
        MsgDocument genericDocument = MsgReader.Read(generic.Build());

        Assert.Equal(MsgReadOutcome.Partial, reportDocument.Outcome);
        Assert.Contains("MSG_ITEM_CLASS_PARTIAL", reportDocument.Issues.Select(static issue => issue.Code));
        Assert.Equal(MsgReadOutcome.Partial, genericDocument.Outcome);
        Assert.Contains("MSG_CLASS_GENERIC", genericDocument.Issues.Select(static issue => issue.Code));
    }

    [Fact]
    public void ReadProtectedAndResourceLimitedResourceOutcomeTakesPrecedence()
    {
        var fixture = new MsgFixture();
        fixture.AddRootProperties(
            Variable(0x001A, 0x001F, "IPM.Note.SMIME"),
            Variable(0x0037, 0x001F, "not reached"));

        MsgDocument document = MsgReader.Read(
            fixture.Build(),
            MsgReadLimits.Default with { MaximumProperties = 1 });

        Assert.Equal(MsgReadOutcome.ResourceLimitExceeded, document.Outcome);
        Assert.Contains("MSG_PROTECTED_CONTENT", document.Issues.Select(static issue => issue.Code));
    }

    [Fact]
    public void ReadOleCustomChildStorageIsExposedOnlyAsPassiveEvidence()
    {
        var fixture = new MsgFixture();
        fixture.AddOleAttachmentWithChildStorage(3);
        fixture.AddRootProperties(Variable(0x001A, 0x001F, "IPM.Note"));

        MsgDocument document = MsgReader.Read(fixture.Build());

        MsgAttachment attachment = Assert.Single(document.Attachments);
        MsgPassiveStorage passive = Assert.Single(attachment.PassiveStorages);
        Assert.Equal("CustomOleStorage", passive.StorageName);
        Assert.Contains("MSG_ATTACHMENT_CHILD_STORAGE_PASSIVE", document.Issues.Select(static issue => issue.Code));
    }

    [Fact]
    public void ReadEmbeddedGenericMessageRetainsNestedLocalIssues()
    {
        var fixture = new MsgFixture();
        fixture.AddRootProperties(Variable(0x001A, 0x001F, "IPM.Note"));
        fixture.AddEmbeddedMessage(4, "IPM.Custom");

        MsgDocument document = MsgReader.Read(fixture.Build());
        MsgDocument embedded = document.Attachments[0].EmbeddedMessage!;

        Assert.Equal(MsgReadOutcome.Partial, embedded.Outcome);
        Assert.Contains("MSG_CLASS_GENERIC", embedded.Issues.Select(static issue => issue.Code));
        Assert.Contains("MSG_CLASS_GENERIC", document.Issues.Select(static issue => issue.Code));
    }

    [Fact]
    public void ReadTransportHeadersAndUnicodeHtmlBodyAreProjectedWithoutBinaryMisdecode()
    {
        var fixture = new MsgFixture();
        fixture.AddRootProperties(
            Variable(0x001A, 0x001F, "IPM.Note"),
            Variable(0x007D, 0x001F, "From: sender@example.invalid\r\n"),
            Variable(0x1013, 0x001F, "<p>Unicode Ω body</p>"));

        MsgDocument document = MsgReader.Read(fixture.Build());

        Assert.Equal("From: sender@example.invalid\r\n", document.Projection.Fields["transportHeaders"]);
        Assert.Equal("<p>Unicode Ω body</p>", document.Bodies.HtmlText);
        Assert.Equal("Unicode Ω body", document.Bodies.CanonicalText);
        Assert.Empty(document.Bodies.HtmlBytes);
    }

    [Fact]
    public void ReadDivergentPlainAndHtmlBodiesRetainsBothAndReportsIssue()
    {
        var fixture = new MsgFixture();
        fixture.AddRootProperties(
            Variable(0x001A, 0x001F, "IPM.Note"),
            Variable(0x1000, 0x001F, "plain"),
            Variable(0x1013, 0x001F, "<p>different</p>"));

        MsgDocument document = MsgReader.Read(fixture.Build());

        Assert.Equal("plain", document.Bodies.PlainText);
        Assert.Equal("<p>different</p>", document.Bodies.HtmlText);
        Assert.Contains("MSG_BODY_VARIANTS_DIVERGE", document.Issues.Select(static issue => issue.Code));
    }

    [Fact]
    public void ReadUnsignedSparseAndDuplicateRecipientSuffixesHaveStableOrderingAndIssue()
    {
        var fixture = new MsgFixture();
        fixture.AddRootProperties(Variable(0x001A, 0x001F, "IPM.Note"));
        fixture.AddRecipientWithSuffix(200, 0x80000000, "high");
        fixture.AddRecipientWithSuffix(300, 2, "low-a");
        fixture.AddRecipientWithSuffix(400, 2, "low-b");

        MsgDocument document = MsgReader.Read(fixture.Build());

        Assert.Equal((uint)2, document.Recipients[0].SourceOrder);
        Assert.Equal((uint)2, document.Recipients[1].SourceOrder);
        Assert.Equal(0x80000000u, document.Recipients[2].SourceOrder);
        Assert.Contains("MSG_RECIPIENT_STORAGE_DUPLICATE", document.Issues.Select(static issue => issue.Code));
    }

    [Fact]
    public void ReadReservedPropertyFlagsAreReportedAndCannotBeComplete()
    {
        var fixture = new MsgFixture();
        fixture.AddRootProperties(
            Variable(0x001A, 0x001F, "IPM.Note"),
            new PropertySpec(0x4000, 0x0003, new byte[8], [], 0x80000000));

        MsgDocument document = MsgReader.Read(fixture.Build());

        Assert.Equal(MsgReadOutcome.Partial, document.Outcome);
        Assert.Contains("MSG_PROPERTY_FLAGS_RESERVED", document.Issues.Select(static issue => issue.Code));
    }

    [Fact]
    public void ReadZeroLengthMultiValueItemConsumesCumulativeValueBudget()
    {
        var fixture = new MsgFixture();
        fixture.AddRootProperties(
            Variable(0x001A, 0x001F, "IPM.Note"),
            MultiVariableBytes(0x4100, 0x0102, Array.Empty<byte>()));

        MsgDocument document = MsgReader.Read(
            fixture.Build(),
            MsgReadLimits.Default with { MaximumValues = 1 });

        Assert.Equal(MsgReadOutcome.ResourceLimitExceeded, document.Outcome);
        Assert.Empty(Find(document, 0x4100).Values);
    }

    [Fact]
    public void ReadNestedAttachmentBudgetIsCumulativeAndPreservesOuterAttachment()
    {
        var fixture = new MsgFixture();
        fixture.AddRootProperties(Variable(0x001A, 0x001F, "IPM.Note"));
        fixture.AddEmbeddedMessageWithAttachment(40, 140, 240);

        MsgDocument document = MsgReader.Read(
            fixture.Build(),
            MsgReadLimits.Default with { MaximumAttachments = 1 });

        Assert.Equal(MsgReadOutcome.ResourceLimitExceeded, document.Outcome);
        MsgAttachment outer = Assert.Single(document.Attachments);
        Assert.NotNull(outer.EmbeddedMessage);
        Assert.Empty(outer.EmbeddedMessage.Attachments);
    }

    [Fact]
    public void ReadDuplicateNamedPropertyMappingReportsAndKeepsFirstIdentity()
    {
        var fixture = new MsgFixture();
        fixture.AddRootProperties(
            Variable(0x001A, 0x001F, "IPM.Note"),
            Fixed(0x8000, 0x0003, 42));
        fixture.AddNamedPropertyMap(includeDuplicate: true);

        MsgDocument document = MsgReader.Read(fixture.Build());

        Assert.Equal((uint)0x1234, Find(document, 0x8000).NamedIdentity!.NumericName);
        Assert.Contains("MSG_NAMED_PROPERTY_DUPLICATE", document.Issues.Select(static issue => issue.Code));
    }

    [Fact]
    public void ReadWrongRootPropertyHeaderLengthIsRejectedByExactContext()
    {
        var fixture = new MsgFixture();
        fixture.AddRootPropertiesWithHeader(24, Variable(0x001A, 0x001F, "IPM.Note"));

        MsgDocument document = MsgReader.Read(fixture.Build());

        Assert.Empty(document.Properties);
        Assert.Contains("MSG_PROPERTY_STREAM_LENGTH_INVALID", document.Issues.Select(static issue => issue.Code));
        Assert.NotEqual(MsgReadOutcome.Complete, document.Outcome);
    }

    [Fact]
    public void ReadNonZeroReservedRootHeaderIsReported()
    {
        var fixture = new MsgFixture();
        fixture.AddRootProperties(Variable(0x001A, 0x001F, "IPM.Note"));
        fixture.SetRootReservedHeaderByte();

        MsgDocument document = MsgReader.Read(fixture.Build());

        Assert.Contains("MSG_PROPERTY_HEADER_RESERVED", document.Issues.Select(static issue => issue.Code));
        Assert.Equal(MsgReadOutcome.Partial, document.Outcome);
    }

    [Fact]
    public void ReadUnsupportedAttachmentMethodRetainsAttachmentAndReportsPartial()
    {
        var fixture = new MsgFixture();
        fixture.AddRootProperties(Variable(0x001A, 0x001F, "IPM.Note"));
        fixture.AddOleAttachment(3, 99);

        MsgDocument document = MsgReader.Read(fixture.Build());

        Assert.Equal(99, Assert.Single(document.Attachments).Method);
        Assert.Contains("MSG_ATTACHMENT_METHOD_UNSUPPORTED", document.Issues.Select(static issue => issue.Code));
        Assert.Equal(MsgReadOutcome.Partial, document.Outcome);
    }

    private static MapiProperty Find(MsgDocument document, ushort id) =>
        Assert.Single(document.Properties.Where(property => property.PropertyId == id));

    private static PropertySpec Variable(ushort id, ushort type, string value) =>
        Variable(id, type, Encoding.Unicode.GetBytes(value + '\0'));
    private static PropertySpec Variable(ushort id, ushort type, byte[] value) => new(id, type, new byte[8], [value]);
    private static PropertySpec VariableBytes(ushort id, ushort type, byte[] value) => new(id, type, new byte[8], [value]);
    private static PropertySpec Fixed(ushort id, ushort type, short value) { byte[] bytes = new byte[8]; BinaryPrimitives.WriteInt16LittleEndian(bytes, value); return new(id, type, bytes, []); }
    private static PropertySpec Fixed(ushort id, ushort type, int value) { byte[] bytes = new byte[8]; BinaryPrimitives.WriteInt32LittleEndian(bytes, value); return new(id, type, bytes, []); }
    private static PropertySpec Fixed(ushort id, ushort type, long value) { byte[] bytes = new byte[8]; BinaryPrimitives.WriteInt64LittleEndian(bytes, value); return new(id, type, bytes, []); }
    private static PropertySpec Fixed(ushort id, ushort type, bool value) => Fixed(id, type, (short)(value ? 1 : 0));
    private static PropertySpec FixedRaw(ushort id, ushort type, byte[] value) => new(id, type, value, []);
    private static PropertySpec MultiFixed(ushort id, ushort baseType, params int[] values)
    {
        byte[] stream = new byte[values.Length * 4];
        for (int index = 0; index < values.Length; index++) BinaryPrimitives.WriteInt32LittleEndian(stream.AsSpan(index * 4), values[index]);
        return new(id, (ushort)(baseType | 0x1000), new byte[8], [stream]);
    }
    private static PropertySpec MultiGuid(ushort id, params Guid[] values)
    {
        byte[] stream = new byte[checked(values.Length * 16)];
        for (int index = 0; index < values.Length; index++)
            values[index].TryWriteBytes(stream.AsSpan(index * 16, 16));
        return new(id, 0x1048, new byte[8], [stream]);
    }
    private static PropertySpec MultiVariable(ushort id, ushort baseType, params string[] values)
    {
        byte[][] encoded = values.Select(static value => Encoding.Unicode.GetBytes(value + '\0')).ToArray();
        byte[] table = new byte[encoded.Length * 4];
        for (int index = 0; index < encoded.Length; index++) BinaryPrimitives.WriteInt32LittleEndian(table.AsSpan(index * 4), encoded[index].Length);
        return new(id, (ushort)(baseType | 0x1000), new byte[8], [table, .. encoded]);
    }

    private static PropertySpec MultiVariableBytes(ushort id, ushort baseType, params byte[][] values)
    {
        byte[] table = new byte[values.Length * 4];
        for (int index = 0; index < values.Length; index++)
            BinaryPrimitives.WriteUInt32LittleEndian(table.AsSpan(index * 4), (uint)values[index].Length);
        return new(id, (ushort)(baseType | 0x1000), new byte[8], [table, .. values]);
    }

    private static PropertySpec MultiVariableMalformed(ushort id, uint declaredLength, byte[] actual, byte[] trailing)
    {
        byte[] table = new byte[4 + trailing.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(table, declaredLength);
        trailing.CopyTo(table, 4);
        return new(id, 0x1102, new byte[8], [table, actual]);
    }

    private sealed record PropertySpec(ushort Id, ushort Type, byte[] Inline, byte[][] Streams, uint Flags = 0);

    private sealed class MsgFixture
    {
        private readonly List<CompoundFileDirectoryEntry> _entries = [Directory(0, "Root Entry", CompoundFileObjectType.RootStorage, null, [])];

        public void AddRootProperties(params PropertySpec[] properties) => AddProperties(0, 32, properties);
        public void AddRootPropertiesWithHeader(int header, params PropertySpec[] properties) => AddProperties(0, header, properties);

        public void SetRootReservedHeaderByte()
        {
            int index = _entries.FindIndex(static entry => entry.ParentStreamId == 0 && entry.Name == "__properties_version1.0");
            CompoundFileDirectoryEntry entry = _entries[index];
            byte[] content = entry.Content.ToArray();
            content[0] = 1;
            _entries[index] = entry with { Content = ImmutableArray.Create(content) };
        }

        public void AddRecipient(uint id, int role, string name, string address)
        {
            AddStorage(id, $"__recip_version1.0_#{id:x8}", 0);
            AddProperties(id, 8,
                Fixed(0x0C15, 0x0003, role),
                Variable(0x3001, 0x001F, name),
                Variable(0x39FE, 0x001F, address));
        }

        public void AddRecipientWithSuffix(uint id, uint suffix, string name)
        {
            AddStorage(id, $"__recip_version1.0_#{suffix:x8}", 0);
            AddProperties(id, 8,
                Fixed(0x0C15, 0x0003, 1),
                Variable(0x3001, 0x001F, name));
        }

        public void AddByValueAttachment(uint id, int renderingPosition, string name, byte[] content)
        {
            AddStorage(id, $"__attach_version1.0_#{id:x8}", 0);
            AddProperties(id, 8,
                Fixed(0x3705, 0x0003, 1), Fixed(0x370B, 0x0003, renderingPosition),
                Variable(0x3707, 0x001F, name), Variable(0x3701, 0x0102, content));
        }

        public void AddReferenceAttachment(uint id, int method, string reference)
        {
            AddStorage(id, $"__attach_version1.0_#{id:x8}", 0);
            AddProperties(id, 8, Fixed(0x3705, 0x0003, method), Variable(0x370D, 0x001F, reference));
        }

        public void AddOleAttachment(uint id, int method)
        {
            AddStorage(id, $"__attach_version1.0_#{id:x8}", 0);
            AddProperties(id, 8, Fixed(0x3705, 0x0003, method));
        }

        public void AddOleAttachmentWithChildStorage(uint id)
        {
            AddOleAttachment(id, 6);
            AddStorage(id + 100, "CustomOleStorage", id);
            AddStream(id + 101, "Contents", id + 100, [1, 2, 3]);
        }

        public void AddEmbeddedMessage(uint id, string messageClass)
        {
            AddStorage(id, $"__attach_version1.0_#{id:x8}", 0);
            AddProperties(id, 8, Fixed(0x3705, 0x0003, 5));
            uint embeddedId = id + 100;
            AddStorage(embeddedId, "__substg1.0_3701000D", id);
            AddProperties(embeddedId, 24, Variable(0x001A, 0x001F, messageClass));
        }

        public void AddEmbeddedMessageWithAttachment(uint attachmentId, uint embeddedId, uint nestedAttachmentId)
        {
            AddStorage(attachmentId, $"__attach_version1.0_#{attachmentId:x8}", 0);
            AddProperties(attachmentId, 8, Fixed(0x3705, 0x0003, 5));
            AddStorage(embeddedId, "__substg1.0_3701000D", attachmentId);
            AddProperties(embeddedId, 24, Variable(0x001A, 0x001F, "IPM.Note"));
            AddStorage(nestedAttachmentId, $"__attach_version1.0_#{nestedAttachmentId:x8}", embeddedId);
            AddProperties(nestedAttachmentId, 8,
                Fixed(0x3705, 0x0003, 1),
                Variable(0x3701, 0x0102, [1]));
        }

        public void AddNamedPropertyMap(bool includeDuplicate = false)
        {
            const uint nameStorage = 90;
            AddStorage(nameStorage, "__nameid_version1.0", 0);
            byte[] strings = Encoding.Unicode.GetBytes("SyntheticName");
            byte[] stringStream = new byte[strings.Length + 4];
            BinaryPrimitives.WriteInt32LittleEndian(stringStream, strings.Length);
            strings.CopyTo(stringStream, 4);
            byte[] entries = new byte[includeDuplicate ? 24 : 16];
            BinaryPrimitives.WriteUInt32LittleEndian(entries, 0x1234);
            BinaryPrimitives.WriteUInt16LittleEndian(entries.AsSpan(4), 2);
            BinaryPrimitives.WriteUInt16LittleEndian(entries.AsSpan(6), 0);
            BinaryPrimitives.WriteUInt32LittleEndian(entries.AsSpan(8), 0);
            BinaryPrimitives.WriteUInt16LittleEndian(entries.AsSpan(12), 3);
            BinaryPrimitives.WriteUInt16LittleEndian(entries.AsSpan(14), 1);
            if (includeDuplicate)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(entries.AsSpan(16), 0x9999);
                BinaryPrimitives.WriteUInt16LittleEndian(entries.AsSpan(20), 2);
                BinaryPrimitives.WriteUInt16LittleEndian(entries.AsSpan(22), 0);
            }
            AddStream(91, "__substg1.0_00020102", nameStorage, []);
            AddStream(92, "__substg1.0_00030102", nameStorage, entries);
            AddStream(93, "__substg1.0_00040102", nameStorage, stringStream);
        }

        public CompoundFile Build() => new(
            new(0x3E, 3, 512, 64, 0, 1, 0, 0, 4096, 0, 0, 0, 0, []), [], [], [], [.. _entries]);

        private void AddProperties(uint parent, int header, params PropertySpec[] properties)
        {
            byte[] propertyBytes = new byte[header + properties.Length * 16];
            for (int index = 0; index < properties.Length; index++)
            {
                PropertySpec property = properties[index];
                int offset = header + index * 16;
                BinaryPrimitives.WriteUInt32LittleEndian(propertyBytes.AsSpan(offset), ((uint)property.Id << 16) | property.Type);
                BinaryPrimitives.WriteUInt32LittleEndian(propertyBytes.AsSpan(offset + 4), property.Flags);
                property.Inline.CopyTo(propertyBytes, offset + 8);
                AddPropertyStreams(parent, property);
            }
            AddStream(NextId(), "__properties_version1.0", parent, propertyBytes);
        }

        private void AddPropertyStreams(uint parent, PropertySpec property)
        {
            if (property.Streams.Length == 0) return;
            string name = $"__substg1.0_{property.Id:x4}{property.Type:x4}";
            AddStream(NextId(), name, parent, property.Streams[0]);
            if ((property.Type & 0x1000) != 0 && property.Streams.Length > 1)
            {
                for (int index = 1; index < property.Streams.Length; index++) AddStream(NextId(), $"{name}-{index - 1:x8}", parent, property.Streams[index]);
            }
        }

        private void AddStorage(uint id, string name, uint parent) => _entries.Add(Directory(id, name, CompoundFileObjectType.Storage, parent, []));
        private void AddStream(uint id, string name, uint parent, byte[] content) => _entries.Add(Directory(id, name, CompoundFileObjectType.Stream, parent, content));
        private uint NextId() => _entries.Count == 0 ? 1 : _entries.Max(static entry => entry.StreamId) + 1;
        private static CompoundFileDirectoryEntry Directory(uint id, string name, CompoundFileObjectType type, uint? parent, byte[] content) =>
            new(id, name, checked((ushort)((name.Length + 1) * 2)), type, CompoundFileNodeColor.Black,
                0xFFFFFFFF, 0xFFFFFFFF, 0xFFFFFFFF, Guid.Empty, 0, 0, 0, 0xFFFFFFFE,
                (ulong)content.Length, parent, ImmutableArray.Create(content));
    }
}
