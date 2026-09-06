using Pegasus.Core.Actors;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake;

/// <summary>
/// The bounds and the authorisation the mail workspace reads through, and the
/// freshness rule the screen puts in front of an operator.
/// </summary>
public sealed class RetainedMailTests
{
    private static readonly Guid MailboxA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid MailboxB = Guid.Parse("22222222-2222-2222-2222-222222222222");
    [Fact]
    public void SearchProjectionKeepsDuplicateAttachmentNamesAsDistinctOccurrences()
    {
        const string readableLabel = "message, attachment 1: estimate.pdf";
        var read = new IntakeSourceReadResult(
            IntakeSourceReadStatus.Readable,
            [new(IntakeEvidenceSource.PdfContent, readableLabel, "searchable text")],
            [],
            [],
            false,
            Assets: [new(readableLabel, "estimate.pdf", "application/pdf", ReadOnlyMemory<byte>.Empty,
                IntakeAssetKind.Attachment, IntakeAssetDisposition.Attachment)],
            Attachments:
            [
                new("estimate.pdf", "application/pdf", 10, 0, readableLabel),
                new("estimate.pdf", "application/octet-stream", 10, 1)
            ]);

        var attachments = IntakeSearchProjection.Create(read, routeDecision: null)
            .Where(item => item.AttachmentOrdinal is not null)
            .OrderBy(item => item.AttachmentOrdinal)
            .ToArray();

        Assert.True(attachments[0].IsSearchable);
        Assert.False(attachments[1].IsSearchable);
        Assert.Equal([0, 1], attachments.Select(item => item.AttachmentOrdinal));
    }

    [Fact]
    public void SearchProjectionCleansTheSameForwardedBodyThatDetailDisplays()
    {
        const string body = "Wrapper [cid:signature]\r\nFrom: Provider <sender@qdosassist.co.uk>\r\n"
            + "Sent: yesterday\r\nTo: intake\r\nSubject: Instruction\r\n\r\nVisible instruction";
        var read = new IntakeSourceReadResult(
            IntakeSourceReadStatus.Readable,
            [new(IntakeEvidenceSource.EmailBody, "message, email body", body)],
            [],
            [],
            false);
        var route = new MailRouteEvaluationResult(
            MailRouteDisposition.Accepted,
            new("QDOS", MailRouteKind.DirectProvider, "QDOS"),
            [],
            "Accepted.",
            "policy",
            1,
            [new("forwarder@collisionengineers.co.uk", "message")],
            [new("sender@qdosassist.co.uk", "message, inline forwarded-message header")],
            new("sender@qdosassist.co.uk", "message, inline forwarded-message header"));

        var root = Assert.Single(IntakeSearchProjection.Create(read, route));

        Assert.Equal(
            StaffForwardBodyCleaner.Clean(body, isStaffForward: true),
            root.Text);
        Assert.DoesNotContain("Wrapper", root.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("cid:", root.Text, StringComparison.OrdinalIgnoreCase);
    }

    private static readonly DateTimeOffset NowUtc = new(2031, 9, 1, 8, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(0, 25)]
    [InlineData(10_001, 25)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task ListRefusesAPageOrSizeOutsideTheSupportedRange(int page, int pageSize)
    {
        var queries = new Queries();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            new ListRetainedMail(queries).ExecuteAsync(
                Caseworker(),
                new(null, MailFolderScope.Inbox),
                page,
                pageSize,
                CancellationToken.None));

        Assert.Empty(queries.Scopes);
    }

    [Fact]
    public async Task ListRefusesAFolderScopeThatIsNotDefined()
    {
        var queries = new Queries();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            new ListRetainedMail(queries).ExecuteAsync(
                Caseworker(),
                new(null, (MailFolderScope)7),
                1,
                25,
                CancellationToken.None));

        Assert.Empty(queries.Scopes);
    }

    [Fact]
    public async Task ListRefusesAMailboxIdentityLongerThanTheColumn()
    {
        var queries = new Queries();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            new ListRetainedMail(queries).ExecuteAsync(
                Caseworker(),
                new(Guid.Empty, MailFolderScope.Inbox),
                1,
                25,
                CancellationToken.None));

