namespace Pegasus.Core.Intake;

public sealed record IntakeSubmissionGroup(
    Guid Id,
    IntakeSourceChannel Channel,
    string SubmissionToken,
    string Actor,
    DateTimeOffset ReceivedAtUtc,
    IReadOnlyList<IntakeSubmissionGroupMember> Members);

public sealed record IntakeSubmissionGroupMember(
    Guid GroupId,
    int Ordinal,
    Guid StagedReceiptId,
    string SourceFileName,
    string SourceHash,
    bool IsDuplicate);

public sealed record GroupedIntakeFile(
    int Ordinal,
    IntakeSource Source);

public sealed record GroupedIntakeSubmissionRequest(
    string SubmissionToken,
    string Actor,
    DateTimeOffset ReceivedAtUtc,
    IReadOnlyList<GroupedIntakeFile> Files);

public sealed record GroupedIntakeSubmissionResult(
    IntakeSubmissionGroup Group,
    IReadOnlyList<IntakeSubmissionGroupMember> Members);

public interface IIntakeSubmissionGroupStore
{
    Task<IntakeSubmissionGroup?> GetAsync(
        Guid groupId,
        CancellationToken cancellationToken = default);

    Task<IntakeSubmissionGroup?> FindAsync(
        IntakeSourceChannel channel,
        string submissionToken,
        CancellationToken cancellationToken = default);

    Task<IntakeSubmissionGroup> GetOrCreateAsync(
        Guid groupId,
        IntakeSourceChannel channel,
        string submissionToken,
        string actor,
        DateTimeOffset receivedAtUtc,
        CancellationToken cancellationToken = default);

    Task<IntakeSubmissionGroupMember?> FindMemberAsync(
        Guid groupId,
        int ordinal,
        CancellationToken cancellationToken = default);

    Task<IntakeSubmissionGroupMember> AddMemberAsync(
        Guid groupId,
        int ordinal,
        ReceivedIntake received,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IntakeSubmissionGroupMember>> ListMembersAsync(
        Guid groupId,
        CancellationToken cancellationToken = default);
}

public interface IGroupedIntakeSubmission
{
    Task<GroupedIntakeSubmissionResult> ExecuteAsync(
        GroupedIntakeSubmissionRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class SubmitGroupedIntake(
    IIntakeSubmission submission,
    IIntakeSubmissionGroupStore groupStore,
    TimeProvider timeProvider) : IGroupedIntakeSubmission
{
    public async Task<GroupedIntakeSubmissionResult> ExecuteAsync(
        GroupedIntakeSubmissionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SubmissionToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Actor);
        if (request.Files is null || request.Files.Count == 0)
        {
            throw new ArgumentException("At least one file is required.", nameof(request));
        }

        var files = request.Files.OrderBy(file => file.Ordinal).ToArray();
        if (files.Select(file => file.Ordinal).Distinct().Count() != files.Length
            || files[0].Ordinal != 0
            || files.Select((file, index) => file.Ordinal == index).Any(isContiguous => !isContiguous))
        {
            throw new ArgumentException("Group file ordinals must be contiguous from zero.", nameof(request));
        }

        var groupId = Guid.NewGuid();
        var group = await groupStore.GetOrCreateAsync(
            groupId,
            IntakeSourceChannel.ManualUpload,
            request.SubmissionToken,
            request.Actor,
            request.ReceivedAtUtc == default ? timeProvider.GetUtcNow() : request.ReceivedAtUtc,
            cancellationToken);

        // ListMembersAsync has no per-call knowledge of duplication, so this
        // call's own replay/duplicate outcome per ordinal is tracked here and
        // stamped onto the members it returns.
        var isDuplicateByOrdinal = new Dictionary<int, bool>(files.Length);
        foreach (var file in files)
        {
            var existing = await groupStore.FindMemberAsync(group.Id, file.Ordinal, cancellationToken);
            if (existing is not null)
            {
                var expectedHash = Convert.ToHexString(
                    System.Security.Cryptography.SHA256.HashData(file.Source.Content.Span));
                if (!string.Equals(existing.SourceHash, expectedHash, StringComparison.Ordinal))
                {
                    throw new IntakeSourceIdentityConflictException(existing.SourceHash, expectedHash);
                }

                isDuplicateByOrdinal[file.Ordinal] = true;
                continue;
            }

            var childToken = ChildToken(request.SubmissionToken, file.Ordinal);
            var childOperation = $"manual-upload:{request.SubmissionToken}:{file.Ordinal}";
            var source = file.Source with
            {
                SourceIdentity = new(IntakeSourceChannel.ManualUpload, childToken)
            };
            var received = await submission.ExecuteAsync(source, childOperation, cancellationToken);
            await groupStore.AddMemberAsync(group.Id, file.Ordinal, received, cancellationToken);
            isDuplicateByOrdinal[file.Ordinal] = received.IsDuplicate;
        }

        var members = await groupStore.ListMembersAsync(group.Id, cancellationToken);
        if (members.Count != files.Length)
        {
            throw new InvalidDataException("The submission group is missing a file member.");
        }

        members = members
            .Select(member => member with { IsDuplicate = isDuplicateByOrdinal[member.Ordinal] })
            .ToArray();

        return new(group with { Members = members }, members);
    }

    private static string ChildToken(string submissionToken, int ordinal) =>
        ordinal == 0 ? submissionToken : $"{submissionToken}:{ordinal}";
}
