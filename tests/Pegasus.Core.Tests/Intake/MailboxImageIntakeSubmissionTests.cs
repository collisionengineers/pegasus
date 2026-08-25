using System.Security.Cryptography;
using Pegasus.Core.Intake;
using Pegasus.Core.Intake.Unidentified;

namespace Pegasus.Core.Tests.Intake;

public sealed class MailboxImageIntakeSubmissionTests
{
    [Fact]
    public async Task SubmitsOnlyThreeDirectImagesAsOneMailboxGroup()
    {
        var parentId = Guid.NewGuid();
        var artifacts = new MemoryArtifactStore();
        var assets = new[]
        {
            artifacts.Asset("source", "message.eml", "message/rfc822", IntakeAssetKind.Source, [0]),
            artifacts.Asset("inline", "logo.png", "image/png", IntakeAssetKind.InlineImage, [1]),
            artifacts.Asset("photo-1", "one.jpg", "image/jpeg", IntakeAssetKind.Attachment, [2]),
            artifacts.Asset("photo-2", "two.jpg", "image/jpeg", IntakeAssetKind.Attachment, [3]),
            artifacts.Asset("photo-3", "three.jpg", "image/jpeg", IntakeAssetKind.Attachment, [4]),
            artifacts.Asset("document", "report.pdf", "application/pdf", IntakeAssetKind.Attachment, [5])
        };
        var submission = new RecordingGroupedSubmission();
        var service = new SubmitMailboxImageIntake(
            artifacts,
            submission,
            new FakeGroupStore(),
            new RecordingUnidentified());

        var handled = await service.ExecuteAsync(Receipt(parentId, assets), false);

        Assert.True(handled);
        var request = Assert.Single(submission.Requests);
        Assert.Equal(IntakeSourceChannel.Mailbox, request.Channel);
        Assert.Equal(parentId, request.ParentReceiptId);
        Assert.Equal(["one.jpg", "two.jpg", "three.jpg"], request.Files.Select(file => file.Source.FileName));
        Assert.Equal([2, 3, 4], request.Files.Select(file => file.Source.Content.Span[0]));
    }

    [Fact]
    public void DoesNotSelectInlineImagesOrNonUnidentifiedMail()
    {
        var artifacts = new MemoryArtifactStore();
        var inlineOnly = Receipt(
            Guid.NewGuid(),
            [artifacts.Asset("inline", "logo.png", "image/png", IntakeAssetKind.InlineImage, [1])]);
        var alreadyRouted = Receipt(
            Guid.NewGuid(),
            [artifacts.Asset("photo", "one.jpg", "image/jpeg", IntakeAssetKind.Attachment, [2])]) with
        {
            Decision = IntakeDecision.CaseCreated
        };
        var instructionBearing = Receipt(
            Guid.NewGuid(),
            [artifacts.Asset("instruction-photo", "instruction.jpg", "image/jpeg", IntakeAssetKind.Attachment, [3])]) with
        {
            InstructionDraft = new(null, null, null, null, null, null, null, null, null, null, null)
        };
        var linkedToCase = Receipt(
            Guid.NewGuid(),
            [artifacts.Asset("case-photo", "case.jpg", "image/jpeg", IntakeAssetKind.Attachment, [4])]) with
        {
            AcceptedCaseId = Guid.NewGuid()
        };

        Assert.Empty(SubmitMailboxImageIntake.SelectAttachments(inlineOnly));
        Assert.Empty(SubmitMailboxImageIntake.SelectAttachments(alreadyRouted));
        Assert.Empty(SubmitMailboxImageIntake.SelectAttachments(instructionBearing));
        Assert.Empty(SubmitMailboxImageIntake.SelectAttachments(linkedToCase));
    }

