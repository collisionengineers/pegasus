namespace Pegasus.Core.Intake;

public sealed record IntakeSubmissionGroup(
    Guid Id,
    IntakeSourceChannel Channel,
    string SubmissionToken,
    int ExpectedMemberCount,
    string Actor,
    DateTimeOffset ReceivedAtUtc,
    IReadOnlyList<IntakeSubmissionGroupMember> Members)
{
    /// <summary>
    /// Whether this submission declared more than one member. Every manual
    /// upload is a submission group (INTK-005), but the grouped image
    /// decision table scopes itself to "a manual upload [that] contains more
    /// than one image" — a one-member group is a lone image governed by the
    /// single-image rules, and this property is the one owner of that
    /// distinction.
    /// </summary>
    public bool HasSiblingMembers => ExpectedMemberCount > 1;
}

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

    /// <summary>
    /// Finds the parent group for a processed child source token, whose shape
    /// <see cref="GroupedIntakeMemberToken"/> owns: an ordinal-zero member
    /// carries the submission token verbatim, later ordinals suffix it. The
    /// parent token is the durable group identity and is safe to use on replay.
    /// </summary>
    Task<IntakeSubmissionGroup?> FindForMemberSourceAsync(
        IntakeSourceIdentity sourceIdentity,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IntakeSubmissionGroup?>(null);

    /// <param name="expectedMemberCount">
    /// The total member count the originating submission declared. Recorded
    /// once at group creation and never revised by a later call — the
    /// wait-for-all-members-terminal rule compares the members durably
    /// present against this rather than against itself, so a group whose
    /// later files are still being staged is never evaluated as complete.
    /// </param>
    Task<IntakeSubmissionGroup> GetOrCreateAsync(
        Guid groupId,
        IntakeSourceChannel channel,
        string submissionToken,
        int expectedMemberCount,
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

/// <summary>
/// The one place that defines a group member's external receipt token, so a
/// lookup built from a group's submission token and a member's ordinal always
/// agrees with the token the member was actually submitted under. A second,
/// drifted copy of this shape previously made image-intake group routing
/// unable to ever find an ordinal-zero member: it always queried for the
/// <c>:0</c>-suffixed shape while the member itself carried the bare
/// submission token.
/// </summary>
public static class GroupedIntakeMemberToken
{
    public static string Create(string submissionToken, int ordinal) =>
        ordinal == 0 ? submissionToken : $"{submissionToken}:{ordinal}";

    /// <summary>
    /// The parent submission tokens a member's external receipt token can
    /// name, in precedence order — the inverse of <see cref="Create"/>, kept
    /// here so the convention still has exactly one owner. A strict
    /// <c>:{ordinal}</c> suffix (positive, unsigned, no whitespace — the
    /// only suffix shape <see cref="Create"/> emits, which never produces
    /// <c>:0</c>) names its parent by stripping; the bare token itself is
    /// always a candidate, because an ordinal-0 member carries the parent
    /// token verbatim.
    /// </summary>
    public static IEnumerable<string> ParentTokenCandidates(string memberToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(memberToken);
        var separator = memberToken.LastIndexOf(':');
        var hasOrdinalSuffix = separator > 0
            && int.TryParse(
                memberToken[(separator + 1)..],
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out var ordinal)
            && ordinal >= 1;
        return hasOrdinalSuffix
            ? [memberToken[..separator], memberToken]
            : [memberToken];
    }
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
            files.Length,
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

            var childToken = GroupedIntakeMemberToken.Create(request.SubmissionToken, file.Ordinal);
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
}
