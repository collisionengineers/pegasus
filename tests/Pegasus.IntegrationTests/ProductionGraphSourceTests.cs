using System.Net;
using System.Text;
using Azure.Core;
using MimeKit;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Operations;
using Pegasus.Core.Workflow;
using Pegasus.Infrastructure.Email;
using Pegasus.Infrastructure.Intake;

namespace Pegasus.IntegrationTests;

public sealed class ProductionGraphSourceTests
{
    [Fact]
    public async Task StaffMailCreatesDraftWithImmutablePreferenceAndOperationMarker()
    {
        HttpRequestMessage? observed = null;
        string? body = null;
        var handler = new DelegateHandler(request =>
        {
            observed = request;
            body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return Response(HttpStatusCode.Created, "{\"id\":\"immutable-draft\"}");
        });
        var options = Options();
        var sender = new GraphStaffMailSender(
            new GraphMailClient(new FixedCredential(), options.BaseUri, new HttpClient(handler)),
            new HttpClient(new DelegateHandler(_ => throw new InvalidOperationException())),
            new UploadProgressFake());
        var operationId = Guid.NewGuid();
        var mailboxId = Guid.NewGuid();

        var result = await sender.CreateDraftAsync(
            new(mailboxId, "mailbox-id", 4, 10_000_000),
            Operation(operationId) with { PayloadHash = "PAYLOAD-HASH" },
            StaffCommand(operationId) with
            {
                ApprovedMailboxId = mailboxId,
                ExpectedMailboxGeneration = 4
            },
            CancellationToken.None);

        Assert.Equal("immutable-draft", result.ImmutableDraftId);
        Assert.Equal(HttpMethod.Post, observed!.Method);
        Assert.Equal("/v1.0/users/mailbox-id/messages", observed.RequestUri!.AbsolutePath);
        Assert.Equal("IdType=\"ImmutableId\"", observed.Headers.GetValues("Prefer").Single());
        await using var mimeBytes = new MemoryStream(Convert.FromBase64String(body!));
        var message = await MimeMessage.LoadAsync(mimeBytes);
        Assert.Equal(operationId.ToString("D"), message.Headers["X-Pegasus-Operation-Id"]);
        Assert.Equal(mailboxId.ToString("D"), message.Headers["X-Pegasus-Mailbox-Id"]);
        Assert.Equal("4", message.Headers["X-Pegasus-Mailbox-Generation"]);
        Assert.Equal("PAYLOAD-HASH", message.Headers["X-Pegasus-Payload-Sha256"]);
        Assert.Equal("Subject", message.Subject);
        Assert.NotNull(message.TextBody);
        Assert.Equal("Body", message.TextBody.TrimEnd('\r', '\n'));
        Assert.Equal("recipient@example.invalid", Assert.Single(message.To.Mailboxes).Address);
    }

    [Fact]
    public async Task KnownGraphDraftRejectionIsTypedAndNeverSends()
    {
        var requests = 0;
        var options = Options();
        var sender = new GraphStaffMailSender(
            new GraphMailClient(new FixedCredential(), options.BaseUri,
                new HttpClient(new DelegateHandler(_ =>
                {
                    requests++;
                    return Response(HttpStatusCode.BadRequest, "{}");
                }))),
            new HttpClient(new DelegateHandler(_ => throw new InvalidOperationException())),
            new UploadProgressFake());

        await Assert.ThrowsAsync<StaffMailTransportRejectedException>(() =>
            sender.CreateDraftAsync(
                new(Guid.NewGuid(), "mailbox-id", 1, 10_000_000),
                Operation(Guid.NewGuid()) with { PayloadHash = "PAYLOAD-HASH" },
                StaffCommand(Guid.NewGuid()),
                CancellationToken.None));

        Assert.Equal(1, requests);
    }

    [Fact]
    public async Task StaffMailSendRequiresAndAcceptsExact202Response()
    {
        HttpRequestMessage? observed = null;
        var options = Options();
        var sender = new GraphStaffMailSender(
            new GraphMailClient(new FixedCredential(), options.BaseUri,
                new HttpClient(new DelegateHandler(request =>
                {
                    observed = request;
                    return Response(HttpStatusCode.Accepted, string.Empty);
                }))),
            new HttpClient(new DelegateHandler(_ => throw new InvalidOperationException())),
            new UploadProgressFake());

        var result = await sender.SendDraftAsync(
            new(Guid.NewGuid(), "mailbox-id", 2, 10_000_000),
            "immutable-draft", CancellationToken.None);

        Assert.Equal(HttpMethod.Post, observed!.Method);
        Assert.Equal(
            "/v1.0/users/mailbox-id/messages/immutable-draft/send",
            observed.RequestUri!.AbsolutePath);
        Assert.Equal(TimeSpan.Zero, result.SubmittedAtUtc.Offset);
    }

