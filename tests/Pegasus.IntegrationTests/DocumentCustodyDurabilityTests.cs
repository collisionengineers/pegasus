using Pegasus.Core.Custody;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Core.Cases;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Custody;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

[Trait("Category", "SqlServer")]
public sealed class DocumentCustodyDurabilityTests
{
    [Fact]
    public async Task CaseDocumentsOriginalReportMarkIsDurableReplaySafeAndUnique()
    {
        var root = Path.Combine(Path.GetTempPath(), "Pegasus.IntegrationTests", Guid.NewGuid().ToString("N"));
        try
        {
            await using var database = await LocalDbTestDatabase.CreateAsync(
                localArtifactRootFactory: _ => root);
            var caseId = await SeedCaseAsync(database, "audit");
            var firstOccurrenceId = await SeedCurrentDocumentAsync(database, caseId, 0, "audit-report.pdf");
            var secondOccurrenceId = await SeedCurrentDocumentAsync(database, caseId, 1, "other-report.pdf");
            var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
            MarkAsOriginalReportCommand command;
            OriginalReportRecorded recorded;

            await using (var scope = database.CreateAsyncScope())
            {
                var lease = await scope.ServiceProvider.GetRequiredService<ILeaseCaseForEdit>()
                    .ClaimAsync(
                        new(caseId, 0, actor, $"original-report-lease:{Guid.NewGuid():N}"),
                        CancellationToken.None);
                command = new(
                    caseId,
                    lease.Version,
                    actor,
                    $"original-report:{Guid.NewGuid():N}",
                    lease.Token,
                    firstOccurrenceId);
                var mark = scope.ServiceProvider.GetRequiredService<MarkAsOriginalReport>();

                await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    mark.ExecuteAsync(
                        command with
                        {
                            OperationKey = $"wrong-case-document:{Guid.NewGuid():N}",
                            DocumentOccurrenceId = Guid.NewGuid()
                        },
                        CancellationToken.None));

                recorded = await mark.ExecuteAsync(command, CancellationToken.None);
                var replay = await mark.ExecuteAsync(command, CancellationToken.None);

                Assert.Equal(recorded, replay);
            }

            await using (var verification = await database.CreateContextAsync())
            {
                var occurrences = await verification.Set<DocumentOccurrenceEntity>()
                    .OrderBy(value => value.Id)
                    .ToArrayAsync();
                Assert.Equal(
                    DocumentSemanticRole.AuditReport,
                    occurrences.Single(value => value.Id == firstOccurrenceId).SemanticRole);
                Assert.Equal(
                    DocumentSemanticRole.Instruction,
                    occurrences.Single(value => value.Id == secondOccurrenceId).SemanticRole);
                var history = await verification.CaseWorkflowEvents
                    .SingleAsync(value => value.EventType == "original_report_recorded");
                Assert.Equal("Original report: audit-report.pdf", history.Reason);
                Assert.Equal(command.OperationKey, history.OperationKey);
            }

            await using (var scope = database.CreateAsyncScope())
            {
                var lease = await scope.ServiceProvider.GetRequiredService<ILeaseCaseForEdit>()
                    .ClaimAsync(
                        new(caseId, recorded.CaseVersion, actor, $"second-original-report-lease:{Guid.NewGuid():N}"),
                        CancellationToken.None);
                var second = command with
                {
                    ExpectedVersion = lease.Version,
                    OperationKey = $"second-original-report:{Guid.NewGuid():N}",
                    EditLeaseToken = lease.Token,
                    DocumentOccurrenceId = secondOccurrenceId
                };

                await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    scope.ServiceProvider.GetRequiredService<MarkAsOriginalReport>()
                        .ExecuteAsync(second, CancellationToken.None));
            }
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task RemovedOriginalReportCanBeReplacedButCannotBeMarkedAgain()
    {
        var root = Path.Combine(Path.GetTempPath(), "Pegasus.IntegrationTests", Guid.NewGuid().ToString("N"));
        try
        {
            await using var database = await LocalDbTestDatabase.CreateAsync(
                localArtifactRootFactory: _ => root);
            var caseId = await SeedCaseAsync(database, "audit");
            var removedOccurrenceId = await SeedCurrentDocumentAsync(
                database, caseId, 0, "removed-original-report.pdf");
            var replacementOccurrenceId = await SeedCurrentDocumentAsync(
                database, caseId, 1, "replacement-original-report.pdf");
            var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
            OriginalReportRecorded firstMark;

            await using (var scope = database.CreateAsyncScope())
            {
                var lease = await scope.ServiceProvider.GetRequiredService<ILeaseCaseForEdit>()
                    .ClaimAsync(
                        new(caseId, 0, actor, $"first-original-report-lease:{Guid.NewGuid():N}"),
                        CancellationToken.None);
                firstMark = await scope.ServiceProvider.GetRequiredService<MarkAsOriginalReport>()
                    .ExecuteAsync(
                        new(
                            caseId,
                            lease.Version,
                            actor,
                            $"first-original-report:{Guid.NewGuid():N}",
                            lease.Token,
                            removedOccurrenceId),
                        CancellationToken.None);
            }

            await using (var scope = database.CreateAsyncScope())
            {
                var lease = await scope.ServiceProvider.GetRequiredService<ILeaseCaseForEdit>()
                    .ClaimAsync(
                        new(
                            caseId,
                            firstMark.CaseVersion,
                            actor,
                            $"remove-original-report-lease:{Guid.NewGuid():N}"),
                        CancellationToken.None);
                await scope.ServiceProvider.GetRequiredService<ILogicallyRemoveDocument>()
                    .ExecuteAsync(
                        new(
                            caseId,
                            removedOccurrenceId,
                            actor,
                            "Superseded original report.",
                            $"remove-original-report:{Guid.NewGuid():N}",
                            lease.Version,
                            lease.Token),
                        CancellationToken.None);
            }

            OriginalReportRecorded replacement;
            await using (var scope = database.CreateAsyncScope())
            {
                var lease = await scope.ServiceProvider.GetRequiredService<ILeaseCaseForEdit>()
                    .ClaimAsync(
                        new(
                            caseId,
                            firstMark.CaseVersion + 1,
                            actor,
                            $"replacement-original-report-lease:{Guid.NewGuid():N}"),
                        CancellationToken.None);
                var mark = scope.ServiceProvider.GetRequiredService<MarkAsOriginalReport>();
                await Assert.ThrowsAsync<InvalidOperationException>(() =>
                    mark.ExecuteAsync(
                        new(
                            caseId,
                            lease.Version,
                            actor,
                            $"remark-removed-original-report:{Guid.NewGuid():N}",
                            lease.Token,
                            removedOccurrenceId),
                        CancellationToken.None));

                replacement = await mark.ExecuteAsync(
                    new(
                        caseId,
                        lease.Version,
                        actor,
                        $"replacement-original-report:{Guid.NewGuid():N}",
                        lease.Token,
                        replacementOccurrenceId),
                    CancellationToken.None);
            }

            Assert.Equal(replacementOccurrenceId, replacement.DocumentOccurrenceId);
            Assert.Equal(firstMark.CaseVersion + 2, replacement.CaseVersion);
            await using var verification = await database.CreateContextAsync();
            var occurrences = await verification.Set<DocumentOccurrenceEntity>()
                .Where(value => value.CaseId == caseId)
                .ToArrayAsync();
            Assert.Equal(
                DocumentSemanticRole.AuditReport,
                occurrences.Single(value => value.Id == removedOccurrenceId).SemanticRole);
            Assert.Equal(
                DocumentSemanticRole.AuditReport,
                occurrences.Single(value => value.Id == replacementOccurrenceId).SemanticRole);
            var documentIds = occurrences.Select(value => value.DocumentId).ToArray();
            var versions = await verification.Set<DocumentVersionEntity>()
                .Where(value => documentIds.Contains(value.DocumentId))
                .ToArrayAsync();
            var removedVersionId = occurrences
                .Single(value => value.Id == removedOccurrenceId)
                .VersionId;
            var replacementVersionId = occurrences
                .Single(value => value.Id == replacementOccurrenceId)
                .VersionId;
            var removedVersion = versions.Single(value => value.Id == removedVersionId);
            Assert.False(removedVersion.IsCurrent);
            Assert.True(removedVersion.IsLogicallyRemoved);
            var replacementVersion = versions.Single(value => value.Id == replacementVersionId);
            Assert.True(replacementVersion.IsCurrent);
            Assert.False(replacementVersion.IsLogicallyRemoved);
            Assert.Equal(
                2,
                await verification.CaseWorkflowEvents.CountAsync(
                    value => value.EventType == "original_report_recorded"));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task RemovingAFileWritesOneNoteTheOperatorCanActuallySee()
    {
        // The point of this test is the ROUND TRIP, not the row. A note written
        // to CaseHistory persists happily, reports success, and never appears on
        // the Notes tab — which is how the Release 22 note defect reached
        // production. So it is asserted through CaseDetails.History, the same
        // read the page makes.
        var root = Path.Combine(Path.GetTempPath(), "Pegasus.IntegrationTests", Guid.NewGuid().ToString("N"));
        try
        {
            await using var database = await LocalDbTestDatabase.CreateAsync(
                localArtifactRootFactory: _ => root);
            var caseId = await SeedCaseAsync(database);
            var occurrenceId = await SeedCurrentImageAsync(database, caseId);
            await using var scope = database.CreateAsyncScope();
            var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
            var lease = await scope.ServiceProvider.GetRequiredService<ILeaseCaseForEdit>()
                .ClaimAsync(
                    new(caseId, 0, actor, $"removal-note-lease:{Guid.NewGuid():N}"),
                    CancellationToken.None);
            var command = new LogicallyRemoveDocumentCommand(
                caseId,
                occurrenceId,
                actor,
                "Wrong vehicle — the photograph belongs to another claim.",
                $"removal-note:{Guid.NewGuid():N}",
                lease.Version,
                lease.Token);

            var remover = scope.ServiceProvider.GetRequiredService<ILogicallyRemoveDocument>();
            await remover.ExecuteAsync(command, CancellationToken.None);
            // Replay must not add a second note.
            await remover.ExecuteAsync(command, CancellationToken.None);

            // Read through ICaseQueryStore — the component that builds the very
            // History collection the Notes tab renders. Asserting the row in
            // CaseWorkflowEvents directly would pass just as happily for a row
            // written to CaseHistory, which is the defect this guards against.
            var details = await scope.ServiceProvider.GetRequiredService<ICaseQueryStore>()
                .GetAsync(new(caseId, actor), CancellationToken.None);
            Assert.NotNull(details);
            var note = Assert.Single(
                details!.History,
                entry => entry.EventType == "case_document_removed");
            Assert.Equal(command.Reason, note.Reason);
            Assert.Equal(ActorKind.Staff.ToString(), note.ActorKind);
            Assert.Equal(actor.SubjectId.ToString(), note.Actor);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    /// <summary>
    /// Tagging an image is a Case mutation like any other: it carries the
    /// lease, the expected version and an operation key, moves the version,
    /// and replays exactly. The tag row keeps who applied it and when.
    /// </summary>
    [Fact]
    public async Task TaggingAnImageIsDurableExactlyReplayableAndReversible()
    {
        var root = Path.Combine(Path.GetTempPath(), "Pegasus.IntegrationTests", Guid.NewGuid().ToString("N"));
        try
        {
            await using var database = await LocalDbTestDatabase.CreateAsync(
                localArtifactRootFactory: _ => root);
            var caseId = await SeedCaseAsync(database);
            var occurrenceId = await SeedCurrentImageAsync(database, caseId);
            await using var scope = database.CreateAsyncScope();
            var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
            var lease = await scope.ServiceProvider.GetRequiredService<ILeaseCaseForEdit>()
                .ClaimAsync(
                    new(caseId, 0, actor, $"image-tag-lease:{Guid.NewGuid():N}"),
                    CancellationToken.None);
            var command = new TagCaseImageCommand(
                caseId,
                occurrenceId,
                ImageTagVocabulary.ThirdPartyId,
                actor,
                $"image-tag:{Guid.NewGuid():N}",
                lease.Version,
                lease.Token);
            var tagger = scope.ServiceProvider.GetRequiredService<ITagCaseImage>();

            await tagger.ExecuteAsync(command, CancellationToken.None);
            await tagger.ExecuteAsync(command, CancellationToken.None);

            long taggedVersion;
            await using (var verification = await database.CreateContextAsync())
            {
                var assignment = await verification.Set<DocumentOccurrenceTagEntity>()
                    .SingleAsync(item => item.OccurrenceId == occurrenceId);
                Assert.Equal(ImageTagVocabulary.ThirdPartyId, assignment.TagId);
                Assert.Equal(nameof(ActorKind.Staff), assignment.AppliedByKind);
                Assert.Equal(actor.SubjectId, assignment.AppliedBySubjectId);
                Assert.Equal(command.OperationKey, assignment.OperationKey);
                var history = await verification.ActionHistory.SingleAsync(item =>
                    item.CorrelationId == command.OperationKey);
                Assert.Equal("case_image_tagged", history.EventKind);
                Assert.Null(history.Reason);
                taggedVersion = await verification.CaseWorkflows
                    .Where(item => item.CaseId == caseId)
                    .Select(item => item.Version)
                    .SingleAsync();
                Assert.Equal(lease.Version + 1, taggedVersion);
            }

            // A second tag under the first operation key is refused, and the
            // tag comes off under its own key.
            await Assert.ThrowsAsync<InvalidOperationException>(() => tagger.ExecuteAsync(
                command with { OperationKey = $"image-tag:{Guid.NewGuid():N}", ExpectedCaseVersion = taggedVersion },
                CancellationToken.None));
            // The tag mutation consumed the lease it was given — every real
            // mutation clears it on completion, the same as any other Case
            // edit — so the untag needs a lease claimed fresh against the
            // post-tag version.
            var untagLease = await scope.ServiceProvider.GetRequiredService<ILeaseCaseForEdit>()
                .ClaimAsync(
                    new(caseId, taggedVersion, actor, $"image-untag-lease:{Guid.NewGuid():N}"),
                    CancellationToken.None);
            var untagCommand = new UntagCaseImageCommand(
                caseId,
                occurrenceId,
                ImageTagVocabulary.ThirdPartyId,
                actor,
                $"image-untag:{Guid.NewGuid():N}",
                taggedVersion,
                untagLease.Token);
            var untagger = scope.ServiceProvider.GetRequiredService<IUntagCaseImage>();
            await untagger.ExecuteAsync(untagCommand, CancellationToken.None);
            await untagger.ExecuteAsync(untagCommand, CancellationToken.None);

            await using var afterRemoval = await database.CreateContextAsync();
            Assert.Empty(await afterRemoval.Set<DocumentOccurrenceTagEntity>()
                .Where(item => item.OccurrenceId == occurrenceId)
                .ToArrayAsync());
            Assert.Equal(
                "case_image_untagged",
                await afterRemoval.ActionHistory
                    .Where(item => item.CorrelationId == untagCommand.OperationKey)
                    .Select(item => item.EventKind)
                    .SingleAsync());
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    /// <summary>
    /// A replay asserts the audited action, not the current state. The image
    /// the tag was put on can legitimately stop being taggable afterwards — it
    /// is removed, or a new version supersedes it — and the retry of the same
    /// operation key still has to answer with the action it already recorded.
    /// The taggable-image rule ran before the replay check and turned that
    /// retry into a refusal, which is a lost tag for any caller that retries.
    /// </summary>
    [Fact]
    public async Task AnImageTagReplaysAfterTheTaggedVersionStopsBeingTaggable()
    {
        var root = Path.Combine(Path.GetTempPath(), "Pegasus.IntegrationTests", Guid.NewGuid().ToString("N"));
        try
        {
            await using var database = await LocalDbTestDatabase.CreateAsync(
                localArtifactRootFactory: _ => root);
            var caseId = await SeedCaseAsync(database);
            var occurrenceId = await SeedCurrentImageAsync(database, caseId);
            await using var scope = database.CreateAsyncScope();
            var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
            var leases = scope.ServiceProvider.GetRequiredService<ILeaseCaseForEdit>();
            var lease = await leases.ClaimAsync(
                new(caseId, 0, actor, $"image-tag-lease:{Guid.NewGuid():N}"),
                CancellationToken.None);
            var command = new TagCaseImageCommand(
                caseId,
                occurrenceId,
                ImageTagVocabulary.ThirdPartyId,
                actor,
                $"image-tag:{Guid.NewGuid():N}",
                lease.Version,
                lease.Token);
            var tagger = scope.ServiceProvider.GetRequiredService<ITagCaseImage>();
            await tagger.ExecuteAsync(command, CancellationToken.None);

            long taggedVersion;
            await using (var verification = await database.CreateContextAsync())
            {
                taggedVersion = await verification.CaseWorkflows
                    .Where(item => item.CaseId == caseId)
                    .Select(item => item.Version)
                    .SingleAsync();
            }

            // The tagged version stops being taggable: the operator removes the
            // file.
            var removalLease = await leases.ClaimAsync(
                new(caseId, taggedVersion, actor, $"image-removal-lease:{Guid.NewGuid():N}"),
                CancellationToken.None);
            await scope.ServiceProvider.GetRequiredService<ILogicallyRemoveDocument>()
                .ExecuteAsync(
                    new(
                        caseId,
                        occurrenceId,
                        actor,
                        "Wrong vehicle — the photograph belongs to another claim.",
                        $"image-removal:{Guid.NewGuid():N}",
                        taggedVersion,
                        removalLease.Token),
                    CancellationToken.None);

            // The exact replay returns, and adds nothing.
            await tagger.ExecuteAsync(command, CancellationToken.None);

            await using var afterReplay = await database.CreateContextAsync();
            Assert.Equal(
                "case_image_tagged",
                await afterReplay.ActionHistory
                    .Where(item => item.CorrelationId == command.OperationKey)
                    .Select(item => item.EventKind)
                    .SingleAsync());

            // A first submission on the same image is still refused: the rule
            // moved behind the replay check, it did not go away.
            var removedVersion = await afterReplay.CaseWorkflows
                .Where(item => item.CaseId == caseId)
                .Select(item => item.Version)
                .SingleAsync();
            var refusedLease = await leases.ClaimAsync(
                new(caseId, removedVersion, actor, $"image-tag-refused-lease:{Guid.NewGuid():N}"),
                CancellationToken.None);
            await Assert.ThrowsAsync<InvalidOperationException>(() => tagger.ExecuteAsync(
                command with
                {
                    OperationKey = $"image-tag:{Guid.NewGuid():N}",
                    ExpectedCaseVersion = refusedLease.Version,
                    EditLeaseToken = refusedLease.Token
                },
                CancellationToken.None));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    /// <summary>
    /// A tag belongs to the case that holds the image: another case's lease
    /// and version reach nothing, and the vocabulary refuses a second entry
    /// with a name it already has, whatever the casing.
    /// </summary>
    [Fact]
    public async Task ImageTagsRefuseAnotherCaseAndDuplicateVocabularyNames()
    {
        var root = Path.Combine(Path.GetTempPath(), "Pegasus.IntegrationTests", Guid.NewGuid().ToString("N"));
        try
        {
            await using var database = await LocalDbTestDatabase.CreateAsync(
                localArtifactRootFactory: _ => root);
            var caseId = await SeedCaseAsync(database);
            var occurrenceId = await SeedCurrentImageAsync(database, caseId);
            await using var scope = database.CreateAsyncScope();
            var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
            var lease = await scope.ServiceProvider.GetRequiredService<ILeaseCaseForEdit>()
                .ClaimAsync(
                    new(caseId, 0, actor, $"image-tag-lease:{Guid.NewGuid():N}"),
                    CancellationToken.None);

            // The image is reached through the case that holds it: another
            // case's identifier finds no occurrence, whatever it carries.
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                scope.ServiceProvider.GetRequiredService<ITagCaseImage>().ExecuteAsync(
                    new(
                        Guid.NewGuid(),
                        occurrenceId,
                        ImageTagVocabulary.ThirdPartyId,
                        actor,
                        $"image-tag-cross:{Guid.NewGuid():N}",
                        lease.Version,
                        lease.Token),
                    CancellationToken.None));

            var creator = scope.ServiceProvider.GetRequiredService<ICreateImageTag>();
            var operationKey = $"image-tag-create:{Guid.NewGuid():N}";
            var created = await creator.ExecuteAsync(
                new("Underside", ImageTagColour.Grey, actor, operationKey), CancellationToken.None);
            var replayed = await creator.ExecuteAsync(
                new("Underside", ImageTagColour.Grey, actor, operationKey), CancellationToken.None);

            Assert.False(created.IsReplay);
            Assert.True(replayed.IsReplay);
            Assert.Equal(created.Tag.Id, replayed.Tag.Id);
            Assert.False(created.Tag.IsBuiltIn);
            await Assert.ThrowsAsync<ImageTagNameInUseException>(() => creator.ExecuteAsync(
                new("UNDERSIDE", ImageTagColour.Blue, actor, $"image-tag-create:{Guid.NewGuid():N}"),
                CancellationToken.None));

            var vocabulary = await scope.ServiceProvider
                .GetRequiredService<IReadImageTagVocabulary>()
                .ListAsync(CancellationToken.None);
            Assert.Equal(
                [.. ImageTagVocabulary.BuiltIn.Select(tag => tag.Name).OrderBy(name => name, StringComparer.Ordinal)],
                vocabulary.Where(tag => tag.IsBuiltIn).Select(tag => tag.Name).OrderBy(name => name, StringComparer.Ordinal));
            Assert.Contains(vocabulary, tag => tag.Id == created.Tag.Id);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task CancelledContentWriteLeavesNoImmutableDestinationAndRetrySucceeds()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "Pegasus.IntegrationTests",
            Guid.NewGuid().ToString("N"));
        try
        {
            var store = new LocalDocumentContentStore(root);
            var caseId = Guid.NewGuid();
            var versionId = Guid.NewGuid();
            var content = "complete managed document content"u8.ToArray();
            var sha256 = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
            using var cancellationSource = new CancellationTokenSource();
            cancellationSource.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                store.StoreAsync(
                    caseId,
                    "QDOS001",
                    versionId,
                    content,
                    sha256,
                    cancellationSource.Token));

            var directory = Path.Combine(
                root,
                "cases",
                "QDOS001",
                "managed",
                versionId.ToString("N"));
            Assert.False(File.Exists(Path.Combine(directory, "content")));
            Assert.Empty(Directory.EnumerateFiles(directory, "*.tmp"));

            await store.StoreAsync(
                caseId,
                "QDOS001",
                versionId,
                content,
                sha256,
                CancellationToken.None);

            await using var retained = await store.OpenReadAsync(
                caseId,
                "QDOS001",
                versionId,
                sha256,
                content.LongLength,
                CancellationToken.None);
            using var copy = new MemoryStream();
            await retained.CopyToAsync(copy);
            Assert.Equal(content, copy.ToArray());
            Assert.Empty(Directory.EnumerateFiles(directory, "*.tmp"));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task FailedDatabaseSaveRollsBackCaseAndRemovesUnreferencedContent()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "Pegasus.IntegrationTests",
            Guid.NewGuid().ToString("N"));
        var interceptor = new FailNextDocumentSaveInterceptor();
        try
        {
            await using var database = await LocalDbTestDatabase.CreateAsync(
                configureDatabase: options => options.AddInterceptors(interceptor),
                localArtifactRootFactory: _ => root);
            var caseId = await SeedCaseAsync(database);
            await using var scope = database.CreateAsyncScope();
            var actor = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
            var lease = await scope.ServiceProvider.GetRequiredService<ILeaseCaseForEdit>()
                .ClaimAsync(
                    new(
                        caseId,
                        ExpectedVersion: 0,
                        actor,
                        $"document-add-lease:{Guid.NewGuid():N}"),
                    CancellationToken.None);
            var command = new AddCaseDocumentCommand(
                caseId,
                "evidence.txt",
                "text/plain",
                "retained evidence"u8.ToArray(),
                DocumentSemanticRole.Other,
                DocumentSource.StaffUpload,
                $"durability:{Guid.NewGuid():N}",
                actor,
                $"document-add:{Guid.NewGuid():N}",
                lease.Version,
                lease.Token);
            var addDocument = scope.ServiceProvider.GetRequiredService<IAddCaseDocument>();
            interceptor.FailNextDocumentSave();

            await Assert.ThrowsAsync<DbUpdateException>(() =>
                addDocument.ExecuteAsync(command, CancellationToken.None));

            var managedDirectory = Path.Combine(
                root,
                "custody",
                "cases",
                "QDOS001",
                "managed");
            Assert.Empty(Directory.EnumerateFiles(
                managedDirectory,
                "content",
                SearchOption.AllDirectories));
            await using (var context = await database.CreateContextAsync())
            {
                Assert.Empty(await context.Set<DocumentVersionEntity>().ToArrayAsync());
                Assert.Equal(
                    3,
                    await context.Set<CaseEntity>()
                        .Where(value => value.Id == caseId)
                        .Select(value => value.Version)
                        .SingleAsync());
                Assert.Equal(
                    0,
                    await context.CaseWorkflows
                        .Where(value => value.CaseId == caseId)
                        .Select(value => value.Version)
                        .SingleAsync());
            }

            var added = await addDocument.ExecuteAsync(command, CancellationToken.None);

            Assert.False(added.IsReplay);
            Assert.Single(Directory.EnumerateFiles(
                managedDirectory,
                "content",
                SearchOption.AllDirectories));
            await using (var context = await database.CreateContextAsync())
            {
                Assert.Equal(
                    1,
                    await context.CaseWorkflows
                        .Where(value => value.CaseId == caseId)
                        .Select(value => value.Version)
                        .SingleAsync());
            }
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static async Task<Guid> SeedCaseAsync(
        LocalDbTestDatabase database,
        string caseType = "inspection")
    {
        await using var context = await database.CreateContextAsync();
        var seeded = await SeededPrincipals.QdosAsync(context);
        var receiptId = Guid.NewGuid();
        var caseId = Guid.NewGuid();
        var occurredAtUtc = new DateTimeOffset(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);
        context.AddRange(
            new IntakeReceiptEntity
            {
                Id = receiptId,
                SourceFileName = "durability.eml",
                MediaType = "message/rfc822",
                SourceLength = 1,
                SourceHash = new string('0', 64),
                SourceChannel = "manual_upload",
                ExternalReceiptToken = $"durability:{Guid.NewGuid():N}",
                ReceivedAtUtc = occurredAtUtc,
                ProcessedAtUtc = occurredAtUtc,
                SourceReaderKey = "durability-test",
                SourceReaderVersion = "1",
                Version = 0,
                Decision = "case_created",
                DecisionReason = "Durability test fixture.",
                EvidenceJson = "[]",
                FieldsJson = "[]",
                OcrCandidatesJson = "[]"
            },
            new CaseEntity
            {
                Id = caseId,
                PrincipalId = seeded.Id,
                SequenceLineageId = seeded.SequenceLineageId,
                Year = 2031,
                Sequence = 1,
                Reference = caseType == "audit" ? "a.QDOS001" : "QDOS001",
                Type = caseType,
                InitialState = "NotReady",
                // Lowercase, as ToCode writes it in production. The seed said
                // "Confirmed" and nothing noticed, because no test in this file
                // had ever read the case back through GetCase.
                CustodyState = "confirmed",
                CustodyRootRemoteId = "case-root-id",
                OriginIntakeReceiptId = receiptId,
                CreatedAtUtc = occurredAtUtc,
                Version = 3,
                ConcurrencyToken = Guid.NewGuid()
            },
            new CaseWorkflowEntity
            {
                CaseId = caseId,
                State = "NotReady",
                Version = 0,
                ConcurrencyToken = Guid.NewGuid()
            });
        await context.SaveChangesAsync();
        return caseId;
    }

    private static async Task<Guid> SeedCurrentDocumentAsync(
        LocalDbTestDatabase database,
        Guid caseId,
        int ordinal,
        string fileName)
    {
        await using var context = await database.CreateContextAsync();
        var documentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var occurrenceId = Guid.NewGuid();
        context.AddRange(
            new CaseDocumentEntity
            {
                Id = documentId,
                CaseId = caseId,
                Ordinal = ordinal,
                SourceOccurrenceIdentity = $"test-document:{occurrenceId:N}"
            },
            new DocumentVersionEntity
            {
                Id = versionId,
                DocumentId = documentId,
                Version = 1,
                FileName = fileName,
                MediaType = "application/pdf",
                ContentLength = 1,
                Sha256 = new string('a', 64),
                CustodyStatus = DocumentCustodyStatus.Confirmed,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "Staff:test",
                IsCurrent = true
            },
            new DocumentOccurrenceEntity
            {
                Id = occurrenceId,
                CaseId = caseId,
                DocumentId = documentId,
                VersionId = versionId,
                Ordinal = ordinal,
                SemanticRole = DocumentSemanticRole.Instruction,
                Source = DocumentSource.StaffUpload,
                SourceOccurrenceIdentity = $"test-document:{occurrenceId:N}",
                RecordedAtUtc = DateTimeOffset.UtcNow,
                OperationKey = $"seed-document:{occurrenceId:N}"
            });
        await context.SaveChangesAsync();
        return occurrenceId;
    }

    private static async Task<Guid> SeedCurrentImageAsync(LocalDbTestDatabase database, Guid caseId)
    {
        await using var context = await database.CreateContextAsync();
        var documentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var occurrenceId = Guid.NewGuid();
        context.AddRange(
            new CaseDocumentEntity
            {
                Id = documentId,
                CaseId = caseId,
                SourceOccurrenceIdentity = $"test-image:{occurrenceId:N}"
            },
            new DocumentVersionEntity
            {
                Id = versionId,
                DocumentId = documentId,
                Version = 1,
                FileName = "third-party.jpg",
                MediaType = "image/jpeg",
                ContentLength = 1,
                Sha256 = new string('a', 64),
                CustodyStatus = DocumentCustodyStatus.Confirmed,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = "Staff:test",
                IsCurrent = true
            },
            new DocumentOccurrenceEntity
            {
                Id = occurrenceId,
                CaseId = caseId,
                DocumentId = documentId,
                VersionId = versionId,
                SemanticRole = DocumentSemanticRole.Image,
                Source = DocumentSource.StaffUpload,
                SourceOccurrenceIdentity = $"test-image:{occurrenceId:N}",
                RecordedAtUtc = DateTimeOffset.UtcNow,
                OperationKey = $"seed-image:{occurrenceId:N}"
            });
        await context.SaveChangesAsync();
        return occurrenceId;
    }

    private sealed class FailNextDocumentSaveInterceptor : SaveChangesInterceptor
    {
        private int failNextDocumentSave;

        public void FailNextDocumentSave() =>
            Interlocked.Exchange(ref failNextDocumentSave, 1);

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (Volatile.Read(ref failNextDocumentSave) == 1
                && eventData.Context is not null
                && eventData.Context.ChangeTracker.Entries<DocumentVersionEntity>()
                    .Any(entry => entry.State == EntityState.Added)
                && Interlocked.Exchange(ref failNextDocumentSave, 0) == 1)
            {
                throw new DbUpdateException("Injected document database failure.");
            }

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    private sealed class ManagedOnlyDocumentContentStore(IDocumentContentStore inner)
        : IDocumentContentStore
    {
        public List<ManagedDocumentContentAddress> Addresses { get; } = [];

        public Task StoreAsync(
            Guid caseId,
            string caseReference,
            Guid versionId,
            ReadOnlyMemory<byte> content,
            string expectedSha256,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Managed custody addressing is required.");

        public async Task<DocumentContentWriteResult> StoreVersionAsync(
            ManagedDocumentContentAddress address,
            ReadOnlyMemory<byte> content,
            string expectedSha256,
            CancellationToken cancellationToken)
        {
            Addresses.Add(address);
            return await inner.StoreVersionAsync(
                address,
                content,
                expectedSha256,
                cancellationToken);
        }

        public async Task<DocumentContentWriteResult> StoreVersionAsync(
            ManagedDocumentContentAddress address,
            Stream content,
            long contentLength,
            string expectedSha256,
            CancellationToken cancellationToken)
        {
            Addresses.Add(address);
            return await inner.StoreVersionAsync(
                address,
                content,
                contentLength,
                expectedSha256,
                cancellationToken);
        }

        public Task<Stream> OpenReadAsync(
            Guid caseId,
            string caseReference,
            Guid versionId,
            string expectedSha256,
            long expectedLength,
            CancellationToken cancellationToken) =>
            inner.OpenReadAsync(
                caseId,
                caseReference,
                versionId,
                expectedSha256,
                expectedLength,
                cancellationToken);

        public Task DeleteAsync(
            Guid caseId,
            string caseReference,
            Guid versionId,
            CancellationToken cancellationToken) =>
            inner.DeleteAsync(caseId, caseReference, versionId, cancellationToken);
    }
}