        Assert.Empty(queries.Scopes);
    }

    [Fact]
    public async Task ListRequiresCaseworkAuthorization()
    {
        var queries = new Queries();

        await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
            new ListRetainedMail(queries).ExecuteAsync(
                ActionActor.RequestLink(Guid.NewGuid()),
                new(null, MailFolderScope.Inbox),
                1,
                25,
                CancellationToken.None));

        Assert.Empty(queries.Scopes);
    }

    [Fact]
    public async Task ListPassesTheRequestedScopeThrough()
    {
        var queries = new Queries();

        await new ListRetainedMail(queries).ExecuteAsync(
            Caseworker(),
            new(MailboxA, MailFolderScope.Sent),
            3,
            25,
            CancellationToken.None);

        var scope = Assert.Single(queries.Scopes);
        Assert.Equal(MailboxA, scope.Scope.MailboxId);
        Assert.Equal(MailFolderScope.Sent, scope.Scope.Folder);
        Assert.Equal(3, scope.Page);
        Assert.Equal(25, scope.PageSize);
    }

    [Fact]
    public async Task ListPassesOneTrimmedSearchTermThroughToTheExistingQueryPort()
    {
        var queries = new Queries();

        await new ListRetainedMail(queries).ExecuteAsync(
            Caseworker(),
            new(MailboxA, MailFolderScope.Inbox, "  estimate  "),
            1,
            25,
            CancellationToken.None);

        Assert.Equal("estimate", Assert.Single(queries.Scopes).Scope.SearchTerm);
    }

    [Fact]
    public async Task CountRequiresCaseworkAuthorization()
    {
        var queries = new Queries();

        await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
            new ListRetainedMail(queries).CountAsync(
                ActionActor.RequestLink(Guid.NewGuid()),
                new(null, MailFolderScope.Inbox),
                CancellationToken.None));

        Assert.Empty(queries.CountedScopes);
    }

    [Fact]
    public async Task CountNormalizesTheScopeExactlyAsTheListDoes()
    {
        var queries = new Queries();

        await new ListRetainedMail(queries).CountAsync(
            Caseworker(),
            new(MailboxA, MailFolderScope.Inbox, "  estimate  ", UnreadOnly: true),
            CancellationToken.None);

        var scope = Assert.Single(queries.CountedScopes);
        Assert.Equal(MailboxA, scope.MailboxId);
        Assert.Equal(MailFolderScope.Inbox, scope.Folder);
        Assert.Equal("estimate", scope.SearchTerm);
        Assert.True(scope.UnreadOnly);
        // The count refuses what the list refuses, through the same guards.
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            new ListRetainedMail(queries).CountAsync(
                Caseworker(),
                new(null, (MailFolderScope)7),
                CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            new ListRetainedMail(queries).CountAsync(
                Caseworker(),
                new(null, MailFolderScope.Inbox, new string('x', 201)),
                CancellationToken.None));
        Assert.Single(queries.CountedScopes);
    }

    [Fact]
    public async Task ListAcceptsOnlyOneCanonicalMailView()
    {
        var queries = new Queries();
        var list = new ListRetainedMail(queries);
        var detailed = MailCategory.Received(ReceivedMailFamily.General, "autoreply");

        await list.ExecuteAsync(
            Caseworker(),
            new(null, MailFolderScope.Inbox, Destination: MailOperationalDestination.Triage),
            1,
            25,
            CancellationToken.None);
        await list.ExecuteAsync(
            Caseworker(),
            new(null, MailFolderScope.Inbox, DetailedClassification: detailed),
            1,
            25,
            CancellationToken.None);

        Assert.Equal(2, queries.Scopes.Count);
        await Assert.ThrowsAsync<ArgumentException>(() => list.ExecuteAsync(
            Caseworker(),
            new(
                null,
                MailFolderScope.Inbox,
                Destination: MailOperationalDestination.Queries,
                DetailedClassification: detailed),
            1,
            25,
            CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => list.ExecuteAsync(
            Caseworker(),
            new(
                null,
                MailFolderScope.Inbox,
                DetailedClassification: MailCategory.Received(
                    ReceivedMailFamily.NewInstructionReceived,
                    "inspection")),
            1,
            25,
            CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => list.ExecuteAsync(
            Caseworker(),
            new(
                null,
                MailFolderScope.Inbox,
                Destination: MailOperationalDestination.DetailedClassification),
            1,
            25,
            CancellationToken.None));
        Assert.Equal(2, queries.Scopes.Count);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ListRefusesAnEmptySearchTerm(string searchTerm)
    {
        var queries = new Queries();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            new ListRetainedMail(queries).ExecuteAsync(
                Caseworker(),
                new(null, MailFolderScope.Inbox, searchTerm),
                1,
                25,
                CancellationToken.None));

        Assert.Empty(queries.Scopes);
    }

    [Fact]
    public async Task DeletedSearchIsAuthorizedBoundedAndPagedAfterNewestFirstOrdering()
    {
        var source = new DeletedSource(new(
            [
                Deleted("old", NowUtc.AddMinutes(-2)),
                Deleted("new", NowUtc),
                Deleted("middle", NowUtc.AddMinutes(-1))
            ],
            true));

        var page = await new SearchDeletedMail(source).ExecuteAsync(
            Caseworker(),
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            " estimate ",
            2,
            2,
            CancellationToken.None);

        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), source.MailboxId);
        Assert.Equal("estimate", source.SearchTerm);
        Assert.Equal(SearchDeletedMail.MaximumMessages, source.MaximumMessages);
        Assert.Equal(["old"], page.Items.Select(item => item.ImmutableMessageId));
        Assert.Equal(3, page.TotalCount);
        Assert.True(page.IsTruncated);
    }

    [Fact]
    public async Task DeletedSearchRequiresCaseworkAuthorizationBeforeCallingItsSource()
    {
        var source = new DeletedSource(new([], false));

        await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
            new SearchDeletedMail(source).ExecuteAsync(
                ActionActor.RequestLink(Guid.NewGuid()),
                null,
                "estimate",
                1,
                25));

        Assert.Null(source.SearchTerm);
    }

    private static DeletedMailSearchItem Deleted(string id, DateTimeOffset receivedAtUtc) => new(
        MailboxA,
        "instructions@collisionengineers.co.uk",
        id,
        null,
        null,
        null,
        null,
        receivedAtUtc,
        false,
        [],
        [new(MailSearchMatchKind.MessageBody)]);

    [Fact]
    public async Task GetRequiresCaseworkAuthorizationAndAnIdentifier()
    {
        var queries = new Queries();

        await Assert.ThrowsAsync<StaffAuthorizationException>(() =>
            new GetRetainedMail(queries, new NoStaffAccounts(), new MailboxStore()).ExecuteAsync(
                ActionActor.RequestLink(Guid.NewGuid()),
                Guid.NewGuid(),
                CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            new GetRetainedMail(queries, new NoStaffAccounts(), new MailboxStore()).ExecuteAsync(
                Caseworker(),
                Guid.Empty,
                CancellationToken.None));
    }

    [Fact]
    public async Task GetResolvesTheClassificationActorsToOperatorFacingNamesAndNeverTheRawSubjectId()
    {
        var staffId = Guid.NewGuid();
        var result = MailClassificationResult.Unclassified([], "No match.", "policy", 1);
        var dossier = new MailClassificationDossier(
            2,
            result,
            $"staff:{staffId:D}",
            NowUtc,
            [
                new(1, result, result, "system-worker:poll", "Automatic classification.", NowUtc.AddDays(-1)),
                new(2, result, result, $"staff:{staffId:D}", "Corrected.", NowUtc)
            ]);
        var summary = new RetainedMailSummary(
            Guid.NewGuid(), MailboxA, "mailbox-a@example.test", true, null, null, null,
            null, null, NowUtc, true, 0, null, null, null, null);
        var detail = new RetainedMailDetail(
            summary, [], [], ["reply@example.invalid"], null, [], [], MailFolderScope.Inbox, null, null,
            "immutable-message", "<message@example.invalid>", "conversation", dossier);
        var queries = new Queries { DetailToReturn = detail };
        var staffAccounts = new FixedStaffAccounts(staffId, "alex");

        var resolved = await new GetRetainedMail(queries, staffAccounts, new MailboxStore()).ExecuteAsync(
            Caseworker(),
            summary.Id,
            CancellationToken.None);

        Assert.Equal("alex", resolved!.Classification!.CurrentActorDisplayName);
        Assert.Equal(ActorDisplayNames.SystemWorker, resolved.Classification.History[0].ActorDisplayName);
        Assert.Equal("alex", resolved.Classification.History[1].ActorDisplayName);
        Assert.DoesNotContain(staffId.ToString("D"), resolved.Classification.CurrentActorDisplayName, StringComparison.OrdinalIgnoreCase);
        foreach (var entry in resolved.Classification.History)
        {
            Assert.DoesNotContain(staffId.ToString("D"), entry.ActorDisplayName, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task GetDerivesTheExactConfiguredFolderAndSuggestedMoveFromCurrentState()
    {
        var detail = ClassifiedDetail(
            "mailbox-a",
            MailCategory.Received(ReceivedMailFamily.NewInstructionReceived, "inspection"),
            classificationVersion: 4);
        var mailbox = ApprovedMailbox(
            "mailbox-a",
            ApprovedMailboxState.Approved,
            version: 7,
            new ApprovedMailboxFolderBinding(
                MailLogicalFolderType.Instructions,
                "outlook-folder-instructions"));
        var folderMoves = new FolderMoveState();
        var sut = new GetRetainedMail(
            new Queries { DetailToReturn = detail },
            new NoStaffAccounts(),
            new MailboxStore(mailbox),
            folderMoves,
            folderMoves);

        var result = await sut.ExecuteAsync(Caseworker(), detail.Summary.Id);
        folderMoves.IsAtDestination = true;
        var atDestination = await sut.ExecuteAsync(Caseworker(), detail.Summary.Id);
        folderMoves.IsAtDestination = false;
        folderMoves.Latest = new(
            RetainedMailFolderMoveOutcome.Uncertain,
            MailLogicalFolderType.Instructions,
            "Confirmed after review.",
            NowUtc);
        var unresolved = await sut.ExecuteAsync(Caseworker(), detail.Summary.Id);

        Assert.Equal(MailLogicalFolderType.Instructions, result!.FolderRecommendation!.FolderType);
        Assert.Equal(MailLogicalFolderPolicy.Key, result.FolderRecommendation.PolicyKey);
        Assert.True(result.FolderRecommendation.IsAvailable);
        Assert.Equal(MailLogicalFolderType.Instructions, result.SuggestedMove!.FolderType);
        Assert.Equal(result.FolderRecommendation.Reason, result.SuggestedMove.Reason);
        Assert.Null(atDestination!.SuggestedMove);
        Assert.Null(unresolved!.SuggestedMove);
        Assert.Equal(RetainedMailFolderMoveOutcome.Uncertain, unresolved.LatestFolderMove!.Outcome);
    }

    [Fact]
    public async Task GetDoesNotConsultMailboxBindingsWhenClassificationAbstains()
    {
        var classification = MailClassificationResult.Ambiguous(
            ["received:billing:invoice", "received:general:general-chase"],
            [],
            "Several categories matched.",
            "classification-policy",
            2);
        var detail = Detail("mailbox-a", new(3, classification, "system-worker:poll", NowUtc, []));
        var mailboxes = new MailboxStore();

        var result = await new GetRetainedMail(
            new Queries { DetailToReturn = detail },
            new NoStaffAccounts(),
            mailboxes).ExecuteAsync(Caseworker(), detail.Summary.Id);

        Assert.False(result!.FolderRecommendation!.IsAvailable);
        Assert.Null(result.FolderRecommendation.FolderType);
        Assert.Null(result.SuggestedMove);
        Assert.Contains("absent or ambiguous", result.FolderRecommendation.Reason, StringComparison.Ordinal);
        Assert.Equal(0, mailboxes.ListCount);
    }

    [Theory]
    [InlineData(ApprovedMailboxState.Approved, "different-mailbox", "currently approved")]
    [InlineData(ApprovedMailboxState.Disabled, "mailbox-a", "currently approved")]
    [InlineData(ApprovedMailboxState.Approved, "mailbox-a", "not configured")]
    public async Task GetFailsClosedWhenTheExactApprovedBindingIsUnavailable(
        ApprovedMailboxState state,
        string mailboxIdentity,
        string expectedReason)
    {
        var detail = ClassifiedDetail(
            "mailbox-a",
            MailCategory.Received(ReceivedMailFamily.Billing, "billing-query"));
        var mailbox = ApprovedMailbox(mailboxIdentity, state, version: 2);

        var result = await new GetRetainedMail(
            new Queries { DetailToReturn = detail },
            new NoStaffAccounts(),
            new MailboxStore(mailbox)).ExecuteAsync(Caseworker(), detail.Summary.Id);

        Assert.False(result!.FolderRecommendation!.IsAvailable);
        Assert.Null(result.FolderRecommendation.FolderType);
        Assert.Null(result.SuggestedMove);
        Assert.Contains(expectedReason, result.FolderRecommendation.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetTreatsNoActionAsAConfiguredFolderRatherThanNoRecommendation()
    {
        var detail = ClassifiedDetail(
            "mailbox-a",
            MailCategory.Received(ReceivedMailFamily.General, "acknowledgement"));
        var mailbox = ApprovedMailbox(
            "mailbox-a",
            ApprovedMailboxState.Approved,
            version: 2,
            new ApprovedMailboxFolderBinding(
                MailLogicalFolderType.NoAction,
                "outlook-folder-no-action"));

        var result = await new GetRetainedMail(
            new Queries { DetailToReturn = detail },
            new NoStaffAccounts(),
            new MailboxStore(mailbox)).ExecuteAsync(Caseworker(), detail.Summary.Id);

        Assert.True(result!.FolderRecommendation!.IsAvailable);
        Assert.Equal(MailLogicalFolderType.NoAction, result.FolderRecommendation.FolderType);
        Assert.Null(result.SuggestedMove);
    }

    [Fact]
    public async Task GetReDerivesAfterTheClassificationAndBindingChange()
    {
        var queries = new Queries
        {
            DetailToReturn = ClassifiedDetail(
                "mailbox-a",
                MailCategory.Received(ReceivedMailFamily.NewInstructionReceived, "inspection"))
        };
        var mailboxes = new MailboxStore(ApprovedMailbox(
            "mailbox-a",
            ApprovedMailboxState.Approved,
            version: 1,
            new ApprovedMailboxFolderBinding(MailLogicalFolderType.Instructions, "instructions-folder")));
        var sut = new GetRetainedMail(queries, new NoStaffAccounts(), mailboxes);

        var before = await sut.ExecuteAsync(Caseworker(), queries.DetailToReturn.Summary.Id);

        queries.DetailToReturn = ClassifiedDetail(
            "mailbox-a",
            MailCategory.Received(ReceivedMailFamily.Billing, "billing-query"));
        mailboxes.Mailboxes =
        [
            ApprovedMailbox(
                "mailbox-a",
                ApprovedMailboxState.Approved,
                version: 2,
                new ApprovedMailboxFolderBinding(MailLogicalFolderType.Billing, "billing-folder"))
        ];
        var after = await sut.ExecuteAsync(Caseworker(), queries.DetailToReturn.Summary.Id);

        Assert.Equal(MailLogicalFolderType.Instructions, before!.FolderRecommendation!.FolderType);
        Assert.Equal(MailLogicalFolderType.Billing, after!.FolderRecommendation!.FolderType);
        Assert.Equal(2, mailboxes.ListCount);
    }

    [Fact]
    public async Task CorrectionPreservesEvidenceAndAppendsAnAttributedBeforeAfterEntry()
    {
        var original = MailClassificationResult.Unclassified(
            [new("provider-route", false, "No accepted provider route matched.")],
            "No supported category matched.",
            "shared-mail-policy",
            7);
        var store = new ClassificationStore(new(1, original, "system-worker:poll", NowUtc.AddMinutes(-1), []));
        var staffId = Guid.NewGuid();
        var sut = new CorrectRetainedMailClassification(store, new FixedTimeProvider(NowUtc));

        var result = await sut.ExecuteAsync(
            ActionActor.Staff(staffId, [StaffRole.User]),
            new(Guid.NewGuid(), 1, MailCategory.Received(ReceivedMailFamily.General, "acknowledgement"),
                "Confirmed from the retained message."));

        Assert.Equal(2, result!.Version);
        Assert.Equal("shared-mail-policy", result.Current.PolicyKey);
        Assert.Equal(7, result.Current.PolicyVersion);
        Assert.Equal(original.Predicates, result.Current.Predicates);
        var history = Assert.Single(result.History);
        Assert.Same(original, history.Before);
        Assert.Equal(result.Current, history.After);
        Assert.Equal($"staff:{staffId:D}", history.Actor);
        Assert.Equal(NowUtc, history.CorrectedAtUtc);
    }

    [Fact]
    public async Task CorrectionFailsClosedForAStaleVersionWithoutWriting()
    {
        var original = MailClassificationResult.Unclassified([], "No match.", "policy", 1);
        var store = new ClassificationStore(new(2, original, "system-worker:poll", NowUtc.AddMinutes(-1), []));
        var sut = new CorrectRetainedMailClassification(store, new FixedTimeProvider(NowUtc));

        await Assert.ThrowsAsync<MailClassificationConcurrencyException>(() => sut.ExecuteAsync(
            Caseworker(),
            new(Guid.NewGuid(), 1, MailCategory.Received(ReceivedMailFamily.InternalCc), "Reviewed.")));

        Assert.Equal(0, store.AppendCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CorrectionRequiresAnAttributableReason(string reason)
    {
        var original = MailClassificationResult.Unclassified([], "No match.", "policy", 1);
        var store = new ClassificationStore(new(1, original, "system-worker:poll", NowUtc.AddMinutes(-1), []));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            new CorrectRetainedMailClassification(store, new FixedTimeProvider(NowUtc)).ExecuteAsync(
                Caseworker(),
                new(Guid.NewGuid(), 1, MailCategory.Received(ReceivedMailFamily.InternalCc), reason)));

        Assert.Equal(0, store.AppendCount);
    }

    [Fact]
    public void ClassificationFactoriesRejectUndefinedFamiliesAndOversizedOtherValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            MailCategory.Received((ReceivedMailFamily)999));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            MailCategory.Sent((SentMailFamily)999));
        Assert.Throws<ArgumentException>(() => MailCategory.Other(
            MailDirection.Received,
            new string('n', MailCategory.OtherNameMaxLength + 1),
            "A reason."));
        Assert.Throws<ArgumentException>(() => MailCategory.Other(
            MailDirection.Received,
            "A new class",
            new string('r', MailCategory.OtherReasoningMaxLength + 1)));
    }

    [Fact]
    public void FreshnessIsUnavailableWhenNothingHasEverPolled() =>
        Assert.Equal(
            new MailFreshness(MailFreshnessState.Unavailable, null),
            GetRetainedMailFreshness.Evaluate([], NowUtc));

    [Fact]
    public void FreshnessIsUnavailableWhenEveryMailboxIsBackingOffAfterAFailure() =>
        Assert.Equal(
            MailFreshnessState.Unavailable,
            GetRetainedMailFreshness.Evaluate(
                [
                    new(MailboxA, NowUtc.AddMinutes(-1), "mailbox_access_denied", NowUtc.AddMinutes(1)),
                    new(MailboxB, NowUtc.AddMinutes(-2), "mailbox_poll_failure", NowUtc.AddMinutes(2))
                ],
                NowUtc).State);

    [Fact]
    public void OneHealthyMailboxIsEnoughToReportTheNewestSuccessfulPoll()
    {
        var freshness = GetRetainedMailFreshness.Evaluate(
            [
                new(MailboxA, NowUtc.AddMinutes(-1), "mailbox_access_denied", NowUtc.AddMinutes(1)),
                new(MailboxB, NowUtc.AddSeconds(-30), null, NowUtc)
            ],
            NowUtc);

        Assert.Equal(MailFreshnessState.Current, freshness.State);
        Assert.Equal(NowUtc.AddSeconds(-30), freshness.LastSuccessfulUpdateAtUtc);
    }

    [Fact]
    public void FreshnessTurnsStaleOnceThePollIsOlderThanTheThreshold()
    {
        var justInside = GetRetainedMailFreshness.Evaluate(
            [new(MailboxA, NowUtc - GetRetainedMailFreshness.StaleAfter, null, NowUtc)],
            NowUtc);
        var justOutside = GetRetainedMailFreshness.Evaluate(
            [new(MailboxA, NowUtc - GetRetainedMailFreshness.StaleAfter - TimeSpan.FromSeconds(1), null, NowUtc)],
            NowUtc);

        Assert.Equal(MailFreshnessState.Current, justInside.State);
        Assert.Equal(MailFreshnessState.Stale, justOutside.State);
    }

    [Fact]
    public void AMailboxThatHasNeverCompletedAPollIsUnavailableRatherThanInfinitelyStale() =>
        Assert.Equal(
            new MailFreshness(MailFreshnessState.Unavailable, null),
            GetRetainedMailFreshness.Evaluate(
                [new(MailboxA, null, null, NowUtc)],
                NowUtc));

    private static ActionActor Caseworker() =>
        ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);

    private static RetainedMailDetail ClassifiedDetail(
        string mailboxId,
        MailCategory category,
        int classificationVersion = 1) => Detail(
            mailboxId,
            new(
                classificationVersion,
                MailClassificationResult.Classified(category, [], "Classified.", "classification-policy", 1),
                "system-worker:poll",
                NowUtc,
                []));

    private static RetainedMailDetail Detail(
        string mailboxId,
        MailClassificationDossier dossier)
    {
        var summary = new RetainedMailSummary(
            Guid.NewGuid(), MailboxId(mailboxId), "mailbox@example.test", true, null, null, null,
            null, null, NowUtc, true, 0, null, null, null, null);
        return new(summary, [], [], ["reply@example.invalid"], null, [], [], MailFolderScope.Inbox,
            dossier.Current.Outcome, null, $"immutable-{mailboxId}",
            $"<{mailboxId}@example.invalid>", $"conversation-{mailboxId}", dossier);
    }

    private static Guid MailboxId(string value) => value switch
    {
        "mailbox-a" => MailboxA,
        "mailbox-b" => MailboxB,
        _ => Guid.Parse("33333333-3333-3333-3333-333333333333")
    };

    private static ApprovedMailbox ApprovedMailbox(
        string mailboxIdentity,
        ApprovedMailboxState state,
        int version,
        params ApprovedMailboxFolderBinding[] bindings) => new(
            MailboxId(mailboxIdentity),
            "mailbox@example.test",
            [ApprovedMailboxRouteScope.InboundIntake],
            state,
            mailboxIdentity,
            "inbox-folder",
            "sent-folder",
            true,
            DateTimeOffset.UtcNow,
            version,
            bindings);

    private sealed class Queries : IRetainedMailQueries
    {
        internal List<(MailWorkspaceScope Scope, int Page, int PageSize)> Scopes { get; } = [];

        internal List<MailWorkspaceScope> CountedScopes { get; } = [];

        internal RetainedMailDetail? DetailToReturn { get; set; }

        public Task<RetainedMailPage> ListAsync(
            MailWorkspaceScope scope,
            int page,
            int pageSize,
            CancellationToken cancellationToken)
        {
            Scopes.Add((scope, page, pageSize));
            return Task.FromResult(new RetainedMailPage([], page, pageSize, 0, false));
        }

        public Task<int> CountAsync(
            MailWorkspaceScope scope,
            CancellationToken cancellationToken)
        {
            CountedScopes.Add(scope);
            return Task.FromResult(0);
        }

        public Task<RetainedMailDetail?> GetAsync(
            Guid id,
            CancellationToken cancellationToken,
            string? searchTerm = null) =>
            Task.FromResult(DetailToReturn);

        public Task<IReadOnlyList<RetainedMailMailbox>> ListMailboxesAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RetainedMailMailbox>>([]);

        public Task<IReadOnlyList<MailPollHealth>> ListPollHealthAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<MailPollHealth>>([]);
    }

    private sealed class DeletedSource(DeletedMailSourceResult result) : IDeletedMailSearchSource
    {
        internal Guid? MailboxId { get; private set; }
        internal string? SearchTerm { get; private set; }
        internal int MaximumMessages { get; private set; }

        public Task<IReadOnlyList<RetainedMailMailbox>> ListMailboxesAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RetainedMailMailbox>>([]);

        public Task<DeletedMailSourceResult> SearchAsync(
            Guid? mailboxId,
            string searchTerm,
            int maximumMessages,
            CancellationToken cancellationToken)
        {
            MailboxId = mailboxId;
            SearchTerm = searchTerm;
            MaximumMessages = maximumMessages;
            return Task.FromResult(result);
        }
    }

    private sealed class MailboxStore(params ApprovedMailbox[] mailboxes) : IApprovedMailboxStore
    {
        internal int ListCount { get; private set; }
        internal IReadOnlyList<ApprovedMailbox> Mailboxes { get; set; } = mailboxes;

        public Task<IReadOnlyList<ApprovedMailbox>> ListAsync(CancellationToken cancellationToken)
        {
            ListCount++;
            return Task.FromResult(Mailboxes);
        }

        public Task<bool> IsApprovedAsync(
            string mailboxAddress,
            ApprovedMailboxRouteScope routeScope,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ApprovedMailbox> UpdateAsync(
            UpdateApprovedMailboxRequest request,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FolderMoveState(bool isAtDestination = false)
        : IRetainedMailFolderMoveStore, IRetainedMailFolderMover
    {
        internal bool IsAtDestination { get; set; } = isAtDestination;
        internal RetainedMailFolderMoveResult? Latest { get; set; }
        public bool IsAvailable => true;

        public Task<RetainedMailFolderMoveResult?> GetLatestAsync(
            Guid messageId,
            CancellationToken cancellationToken) =>
            Task.FromResult(Latest);

        public Task<bool> IsCurrentLocationAsync(
            Guid messageId,
            string folderIdentity,
            CancellationToken cancellationToken) =>
            Task.FromResult(IsAtDestination);

        public Task<RetainedMailFolderMoveResult?> MoveAsync(
            ActionActor actor,
            MoveRetainedMailFolderRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("Viewing a suggestion must not execute a move.");

        public Task MoveAsync(
            RetainedMailFolderMoveCoordinates coordinates,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("Viewing a suggestion must not execute a move.");

        public Task<string?> GetParentFolderIdAsync(
            string mailboxId,
            string immutableMessageId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("Viewing a suggestion must not probe the provider.");
    }

    private sealed class ClassificationStore(MailClassificationDossier dossier)
        : IRetainedMailClassificationStore
    {
        internal int AppendCount { get; private set; }

        public Task<MailClassificationDossier?> GetClassificationAsync(
            Guid messageId,
            CancellationToken cancellationToken) => Task.FromResult<MailClassificationDossier?>(dossier);

        public Task<MailClassificationDossier> AppendCorrectionAsync(
            Guid messageId,
            int expectedVersion,
            MailClassificationResult before,
            MailClassificationResult after,
            string actor,
            string reason,
            DateTimeOffset correctedAtUtc,
            CancellationToken cancellationToken)
        {
            AppendCount++;
            return Task.FromResult(new MailClassificationDossier(
                expectedVersion + 1,
                after,
                actor,
                correctedAtUtc,
                [.. dossier.History, new(expectedVersion + 1, before, after, actor, reason, correctedAtUtc)]));
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    /// <summary>Unused by these tests: every case here fails closed before a lookup would happen.</summary>
    private sealed class NoStaffAccounts : IStaffAccountQueries
    {
        public Task<StaffAccountQuerySlice> ListAsync(
            int offset,
            int limit,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("Not used by these tests.");

        public Task<StaffAccountSummary?> GetAsync(Guid staffId, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Not used by these tests.");

        public Task<IReadOnlyList<SignOffEngineerProfile>> ListSignOffEngineersAsync(
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("Not used by these tests.");

        public Task<SignOffEngineerProfile?> GetSignOffEngineerAsync(
            Guid staffId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("Not used by these tests.");
    }

    private sealed class FixedStaffAccounts(Guid staffId, string userName) : IStaffAccountQueries
    {
        public Task<StaffAccountQuerySlice> ListAsync(
            int offset,
            int limit,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("Not used by these tests.");

        public Task<StaffAccountSummary?> GetAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(id == staffId
                ? new StaffAccountSummary(staffId, userName, true, false, [StaffRole.User], null)
                : null);

        public Task<IReadOnlyList<SignOffEngineerProfile>> ListSignOffEngineersAsync(
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("Not used by these tests.");

        public Task<SignOffEngineerProfile?> GetSignOffEngineerAsync(
            Guid id,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException("Not used by these tests.");
    }
}
