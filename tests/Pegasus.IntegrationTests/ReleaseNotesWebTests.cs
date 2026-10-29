using System.Data.Common;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Pegasus.Infrastructure.Persistence;
using Pegasus.Web.Presentation;
using static Pegasus.IntegrationTests.CaseWebTestSupport;

namespace Pegasus.IntegrationTests;

/// <summary>
/// Release notes (FRD-12 What's new, FRD-17 Release notes, ADR-0054), driven
/// through the real pages, Core and the EF store over the seeded LocalDB: an
/// Administrator writes and publishes; everyone sees the newest published
/// note once, until they press Got it; nobody else can write.
/// </summary>
[Trait("Category", "SqlServer")]
public sealed partial class ReleaseNotesWebTests
{
    private const string AdministrationPage = "/Administration/ReleaseNotes";
    private const string EditPage = "/Administration/ReleaseNotes/Edit";
    private static readonly Guid Reader = Guid.Parse("00000000-0000-4000-8000-0000000000a1");

    [Fact]
    public async Task OnlyAnAdministratorCanOpenOrPostTheReleaseNotesArea()
    {
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var user = CreateClient(factory, "User", Reader);

        using var list = await user.GetAsync(AdministrationPage);
        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);

        using var save = await user.PostAsync($"{EditPage}?handler=Save", new FormUrlEncodedContent(new Dictionary<string, string>()));
        Assert.Equal(HttpStatusCode.Forbidden, save.StatusCode);