    [Fact]
    public async Task AttachmentRetryCompletesVerifiedExistingContentWithoutPostingDuplicate()
    {
        var bytes = new byte[] { 4, 3, 2, 1 };
        var attachment = new StaffMailAttachment(Guid.NewGuid(), Guid.NewGuid(),
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)),
            bytes.Length, "report.pdf", "application/pdf");
        var writes = 0;
        var handler = new DelegateHandler(request =>
        {
            if (request.Method != HttpMethod.Get) writes++;
            return Response(HttpStatusCode.OK, System.Text.Json.JsonSerializer.Serialize(new
            {
                value = new[] { new { name = "report.pdf", size = 4, contentType = "application/pdf",
                    contentBytes = Convert.ToBase64String(bytes) } }
            }));
        });
        var progress = new UploadProgressFake
        {
            Current = new(null, DateTimeOffset.MaxValue, 0)
        };
        var options = Options();
        var sender = new GraphStaffMailSender(
            new GraphMailClient(new FixedCredential(), options.BaseUri, new HttpClient(handler)),
            new HttpClient(new DelegateHandler(_ => throw new InvalidOperationException())), progress);

        await sender.AttachAsync(new(Guid.NewGuid(), "mailbox-id", 1, 10_000_000),
            Guid.NewGuid(), "draft", attachment, new MemoryStream(bytes), CancellationToken.None);

        Assert.Equal(0, writes);
        Assert.True(progress.Completed);
    }

    [Fact]
    public async Task ChunkRetryReadsProviderRangeBeforeWritingAgain()
    {
        var length = 3 * 1024 * 1024 + 1;
        var bytes = new byte[length];
        var attachment = new StaffMailAttachment(Guid.NewGuid(), Guid.NewGuid(),
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)),
            bytes.Length, "large.pdf", "application/pdf");
        var putStart = -1L;
        var graph = new DelegateHandler(_ => Response(HttpStatusCode.OK, "{\"value\":[]}"));
        var upload = new DelegateHandler(request =>
        {
            if (request.Method == HttpMethod.Get)
                return Response(HttpStatusCode.OK, "{\"nextExpectedRanges\":[\"327680-\"]}");
            putStart = request.Content!.Headers.ContentRange!.From!.Value;
            return Response(HttpStatusCode.Created, "{}");
        });
        var progress = new UploadProgressFake
        {
            Current = new(new Uri("https://graph.microsoft.com/upload/session"),
                DateTimeOffset.UtcNow.AddMinutes(10), 655360)
        };
        var options = Options();
        var sender = new GraphStaffMailSender(
            new GraphMailClient(new FixedCredential(), options.BaseUri, new HttpClient(graph)),
            new HttpClient(upload), progress);

        await sender.AttachAsync(new(Guid.NewGuid(), "mailbox-id", 1, 10_000_000),
            Guid.NewGuid(), "draft", attachment, new MemoryStream(bytes), CancellationToken.None);

        Assert.Equal(327680, putStart);
        Assert.True(progress.Completed);
    }

    [Fact]
    public async Task ExactMimeSizeValidationRejectsBeforeAnyGraphWrite()
    {
        var graphWrites = 0;
        var options = Options();
        var sender = new GraphStaffMailSender(
            new GraphMailClient(new FixedCredential(), options.BaseUri,
                new HttpClient(new DelegateHandler(_ => { graphWrites++; return Response(HttpStatusCode.OK, "{}"); }))),
            new HttpClient(new DelegateHandler(_ => throw new InvalidOperationException())),
            new UploadProgressFake());
        var command = StaffCommand(Guid.NewGuid()) with { Body = new string('\u20ac', 100) };

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            sender.ValidateEncodedSizeAsync(new(Guid.NewGuid(), "mailbox-id", 1, 32),
                Operation(Guid.NewGuid()), command, [], CancellationToken.None));

        Assert.Equal(0, graphWrites);
    }

    [Fact]
    public async Task ExactMimeSizeValidationCountsFrozenCorrelationHeadersBeforeGraphWrite()
    {
        string? createdPayload = null;
        var options = Options();
        var mailboxId = Guid.NewGuid();
        var operationId = Guid.NewGuid();
        var command = StaffCommand(operationId) with
        {
            ApprovedMailboxId = mailboxId,
            ExpectedMailboxGeneration = 7
        };
        var operation = Operation(operationId) with
        {
            ApprovedMailboxId = mailboxId,
            MailboxGeneration = 7
        };
        var measuringSender = new GraphStaffMailSender(
            new GraphMailClient(new FixedCredential(), options.BaseUri,
                new HttpClient(new DelegateHandler(request =>
                {
                    createdPayload = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                    return Response(HttpStatusCode.Created, "{\"id\":\"draft\"}");
                }))),
            new HttpClient(new DelegateHandler(_ => throw new InvalidOperationException())),
            new UploadProgressFake());
        await measuringSender.CreateDraftAsync(
            new(mailboxId, "mailbox-id", 7, int.MaxValue), operation,
            command, CancellationToken.None);
        var exactMimeLength = Convert.FromBase64String(createdPayload!).LongLength;

        var validationGraphWrites = 0;
        var validatingSender = new GraphStaffMailSender(
            new GraphMailClient(new FixedCredential(), options.BaseUri,
                new HttpClient(new DelegateHandler(_ =>
                {
                    validationGraphWrites++;
                    return Response(HttpStatusCode.OK, "{}");
                }))),
            new HttpClient(new DelegateHandler(_ => throw new InvalidOperationException())),
            new UploadProgressFake());

        await Assert.ThrowsAsync<InvalidDataException>(() => validatingSender.ValidateEncodedSizeAsync(
            new(mailboxId, "mailbox-id", 7, exactMimeLength - 1),
            operation, command, [], CancellationToken.None));
        Assert.Equal(0, validationGraphWrites);
    }

    [Fact]
    public async Task AttachmentMimeSizeBoundaryIsExactAndAlwaysResetsContentStream()
    {
        var graphWrites = 0;
        var options = Options();
        var mailboxId = Guid.NewGuid();
        var operationId = Guid.NewGuid();
        var command = StaffCommand(operationId) with
        {
            ApprovedMailboxId = mailboxId,
            ExpectedMailboxGeneration = 9
        };
        var operation = Operation(operationId) with
        {
            ApprovedMailboxId = mailboxId,
            MailboxGeneration = 9
        };
        var bytes = Enumerable.Range(0, 4096).Select(value => (byte)(value % 251)).ToArray();
        var attachment = new StaffMailAttachment(
            Guid.NewGuid(), Guid.NewGuid(),
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)),
            bytes.LongLength, "evidence.bin", "application/octet-stream");
        await using var content = new MemoryStream(bytes, writable: false);
        var sender = new GraphStaffMailSender(
            new GraphMailClient(new FixedCredential(), options.BaseUri,
                new HttpClient(new DelegateHandler(_ =>
                {
                    graphWrites++;
                    return Response(HttpStatusCode.OK, "{}");
                }))),
            new HttpClient(new DelegateHandler(_ => throw new InvalidOperationException())),
            new UploadProgressFake());

        long lower = 0;
        long upper = 64 * 1024;
        while (lower < upper)
        {
            var candidate = lower + ((upper - lower) / 2);
            content.Position = 137;
            try
            {
                await sender.ValidateEncodedSizeAsync(
                    new(mailboxId, "mailbox-id", 9, candidate), operation, command,
                    [new(attachment, content)], CancellationToken.None);
                upper = candidate;
            }
            catch (InvalidDataException)
            {
                lower = candidate + 1;
            }
            Assert.Equal(0, content.Position);
        }
        var exactSize = lower;

        content.Position = 211;
        await sender.ValidateEncodedSizeAsync(
            new(mailboxId, "mailbox-id", 9, exactSize), operation, command,
            [new(attachment, content)], CancellationToken.None);
        Assert.Equal(0, content.Position);

        content.Position = 307;
        await Assert.ThrowsAsync<InvalidDataException>(() => sender.ValidateEncodedSizeAsync(
            new(mailboxId, "mailbox-id", 9, exactSize - 1), operation, command,
            [new(attachment, content)], CancellationToken.None));
        Assert.Equal(0, content.Position);
        Assert.Equal(0, graphWrites);
    }

    [Fact]
    public async Task DraftReconciliationRejectsOversizedGraphJson()
    {
        var options = Options();
        var oversized = new string('x', 2 * 1024 * 1024 + 1);
        var sender = new GraphStaffMailSender(
            new GraphMailClient(new FixedCredential(), options.BaseUri,
                new HttpClient(new DelegateHandler(_ =>
                    Response(HttpStatusCode.OK, oversized)))),
            new HttpClient(new DelegateHandler(_ => throw new InvalidOperationException())),
            new UploadProgressFake());

        await Assert.ThrowsAsync<InvalidDataException>(() => sender.FindDraftAsync(
            new(Guid.NewGuid(), "mailbox-id", 1, 10_000_000),
            Operation(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task DraftReconciliationUsesDraftWindowAndRetainsCursorAtFiveHundredCandidates()
    {
        var requests = 0;
        var operationId = Guid.NewGuid();
        var options = Options();
        var handler = new DelegateHandler(request =>
        {
            requests++;
            if (requests == 1)
            {
                Assert.Contains("$top=50", Uri.UnescapeDataString(request.RequestUri!.Query),
                    StringComparison.Ordinal);
                Assert.Contains("createdDateTime ge 2026-09-06T09:55:00.0000000+00:00",
                    Uri.UnescapeDataString(request.RequestUri.Query), StringComparison.Ordinal);
                Assert.Contains("createdDateTime le 2026-09-06T11:00:00.0000000+00:00",
                    Uri.UnescapeDataString(request.RequestUri.Query), StringComparison.Ordinal);
            }
            Assert.Contains("/mailFolders/drafts/messages", request.RequestUri!.AbsolutePath,
                StringComparison.Ordinal);
            var values = Enumerable.Range(0, requests == 11 ? 1 : 50)
                .Select(index => requests is 1 or 11 && index == 0
                    ? System.Text.Json.JsonSerializer.Deserialize<object>(
                        DraftJson("replayed-draft", operationId, "PAYLOAD-HASH"))!
                    : (object)new
                {
                    id = $"other-{requests}-{index}",
                    internetMessageHeaders = Array.Empty<object>()
                }).ToArray();
            var valueJson = System.Text.Json.JsonSerializer.Serialize(values);
            var next = requests == 11 ? string.Empty
                : $",\"@odata.nextLink\":\"https://graph.microsoft.com/v1.0/users/mailbox-id/mailFolders/drafts/messages?$skiptoken={requests}\"";
            return Response(HttpStatusCode.OK, $"{{\"value\":{valueJson}{next}}}");
        });
        var sender = new GraphStaffMailSender(
            new GraphMailClient(new FixedCredential(), options.BaseUri, new HttpClient(handler)),
            new HttpClient(new DelegateHandler(_ => throw new InvalidOperationException())),
            new UploadProgressFake());

        var operation = Operation(operationId);
        var result = await sender.FindDraftAsync(
            new(Guid.Parse("11111111-1111-1111-1111-111111111111"), "mailbox-id", 1, 10_000_000),
            operation, CancellationToken.None);

        Assert.False(result.Complete);
        Assert.Null(result.Draft);
        Assert.NotNull(result.Continuation);
        Assert.Equal(10, requests);

        var resumed = await sender.FindDraftAsync(
            new(Guid.Parse("11111111-1111-1111-1111-111111111111"), "mailbox-id", 1, 10_000_000),
            operation with { ReconciliationContinuation = result.Continuation }, CancellationToken.None);
        Assert.True(resumed.Complete);
        Assert.Equal("replayed-draft", Assert.IsType<StaffMailDraftResult>(resumed.Draft).ImmutableDraftId);
        Assert.Null(resumed.Continuation);
        Assert.Equal(11, requests);
    }

    [Fact]
    public async Task DraftReconciliationRequiresAllFrozenMarkersAndRejectsMultipleMatches()
    {
        var operationId = Guid.NewGuid();
        var call = 0;
        var options = Options();
        var handler = new DelegateHandler(_ =>
        {
            call++;
            var values = call == 1
                ? new[] { DraftJson("draft-one", operationId, "PAYLOAD-HASH") }
                : new[]
                {
                    DraftJson("payload-mismatch", operationId, "OTHER-HASH"),
                    DraftJson("draft-two", operationId, "PAYLOAD-HASH")
                };
            var next = call == 1
                ? ",\"@odata.nextLink\":\"https://graph.microsoft.com/v1.0/users/mailbox-id/mailFolders/drafts/messages?$skiptoken=next\""
                : string.Empty;
            return Response(HttpStatusCode.OK, $"{{\"value\":[{string.Join(',', values)}]{next}}}");
        });
        var sender = new GraphStaffMailSender(
            new GraphMailClient(new FixedCredential(), options.BaseUri, new HttpClient(handler)),
            new HttpClient(new DelegateHandler(_ => throw new InvalidOperationException())),
            new UploadProgressFake());

        await Assert.ThrowsAsync<InvalidDataException>(() => sender.FindDraftAsync(
            new(Guid.Parse("11111111-1111-1111-1111-111111111111"), "mailbox-id", 1, 10_000_000),
            Operation(operationId), CancellationToken.None));
        Assert.Equal(2, call);
    }

    [Fact]
    public async Task DraftReconciliationRejectsProviderContinuationOutsideApprovedDraftsFolder()
    {
        var requests = 0;
        var options = Options();
        var sender = new GraphStaffMailSender(
            new GraphMailClient(new FixedCredential(), options.BaseUri,
                new HttpClient(new DelegateHandler(_ =>
                {
                    requests++;
                    return Response(HttpStatusCode.OK,
                        "{\"value\":[],\"@odata.nextLink\":\"https://graph.microsoft.com/v1.0/users/other-mailbox/mailFolders/inbox/messages?$skiptoken=wrong\"}");
                }))),
            new HttpClient(new DelegateHandler(_ => throw new InvalidOperationException())),
            new UploadProgressFake());

        await Assert.ThrowsAsync<InvalidDataException>(() => sender.FindDraftAsync(
            new(Guid.Parse("11111111-1111-1111-1111-111111111111"), "mailbox-id", 1, 10_000_000),
            Operation(Guid.NewGuid()), CancellationToken.None));

        Assert.Equal(1, requests);
    }

    [Fact]
    public async Task DraftReadyReconciliationReadsRecordedImmutableIdWithoutCreationWindowScan()
    {
        var operationId = Guid.NewGuid();
        Uri? requested = null;
        var options = Options();
        var sender = new GraphStaffMailSender(
            new GraphMailClient(new FixedCredential(), options.BaseUri,
                new HttpClient(new DelegateHandler(request =>
                {
                    requested = request.RequestUri;
                    return Response(HttpStatusCode.OK,
                        DraftJson("recorded-draft", operationId, "PAYLOAD-HASH", isDraft: true));
                }))),
            new HttpClient(new DelegateHandler(_ => throw new InvalidOperationException())),
            new UploadProgressFake());

        var result = await sender.FindDraftAsync(
            new(Guid.Parse("11111111-1111-1111-1111-111111111111"), "mailbox-id", 1, 10_000_000),
            Operation(operationId) with
            {
                State = StaffMailState.DraftReady,
                AttemptStage = StaffMailAttemptStage.Attach,
                AttemptRequestedAtUtc = new DateTimeOffset(2026, 9, 7, 12, 0, 0, TimeSpan.Zero),
                DraftImmutableId = "recorded-draft"
            }, CancellationToken.None);

        Assert.True(result.Complete);
        Assert.Equal("recorded-draft", Assert.IsType<StaffMailDraftResult>(result.Draft).ImmutableDraftId);
        Assert.Equal("/v1.0/users/mailbox-id/messages/recorded-draft", requested!.AbsolutePath);
        Assert.DoesNotContain("createdDateTime", requested.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DraftReadyReconciliationRejectsRecordedIdWithDifferentFrozenMarkers()
    {
        var operationId = Guid.NewGuid();
        var options = Options();
        var sender = new GraphStaffMailSender(
            new GraphMailClient(new FixedCredential(), options.BaseUri,
                new HttpClient(new DelegateHandler(_ => Response(HttpStatusCode.OK,
                    DraftJson("recorded-draft", operationId, "DIFFERENT-HASH", isDraft: true))))),
            new HttpClient(new DelegateHandler(_ => throw new InvalidOperationException())),
            new UploadProgressFake());

        await Assert.ThrowsAsync<InvalidDataException>(() => sender.FindDraftAsync(
            new(Guid.Parse("11111111-1111-1111-1111-111111111111"), "mailbox-id", 1, 10_000_000),
            Operation(operationId) with { DraftImmutableId = "recorded-draft" },
            CancellationToken.None));
    }

    private static string DraftJson(
        string id, Guid operationId, string payloadHash, bool? isDraft = null) =>
        System.Text.Json.JsonSerializer.Serialize(new
        {
            id,
            isDraft,
            internetMessageHeaders = new[]
            {
                new { name = "x-pegasus-operation-id", value = operationId.ToString("D") },
                new { name = "x-pegasus-mailbox-id", value = "11111111-1111-1111-1111-111111111111" },
                new { name = "x-pegasus-mailbox-generation", value = "1" },
                new { name = "x-pegasus-payload-sha256", value = payloadHash }
            }
        });

    private static StaffMailSendCommand StaffCommand(Guid operationId) => new(
        ActionActor.Staff(Guid.NewGuid(), [StaffRole.User]),
        Guid.NewGuid(), 1, StaffMailPurpose.GeneralCorrespondence,
        Guid.NewGuid(), 1, StaffMailComposeMode.New, null,
        [new("recipient@example.invalid", null)], [], "Subject", "Body", [],
        operationId.ToString("N"));

    private static StaffMailOperation Operation(Guid operationId, string? continuation = null) => new(
        operationId, StaffMailState.DraftCreating, StaffMailAttemptStage.CreateDraft, 1,
        new DateTimeOffset(2026, 9, 6, 8, 0, 0, TimeSpan.Zero), null, null, null,
        Guid.Parse("11111111-1111-1111-1111-111111111111"), 1, "PAYLOAD-HASH",
        new DateTimeOffset(2026, 9, 6, 10, 0, 0, TimeSpan.Zero), null, continuation);
    [Fact]
    public async Task ChangeSubscriptionCreatesExactInboxBasicNotification()
    {
        HttpRequestMessage? observed = null;
        string? body = null;
        var handler = new DelegateHandler(request =>
        {
            observed = request;
            body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return Response(HttpStatusCode.Created,
                "{\"id\":\"11111111-2222-3333-4444-555555555555\",\"expirationDateTime\":\"2031-05-12T10:30:00Z\"}");
        });
        var adapter = new GraphMailboxChangeSubscriptions(
            new FixedCredential(), Options(), new HttpClient(handler));

        var result = await adapter.MaintainAsync(
            new(Guid.NewGuid(), "mailbox-id", "inbox-folder", null),
            new Uri("https://pegasus.example.test/hooks/microsoft-graph/mail"),
            "secret-state",
            new DateTimeOffset(2031, 5, 6, 10, 30, 0, TimeSpan.Zero),
            CancellationToken.None);

        Assert.Equal(HttpMethod.Post, observed!.Method);
        Assert.Equal("/v1.0/subscriptions", observed.RequestUri!.AbsolutePath);
        Assert.Contains("\"changeType\":\"created\"", body, StringComparison.Ordinal);
        Assert.Contains("users/mailbox-id/mailFolders/inbox-folder/messages", body, StringComparison.Ordinal);
        Assert.Contains("\"includeResourceData\":false", body, StringComparison.Ordinal);
        Assert.Equal("11111111-2222-3333-4444-555555555555", result.SubscriptionId);
    }

    [Fact]
    public async Task ChangeSubscriptionRenewsActiveExactScopeWithPatch()
    {
        HttpRequestMessage? observed = null;
        var handler = new DelegateHandler(request =>
        {
            observed = request;
            return Response(HttpStatusCode.OK,
                "{\"id\":\"11111111-2222-3333-4444-555555555555\",\"expirationDateTime\":\"2031-05-12T10:30:00Z\"}");
        });
        var mailboxId = Guid.NewGuid();
        var subscription = new ApprovedMailboxSubscription(
            mailboxId,
            "11111111-2222-3333-4444-555555555555",
            "users/mailbox-id/mailFolders/inbox-folder/messages",
            new DateTimeOffset(2031, 5, 7, 10, 30, 0, TimeSpan.Zero),
            ApprovedMailboxSubscriptionLifecycleState.Active,
            null,
            null,
            1);
        var adapter = new GraphMailboxChangeSubscriptions(
            new FixedCredential(), Options(), new HttpClient(handler));

        await adapter.MaintainAsync(
            new(mailboxId, "mailbox-id", "inbox-folder", subscription, 1),
            new Uri("https://pegasus.example.test/hooks/microsoft-graph/mail"),
            "secret-state",
            new DateTimeOffset(2031, 5, 6, 10, 30, 0, TimeSpan.Zero),
            CancellationToken.None);

        Assert.Equal(HttpMethod.Patch, observed!.Method);
        Assert.EndsWith("/subscriptions/11111111-2222-3333-4444-555555555555",
            observed.RequestUri!.AbsolutePath, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FolderMoveUsesExactScopedPostAndImmutableIdHeader()
    {
        HttpRequestMessage? observed = null;
        string? body = null;
        var handler = new DelegateHandler(request =>
        {
            observed = request;
            body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return Response(HttpStatusCode.Created, "{\"id\":\"moved-message\"}");
        });
        var options = Options();
        var mover = new GraphRetainedMailFolderMover(
            new GraphMailClient(new FixedCredential(), options.BaseUri, new HttpClient(handler)));

        await mover.MoveAsync(
            new("mailbox-id", "source-folder", "immutable-message", "destination-folder"),
            CancellationToken.None);

        Assert.Equal(HttpMethod.Post, observed!.Method);
        Assert.Equal(
            "/v1.0/users/mailbox-id/mailFolders/source-folder/messages/immutable-message/move",
            observed.RequestUri!.AbsolutePath);
        Assert.Equal("IdType=\"ImmutableId\"", observed.Headers.GetValues("Prefer").Single());
        Assert.Equal("{\"destinationId\":\"destination-folder\"}", body);
    }

    [Fact]
    public async Task FolderMoveProbeReadsTheImmutableMessageParent()
    {
        var handler = new DelegateHandler(request =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("IdType=\"ImmutableId\"", request.Headers.GetValues("Prefer").Single());
            return Response(HttpStatusCode.OK, "{\"parentFolderId\":\"destination-folder\"}");
        });
        var options = Options();
        var mover = new GraphRetainedMailFolderMover(
            new GraphMailClient(new FixedCredential(), options.BaseUri, new HttpClient(handler)));

        var parent = await mover.GetParentFolderIdAsync(
            "mailbox-id", "immutable-message", CancellationToken.None);

        Assert.Equal("destination-folder", parent);
    }

    [Fact]
    public async Task DeletedSearchReadsOnlyTheResolvedFolderAndPassesMimeThroughTheCanonicalReader()
    {
        var requests = new List<(HttpMethod Method, string Path, string? Prefer)>();
        var handler = new DelegateHandler(request =>
        {
            requests.Add((
                request.Method,
                request.RequestUri!.AbsolutePath,
                request.Headers.TryGetValues("Prefer", out var values) ? values.Single() : null));
            if (request.RequestUri.AbsolutePath.EndsWith("/mailFolders/deleteditems", StringComparison.Ordinal))
            {
                return Response(HttpStatusCode.OK, """{"id":"deleted-folder"}""");
            }
            if (request.RequestUri.AbsolutePath.EndsWith("/$value", StringComparison.Ordinal))
            {
                return Response(
                    HttpStatusCode.OK,
                    "From: sender@example.test\r\nSubject: Deleted instruction\r\n"
                    + "MIME-Version: 1.0\r\nContent-Type: multipart/mixed; boundary=part\r\n\r\n"
                    + "--part\r\nContent-Type: text/plain\r\n\r\nThe searchable needle is here.\r\n"
                    + "--part\r\nContent-Type: application/octet-stream; name=source.bin\r\n"
                    + "Content-Disposition: attachment; filename=source.bin\r\n"
                    + "Content-Transfer-Encoding: base64\r\n\r\nAQID\r\n--part--\r\n",
                    "message/rfc822");
            }
            return Response(
                HttpStatusCode.OK,
                """{"value":[{"id":"deleted-1","parentFolderId":"deleted-folder","receivedDateTime":"2026-07-31T10:00:00Z","isRead":false}]}""");
        });
        var options = Options();
        var source = new GraphDeletedMailSearchSource(
            new GraphMailClient(new FixedCredential(), options.BaseUri, new HttpClient(handler)),
            new MailboxEstate([Mailbox(options.MailboxId, options.MailboxAddress, options.InboxFolderId)]),
            new MimeKitPdfPigOpenXmlIntakeSourceReader(TimeProvider.System));

        var result = await source.SearchAsync(
            StableMailboxId(options.MailboxId),
            "needle",
            100,
            CancellationToken.None);

        var item = Assert.Single(result.Items);
        Assert.Equal("Deleted instruction", item.Subject);
        Assert.Equal("sender@example.test", item.SenderAddress);
        Assert.Equal("The searchable needle is here.", item.BodyPlainText);
        Assert.Contains(item.Matches, match => match.Kind == MailSearchMatchKind.MessageBody);
        Assert.False(Assert.Single(item.Attachments).IsSearchable);
        Assert.All(requests, request => Assert.Equal(HttpMethod.Get, request.Method));
        Assert.All(
            requests.Where(request => !request.Path.EndsWith("/mailFolders/deleteditems", StringComparison.Ordinal)),
            request => Assert.Equal("IdType=\"ImmutableId\"", request.Prefer));
        Assert.Contains(requests, request => request.Path.EndsWith("/mailFolders/deleteditems", StringComparison.Ordinal));
        Assert.Contains(requests, request => request.Path.Contains("/mailFolders/deleted-folder/messages", StringComparison.Ordinal));
        Assert.Contains(requests, request => request.Path.EndsWith(
            "/mailFolders/deleted-folder/messages/deleted-1/$value",
            StringComparison.Ordinal));
    }

    [Fact]
    public async Task DeletedSearchBecomesUnavailableWhenTheMessageMovesBeforeItsFolderScopedMimeRead()
    {
        var handler = new DelegateHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/mailFolders/deleteditems", StringComparison.Ordinal))
            {
                return Response(HttpStatusCode.OK, """{"id":"deleted-folder"}""");
            }
            if (request.RequestUri.AbsolutePath.EndsWith("/$value", StringComparison.Ordinal))
            {
                return Response(HttpStatusCode.NotFound, "{}");
            }
            return Response(
                HttpStatusCode.OK,
                """{"value":[{"id":"moved-1","parentFolderId":"deleted-folder","receivedDateTime":"2026-07-31T10:00:00Z","isRead":false}]}""");
        });
        var options = Options();
        var source = new GraphDeletedMailSearchSource(
            new GraphMailClient(new FixedCredential(), options.BaseUri, new HttpClient(handler)),
            new MailboxEstate([Mailbox(options.MailboxId, options.MailboxAddress, options.InboxFolderId)]),
            new MimeKitPdfPigOpenXmlIntakeSourceReader(TimeProvider.System));

        var result = await source.SearchAsync(null, "needle", 100, CancellationToken.None);

        Assert.Equal(DeletedMailSearchState.Unavailable, result.State);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task DeletedSearchDoesNotCallGraphForAMailboxOutsideTheApprovedEstate()
    {
        var calls = 0;
        var handler = new DelegateHandler(_ =>
        {
            calls++;
            return Response(HttpStatusCode.OK, "{}");
        });
        var options = Options();
        var source = new GraphDeletedMailSearchSource(
            new GraphMailClient(new FixedCredential(), options.BaseUri, new HttpClient(handler)),
            new MailboxEstate([Mailbox(options.MailboxId, options.MailboxAddress, options.InboxFolderId)]),
            new MimeKitPdfPigOpenXmlIntakeSourceReader(TimeProvider.System));

        var result = await source.SearchAsync(Guid.NewGuid(), "needle", 100, CancellationToken.None);

        Assert.Equal(DeletedMailSearchState.Unavailable, result.State);
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task DeletedSearchChoosesTheNewestBoundedMessagesAcrossApprovedMailboxes()
    {
        var mimePaths = new List<string>();
        var handler = new DelegateHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/mailFolders/deleteditems", StringComparison.Ordinal))
            {
                return Response(HttpStatusCode.OK, path.Contains("mailbox-two", StringComparison.Ordinal)
                    ? """{"id":"deleted-two"}"""
                    : """{"id":"deleted-one"}""");
            }
            if (path.EndsWith("/$value", StringComparison.Ordinal))
            {
                mimePaths.Add(path);
                return Response(HttpStatusCode.OK, "Subject: Match\r\n\r\nneedle", "message/rfc822");
            }
            var second = path.Contains("mailbox-two", StringComparison.Ordinal);
            return Response(HttpStatusCode.OK, second
                ? """{"value":[{"id":"newer","parentFolderId":"deleted-two","receivedDateTime":"2026-08-20T11:00:00Z","isRead":false}]}"""
                : """{"value":[{"id":"older","parentFolderId":"deleted-one","receivedDateTime":"2026-08-20T10:00:00Z","isRead":false}]}""");
        });
        var options = Options();
        var source = new GraphDeletedMailSearchSource(
            new GraphMailClient(new FixedCredential(), options.BaseUri, new HttpClient(handler)),
            new MailboxEstate([
                Mailbox("mailbox-id", "one@example.test", "inbox-one"),
                Mailbox("mailbox-two", "two@example.test", "inbox-two")]),
            new MimeKitPdfPigOpenXmlIntakeSourceReader(TimeProvider.System));

        var result = await source.SearchAsync(null, "needle", 1, CancellationToken.None);

        Assert.Equal("newer", Assert.Single(result.Items).ImmutableMessageId);
        Assert.Single(mimePaths);
        Assert.Contains(
            "mailbox-two/mailFolders/deleted-two/messages/newer",
            mimePaths[0],
            StringComparison.Ordinal);
        Assert.True(result.IsTruncated);
    }

    [Fact]
    public async Task DeletedSearchListsApprovedMailboxesWithoutRetainedRows()
    {
        var options = Options();
        var source = new GraphDeletedMailSearchSource(
            new GraphMailClient(new FixedCredential(), options.BaseUri, new HttpClient(new DelegateHandler(_ => Response(HttpStatusCode.OK, "{}")))),
            new MailboxEstate([Mailbox("mailbox-zero", "zero@example.test", "inbox-zero")]),
            new MimeKitPdfPigOpenXmlIntakeSourceReader(TimeProvider.System));

        var mailbox = Assert.Single(await source.ListMailboxesAsync(CancellationToken.None));

        Assert.Equal(StableMailboxId("mailbox-zero"), mailbox.MailboxId);
        Assert.True(mailbox.IsPolled);
    }

    [Fact]
    public async Task DeletedSearchTurnsHttpTimeoutIntoUnavailable()
    {
        var options = Options();
        var source = new GraphDeletedMailSearchSource(
            new GraphMailClient(new FixedCredential(), options.BaseUri, new HttpClient(
                new DelegateHandler(_ => throw new TaskCanceledException("timeout")))),
            new MailboxEstate([Mailbox(options.MailboxId, options.MailboxAddress, options.InboxFolderId)]),
            new MimeKitPdfPigOpenXmlIntakeSourceReader(TimeProvider.System));

        var result = await source.SearchAsync(null, "needle", 100, CancellationToken.None);

        Assert.Equal(DeletedMailSearchState.Unavailable, result.State);
    }

    [Fact]
    public async Task DeletedSearchDoesNotTurnCallerCancellationIntoUnavailable()
    {
        var options = Options();
        var source = new GraphDeletedMailSearchSource(
            new GraphMailClient(new FixedCredential(), options.BaseUri, new HttpClient(
                new DelegateHandler(_ => throw new TaskCanceledException("cancelled")))),
            new MailboxEstate([Mailbox(options.MailboxId, options.MailboxAddress, options.InboxFolderId)]),
            new MimeKitPdfPigOpenXmlIntakeSourceReader(TimeProvider.System));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<TaskCanceledException>(() =>
            source.SearchAsync(null, "needle", 100, cancellation.Token));
    }

    [Theory]
    [InlineData("malformed-json")]
    [InlineData("missing-id")]
    [InlineData("missing-received-time")]
    [InlineData("foreign-parent")]
    [InlineData("escaped-next-link")]
    [InlineData("non-object-folder-root")]
    [InlineData("non-object-page-root")]
    [InlineData("missing-value")]
    [InlineData("non-array-value")]
    [InlineData("invalid-next-link")]
    [InlineData("relative-next-link")]
    public async Task DeletedSearchTurnsInvalidGraphResponsesIntoUnavailable(string responseCase)
    {
        var handler = new DelegateHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/mailFolders/deleteditems", StringComparison.Ordinal))
            {
                return Response(
                    HttpStatusCode.OK,
                    responseCase == "non-object-folder-root"
                        ? "[]"
                        : """{"id":"deleted-folder"}""");
            }

            var body = responseCase switch
            {
                "malformed-json" => "{",
                "missing-id" => """{"value":[{"parentFolderId":"deleted-folder","receivedDateTime":"2026-07-31T10:00:00Z"}]}""",
                "missing-received-time" => """{"value":[{"id":"deleted-1","parentFolderId":"deleted-folder"}]}""",
                "foreign-parent" => """{"value":[{"id":"deleted-1","parentFolderId":"inbox-folder","receivedDateTime":"2026-07-31T10:00:00Z"}]}""",
                "escaped-next-link" => """{"value":[],"@odata.nextLink":"https://graph.microsoft.com/v1.0/users/other-mailbox/mailFolders/deleted-folder/messages?$top=100"}""",
                "non-object-page-root" => "[]",
                "missing-value" => "{}",
                "non-array-value" => """{"value":{}}""",
                "invalid-next-link" => """{"value":[],"@odata.nextLink":"not a URI"}""",
                "relative-next-link" => """{"value":[],"@odata.nextLink":"/v1.0/users/mailbox-id/mailFolders/deleted-folder/messages?$top=100"}""",
                _ => throw new InvalidOperationException("Unknown Graph response case.")
            };
            return Response(HttpStatusCode.OK, body);
        });
        var options = Options();
        var source = new GraphDeletedMailSearchSource(
            new GraphMailClient(new FixedCredential(), options.BaseUri, new HttpClient(handler)),
            new MailboxEstate([Mailbox(options.MailboxId, options.MailboxAddress, options.InboxFolderId)]),
            new MimeKitPdfPigOpenXmlIntakeSourceReader(TimeProvider.System));

        var result = await source.SearchAsync(null, "needle", 100, CancellationToken.None);

        Assert.Equal(DeletedMailSearchState.Unavailable, result.State);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task InboxUsesImmutableIdsAndContinuesWithDeltaCursorWithoutMutation()
    {
        var requests = new List<(HttpMethod Method, string Uri, string? Prefer)>();
        var handler = new DelegateHandler(request =>
        {
            requests.Add((request.Method, request.RequestUri!.AbsoluteUri,
                request.Headers.TryGetValues("Prefer", out var values) ? values.Single() : null));
            return request.RequestUri.AbsolutePath.EndsWith("/$value", StringComparison.Ordinal)
                ? Response(HttpStatusCode.OK, "From: sender@example.test\r\nMessage-Id: <one@example.test>\r\n\r\nBody", "message/rfc822")
                : Response(HttpStatusCode.OK,
                    """{"value":[{"id":"immutable-1","parentFolderId":"inbox-folder","receivedDateTime":"2026-07-31T10:00:00Z"}],"@odata.deltaLink":"https://graph.microsoft.com/v1.0/users/mailbox-id/mailFolders/inbox-folder/messages/delta?$deltatoken=final"}""");
        });
        var options = Options();
        var source = new GraphApprovedInboxSource(
            new GraphMailClient(new FixedCredential(), options.BaseUri, new HttpClient(handler)));

        var page = await source.ReadAsync(
            Lease(options.MailboxId, options.MailboxAddress, options.InboxFolderId, null, "lease"),
            10,
            CancellationToken.None);

        var message = Assert.Single(page.Messages);
        Assert.Equal("immutable-1", message.ImmutableMessageId);
        Assert.Equal(page.NextCursor, message.NextCursor);
        Assert.All(requests, request => Assert.Equal(HttpMethod.Get, request.Method));
        Assert.All(requests, request => Assert.Equal("IdType=\"ImmutableId\"", request.Prefer));
    }

    [Fact]
    public async Task InboxSkipsMessagesBeforeTheActivationBoundaryAndAdvancesTheOpaqueCursor()
    {
        var requests = new List<string>();
        var handler = new DelegateHandler(request =>
        {
            requests.Add(request.RequestUri!.AbsolutePath);
            return Response(HttpStatusCode.OK,
                """{"value":[{"id":"historic","parentFolderId":"inbox-folder","receivedDateTime":"2026-07-31T09:59:59Z"}],"@odata.nextLink":"https://graph.microsoft.com/v1.0/users/mailbox-id/mailFolders/inbox-folder/messages/delta?$skiptoken=opaque"}""");
        });
        var options = Options();
        var source = new GraphApprovedInboxSource(
            new GraphMailClient(new FixedCredential(), options.BaseUri, new HttpClient(handler)));
        var lease = Lease(options.MailboxId, options.MailboxAddress, options.InboxFolderId, null, "lease")
            with { StartBoundaryUtc = new DateTimeOffset(2026, 7, 31, 10, 0, 0, TimeSpan.Zero) };

        var page = await source.ReadAsync(lease, 10, CancellationToken.None);

        Assert.Empty(page.Messages);
        Assert.DoesNotContain(requests, path => path.EndsWith("/$value", StringComparison.Ordinal));
        var cursor = GraphCursor.Parse(page.NextCursor, new Uri("https://example.test"));
        Assert.Contains("$skiptoken=opaque", cursor.PageUri.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NotifiedInboxMessageIsVerifiedInTheExactFolderBeforeMimeIsRead()
    {
        var requests = new List<string>();
        var handler = new DelegateHandler(request =>
        {
            requests.Add(request.RequestUri!.AbsolutePath);
            return request.RequestUri.AbsolutePath.EndsWith("/$value", StringComparison.Ordinal)
                ? Response(HttpStatusCode.OK, "Message-Id: <one@example.test>\r\n\r\nBody", "message/rfc822")
                : Response(HttpStatusCode.OK,
                    """{"id":"immutable-1","parentFolderId":"inbox-folder","receivedDateTime":"2026-07-31T10:00:00Z"}""");
        });
        var options = Options();
        var source = new GraphApprovedInboxSource(
            new GraphMailClient(new FixedCredential(), options.BaseUri, new HttpClient(handler)));

        var message = await source.ReadNotifiedAsync(
            Lease(options.MailboxId, options.MailboxAddress, options.InboxFolderId, null, "lease"),
            "immutable-1",
            CancellationToken.None);

        Assert.NotNull(message);
        Assert.Equal(2, requests.Count);
        Assert.EndsWith("/messages/immutable-1", requests[0], StringComparison.Ordinal);
        Assert.EndsWith("/messages/immutable-1/$value", requests[1], StringComparison.Ordinal);
    }

    /// <summary>
    /// Microsoft Graph guarantees only "at least the updated properties" on a sparse
    /// delta entry, so an already-known item can recur without receivedDateTime (e.g. a
    /// read/flag change) even though the initial call selected it. That must not stall
    /// the mailbox poll cursor the way it did in production (MAIL-029).
    /// </summary>
    [Fact]
    public async Task InboxSkipsASparseDeltaItemMissingReceivedDateTimeWithoutFetchingMime()
    {
        var requests = new List<string>();
        var handler = new DelegateHandler(request =>
        {
            requests.Add(request.RequestUri!.AbsolutePath);
            return Response(HttpStatusCode.OK,
                """{"value":[{"id":"sparse-update-1","parentFolderId":"inbox-folder"}],"@odata.deltaLink":"https://graph.microsoft.com/v1.0/users/mailbox-id/mailFolders/inbox-folder/messages/delta?$deltatoken=final"}""");
        });
        var options = Options();
        var source = new GraphApprovedInboxSource(
            new GraphMailClient(new FixedCredential(), options.BaseUri, new HttpClient(handler)));

        var page = await source.ReadAsync(
            Lease(options.MailboxId, options.MailboxAddress, options.InboxFolderId, null, "lease"),
            10,
            CancellationToken.None);

        Assert.Empty(page.Messages);
        Assert.DoesNotContain(requests, path => path.EndsWith("/$value", StringComparison.Ordinal));
        var cursor = GraphCursor.Parse(page.NextCursor, new Uri("https://example.test"));
        Assert.Equal(
            "v1.0/users/mailbox-id/mailFolders/inbox-folder/messages/delta",
            cursor.PageUri.GetComponents(UriComponents.Path, UriFormat.Unescaped));
    }

    /// <summary>
    /// A present-but-unparseable receivedDateTime is a different, reportable fault from
    /// Graph genuinely omitting it, and must still surface rather than being silently
    /// treated as a benign sparse update (review finding on MAIL-029).
    /// </summary>
    [Fact]
    public async Task InboxThrowsOnAPresentButUnparseableReceivedDateTimeRatherThanSkipping()
    {
        var handler = new DelegateHandler(_ => Response(HttpStatusCode.OK,
            """{"value":[{"id":"corrupt-1","parentFolderId":"inbox-folder","receivedDateTime":"not-a-date"}],"@odata.deltaLink":"https://graph.microsoft.com/v1.0/users/mailbox-id/mailFolders/inbox-folder/messages/delta?$deltatoken=final"}"""));
        var options = Options();
        var source = new GraphApprovedInboxSource(
            new GraphMailClient(new FixedCredential(), options.BaseUri, new HttpClient(handler)));

        await Assert.ThrowsAsync<InvalidDataException>(() => source.ReadAsync(
            Lease(options.MailboxId, options.MailboxAddress, options.InboxFolderId, null, "lease"),
            10,
            CancellationToken.None));
    }

    [Fact]
    public async Task InboxAcceptsTheGraphCanonicalODataCursorForTheExactFolder()
    {
        var handler = new DelegateHandler(_ => Response(
            HttpStatusCode.OK,
            """{"value":[],"@odata.deltaLink":"https://graph.microsoft.com/v1.0/users/mailbox-id/mailFolders('inbox-folder==')/messages/delta?$deltatoken=final"}"""));
        var options = GraphApprovedMailboxOptions.Create(
            "https://graph.microsoft.com/v1.0/",
            "mailbox-id",
            "instructions@collisionengineers.co.uk",
            "inbox-folder==",
            "sent-folder==");
        var source = new GraphApprovedInboxSource(
            new GraphMailClient(new FixedCredential(), options.BaseUri, new HttpClient(handler)));

        var page = await source.ReadAsync(
            Lease(options.MailboxId, options.MailboxAddress, options.InboxFolderId, null, "lease"),
            10,
            CancellationToken.None);

        Assert.Empty(page.Messages);
        var cursor = GraphCursor.Parse(page.NextCursor, new Uri("https://example.test"));
        Assert.Equal(
            "v1.0/users/mailbox-id/mailFolders('inbox-folder==')/messages/delta",
            cursor.PageUri.GetComponents(UriComponents.Path, UriFormat.Unescaped));
    }

    /// <summary>
    /// The lease's own folder is what bounds the read. A persisted cursor pointing at a
    /// different folder of the same mailbox is refused before Graph is called, so the
    /// estate cannot be widened by a stale cursor.
    /// </summary>
    [Fact]
    public async Task InboxRejectsACursorForAnotherFolderOfTheSameMailboxBeforeCallingGraph()
    {
        var calls = 0;
        var handler = new DelegateHandler(_ => { calls++; return Response(HttpStatusCode.OK, "{}"); });
        var options = Options();
        var source = new GraphApprovedInboxSource(
            new GraphMailClient(new FixedCredential(), options.BaseUri, new HttpClient(handler)));
        var otherFolderCursor = GraphCursor.Serialize(
            new Uri("https://graph.microsoft.com/v1.0/users/mailbox-id/mailFolders/other-folder/messages/delta?$deltatoken=x"),
            0);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => source.ReadAsync(
            Lease(options.MailboxId, options.MailboxAddress, options.InboxFolderId, otherFolderCursor, "lease"),
            10,
            CancellationToken.None));

        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData("has space", "inbox-folder")]
    [InlineData("mailbox-id", "has space")]
    [InlineData("mailbox-id", " ")]
    public async Task InboxRefusesALeaseWithoutAnExactMailboxAndFolderIdentity(
        string mailboxId,
        string inboxFolderIdentity)
    {
        var calls = 0;
        var handler = new DelegateHandler(_ => { calls++; return Response(HttpStatusCode.OK, "{}"); });
        var options = Options();
        var source = new GraphApprovedInboxSource(
            new GraphMailClient(new FixedCredential(), options.BaseUri, new HttpClient(handler)));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => source.ReadAsync(
            Lease(mailboxId, options.MailboxAddress, inboxFolderIdentity, null, "lease"),
            10,
            CancellationToken.None));

        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task InboxReportsATenantThatHasNotAdmittedTheApplicationToThisMailbox(
        HttpStatusCode status)
    {
        var handler = new DelegateHandler(_ => Response(status, "{}"));
        var options = Options();
        var source = new GraphApprovedInboxSource(
            new GraphMailClient(new FixedCredential(), options.BaseUri, new HttpClient(handler)));

        await Assert.ThrowsAsync<ApprovedMailboxAccessDeniedException>(() => source.ReadAsync(
            Lease(options.MailboxId, options.MailboxAddress, options.InboxFolderId, null, "lease"),
            10,
            CancellationToken.None));
    }

    /// <summary>
    /// The same singleton client reads a second mailbox in the same tick without being
    /// rebuilt, because it no longer closes over one mailbox.
    /// </summary>
    [Fact]
    public async Task OneClientReadsTwoMailboxesEachInsideItsOwnFolder()
    {
        var paths = new List<string>();
        var handler = new DelegateHandler(request =>
        {
            paths.Add(request.RequestUri!.AbsolutePath);
            var mailbox = request.RequestUri.AbsolutePath.Contains("mailbox-two", StringComparison.Ordinal)
                ? "mailbox-two"
                : "mailbox-id";
            var folder = string.Equals(mailbox, "mailbox-two", StringComparison.Ordinal)
                ? "inbox-two"
                : "inbox-folder";
            return Response(
                HttpStatusCode.OK,
                $$"""{"value":[],"@odata.deltaLink":"https://graph.microsoft.com/v1.0/users/{{mailbox}}/mailFolders/{{folder}}/messages/delta?$deltatoken=final"}""");
        });
        var options = Options();
        var source = new GraphApprovedInboxSource(
            new GraphMailClient(new FixedCredential(), options.BaseUri, new HttpClient(handler)));

        await source.ReadAsync(
            Lease("mailbox-id", "a@collisionengineers.co.uk", "inbox-folder", null, "lease-1"),
            10,
            CancellationToken.None);
        await source.ReadAsync(
            Lease("mailbox-two", "b@collisionengineers.co.uk", "inbox-two", null, "lease-2"),
            10,
            CancellationToken.None);

        Assert.Contains(
            paths,
            path => path.Contains("mailbox-id/mailFolders/inbox-folder", StringComparison.Ordinal));
        Assert.Contains(
            paths,
            path => path.Contains("mailbox-two/mailFolders/inbox-two", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExpiredDeltaCursorResetsToTheExactApprovedFolder()
    {
        var calls = 0;
        var handler = new DelegateHandler(request =>
        {
            calls++;
            if (calls == 1)
            {
                return Response(HttpStatusCode.Gone, "{}");
            }
            Assert.Contains("mailFolders/inbox-folder/messages/delta", request.RequestUri!.AbsoluteUri, StringComparison.Ordinal);
            return Response(HttpStatusCode.OK,
                """{"value":[],"@odata.deltaLink":"https://graph.microsoft.com/v1.0/users/mailbox-id/mailFolders/inbox-folder/messages/delta?$deltatoken=reset"}""");
        });
        var options = Options();
        var source = new GraphApprovedInboxSource(
            new GraphMailClient(new FixedCredential(), options.BaseUri, new HttpClient(handler)));
        var staleCursor = GraphCursor.Serialize(
            new Uri("https://graph.microsoft.com/v1.0/users/mailbox-id/mailFolders/inbox-folder/messages/delta?$deltatoken=stale"),
            0);

        var page = await source.ReadAsync(
            Lease(options.MailboxId, options.MailboxAddress, options.InboxFolderId, staleCursor, "lease"),
            10,
            CancellationToken.None);

        Assert.Empty(page.Messages);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task InboxRejectsPersistedCursorForAnotherFolderBeforeCallingGraph()
    {
        var calls = 0;
        var handler = new DelegateHandler(_ => { calls++; return Response(HttpStatusCode.OK, "{}"); });
        var options = Options();
        var source = new GraphApprovedInboxSource(
            new GraphMailClient(new FixedCredential(), options.BaseUri, new HttpClient(handler)));
        var escapedCursor = GraphCursor.Serialize(
            new Uri("https://graph.microsoft.com/v1.0/users/other/mailFolders/other/messages/delta?$deltatoken=x"),
            0);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => source.ReadAsync(
            Lease(options.MailboxId, options.MailboxAddress, options.InboxFolderId, escapedCursor, "lease"),
            10,
            CancellationToken.None));

        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("other-folder")]
    public async Task InboxRejectsAnItemOutsideTheExactApprovedFolderBeforeReadingMime(string? parentFolderId)
    {
        var mimeCalls = 0;
        var parentProperty = parentFolderId is null
            ? string.Empty
            : $",\"parentFolderId\":\"{parentFolderId}\"";
        var handler = new DelegateHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/$value", StringComparison.Ordinal))
            {
                mimeCalls++;
                return Response(HttpStatusCode.OK, "Body", "message/rfc822");
            }
            return Response(HttpStatusCode.OK,
                $"{{\"value\":[{{\"id\":\"immutable-1\"{parentProperty},\"receivedDateTime\":\"2026-07-31T10:00:00Z\"}}],\"@odata.deltaLink\":\"https://graph.microsoft.com/v1.0/users/mailbox-id/mailFolders/inbox-folder/messages/delta?$deltatoken=final\"}}");
        });
        var options = Options();
        var source = new GraphApprovedInboxSource(
            new GraphMailClient(new FixedCredential(), options.BaseUri, new HttpClient(handler)));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => source.ReadAsync(
            Lease(options.MailboxId, options.MailboxAddress, options.InboxFolderId, null, "lease"),
            10,
            CancellationToken.None));

        Assert.Equal(0, mimeCalls);
    }

    [Fact]
    public async Task SentThrottlingRetainsTheProviderRetryDelay()
    {
        var handler = new DelegateHandler(_ =>
        {
            var response = Response(HttpStatusCode.TooManyRequests, "{}");
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(17));
            return response;
        });
        var options = Options();
        var source = new GraphApprovedSentSource(
            new GraphMailClient(new FixedCredential(), options.BaseUri, new HttpClient(handler)));

        var error = await Assert.ThrowsAsync<ApprovedSentSourceThrottledException>(() => source.ReadAsync(
            new(options.MailboxId, options.MailboxAddress, options.SentFolderId, null, "lease",
                Guid.NewGuid(), 1, new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero)),
            10,
            CancellationToken.None));

        Assert.Equal(TimeSpan.FromSeconds(17), error.RetryAfter);
    }

    [Fact]
    public async Task SentDeltaParsesOperationMarkerAndExactMimeAttachmentHash()
    {
        var operationId = Guid.NewGuid();
        var approvedMailboxId = Guid.NewGuid();
        var attachmentBytes = new byte[] { 1, 2, 3, 4 };
        var mime = $"Message-Id: <sent@example.test>\r\nX-Pegasus-Operation-Id: {operationId:D}\r\nX-Pegasus-Mailbox-Id: {approvedMailboxId:D}\r\nX-Pegasus-Mailbox-Generation: 3\r\nX-Pegasus-Payload-Sha256: {new string('A', 64)}\r\nContent-Type: multipart/mixed; boundary=part\r\n\r\n--part\r\nContent-Type: text/plain\r\n\r\nBody\r\n--part\r\nContent-Type: application/pdf\r\nContent-Disposition: attachment; filename=report.pdf\r\nContent-Transfer-Encoding: base64\r\n\r\n{Convert.ToBase64String(attachmentBytes)}\r\n--part--\r\n";
        var handler = new DelegateHandler(request =>
            request.RequestUri!.AbsolutePath.EndsWith("/$value", StringComparison.Ordinal)
                ? Response(HttpStatusCode.OK, mime, "message/rfc822")
                : Response(HttpStatusCode.OK,
                    """{"value":[{"id":"immutable-sent","parentFolderId":"sent-items","sentDateTime":"2026-09-06T10:00:00Z","conversationId":"conversation","internetMessageId":"<sent@example.test>"}],"@odata.deltaLink":"https://graph.microsoft.com/v1.0/users/mailbox-id/mailFolders/sent-items/messages/delta?$deltatoken=final"}"""));
        var options = Options();
        var source = new GraphApprovedSentSource(
            new GraphMailClient(new FixedCredential(), options.BaseUri, new HttpClient(handler)));
        var lease = new ApprovedSentPollLease(
            "mailbox-id", options.MailboxAddress, "sent-items", null, "lease",
            approvedMailboxId, 3, new DateTimeOffset(2026, 9, 6, 9, 0, 0, TimeSpan.Zero));

        var page = await source.ReadAsync(lease, 10, CancellationToken.None);

        var provenance = Assert.Single(page.Items).Provenance!;
        Assert.Equal(operationId, provenance.StaffMailOperationId);
        Assert.Equal(approvedMailboxId, provenance.StaffMailMailboxId);
        Assert.Equal(3, provenance.StaffMailMailboxGeneration);
        Assert.Equal(new string('A', 64), provenance.StaffMailPayloadHash);
        Assert.Equal(
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(attachmentBytes)),
            Assert.Single(provenance.AttachmentSha256!));
        Assert.Equal(new DateTimeOffset(2026, 9, 6, 10, 0, 0, TimeSpan.Zero), provenance.SentAtUtc);
    }

    [Fact]
    public async Task SentDeltaAdvancesPastHistoricItemsWithoutReadingOrRetainingMime()
    {
        var mimeReads = 0;
        var handler = new DelegateHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/$value", StringComparison.Ordinal))
            {
                mimeReads++;
                return Response(HttpStatusCode.OK, "Message-Id: <old@example.test>\r\n\r\nOld", "message/rfc822");
            }
            return Response(HttpStatusCode.OK,
                """{"value":[{"id":"historic","parentFolderId":"sent-items","sentDateTime":"2026-09-06T08:59:59Z","conversationId":"old","internetMessageId":"<old@example.test>"}],"@odata.deltaLink":"https://graph.microsoft.com/v1.0/users/mailbox-id/mailFolders/sent-items/messages/delta?$deltatoken=final"}""");
        });
        var options = Options();
        var source = new GraphApprovedSentSource(
            new GraphMailClient(new FixedCredential(), options.BaseUri, new HttpClient(handler)));

        var page = await source.ReadAsync(
            new("mailbox-id", options.MailboxAddress, "sent-items", null, "lease",
                Guid.NewGuid(), 2, new DateTimeOffset(2026, 9, 6, 9, 0, 0, TimeSpan.Zero)),
            10, CancellationToken.None);

        Assert.Empty(page.Items);
        Assert.False(page.HasMore);
        Assert.Equal(0, mimeReads);
        Assert.Contains("deltatoken", page.NextCursor, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InboxCursorContinuesInsideAnOversizedGraphPageWithoutDroppingMessages()
    {
        var handler = new DelegateHandler(request => request.RequestUri!.AbsolutePath.EndsWith("/$value", StringComparison.Ordinal)
            ? Response(HttpStatusCode.OK, $"Message-Id: <{request.RequestUri.Segments[^2]}@example.test>\r\n\r\nBody", "message/rfc822")
            : Response(HttpStatusCode.OK,
                """{"value":[{"id":"immutable-1","parentFolderId":"inbox-folder","receivedDateTime":"2026-07-31T10:00:00Z"},{"id":"immutable-2","parentFolderId":"inbox-folder","receivedDateTime":"2026-07-31T10:01:00Z"}],"@odata.deltaLink":"https://graph.microsoft.com/v1.0/users/mailbox-id/mailFolders/inbox-folder/messages/delta?$deltatoken=final"}"""));
        var options = Options();
        var source = new GraphApprovedInboxSource(
            new GraphMailClient(new FixedCredential(), options.BaseUri, new HttpClient(handler)));

        var first = await source.ReadAsync(
            Lease(options.MailboxId, options.MailboxAddress, options.InboxFolderId, null, "lease-1"),
            1,
            CancellationToken.None);
        var second = await source.ReadAsync(
            Lease(options.MailboxId, options.MailboxAddress, options.InboxFolderId, first.NextCursor, "lease-2"),
            1,
            CancellationToken.None);

        Assert.Equal("immutable-1", Assert.Single(first.Messages).ImmutableMessageId);
        Assert.Equal("immutable-2", Assert.Single(second.Messages).ImmutableMessageId);
        Assert.NotEqual(first.NextCursor, second.NextCursor);
    }

    private static GraphApprovedMailboxOptions Options() => GraphApprovedMailboxOptions.Create(
        "https://graph.microsoft.com/v1.0/",
        "mailbox-id",
        "instructions@collisionengineers.co.uk",
        "inbox-folder",
        "sent-folder");

    private static HttpResponseMessage Response(HttpStatusCode status, string body, string mediaType = "application/json") =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, mediaType) };

    private sealed class UploadProgressFake : IStaffMailUploadProgress
    {
        public StaffMailUploadSession? Current { get; set; }
        public bool Completed { get; private set; }
        public Task<StaffMailUploadSession?> GetAsync(Guid operationId, Guid attachmentVersionId,
            CancellationToken cancellationToken) => Task.FromResult(Current);

        public Task SaveAsync(Guid operationId, Guid attachmentVersionId,
            StaffMailUploadSession session, CancellationToken cancellationToken) { Current = session; return Task.CompletedTask; }

        public Task CompleteAsync(Guid operationId, Guid attachmentVersionId,
            CancellationToken cancellationToken) { Completed = true; return Task.CompletedTask; }
    }

    private sealed class FixedCredential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            new("token", DateTimeOffset.UtcNow.AddHours(1));

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
            ValueTask.FromResult(GetToken(requestContext, cancellationToken));
    }

    private sealed class DelegateHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(handler(request));
    }

    private sealed class MailboxEstate(IReadOnlyList<ApprovedIntakeMailbox> mailboxes)
        : IApprovedIntakeMailboxes
    {
        public Task<IReadOnlyList<ApprovedIntakeMailbox>> ListPollableAsync(
            CancellationToken cancellationToken) => Task.FromResult(mailboxes);
    }

    private static ApprovedIntakeMailbox Mailbox(string graphId, string address, string inboxFolderId) =>
        new(StableMailboxId(graphId), graphId, address, inboxFolderId, DateTimeOffset.MinValue);

    private static ApprovedInboxPollLease Lease(
        string graphId,
        string address,
        string inboxFolderId,
        string? cursor,
        string leaseToken) =>
        new(StableMailboxId(graphId), graphId, address, inboxFolderId, DateTimeOffset.MinValue, cursor, leaseToken);

    private static Guid StableMailboxId(string graphId) => graphId switch
    {
        "mailbox-two" => Guid.Parse("22222222-2222-2222-2222-222222222222"),
        "mailbox-zero" => Guid.Parse("00000000-0000-0000-0000-000000000010"),
        _ => Guid.Parse("11111111-1111-1111-1111-111111111111")
    };
}