    [Fact]
    public async Task ReplayFindsTheExistingParentGroupWithoutSubmittingAgain()
    {
        var parentId = Guid.NewGuid();
        var artifacts = new MemoryArtifactStore();
        var receipt = Receipt(
            parentId,
            [artifacts.Asset("photo", "one.jpg", "image/jpeg", IntakeAssetKind.Attachment, [2])]);
        var groupStore = new FakeGroupStore
        {
            ParentGroup = new(
                Guid.NewGuid(),
                IntakeSourceChannel.Mailbox,
                $"mailbox-images:{parentId:N}",
                1,
                "system-worker:mailbox-image-intake",
                receipt.ReceivedAtUtc,
                [],
                parentId)
        };
        var submission = new RecordingGroupedSubmission();
        var service = new SubmitMailboxImageIntake(
            artifacts,
            submission,
            groupStore,
            new RecordingUnidentified());

        Assert.True(await service.HasSubmissionAsync(receipt));
        Assert.Empty(submission.Requests);
    }

    [Fact]
    public async Task FinalSubmissionFailureRegistersTechnicalUnidentified()
    {
        var parentId = Guid.NewGuid();
        var artifacts = new MemoryArtifactStore();
        var unidentified = new RecordingUnidentified();
        var service = new SubmitMailboxImageIntake(
            artifacts,
            new RecordingGroupedSubmission(new IOException("queue unavailable")),
            new FakeGroupStore(),
            unidentified);
        var receipt = Receipt(
            parentId,
            [artifacts.Asset("photo", "one.jpg", "image/jpeg", IntakeAssetKind.Attachment, [2])]);

        var handled = await service.ExecuteAsync(receipt, true);

        Assert.True(handled);
        var request = Assert.Single(unidentified.Requests);
        Assert.Equal(UnidentifiedReasonCode.TechnicalProcessingFailure, request.ReasonCode);
        Assert.Equal(parentId, request.Origin.Id);
    }

    [Fact]
    public async Task FinalFailureAfterGroupCreationRegistersOneGroupTechnicalOutcome()
    {
        var parentId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var artifacts = new MemoryArtifactStore();
        var receipt = Receipt(
            parentId,
            [artifacts.Asset("photo", "one.jpg", "image/jpeg", IntakeAssetKind.Attachment, [2])]);
        var groupStore = new FakeGroupStore
        {
            ParentGroup = new(
                groupId,
                IntakeSourceChannel.Mailbox,
                $"mailbox-images:{parentId:N}",
                2,
                "system-worker:mailbox-image-intake",
                receipt.ReceivedAtUtc,
                [],
                parentId)
        };
        var unidentified = new RecordingUnidentified();
        var service = new SubmitMailboxImageIntake(
            artifacts,
            new RecordingGroupedSubmission(new IOException("second child unavailable")),
            groupStore,
            unidentified);

        Assert.True(await service.ExecuteAsync(receipt, true));

        var request = Assert.Single(unidentified.Requests);
        Assert.Equal(UnidentifiedOrigin.SubmissionGroup(groupId), request.Origin);
        Assert.Equal(UnidentifiedReasonCode.TechnicalProcessingFailure, request.ReasonCode);
        Assert.Equal($"mailbox-image-submission-failure:group:{groupId:N}", request.OperationKey);
    }

    [Fact]
    public async Task TransientSubmissionFailureRetriesBeforeBecomingUnidentified()
    {
        var artifacts = new MemoryArtifactStore();
        var unidentified = new RecordingUnidentified();
        var receipt = Receipt(
            Guid.NewGuid(),
            [artifacts.Asset("photo", "one.jpg", "image/jpeg", IntakeAssetKind.Attachment, [2])]);
        var service = new SubmitMailboxImageIntake(
            artifacts,
            new RecordingGroupedSubmission(new IOException("queue unavailable")),
            new FakeGroupStore(),
            unidentified);

        await Assert.ThrowsAsync<IOException>(() => service.ExecuteAsync(receipt, false));
        Assert.Empty(unidentified.Requests);
    }