        using var engineer = CreateClient(factory, "Engineer", Reader);
        using var publish = await engineer.PostAsync($"{EditPage}?handler=Publish", new FormUrlEncodedContent(new Dictionary<string, string>()));
        Assert.Equal(HttpStatusCode.Forbidden, publish.StatusCode);
    }

    [Fact]
    public async Task AnAdministratorPublishesANoteAndEachPersonSeesItOnceUntilGotIt()
    {
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var administrator = CreateClient(factory);
        using var user = CreateClient(factory, "User", Reader);

        // Nothing published: no dialog for anyone, and the list is empty.
        Assert.DoesNotContain("whats-new-dialog", await GetHtmlAsync(user, "/"), StringComparison.Ordinal);
        Assert.Contains("No release notes", await GetHtmlAsync(user, "/ReleaseNotes"), StringComparison.Ordinal);

        // A draft.
        var newForm = await GetHtmlAsync(administrator, EditPage);
        using var saved = await administrator.PostAsync($"{EditPage}?handler=Save", Form(newForm, new()
        {
            ["Id"] = string.Empty,
            ["ExpectedRowVersion"] = "0",
            ["OperationKey"] = Guid.NewGuid().ToString("N"),
            ["Title"] = "Repair Spec and images",
            ["Body"] = "The Estimate section is now Repair Spec.\r\n\r\n- Images carry their report role\r\n- Produce PDF previews the report"
        }));
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        var editLocation = saved.Headers.Location!.OriginalString;
        Assert.Contains(EditPage, editLocation, StringComparison.OrdinalIgnoreCase);
        var draftPage = await GetHtmlAsync(administrator, editLocation);
        Assert.Contains("Release note saved.", draftPage, StringComparison.Ordinal);
        Assert.Contains("value=\"Repair Spec and images\"", draftPage, StringComparison.Ordinal);
        Assert.DoesNotContain("whats-new-dialog", await GetHtmlAsync(user, "/"), StringComparison.Ordinal);

        var list = await GetHtmlAsync(administrator, AdministrationPage);
        Assert.Contains("Repair Spec and images", list, StringComparison.Ordinal);
        Assert.Contains(">Draft<", list, StringComparison.Ordinal);

        // Publish stamps the build and freezes the note.
        var id = InputValue(draftPage, "Id");
        using var published = await administrator.PostAsync($"{EditPage}/{id}?handler=Publish", Form(draftPage, new()
        {
            ["Id"] = id,
            ["ExpectedRowVersion"] = InputValue(draftPage, "ExpectedRowVersion"),
            ["OperationKey"] = Guid.NewGuid().ToString("N"),
            ["Title"] = "Repair Spec and images",
            ["Body"] = InputValue(draftPage, "Body", textarea: true)
        }));
        Assert.Equal(HttpStatusCode.Redirect, published.StatusCode);
        list = await GetHtmlAsync(administrator, AdministrationPage);
        Assert.Contains("Release note published.", list, StringComparison.Ordinal);
        Assert.Contains(">Published<", list, StringComparison.Ordinal);
        var readOnly = await GetHtmlAsync(administrator, $"{EditPage}/{id}");
        Assert.Contains("data-release-note-published", readOnly, StringComparison.Ordinal);
        Assert.DoesNotContain("data-release-note-form", readOnly, StringComparison.Ordinal);

        // Everyone sees it once.
        var userHome = await GetHtmlAsync(user, "/");
        Assert.Contains("data-dialog=\"whats-new-dialog\" data-dialog-open-on-load=\"true\"", userHome, StringComparison.Ordinal);
        Assert.Contains("Repair Spec and images", userHome, StringComparison.Ordinal);
        Assert.Contains("<li>Images carry their report role</li>", userHome, StringComparison.Ordinal);
        Assert.Contains("whats-new-dialog", await GetHtmlAsync(administrator, "/"), StringComparison.Ordinal);

        using var acknowledged = await user.PostAsync($"/ReleaseNotes?handler=Acknowledge&id={id}", Form(userHome, new()
        {
            ["returnUrl"] = "/Cases"
        }));
        Assert.Equal(HttpStatusCode.Redirect, acknowledged.StatusCode);
        Assert.Equal("/Cases", acknowledged.Headers.Location!.OriginalString);
        Assert.DoesNotContain("whats-new-dialog", await GetHtmlAsync(user, "/"), StringComparison.Ordinal);
        // The Administrator has not pressed Got it, so theirs still opens.
        Assert.Contains("whats-new-dialog", await GetHtmlAsync(administrator, "/"), StringComparison.Ordinal);

        // The history lists it for everyone.
        var history = await GetHtmlAsync(user, "/ReleaseNotes");
        Assert.Contains($"data-release-note=\"{id}\"", history, StringComparison.Ordinal);
        Assert.Contains("<li>Produce PDF previews the report</li>", history, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LostResponsesToSaveAndPublishDoNotCreateAnotherNote()
    {
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var administrator = CreateClient(factory);
        var newForm = await GetHtmlAsync(administrator, EditPage);
        var key = Guid.NewGuid().ToString("N");
        var fields = new Dictionary<string, string>
        {
            ["Id"] = string.Empty,
            ["ExpectedRowVersion"] = "0",
            ["OperationKey"] = key,
            ["Title"] = "Release update",
            ["Body"] = "First body"
        };
        using var saved = await administrator.PostAsync($"{EditPage}?handler=Save", Form(newForm, new(fields)));
        using var replay = await administrator.PostAsync($"{EditPage}?handler=Save", Form(newForm, new(fields)));
        Assert.Equal(saved.Headers.Location, replay.Headers.Location);

        using var changed = await administrator.PostAsync($"{EditPage}?handler=Save", Form(newForm, new()
        {
            ["Id"] = string.Empty,
            ["ExpectedRowVersion"] = "0",
            ["OperationKey"] = key,
            ["Title"] = "Changed",
            ["Body"] = "First body"
        }));
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        Assert.Contains("changed before this edit was saved",
            await changed.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var draft = await GetHtmlAsync(administrator, saved.Headers.Location!.OriginalString);
        var id = InputValue(draft, "Id");
        var publishFields = new Dictionary<string, string>
        {
            ["Id"] = id,
            ["ExpectedRowVersion"] = InputValue(draft, "ExpectedRowVersion"),
            ["OperationKey"] = Guid.NewGuid().ToString("N"),
            ["Title"] = "Release update",
            ["Body"] = "Final body"
        };
        using var published = await administrator.PostAsync(
            $"{EditPage}/{id}?handler=Publish", Form(draft, new(publishFields)));
        using var publishedReplay = await administrator.PostAsync(
            $"{EditPage}/{id}?handler=Publish", Form(draft, new(publishFields)));
        Assert.Equal(HttpStatusCode.Redirect, published.StatusCode);
        Assert.Equal(published.Headers.Location, publishedReplay.Headers.Location);

        await using var scope = factory.Services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        var note = Assert.Single(await context.Set<ReleaseNoteEntity>().ToArrayAsync());
        Assert.Equal(id, note.Id.ToString("D"));
        Assert.Equal("Final body", note.Body);
        Assert.Equal("Published", note.Status);
    }

    /// <summary>
    /// Core takes no operation key on Publish; the row version alone makes a
    /// replay safe, so a saved draft posted with a malformed key, and its
    /// replay with none, publishes once (#1584). With no saved draft Publish
    /// is still refused. The list and the read-only note read their words from
    /// <see cref="OperatorLabels.ReleaseNotes"/> (#1583).
    /// </summary>
    [Fact]
    public async Task PublishNeedsASavedDraftButNoOperationKey()
    {
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var administrator = CreateClient(factory);
        var newForm = await GetHtmlAsync(administrator, EditPage);
        using var unsaved = await administrator.PostAsync($"{EditPage}?handler=Publish", Form(newForm, new()
        {
            ["Id"] = string.Empty,
            ["ExpectedRowVersion"] = "0",
            ["Title"] = "Keyless",
            ["Body"] = "One"
        }));
        Assert.Equal(HttpStatusCode.OK, unsaved.StatusCode);
        Assert.Contains(OperatorLabels.ReleaseNotes.SaveBeforePublish,
            await unsaved.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        using var saved = await administrator.PostAsync($"{EditPage}?handler=Save", Form(newForm, new()
        {
            ["Id"] = string.Empty,
            ["ExpectedRowVersion"] = "0",
            ["OperationKey"] = Guid.NewGuid().ToString("N"),
            ["Title"] = "Keyless",
            ["Body"] = "One"
        }));
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);
        var draft = await GetHtmlAsync(administrator, saved.Headers.Location!.OriginalString);
        var id = InputValue(draft, "Id");
        var publishFields = new Dictionary<string, string>
        {
            ["Id"] = id,
            ["ExpectedRowVersion"] = InputValue(draft, "ExpectedRowVersion"),
            ["OperationKey"] = "x",
            ["Title"] = "Keyless",
            ["Body"] = "One"
        };
        using var published = await administrator.PostAsync(
            $"{EditPage}/{id}?handler=Publish", Form(draft, new(publishFields)));
        publishFields.Remove("OperationKey");
        using var replay = await administrator.PostAsync(
            $"{EditPage}/{id}?handler=Publish", Form(draft, new(publishFields)));
        Assert.Equal(HttpStatusCode.Redirect, published.StatusCode);
        Assert.Equal(published.Headers.Location, replay.Headers.Location);

        var list = await GetHtmlAsync(administrator, AdministrationPage);
        Assert.Contains($"<th scope=\"col\">{OperatorLabels.ReleaseNotes.PublishedBy}</th>", list, StringComparison.Ordinal);
        Assert.Contains($">{OperatorLabels.ReleaseNotes.Published}<", list, StringComparison.Ordinal);
        var readOnly = await GetHtmlAsync(administrator, $"{EditPage}/{id}");
        Assert.Contains($"<dt>{OperatorLabels.ReleaseNotes.Published}</dt>", readOnly, StringComparison.Ordinal);

        await using var scope = factory.Services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        Assert.Equal("Published", Assert.Single(await context.Set<ReleaseNoteEntity>().ToArrayAsync()).Status);
    }

    [Fact]
    public async Task AStaleDraftSaveIsRefusedWithoutOverwritingTheNewerText()
    {
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var administrator = CreateClient(factory);
        var newForm = await GetHtmlAsync(administrator, EditPage);
        using var saved = await administrator.PostAsync($"{EditPage}?handler=Save", Form(newForm, new()
        {
            ["Id"] = string.Empty,
            ["ExpectedRowVersion"] = "0",
            ["OperationKey"] = Guid.NewGuid().ToString("N"),
            ["Title"] = "First",
            ["Body"] = "One"
        }));
        var draftPage = await GetHtmlAsync(administrator, saved.Headers.Location!.OriginalString);
        var id = InputValue(draftPage, "Id");
        var rowVersion = InputValue(draftPage, "ExpectedRowVersion");

        using var rewritten = await administrator.PostAsync($"{EditPage}/{id}?handler=Save", Form(draftPage, new()
        {
            ["Id"] = id, ["ExpectedRowVersion"] = rowVersion, ["OperationKey"] = Guid.NewGuid().ToString("N"),
            ["Title"] = "Second", ["Body"] = "Two"
        }));
        Assert.Equal(HttpStatusCode.Redirect, rewritten.StatusCode);

        using var stale = await administrator.PostAsync($"{EditPage}/{id}?handler=Save", Form(draftPage, new()
        {
            ["Id"] = id, ["ExpectedRowVersion"] = rowVersion, ["OperationKey"] = Guid.NewGuid().ToString("N"),
            ["Title"] = "Third", ["Body"] = "Three"
        }));
        Assert.Equal(HttpStatusCode.OK, stale.StatusCode);
        var stalePage = await stale.Content.ReadAsStringAsync();
        Assert.Contains("changed before this edit was saved", stalePage, StringComparison.Ordinal);
        Assert.Equal(rowVersion, InputValue(stalePage, "ExpectedRowVersion"));

        using var staleSaveRetry = await administrator.PostAsync($"{EditPage}/{id}?handler=Save", Form(stalePage, new()
        {
            ["Id"] = id, ["ExpectedRowVersion"] = InputValue(stalePage, "ExpectedRowVersion"), ["OperationKey"] = Guid.NewGuid().ToString("N"),
            ["Title"] = "Third", ["Body"] = "Three"
        }));
        Assert.Equal(HttpStatusCode.OK, staleSaveRetry.StatusCode);
        var staleSaveRetryPage = await staleSaveRetry.Content.ReadAsStringAsync();
        Assert.Contains("changed before this edit was saved", staleSaveRetryPage, StringComparison.Ordinal);
        Assert.Equal(rowVersion, InputValue(staleSaveRetryPage, "ExpectedRowVersion"));

        using var stalePublishRetry = await administrator.PostAsync($"{EditPage}/{id}?handler=Publish", Form(staleSaveRetryPage, new()
        {
            ["Id"] = id, ["ExpectedRowVersion"] = InputValue(staleSaveRetryPage, "ExpectedRowVersion"), ["OperationKey"] = Guid.NewGuid().ToString("N"),
            ["Title"] = "Third", ["Body"] = "Three"
        }));
        Assert.Equal(HttpStatusCode.OK, stalePublishRetry.StatusCode);
        var stalePublishRetryPage = await stalePublishRetry.Content.ReadAsStringAsync();
        Assert.Contains("changed before this edit was saved", stalePublishRetryPage, StringComparison.Ordinal);
        Assert.Equal(rowVersion, InputValue(stalePublishRetryPage, "ExpectedRowVersion"));

        var reloaded = await GetHtmlAsync(administrator, $"{EditPage}/{id}");
        Assert.Contains("value=\"Second\"", reloaded, StringComparison.Ordinal);
        Assert.DoesNotContain("value=\"Third\"", reloaded, StringComparison.Ordinal);
        Assert.Contains("data-release-note-form", reloaded, StringComparison.Ordinal);
    }

    /// <summary>
    /// A title of spaces passes the browser's required check; Save and Publish
    /// refuse it with Core's reason and no internal parameter name (#1581).
    /// </summary>
    [Fact]
    public async Task ABlankTitleIsRefusedWithItsReasonOnSaveAndPublish()
    {
        const string Refusal = "A release note title is 1 to 120 characters on one line.";
        using var factory = new IntakeWebApplicationFactory(useIntegrationTestAuthentication: true);
        using var administrator = CreateClient(factory);
        var newForm = await GetHtmlAsync(administrator, EditPage);
        using var blankSave = await administrator.PostAsync($"{EditPage}?handler=Save", Form(newForm, new()
        {
            ["Id"] = string.Empty,
            ["ExpectedRowVersion"] = "0",
            ["OperationKey"] = Guid.NewGuid().ToString("N"),
            ["Title"] = "   ",
            ["Body"] = "One"
        }));
        Assert.Equal(HttpStatusCode.OK, blankSave.StatusCode);
        var refusedSave = await blankSave.Content.ReadAsStringAsync();
        Assert.Contains(Refusal, refusedSave, StringComparison.Ordinal);
        Assert.DoesNotContain("(Parameter", refusedSave, StringComparison.Ordinal);

        using var saved = await administrator.PostAsync($"{EditPage}?handler=Save", Form(newForm, new()
        {
            ["Id"] = string.Empty,
            ["ExpectedRowVersion"] = "0",
            ["OperationKey"] = Guid.NewGuid().ToString("N"),
            ["Title"] = "First",
            ["Body"] = "One"
        }));
        var draftPage = await GetHtmlAsync(administrator, saved.Headers.Location!.OriginalString);
        var id = InputValue(draftPage, "Id");
        using var blankPublish = await administrator.PostAsync($"{EditPage}/{id}?handler=Publish", Form(draftPage, new()
        {
            ["Id"] = id,
            ["ExpectedRowVersion"] = InputValue(draftPage, "ExpectedRowVersion"),
            ["OperationKey"] = Guid.NewGuid().ToString("N"),
            ["Title"] = "   ",
            ["Body"] = "One"
        }));
        Assert.Equal(HttpStatusCode.OK, blankPublish.StatusCode);
        var refusedPublish = await blankPublish.Content.ReadAsStringAsync();
        Assert.Contains(Refusal, refusedPublish, StringComparison.Ordinal);
        Assert.DoesNotContain("(Parameter", refusedPublish, StringComparison.Ordinal);
    }

    /// <summary>
    /// The shell asks for the newest unacknowledged note on every full page. It
    /// is one read that joins the person's acknowledgement (before: two reads,
    /// the newest note with its body, then the acknowledgement), and the body is
    /// read only for a note that is about to be shown. The answer is kept for
    /// the person until a publish or their acknowledgement changes it.
    /// </summary>
    [Fact]
    public async Task TheShellReadsTheNewestNoteAndItsAcknowledgementInOneCommand()
    {
        var recorder = new SqlStatementCounter();
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            useIntegrationTestAuthentication: true,
            commandInterceptor: recorder);
        using var administrator = CreateClient(factory);
        using var user = CreateClient(factory, "User", Reader);
        var id = await PublishNoteAsync(administrator, "One read");

        recorder.Reset();
        var unread = await GetHtmlAsync(user, "/");
        Assert.Contains("whats-new-dialog", unread, StringComparison.Ordinal);
        // Not yet acknowledged: the joined read, then the body of the note shown.
        Assert.True(ReleaseNoteReads(recorder) <= 2, recorder.Describe());

        using var acknowledged = await user.PostAsync($"/ReleaseNotes?handler=Acknowledge&id={id}", Form(unread, new()
        {
            ["returnUrl"] = "/Cases"
        }));
        Assert.Equal(HttpStatusCode.Redirect, acknowledged.StatusCode);

        recorder.Reset();
        Assert.DoesNotContain("whats-new-dialog", await GetHtmlAsync(user, "/"), StringComparison.Ordinal);
        // Acknowledged: the joined read alone.
        Assert.Equal(1, ReleaseNoteReads(recorder));

        // The answer is kept for the person, so a repeat load reads nothing.
        recorder.Reset();
        Assert.DoesNotContain("whats-new-dialog", await GetHtmlAsync(user, "/"), StringComparison.Ordinal);
        Assert.Equal(0, ReleaseNoteReads(recorder));
    }

    /// <summary>
    /// The Administrator's list pages its notes at 50, newest change first,
    /// with Previous and Next; no page reads a note's body or more than a page
    /// and one row (#1542, R418).
    /// </summary>
    [Fact]
    public async Task TheAdministratorsListPagesTheNotesAndReadsNoBody()
    {
        var counter = new SqlStatementCounter();
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            useIntegrationTestAuthentication: true,
            commandInterceptor: counter);
        using var administrator = CreateClient(factory);
        var start = DateTimeOffset.UtcNow.AddDays(-1);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await using var context = await scope.ServiceProvider
                .GetRequiredService<IDbContextFactory<PegasusDbContext>>()
                .CreateDbContextAsync();
            for (var index = 0; index <= 50; index++)
            {
                var at = start.AddMinutes(index);
                context.Set<ReleaseNoteEntity>().Add(new ReleaseNoteEntity
                {
                    Id = Guid.NewGuid(),
                    Title = $"Paged note {index:D2}",
                    Body = "A release note body.",
                    Status = "Draft",
                    CreatedByStaffId = Reader,
                    CreatedAtUtc = at,
                    UpdatedAtUtc = at,
                    RowVersion = 1,
                    OperationKey = Guid.NewGuid().ToString("N"),
                    RequestHash = new string('0', 64)
                });
            }

            await context.SaveChangesAsync();
        }

        counter.Reset();
        var first = await GetHtmlAsync(administrator, AdministrationPage);
        var firstReads = counter.Reads.ToArray();
        var firstBodyReads = counter.CountContaining("[ReleaseNotes]", "[Body]");
        counter.Reset();
        var second = await GetHtmlAsync(administrator, $"{AdministrationPage}?pageNumber=2");
        var secondBodyReads = counter.CountContaining("[ReleaseNotes]", "[Body]");

        Assert.Equal(50, Regex.Count(first, "<tr data-release-note="));
        Assert.Contains(">Paged note 50</a>", first, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Release note pages\"", first, StringComparison.Ordinal);
        Assert.Contains("pageNumber=2\">Next</a>", first, StringComparison.Ordinal);
        Assert.DoesNotContain(">Previous</a>", first, StringComparison.Ordinal);
        Assert.Equal(1, Regex.Count(second, "<tr data-release-note="));
        Assert.Contains(">Paged note 00</a>", second, StringComparison.Ordinal);
        Assert.Contains("pageNumber=1\">Previous</a>", second, StringComparison.Ordinal);
        Assert.DoesNotContain(">Next</a>", second, StringComparison.Ordinal);
        Assert.True(firstBodyReads == 0 && secondBodyReads == 0, "A list page read a note's body.");
        Assert.All(
            firstReads.Where(read => read.CommandText.Contains("FROM [ReleaseNotes]", StringComparison.Ordinal)),
            read => Assert.True(read.Rows <= 51, $"A list read took {read.Rows} rows."));
    }

    [Fact]
    public async Task AnAcknowledgementPersistenceFailureIsNotReportedAsSuccess()
    {
        var interceptor = new NonDuplicateAcknowledgementFailureInterceptor();
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            useIntegrationTestAuthentication: true,
            commandInterceptor: interceptor);
        using var administrator = CreateClient(factory);
        using var user = CreateClient(factory, "User", Reader);
        var id = await PublishNoteAsync(administrator, "Persistence failure");
        var home = await GetHtmlAsync(user, "/");

        interceptor.Arm();
        using var failed = await user.PostAsync($"/ReleaseNotes?handler=Acknowledge&id={id}", Form(home, new()
        {
            ["returnUrl"] = "/Cases"
        }));
        Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        Assert.True(interceptor.InterceptedCount > 0);
        Assert.Contains("whats-new-dialog", await GetHtmlAsync(user, "/"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConcurrentAcknowledgementsRemainIdempotentWhenOneInsertLosesTheRace()
    {
        var interceptor = new DuplicateAcknowledgementRaceInterceptor();
        using var factory = new IntakeWebApplicationFactory(
            "Development",
            true,
            useIntegrationTestAuthentication: true,
            commandInterceptor: interceptor);
        using var administrator = CreateClient(factory);
        using var user = CreateClient(factory, "User", Reader);
        var id = await PublishNoteAsync(administrator, "Duplicate race");
        var home = await GetHtmlAsync(user, "/");

        interceptor.Arm();
        var first = user.PostAsync($"/ReleaseNotes?handler=Acknowledge&id={id}", Form(home, new()
        {
            ["returnUrl"] = "/Cases"
        }));
        var second = user.PostAsync($"/ReleaseNotes?handler=Acknowledge&id={id}", Form(home, new()
        {
            ["returnUrl"] = "/Cases"
        }));
        var responses = await Task.WhenAll(first, second);
        foreach (var response in responses)
        {
            using (response)
            {
                Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
                Assert.Equal("/Cases", response.Headers.Location!.OriginalString);
            }
        }
        Assert.Equal(2, interceptor.ExistenceChecks);

        await using var scope = factory.Services.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        Assert.Equal(1, await context.Set<ReleaseNoteAcknowledgementEntity>()
            .CountAsync(item => item.StaffId == Reader && item.ReleaseNoteId == id));
    }

    private static async Task<Guid> PublishNoteAsync(HttpClient administrator, string title)
    {
        var newForm = await GetHtmlAsync(administrator, EditPage);
        using var saved = await administrator.PostAsync($"{EditPage}?handler=Save", Form(newForm, new()
        {
            ["Id"] = string.Empty,
            ["ExpectedRowVersion"] = "0",
            ["OperationKey"] = Guid.NewGuid().ToString("N"),
            ["Title"] = title,
            ["Body"] = "A release note body."
        }));
        Assert.Equal(HttpStatusCode.Redirect, saved.StatusCode);

        var draftPage = await GetHtmlAsync(administrator, saved.Headers.Location!.OriginalString);
        var id = InputValue(draftPage, "Id");
        using var published = await administrator.PostAsync($"{EditPage}/{id}?handler=Publish", Form(draftPage, new()
        {
            ["Id"] = id,
            ["ExpectedRowVersion"] = InputValue(draftPage, "ExpectedRowVersion"),
            ["OperationKey"] = Guid.NewGuid().ToString("N"),
            ["Title"] = title,
            ["Body"] = InputValue(draftPage, "Body", textarea: true)
        }));
        Assert.Equal(HttpStatusCode.Redirect, published.StatusCode);
        return Guid.Parse(id);
    }

    private static HttpClient CreateClient(IntakeWebApplicationFactory factory, string? role = null, Guid? subject = null)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost:7139")
        });
        if (role is not null)
        {
            client.DefaultRequestHeaders.Add("X-Test-Roles", role);
        }

        if (subject is { } id)
        {
            client.DefaultRequestHeaders.Add("X-Test-Subject", id.ToString("D"));
        }

        return client;
    }

    private static FormUrlEncodedContent Form(string page, Dictionary<string, string> fields)
    {
        fields["__RequestVerificationToken"] = InputValue(page, "__RequestVerificationToken");
        return new FormUrlEncodedContent(fields);
    }

    private static string InputValue(string page, string name, bool textarea = false)
    {
        if (textarea)
        {
            var area = Regex.Match(page, $"<textarea[^>]*name=\"{name}\"[^>]*>(?<value>[\\s\\S]*?)</textarea>", RegexOptions.IgnoreCase);
            Assert.True(area.Success, $"No textarea named {name}.");
            return WebUtility.HtmlDecode(area.Groups["value"].Value);
        }

        var input = Regex.Match(page, $"<input[^>]*name=\"{name}\"[^>]*>", RegexOptions.IgnoreCase);
        Assert.True(input.Success, $"No input named {name}.");
        var value = Regex.Match(input.Value, "value=\"(?<value>[^\"]*)\"", RegexOptions.IgnoreCase);
        return WebUtility.HtmlDecode(value.Groups["value"].Value);
    }

    /// <summary>The queries that touch a release-note table.</summary>
    private static int ReleaseNoteReads(SqlStatementCounter recorder) =>
        recorder.ReaderStatements.Count(text => text.Contains("[ReleaseNote", StringComparison.Ordinal));

    private sealed class NonDuplicateAcknowledgementFailureInterceptor : DbCommandInterceptor
    {
        private int interceptedCount;

        public int InterceptedCount => Volatile.Read(ref interceptedCount);

        public void Arm() => Interlocked.Exchange(ref _armed, 1);

        private int _armed;

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (Volatile.Read(ref _armed) == 1
                && eventData.CommandSource == CommandSource.SaveChanges
                && command.CommandText.Contains("[ReleaseNoteAcknowledgements]", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref interceptedCount);
                throw new InvalidOperationException("Simulated non-duplicate acknowledgement database failure.");
            }

            return ValueTask.FromResult(result);
        }
    }

    private sealed class DuplicateAcknowledgementRaceInterceptor : DbCommandInterceptor
    {
        private readonly object sync = new();
        private TaskCompletionSource<bool> checksReleased = CompletedSource();
        private bool armed;
        private int checks;

        public int ExistenceChecks => Volatile.Read(ref checks);

        public void Arm()
        {
            lock (sync)
            {
                checks = 0;
                armed = true;
                checksReleased = new(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            DbDataReader result,
            CancellationToken cancellationToken = default)
        {
            Task? release = null;
            lock (sync)
            {
                if (armed
                    && eventData.CommandSource == CommandSource.LinqQuery
                    && command.CommandText.Contains("[ReleaseNoteAcknowledgements]", StringComparison.Ordinal))
                {
                    checks++;
                    release = checksReleased.Task;
                    if (checks == 2)
                    {
                        checksReleased.TrySetResult(true);
                    }
                }
            }

            if (release is not null)
            {
                await release.WaitAsync(cancellationToken);
            }

            return result;
        }

        private static TaskCompletionSource<bool> CompletedSource()
        {
            var source = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            source.SetResult(true);
            return source;
        }
    }
}