    private static IntakeReceipt Receipt(Guid id, IReadOnlyList<IntakeAssetRecord> assets) =>
        new(
            id,
            "message.eml",
            "message/rfc822",
            1,
            "HASH",
            new(IntakeSourceChannel.Mailbox, $"mail-{id:N}"),
            new DateTimeOffset(2026, 8, 25, 14, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 25, 14, 0, 1, TimeSpan.Zero),
            IntakeDecision.NeedsSorting,
            "No usable identification.",
            [],
            [],
            null,
            [],
            null,
            null,
            false,
            "mail-reader",
            "1",
            null,
            null,
            Assets: assets);

    private sealed class MemoryArtifactStore : IIntakeArtifactStore
    {
        private readonly Dictionary<string, ReadOnlyMemory<byte>> content = [];

        public IntakeAssetRecord Asset(
            string label,
            string fileName,
            string mediaType,
            IntakeAssetKind kind,
            byte[] bytes)
        {
            var hash = Convert.ToHexString(SHA256.HashData(bytes));
            var storageKey = $"asset/{label}";
            content[storageKey] = bytes;
            return new(
                Guid.NewGuid(),
                label,
                fileName,
                mediaType,
                kind,
                kind == IntakeAssetKind.Source
                    ? IntakeAssetDisposition.Source
                    : kind == IntakeAssetKind.InlineImage
                        ? IntakeAssetDisposition.Inline
                        : IntakeAssetDisposition.Attachment,
                bytes.Length,
                hash,
                storageKey,
                null,
                null,
                null,
                null);
        }

        public Task<string> StoreAsync(string contentHash, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ReadOnlyMemory<byte>?> ReadAsync(string storageKey, CancellationToken cancellationToken) =>
            Task.FromResult<ReadOnlyMemory<byte>?>(content.GetValueOrDefault(storageKey));
    }

    private sealed class RecordingGroupedSubmission(Exception? exception = null) : IGroupedIntakeSubmission
    {
        public List<GroupedIntakeSubmissionRequest> Requests { get; } = [];

        public Task<GroupedIntakeSubmissionResult> ExecuteAsync(
            GroupedIntakeSubmissionRequest request,
            CancellationToken cancellationToken = default)
        {
            if (exception is not null)
            {
                throw exception;
            }

            Requests.Add(request);
            return Task.FromResult(new GroupedIntakeSubmissionResult(
                new(
                    Guid.NewGuid(),
                    request.Channel,
                    request.SubmissionToken,
                    request.Files.Count,
                    request.Actor,
                    request.ReceivedAtUtc,
                    [],
                    request.ParentReceiptId),
                []));
        }
    }

    private sealed class RecordingUnidentified : IRegisterUnidentified
    {
        public List<RegisterUnidentifiedRequest> Requests { get; } = [];

        public Task<UnidentifiedRegisterResult> ExecuteAsync(
            RegisterUnidentifiedRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult<UnidentifiedRegisterResult>(null!);
        }
    }

    private sealed class FakeGroupStore : IIntakeSubmissionGroupStore
    {
        public IntakeSubmissionGroup? ParentGroup { get; init; }

        public Task<IntakeSubmissionGroup?> GetAsync(Guid groupId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IntakeSubmissionGroup?> FindAsync(IntakeSourceChannel channel, string submissionToken, CancellationToken cancellationToken = default) =>
            Task.FromResult(
                ParentGroup?.Channel == channel && ParentGroup.SubmissionToken == submissionToken
                    ? ParentGroup
                    : null);

        public Task<IntakeSubmissionGroup> GetOrCreateAsync(Guid groupId, IntakeSourceChannel channel, string submissionToken, int expectedMemberCount, string actor, DateTimeOffset receivedAtUtc, Guid? parentReceiptId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IntakeSubmissionGroupMember?> FindMemberAsync(Guid groupId, int ordinal, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IntakeSubmissionGroupMember> AddMemberAsync(Guid groupId, int ordinal, ReceivedIntake received, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<IntakeSubmissionGroupMember>> ListMembersAsync(Guid groupId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
