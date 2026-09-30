using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Azure;
using Azure.Core;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Pegasus.Core.Documents;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Core.Operations;
using Pegasus.Infrastructure;
using Pegasus.Infrastructure.Custody;
using Pegasus.Infrastructure.Persistence;
using SkiaSharp;
using System.Diagnostics;
using Xunit.Abstractions;

namespace Pegasus.IntegrationTests;

public sealed class DocumentContentCacheTests(ITestOutputHelper output)
{
    [Fact]
    [Trait("Category", "Corpus")]
    public async Task GenuineImageColdWarmExpiryAndExactVersionRehydrationAreMeasured()
    {
        var sample = GenuineMultiFormatCorpus.ReadSelected(".jpg", expectedHash: null);
        var estate = await Estate.CreateAsync(sample.Bytes);
        await using (estate)
        {
            const int iterations = 5;
            var cold = new Measurement[iterations];
            var warm = new Measurement[iterations];
            for (var index = 0; index < iterations; index++)
            {
                var downloadsBeforeCold = estate.Box.Downloads;
                cold[index] = await MeasureReadAsync(estate.Reader, estate.Request, sample.Bytes);
                Assert.Equal(downloadsBeforeCold + 1, estate.Box.Downloads);
                Assert.Equal("box-version-1", estate.Box.RequestedVersion);

                var downloadsBeforeWarm = estate.Box.Downloads;
                warm[index] = await MeasureReadAsync(estate.Reader, estate.Request, sample.Bytes);
                Assert.Equal(downloadsBeforeWarm, estate.Box.Downloads);

                await using var db = await estate.Database.CreateContextAsync();
                var entry = await db.Set<DocumentContentCacheEntryEntity>().SingleAsync();
                entry.ExpiresAtUtc = estate.Clock.GetUtcNow().AddTicks(-1);
                await db.SaveChangesAsync();
                var cleanup = await estate.Reader.ExecuteAsync(1, default);
                Assert.Equal(1, cleanup.Deleted);
            }

            var downloadsBeforeRehydrate = estate.Box.Downloads;
            await using var rehydrated = await estate.Reader.OpenAsync(estate.Request, default);
            Assert.Equal(sample.Bytes, await ReadAsync(rehydrated.Content));
            Assert.Equal(downloadsBeforeRehydrate + 1, estate.Box.Downloads);
            Assert.Equal("box-version-1", estate.Box.RequestedVersion);

            WriteMeasurements("cold", cold, sample.Bytes.LongLength);
            WriteMeasurements("warm", warm, sample.Bytes.LongLength);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ColdExactVersionBecomesWarmAndSuccessfulAccessExtendsIdleExpiry(bool systemWorker)
    {
        var bytes = "cached exact version"u8.ToArray();
        var estate = await Estate.CreateAsync(bytes);
        await using (estate)
        {
            var request = systemWorker
                ? estate.Request with { Actor = ActionActor.SystemWorker("intake-processing") }
                : estate.Request;
            await using var cold = await estate.Reader.OpenAsync(request, CancellationToken.None);
            Assert.Equal(bytes, await ReadAsync(cold.Content));
            Assert.Equal(1, estate.Box.Downloads);
            Assert.Equal("box-version-1", estate.Box.RequestedVersion);

            estate.Clock.Advance(TimeSpan.FromHours(5));
            estate.Box.Unavailable = true;
            await using var warm = await estate.Reader.OpenAsync(request, CancellationToken.None);
            Assert.Equal(bytes, await ReadAsync(warm.Content));
            Assert.Equal(1, estate.Box.Downloads);
            await using var db = await estate.Database.CreateContextAsync();
            Assert.Equal(
                estate.Clock.GetUtcNow().AddHours(24),
                (await db.Set<DocumentContentCacheEntryEntity>().SingleAsync()).ExpiresAtUtc);
        }
    }

    [Fact]
    public async Task ConfirmedIntakeAssetWithRemoteIdsOpensThroughTheProductionLogicalReader()
    {
        var bytes = "confirmed intake custody"u8.ToArray();
        var estate = await Estate.CreateAsync(bytes);
        await using (estate)
        {
            await using var content = await estate.Reader.OpenAsync(estate.Request, CancellationToken.None);

            Assert.Equal(bytes, await ReadAsync(content.Content));
            Assert.Equal(1, estate.Box.Downloads);
        }
    }

    [Fact]
    public async Task IntakeAssetFiledOnItsCaseOpensFromTheCaseFolderWithoutACaseRequest()
    {
        var bytes = "case-filed intake custody"u8.ToArray();
        var estate = await Estate.CreateAsync(bytes);
        await using (estate)
        {
            // Case custody filed the asset: its confirmed copy is in the Case
            // folder, and no holding copy exists.
            estate.Box.FileParent = "case-root";
            await using (var db = await estate.Database.CreateContextAsync())
            {
                (await db.Set<IntakeAssetEntity>().SingleAsync()).BoxParentFolderId = "case-root";
                await db.SaveChangesAsync();
            }

            await using var content = await estate.Reader.OpenAsync(estate.Request, CancellationToken.None);

            Assert.Equal(bytes, await ReadAsync(content.Content));
        }
    }

    [Fact]
    public async Task IntakeAssetOutsideItsRecordedFolderCannotOpen()
    {
        var estate = await Estate.CreateAsync("misplaced intake custody"u8.ToArray());
        await using (estate)
        {
            await using (var db = await estate.Database.CreateContextAsync())
            {
                (await db.Set<IntakeAssetEntity>().SingleAsync()).BoxParentFolderId = "case-root";
                await db.SaveChangesAsync();
            }

            await Assert.ThrowsAsync<InvalidDataException>(
                () => estate.Reader.OpenAsync(estate.Request, CancellationToken.None));

            // The content is asked for while the file's folder is being checked,
            // so at most that one request was made. Its bytes were refused with
            // the file: the read threw and handed nothing over.
            Assert.InRange(estate.Box.Downloads, 0, 1);
        }
    }

    [Fact]
    public async Task IntakeAssetWithoutARecordedFolderCannotOpen()
    {
        var estate = await Estate.CreateAsync("unplaced intake custody"u8.ToArray());
        await using (estate)
        {
            await using (var db = await estate.Database.CreateContextAsync())
            {
                (await db.Set<IntakeAssetEntity>().SingleAsync()).BoxParentFolderId = null;
                await db.SaveChangesAsync();
            }

            await Assert.ThrowsAsync<IntakeCustodyUnavailableException>(
                () => estate.Reader.OpenAsync(estate.Request, CancellationToken.None));

            Assert.Equal(0, estate.Box.Downloads);
        }
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("failed")]
    public async Task IntakeAssetWithStaleRemoteIdsButUnconfirmedCustodyCannotOpen(string custodyStatus)
    {
        var estate = await Estate.CreateAsync("stale intake custody"u8.ToArray());
        await using (estate)
        {
            await using (var db = await estate.Database.CreateContextAsync())
            {
                (await db.Set<IntakeAssetEntity>().SingleAsync()).CustodyStatus = custodyStatus;
                await db.SaveChangesAsync();
            }

            await Assert.ThrowsAsync<IntakeCustodyUnavailableException>(
                () => estate.Reader.OpenAsync(estate.Request, CancellationToken.None));

            Assert.Equal(0, estate.Box.Downloads);
        }
    }

    [Fact]
    public async Task AColdDocumentReadEmitsOnlyAllowlistedCacheProviderAndVerificationPhases()
    {
        var phases = new List<string>();
        using var scope = new Activity(nameof(AColdDocumentReadEmitsOnlyAllowlistedCacheProviderAndVerificationPhases));
        scope.Start();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Pegasus.Documents",
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity =>
            {
                if (activity.TraceId == scope.TraceId)
                {
                    phases.Add(activity.DisplayName);
                }
            }
        };
        ActivitySource.AddActivityListener(listener);

        var bytes = "telemetry phases"u8.ToArray();
        var estate = await Estate.CreateAsync(bytes);
        await using (estate)
        await using (var content = await estate.Reader.OpenAsync(estate.Request, CancellationToken.None))
        {
            Assert.Equal(bytes, await ReadAsync(content.Content));
        }

        Assert.Contains("document.original.cache.read", phases);
        Assert.Contains("document.provider.gate", phases);
        Assert.Contains("document.provider.read", phases);
        Assert.Contains("document.content.verify", phases);
        Assert.Contains("document.original.cache.write", phases);
        Assert.All(phases, phase => Assert.StartsWith("document.", phase, StringComparison.Ordinal));
    }

    [Fact]
    public async Task AdministrationHealthReadsRecordedCacheFactsWithoutTouchingContent()
    {
        var bytes = "health cache bytes"u8.ToArray();
        var estate = await Estate.CreateAsync(bytes);
        await using (estate)
        {
            await using (var cold = await estate.Reader.OpenAsync(estate.Request, CancellationToken.None))
            {
                _ = await ReadAsync(cold.Content);
            }
            await using (var db = await estate.Database.CreateContextAsync())
            {
                (await db.Set<DocumentContentCacheEntryEntity>().SingleAsync()).LastCleanupOutcome =
                    "delete_failed";
                await db.SaveChangesAsync();
            }

            await using var scope = estate.Database.CreateAsyncScope();
            var health = scope.ServiceProvider
                .GetRequiredService<IAdministrationHealthMetricsQueries>();
            var metrics = await health.GetAsync(estate.Clock.GetUtcNow(), CancellationToken.None);

            Assert.Equal(1, metrics.CacheEntries);
            Assert.Equal(bytes.Length, metrics.CacheBytes);
            Assert.Equal(1, metrics.CacheCleanupFailures);
            Assert.Equal(1, estate.Box.Downloads);
        }
    }

    [Fact]
    public async Task ActiveReadLeasePreventsCleanupAndCorruptWarmBytesAreRefused()
    {
        var bytes = "race cache bytes"u8.ToArray();
        var estate = await Estate.CreateAsync(bytes);
        await using (estate)
        {
            await using (var cold = await estate.Reader.OpenAsync(estate.Request, CancellationToken.None))
            {
                _ = await ReadAsync(cold.Content);
            }
            await using (var db = await estate.Database.CreateContextAsync())
            {
                var entry = await db.Set<DocumentContentCacheEntryEntity>().SingleAsync();
                entry.ExpiresAtUtc = estate.Clock.GetUtcNow().AddMinutes(-1);
                entry.ReadLeaseExpiresAtUtc = estate.Clock.GetUtcNow().AddMinutes(1);
                await db.SaveChangesAsync();
            }
            var cleanup = await estate.Reader.ExecuteAsync(10, CancellationToken.None);
            Assert.Equal(0, cleanup.Candidates);
            Assert.Equal(0, estate.Blob.DeleteCount);
            await using (var db = await estate.Database.CreateContextAsync())
            {
                var entry = await db.Set<DocumentContentCacheEntryEntity>().SingleAsync();
                entry.ExpiresAtUtc = estate.Clock.GetUtcNow().AddMinutes(1);
                await db.SaveChangesAsync();
            }
            // A claimed entry is never served from the cache, whatever its
            // expiry says: the read goes to Box.
            await using (var claimed = await estate.Reader.OpenAsync(
                             estate.Request,
                             CancellationToken.None))
            {
                Assert.Equal(bytes, await ReadAsync(claimed.Content));
            }
            Assert.Equal(2, estate.Box.Downloads);

            await using (var db = await estate.Database.CreateContextAsync())
            {
                var entry = await db.Set<DocumentContentCacheEntryEntity>().SingleAsync();
                entry.ExpiresAtUtc = estate.Clock.GetUtcNow().AddHours(1);
                entry.ReadLeaseExpiresAtUtc = null;
                await db.SaveChangesAsync();
            }
            estate.Blob.Content = "corrupt"u8.ToArray();
            await Assert.ThrowsAsync<InvalidDataException>(
                () => estate.Reader.OpenAsync(estate.Request, CancellationToken.None));
        }
    }

    [Fact]
    public async Task CleanupRemovesSqlEntryWhenBlobIsAlreadyMissing()
    {
        var estate = await Estate.CreateAsync("missing cache"u8.ToArray());
        await using (estate)
        {
            await using (var cold = await estate.Reader.OpenAsync(estate.Request, CancellationToken.None))
            {
                _ = await ReadAsync(cold.Content);
            }
            await using (var db = await estate.Database.CreateContextAsync())
            {
                var entry = await db.Set<DocumentContentCacheEntryEntity>().SingleAsync();
                entry.ExpiresAtUtc = estate.Clock.GetUtcNow().AddMinutes(-1);
                await db.SaveChangesAsync();
            }
            estate.Blob.DeleteResult = false;

            var result = await estate.Reader.ExecuteAsync(10, CancellationToken.None);

            Assert.Equal(1, result.Deleted);
            await using var verify = await estate.Database.CreateContextAsync();
            Assert.Empty(await verify.Set<DocumentContentCacheEntryEntity>().ToArrayAsync());
        }
    }

    [Fact]
    public async Task MissingBlobRepublishedDuringActiveReadLeaseRecordsCompletedAccess()
    {
        var bytes = "republished under read lease"u8.ToArray();
        var estate = await Estate.CreateAsync(bytes);
        await using (estate)
        {
            await using (var cold = await estate.Reader.OpenAsync(estate.Request, default))
            {
                _ = await ReadAsync(cold.Content);
            }
            await using (var db = await estate.Database.CreateContextAsync())
            {
                var entry = await db.Set<DocumentContentCacheEntryEntity>().SingleAsync();
                entry.ExpiresAtUtc = estate.Clock.GetUtcNow().AddHours(1);
                entry.ReadLeaseExpiresAtUtc = estate.Clock.GetUtcNow().AddMinutes(1);
                await db.SaveChangesAsync();
            }
            estate.Blob.Content = null;
            estate.Blob.MissingContentIsNotFound = true;

            await using var republished = await estate.Reader.OpenAsync(estate.Request, default);
            Assert.Equal(bytes, await ReadAsync(republished.Content));
            Assert.Equal(2, estate.Box.Downloads);
            await using var verify = await estate.Database.CreateContextAsync();
            Assert.Equal(
                estate.Clock.GetUtcNow().AddHours(24),
                (await verify.Set<DocumentContentCacheEntryEntity>().SingleAsync()).ExpiresAtUtc);
        }
    }

    [Fact]
    public async Task DisabledStaffCannotReadPreviouslyWarmContent()
    {
        var estate = await Estate.CreateAsync("revoked cache read"u8.ToArray());
        await using (estate)
        {
            await using (var cold = await estate.Reader.OpenAsync(estate.Request, default))
            {
                _ = await ReadAsync(cold.Content);
            }
            await using (var db = await estate.Database.CreateContextAsync())
            {
                (await db.Users.SingleAsync(value => value.Id == estate.StaffId)).IsEnabled = false;
                await db.SaveChangesAsync();
            }
            estate.Box.Unavailable = true;

            await Assert.ThrowsAsync<StaffAuthorizationException>(
                () => estate.Reader.OpenAsync(estate.Request, default));

            Assert.Equal(1, estate.Box.Downloads);
        }
    }

    [Fact]
    public async Task StaffWithoutCurrentRoleCannotReadPreviouslyWarmContent()
    {
        var estate = await Estate.CreateAsync("removed role cache read"u8.ToArray());
        await using (estate)
        {
            await using (var cold = await estate.Reader.OpenAsync(estate.Request, default))
            {
                _ = await ReadAsync(cold.Content);
            }
            await using (var db = await estate.Database.CreateContextAsync())
            {
                db.UserRoles.RemoveRange(db.UserRoles.Where(value => value.UserId == estate.StaffId));
                await db.SaveChangesAsync();
            }
            estate.Box.Unavailable = true;

            await Assert.ThrowsAsync<StaffAuthorizationException>(
                () => estate.Reader.OpenAsync(estate.Request, default));

            Assert.Equal(1, estate.Box.Downloads);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CleanupPreconditionConflictReleasesClaimAndRecordsBoundedFailure(bool corruptReplacement)
    {
        var estate = await Estate.CreateAsync("cleanup conflict"u8.ToArray());
        await using (estate)
        {
            await using (var cold = await estate.Reader.OpenAsync(estate.Request, default))
            {
                _ = await ReadAsync(cold.Content);
            }
            await using (var db = await estate.Database.CreateContextAsync())
            {
                (await db.Set<DocumentContentCacheEntryEntity>().SingleAsync()).ExpiresAtUtc =
                    estate.Clock.GetUtcNow().AddMinutes(-1);
                await db.SaveChangesAsync();
            }
            estate.Blob.DeletePreconditionFails = true;
            estate.Blob.ReplaceContent(corruptReplacement ? "changed replacement"u8.ToArray() : estate.Blob.Content!);

            var result = await estate.Reader.ExecuteAsync(10, default);

            Assert.Equal(1, result.Failures);
            Assert.Equal(0, result.Deleted);
            await using var verify = await estate.Database.CreateContextAsync();
            var retained = await verify.Set<DocumentContentCacheEntryEntity>().SingleAsync();
            Assert.Null(retained.ReadLeaseExpiresAtUtc);
            Assert.Equal("RequestFailedException:412", retained.LastCleanupOutcome);
            Assert.Equal(corruptReplacement ? "\"1\"" : "\"2\"", retained.ETag);
            estate.Blob.DeletePreconditionFails = false;
            var retry = await estate.Reader.ExecuteAsync(10, default);
            Assert.Equal(corruptReplacement ? 0 : 1, retry.Deleted);
            Assert.Equal(corruptReplacement ? 1 : 0, retry.Failures);
        }
    }

    [Fact]
    public async Task ConcurrentColdMissesConvergeOnOneVerifiedCacheEntry()
    {
        var bytes = "concurrent cache miss"u8.ToArray();
        var estate = await Estate.CreateAsync(bytes);
        await using (estate)
        {
            var firstTask = estate.Reader.OpenAsync(estate.Request, default);
            var secondTask = estate.Reader.OpenAsync(estate.Request, default);
            var opened = await Task.WhenAll(firstTask, secondTask);
            await using var first = opened[0];
            await using var second = opened[1];

            Assert.Equal(bytes, await ReadAsync(first.Content));
            Assert.Equal(bytes, await ReadAsync(second.Content));
            await using var db = await estate.Database.CreateContextAsync();
            Assert.Single(await db.Set<DocumentContentCacheEntryEntity>().ToArrayAsync());
            Assert.Equal(1, estate.Blob.UploadCount);
        }
    }

    [Fact]
    public async Task CancelledThumbnailWaiterDoesNotFetchWhileTheCurrentVariantRenders()
    {
        var sourceBytes = TransparentPng(width: 960, height: 480);
        var estate = await Estate.CreateDocumentAsync(sourceBytes);
        await using (estate)
        {
            await using var scope = estate.Database.CreateAsyncScope();
            var cache = new DocumentThumbnailCache(
                scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>(),
                new CacheContainer(estate.Blob),
                estate.Clock);
            var source = new GatedThumbnailSource(sourceBytes);
            var reader = new CaseDocumentThumbnailReader(source, cache);
            var request = new CaseDocumentThumbnailRequest(
                estate.Request.Actor,
                estate.Request.CaseId!.Value,
                estate.Request.DocumentId!.Value,
                estate.Request.VersionId!.Value,
                estate.Request.ExpectedSha256,
                sourceBytes.LongLength,
                "image/png");

            using var renderScope = new Activity(nameof(CancelledThumbnailWaiterDoesNotFetchWhileTheCurrentVariantRenders));
            renderScope.Start();
            var secondGateEntry = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var renderGateStarts = 0;
            using var listener = new ActivityListener
            {
                ShouldListenTo = activitySource => activitySource.Name == "Pegasus.Documents",
                Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
                ActivityStarted = activity =>
                {
                    if (activity.TraceId == renderScope.TraceId
                        && activity.DisplayName == "document.thumbnail.render.gate"
                        && Interlocked.Increment(ref renderGateStarts) == 1)
                    {
                        secondGateEntry.TrySetResult();
                    }
                }
            };
            ActivitySource.AddActivityListener(listener);

            var first = reader.OpenAsync(request, CancellationToken.None);
            await source.FirstRead.WaitAsync(TimeSpan.FromSeconds(30));
            try
            {
                using var cancellation = new CancellationTokenSource();
                var second = reader.OpenAsync(request, cancellation.Token);
                await secondGateEntry.Task.WaitAsync(TimeSpan.FromSeconds(30));
                cancellation.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => second);
                Assert.Equal(1, source.Reads);
            }
            finally
            {
                source.Release();
            }

            await using var firstThumbnail = Assert.IsType<CaseDocumentThumbnail>(await first);
            Assert.Equal(1, estate.Blob.UploadCount);
            await using var cachedThumbnail = Assert.IsType<CaseDocumentThumbnail>(
                await reader.OpenAsync(request, CancellationToken.None));
            Assert.Equal(1, source.Reads);
            await using var db = await estate.Database.CreateContextAsync();
            var entry = Assert.Single(await db.Set<DocumentContentCacheEntryEntity>().ToArrayAsync());
            Assert.Equal(
                CaseDocumentThumbnails.VariantToken(CaseAssetRotation.None, null),
                entry.Variant);
        }
    }

    [Fact]
    public async Task ConcurrentSuccessfulThumbnailMissesFetchAndRenderOneCurrentVariant()
    {
        var sourceBytes = TransparentPng(width: 960, height: 480);
        var estate = await Estate.CreateDocumentAsync(sourceBytes);
        await using (estate)
        {
            await using var scope = estate.Database.CreateAsyncScope();
            var cache = new DocumentThumbnailCache(
                scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>(),
                new CacheContainer(estate.Blob),
                estate.Clock);
            var source = new GatedThumbnailSource(sourceBytes);
            var reader = new CaseDocumentThumbnailReader(source, cache);
            var request = new CaseDocumentThumbnailRequest(
                estate.Request.Actor,
                estate.Request.CaseId!.Value,
                estate.Request.DocumentId!.Value,
                estate.Request.VersionId!.Value,
                estate.Request.ExpectedSha256,
                sourceBytes.LongLength,
                "image/png");

            using var renderScope = new Activity(nameof(ConcurrentSuccessfulThumbnailMissesFetchAndRenderOneCurrentVariant));
            renderScope.Start();
            var secondGateEntry = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var renderGateStarts = 0;
            using var listener = new ActivityListener
            {
                ShouldListenTo = activitySource => activitySource.Name == "Pegasus.Documents",
                Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
                ActivityStarted = activity =>
                {
                    if (activity.TraceId == renderScope.TraceId
                        && activity.DisplayName == "document.thumbnail.render.gate"
                        && Interlocked.Increment(ref renderGateStarts) == 1)
                    {
                        secondGateEntry.TrySetResult();
                    }
                }
            };
            ActivitySource.AddActivityListener(listener);

            var first = reader.OpenAsync(request, CancellationToken.None);
            await source.FirstRead.WaitAsync(TimeSpan.FromSeconds(30));
            var second = reader.OpenAsync(request, CancellationToken.None);
            try
            {
                await secondGateEntry.Task.WaitAsync(TimeSpan.FromSeconds(30));
                source.Release();
                await using var firstThumbnail = Assert.IsType<CaseDocumentThumbnail>(await first);
                await using var secondThumbnail = Assert.IsType<CaseDocumentThumbnail>(await second);
            }
            finally
            {
                source.Release();
            }

            Assert.Equal(1, source.Reads);
            Assert.Equal(1, estate.Blob.UploadCount);
            await using var db = await estate.Database.CreateContextAsync();
            var entry = Assert.Single(await db.Set<DocumentContentCacheEntryEntity>().ToArrayAsync());
            Assert.Equal(
                CaseDocumentThumbnails.VariantToken(CaseAssetRotation.None, null),
                entry.Variant);
        }
    }

    [Fact]
    public async Task PlainThumbnailScalesAnOrientedLargeJpegToTheDisplayed480PixelEdge()
    {
        var sourceBytes = JpegWithOrientation(width: 960, height: 640, orientation: 6);
        using var source = new MemoryStream(sourceBytes, writable: false);

        var rendered = await ImageThumbnailRendering.TryRenderAsync(
            source,
            sourceBytes.LongLength,
            CancellationToken.None);

        Assert.NotNull(rendered);
        using var thumbnail = SKBitmap.Decode(rendered);
        Assert.NotNull(thumbnail);
        Assert.Equal(320, thumbnail.Width);
        Assert.Equal(CaseDocumentThumbnails.LongestEdge, thumbnail.Height);
        AssertColorClose(thumbnail.GetPixel(80, 120), SKColors.Blue);
        AssertColorClose(thumbnail.GetPixel(240, 120), SKColors.Red);
        AssertColorClose(thumbnail.GetPixel(80, 360), SKColors.Yellow);
        AssertColorClose(thumbnail.GetPixel(240, 360), SKColors.Green);
    }

    [Fact]
    public async Task PlainThumbnailScalesTransparentPixelsOntoWhite()
    {
        var sourceBytes = TransparentPng(width: 960, height: 480);
        using var source = new MemoryStream(sourceBytes, writable: false);

        var rendered = await ImageThumbnailRendering.TryRenderAsync(
            source,
            sourceBytes.LongLength,
            CancellationToken.None);

        Assert.NotNull(rendered);
        using var thumbnail = SKBitmap.Decode(rendered);
        Assert.NotNull(thumbnail);
        Assert.Equal(CaseDocumentThumbnails.LongestEdge, thumbnail.Width);
        Assert.Equal(240, thumbnail.Height);
        var pixel = thumbnail.GetPixel(240, 120);
        Assert.InRange(pixel.Red, 250, byte.MaxValue);
        Assert.InRange(pixel.Green, 250, byte.MaxValue);
        Assert.InRange(pixel.Blue, 250, byte.MaxValue);
        Assert.Equal(byte.MaxValue, pixel.Alpha);
    }

    [Fact]
    public async Task MalformedAndOversizedThumbnailSourcesAreNotDecoded()
    {
        using var malformed = new MemoryStream("not an image"u8.ToArray(), writable: false);
        using var oversized = new MemoryStream();

        Assert.Null(await ImageThumbnailRendering.TryRenderAsync(
            malformed,
            malformed.Length,
            CancellationToken.None));
        Assert.Null(await ImageThumbnailRendering.TryRenderAsync(
            oversized,
            32L * 1024 * 1024 + 1,
            CancellationToken.None));
    }

    [Fact]
    public async Task CancelledThumbnailSourceReadPropagatesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        using var source = new CancellingReadStream(cancellation);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ImageThumbnailRendering.TryRenderAsync(
                source,
                1,
                cancellation.Token));
    }

    [Fact]
    public async Task VerifiedBlobFromInterruptedPublishIsAdoptedOnRetry()
    {
        var bytes = "interrupted cache publish"u8.ToArray();
        var estate = await Estate.CreateAsync(bytes);
        await using (estate)
        {
            estate.Blob.Content = bytes;

            await using var opened = await estate.Reader.OpenAsync(estate.Request, default);

            Assert.Equal(bytes, await ReadAsync(opened.Content));
            Assert.Equal(0, estate.Blob.UploadCount);
            await using var db = await estate.Database.CreateContextAsync();
            Assert.Single(await db.Set<DocumentContentCacheEntryEntity>().ToArrayAsync());
        }
    }

    [Fact]
    public async Task SevenDecimalCropVariantWarmsAndExpiresAsItsOwnCacheEntry()
    {
        var estate = await Estate.CreateDocumentAsync("prepared thumbnail"u8.ToArray());
        await using (estate)
        {
            var versionId = estate.Request.VersionId!.Value;
            var variant = CaseDocumentThumbnails.VariantToken(
                CaseAssetRotation.None,
                new CaseAssetCrop(0.1234567m, 0.1234567m, 0.1234567m, 0.1234567m));
            var rendering = "prepared thumbnail rendering"u8.ToArray();
            await using var scope = estate.Database.CreateAsyncScope();
            var cache = new DocumentThumbnailCache(
                scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>(),
                new CacheContainer(estate.Blob),
                estate.Clock);

            await cache.WriteAsync(versionId, variant, rendering, CancellationToken.None);
            estate.Clock.Advance(TimeSpan.FromHours(5));

            Assert.Equal(
                rendering,
                await cache.TryReadAsync(estate.Request.Actor, versionId, variant, CancellationToken.None));
            await using (var db = await estate.Database.CreateContextAsync())
            {
                var entry = await db.Set<DocumentContentCacheEntryEntity>().SingleAsync();
                Assert.Equal(variant, entry.Variant);
                Assert.Equal(estate.Clock.GetUtcNow().AddHours(24), entry.ExpiresAtUtc);
            }

            estate.Clock.Advance(TimeSpan.FromHours(24));
            Assert.Null(await cache.TryReadAsync(estate.Request.Actor, versionId, variant, CancellationToken.None));
        }
    }

    [Fact]
    public async Task ColdProviderFailureDoesNotPublishCacheState()
    {
        var estate = await Estate.CreateAsync("cold provider failure"u8.ToArray());
        await using (estate)
        {
            estate.Box.Unavailable = true;

            await Assert.ThrowsAnyAsync<Exception>(
                () => estate.Reader.OpenAsync(estate.Request, default));

            Assert.Null(estate.Blob.Content);
            await using var db = await estate.Database.CreateContextAsync();
            Assert.Empty(await db.Set<DocumentContentCacheEntryEntity>().ToArrayAsync());
        }
    }

    /// <summary>
    /// A miss hashes the download as it is written, so the temporary file the
    /// caller reads is the download's own file. There is no second copy of it.
    /// </summary>
    [Fact]
    public async Task AColdMissServesTheDownloadsOwnVerifiedTemporaryFile()
    {
        var bytes = "one temporary copy"u8.ToArray();
        var estate = await Estate.CreateAsync(bytes);
        await using (estate)
        {
            await using var cold = await estate.Reader.OpenAsync(estate.Request, CancellationToken.None);

            var file = Assert.IsType<FileStream>(cold.Content);
            Assert.StartsWith("pegasus-box-version-", Path.GetFileName(file.Name), StringComparison.Ordinal);
            Assert.Equal(bytes, await ReadAsync(cold.Content));
            Assert.Equal(1, estate.Box.Downloads);
            Assert.Equal(bytes, estate.Blob.Content);
        }
    }

    /// <summary>
    /// The check that moved into the download is the same check: a length or a
    /// hash that is not the recorded one is refused, nothing is published, and
    /// the temporary file the download wrote is gone.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(-1)]
    public async Task AColdMissWhoseContentDoesNotVerifyIsRefusedAndLeavesNoTemporaryFile(int lengthDifference)
    {
        var recorded = "recorded content"u8.ToArray();
        var tampered = Encoding.UTF8.GetBytes(
            Guid.NewGuid().ToString("N")[..(recorded.Length + lengthDifference)]);
        var estate = await Estate.CreateAsync(recorded);
        await using (estate)
        {
            estate.Box.Serve(tampered);

            await Assert.ThrowsAsync<InvalidDataException>(
                () => estate.Reader.OpenAsync(estate.Request, CancellationToken.None));

            Assert.Equal(1, estate.Box.Downloads);
            Assert.Null(estate.Blob.Content);
            await using var db = await estate.Database.CreateContextAsync();
            Assert.Empty(await db.Set<DocumentContentCacheEntryEntity>().ToArrayAsync());
            Assert.Empty(TemporaryFilesHolding(tampered));
        }
    }

    /// <summary>
    /// The temporary files, if any, that still hold these exact bytes. Other
    /// tests write temporary files of their own at the same time, so a file is
    /// this test's only when it holds this test's unique content.
    /// </summary>
    private static string[] TemporaryFilesHolding(byte[] marker)
    {
        var holding = new List<string>();
        foreach (var path in Directory.EnumerateFiles(Path.GetTempPath(), "pegasus-*.tmp"))
        {
            try
            {
                using var stream = new FileStream(
                    path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var buffer = new MemoryStream();
                stream.CopyTo(buffer);
                if (buffer.ToArray().AsSpan().IndexOf(marker) >= 0)
                {
                    holding.Add(path);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Another test's file, gone or in use: not this test's.
            }
        }
        return [.. holding];
    }

    [Fact]
    public async Task LogicallyRemovedVersionCannotUsePreviouslyWarmContent()
    {
        var bytes = "removed document version"u8.ToArray();
        var estate = await Estate.CreateDocumentAsync(bytes);
        await using (estate)
        {
            await using (var cold = await estate.Reader.OpenAsync(estate.Request, default))
            {
                _ = await ReadAsync(cold.Content);
            }
            await using (var db = await estate.Database.CreateContextAsync())
            {
                (await db.Set<DocumentVersionEntity>().SingleAsync()).IsLogicallyRemoved = true;
                await db.SaveChangesAsync();
            }
            estate.Box.Unavailable = true;

            await Assert.ThrowsAsync<FileNotFoundException>(
                () => estate.Reader.OpenAsync(estate.Request, default));

            Assert.Equal(1, estate.Box.Downloads);
        }
    }

    [Fact]
    public async Task FailedVersionWithRemoteIdsIsRejectedBeforeContentRead()
    {
        var estate = await Estate.CreateDocumentAsync("failed document version"u8.ToArray());
        await using (estate)
        {
            await using (var db = await estate.Database.CreateContextAsync())
            {
                (await db.Set<DocumentVersionEntity>().SingleAsync()).CustodyStatus =
                    DocumentCustodyStatus.Failed;
                await db.SaveChangesAsync();
            }

            await Assert.ThrowsAsync<FileNotFoundException>(
                () => estate.Reader.OpenAsync(estate.Request, default));

            Assert.Equal(0, estate.Box.Downloads);
        }
    }

    /// <summary>
    /// A warm read is the actor, the source and the cache entry: three SQL
    /// round trips, and a fourth only when the entry's expiry is due to be
    /// pushed out, which happens at most once an hour.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AWarmReadCostsThreeSqlRoundTripsAndFourWhenItExtendsTheEntry(bool document)
    {
        var bytes = "counted warm read"u8.ToArray();
        var estate = document ? await Estate.CreateDocumentAsync(bytes) : await Estate.CreateAsync(bytes);
        await using (estate)
        {
            await using (var cold = await estate.Reader.OpenAsync(estate.Request, default))
            {
                _ = await ReadAsync(cold.Content);
            }
            var commands = new CountingCommandInterceptor();
            var counted = new CachedDocumentContentStore(
                new PooledDbContextFactory<PegasusDbContext>(
                    new DbContextOptionsBuilder<PegasusDbContext>()
                        .UseSqlServer(estate.Database.ConnectionString)
                        .AddInterceptors(commands)
                        .Options),
                new CacheContainer(estate.Blob),
                new BoxContentClient(BoxOptions(), new HttpClient(estate.Box), new Header(), estate.Clock),
                estate.Clock);
            estate.Box.Unavailable = true;

            await using (var warm = await counted.OpenAsync(estate.Request, default))
            {
                Assert.Equal(bytes, await ReadAsync(warm.Content));
            }
            Assert.Equal(3, commands.Count);

            estate.Clock.Advance(TimeSpan.FromHours(2));
            commands.Reset();
            await using (var extended = await counted.OpenAsync(estate.Request, default))
            {
                Assert.Equal(bytes, await ReadAsync(extended.Content));
            }
            Assert.Equal(4, commands.Count);
            Assert.Equal(1, estate.Box.Downloads);
            await using var db = await estate.Database.CreateContextAsync();
            Assert.Equal(
                estate.Clock.GetUtcNow().AddHours(24),
                (await db.Set<DocumentContentCacheEntryEntity>().SingleAsync()).ExpiresAtUtc);
        }
    }

    /// <summary>
    /// The race the read lease used to guard: a hit pushes the expiry out
    /// before it reads the object, and cleanup claims only expired entries, so
    /// a cleanup pass after the hit leaves the entry and its object alone.
    /// </summary>
    [Fact]
    public async Task AHitExtendsTheEntryBeforeReadingSoCleanupCannotRemoveItUnderTheRead()
    {
        var bytes = "touch before cleanup"u8.ToArray();
        var estate = await Estate.CreateAsync(bytes);
        await using (estate)
        {
            await using (var cold = await estate.Reader.OpenAsync(estate.Request, default))
            {
                _ = await ReadAsync(cold.Content);
            }
            await using (var db = await estate.Database.CreateContextAsync())
            {
                (await db.Set<DocumentContentCacheEntryEntity>().SingleAsync()).ExpiresAtUtc =
                    estate.Clock.GetUtcNow().AddMinutes(1);
                await db.SaveChangesAsync();
            }
            estate.Box.Unavailable = true;

            await using (var warm = await estate.Reader.OpenAsync(estate.Request, default))
            {
                Assert.Equal(bytes, await ReadAsync(warm.Content));
            }
            estate.Clock.Advance(TimeSpan.FromMinutes(2));
            var cleanup = await estate.Reader.ExecuteAsync(10, default);

            Assert.Equal(0, cleanup.Candidates);
            Assert.Equal(0, estate.Blob.DeleteCount);
            await using var verify = await estate.Database.CreateContextAsync();
            Assert.Single(await verify.Set<DocumentContentCacheEntryEntity>().ToArrayAsync());
        }
    }

    /// <summary>
    /// An entry past its expiry is never served, even before cleanup has come
    /// for it: the read goes to Box and records the entry afresh.
    /// </summary>
    [Fact]
    public async Task AnExpiredEntryIsReadFromBoxAgainEvenBeforeCleanupRuns()
    {
        var bytes = "expired before cleanup"u8.ToArray();
        var estate = await Estate.CreateAsync(bytes);
        await using (estate)
        {
            await using (var cold = await estate.Reader.OpenAsync(estate.Request, default))
            {
                _ = await ReadAsync(cold.Content);
            }
            await using (var db = await estate.Database.CreateContextAsync())
            {
                (await db.Set<DocumentContentCacheEntryEntity>().SingleAsync()).ExpiresAtUtc =
                    estate.Clock.GetUtcNow().AddMinutes(-1);
                await db.SaveChangesAsync();
            }

            await using (var reread = await estate.Reader.OpenAsync(estate.Request, default))
            {
                Assert.Equal(bytes, await ReadAsync(reread.Content));
            }

            Assert.Equal(2, estate.Box.Downloads);
            await using var verify = await estate.Database.CreateContextAsync();
            Assert.Equal(
                estate.Clock.GetUtcNow().AddHours(24),
                (await verify.Set<DocumentContentCacheEntryEntity>().SingleAsync()).ExpiresAtUtc);
        }
    }

    /// <summary>
    /// The export and the report read a Case's photographs cache first: a
    /// cold set is read from Box once and cached, and the same set again is
    /// served from the cache without asking Box.
    /// </summary>
    [Fact]
    public async Task ABatchReadIsServedFromTheCacheAfterItsFirstRead()
    {
        var bytes = "batched photograph"u8.ToArray();
        var estate = await Estate.CreateDocumentAsync(bytes);
        await using (estate)
        {
            var request = estate.Request;
            ManagedDocumentContentRead[] reads =
            [
                new(
                    new ManagedDocumentContentAddress(
                        request.CaseId!.Value,
                        "QDOS091",
                        "holding",
                        Guid.NewGuid(),
                        1,
                        request.DocumentId!.Value,
                        request.VersionId!.Value,
                        1,
                        DocumentSemanticRole.Image,
                        "source.bin",
                        "application/octet-stream",
                        "box-file-1",
                        "box-version-1"),
                    request.ExpectedSha256,
                    request.ExpectedContentLength)
            ];

            var cold = await estate.Reader.ReadVersionsAsync(reads, default);
            Assert.Equal(bytes, Assert.Single(cold).ToArray());
            Assert.Equal(1, estate.Box.Downloads);

            estate.Box.Unavailable = true;
            var warm = await estate.Reader.ReadVersionsAsync(reads, default);
            Assert.Equal(bytes, Assert.Single(warm).ToArray());
            Assert.Equal(1, estate.Box.Downloads);

            // A cached copy that fails verification is a miss, not a failed
            // export: that version is read from Box instead.
            estate.Box.Unavailable = false;
            estate.Blob.Content = "corrupt"u8.ToArray();
            var recovered = await estate.Reader.ReadVersionsAsync(reads, default);
            Assert.Equal(bytes, Assert.Single(recovered).ToArray());
            Assert.Equal(2, estate.Box.Downloads);
        }
    }

    /// <summary>
    /// Whichever host files a document to Box publishes the copy while it holds
    /// the bytes, under the identity the read will ask for, so the first read
    /// of it is a hit and asks Box for nothing.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AFiledFilesFirstReadIsACacheHitWithNoBoxCall(bool document)
    {
        var bytes = "filed and never opened"u8.ToArray();
        var estate = document ? await Estate.CreateDocumentAsync(bytes) : await Estate.CreateAsync(bytes);
        await using (estate)
        {
            estate.Box.Unavailable = true;
            var publisher = estate.Reader;

            await publisher.PublishAsync(
                KeyOf(estate.Request), bytes, estate.Request.ExpectedSha256, CancellationToken.None);

            Assert.Equal(1, estate.Blob.UploadCount);
            await using (var db = await estate.Database.CreateContextAsync())
            {
                var entry = await db.Set<DocumentContentCacheEntryEntity>().SingleAsync();
                Assert.Equal(estate.Request.VersionId, entry.DocumentVersionId);
                Assert.Equal(estate.Request.IntakeAssetId, entry.IntakeAssetId);
                Assert.Equal(CachedDocumentContentStore.OriginalVariant, entry.Variant);
                Assert.Equal(estate.Request.ExpectedSha256, entry.VerifiedSha256);
                Assert.Equal(bytes.LongLength, entry.VerifiedSize);
            }

            await using var read = await estate.Reader.OpenAsync(estate.Request, CancellationToken.None);
            Assert.Equal(bytes, await ReadAsync(read.Content));
            Assert.Equal(0, estate.Box.Downloads);
        }
    }

    /// <summary>
    /// A publish that fails is logged and forgotten: it never throws, so it
    /// cannot fail the filing it follows, and the next read misses and
    /// publishes as it always did.
    /// </summary>
    [Fact]
    public async Task AFailedPublishNeverThrowsAndTheNextReadPublishesAsItAlwaysDid()
    {
        var bytes = "publish that fails"u8.ToArray();
        var logger = new RecordingLogger();
        var estate = await Estate.CreateDocumentAsync(bytes, logger: logger);
        await using (estate)
        {
            var publisher = estate.Reader;
            estate.Blob.FailUploads = true;

            await publisher.PublishAsync(
                KeyOf(estate.Request), bytes, estate.Request.ExpectedSha256, CancellationToken.None);

            Assert.Equal([LogLevel.Warning], logger.Levels);
            Assert.Null(estate.Blob.Content);
            await using (var db = await estate.Database.CreateContextAsync())
            {
                Assert.Empty(await db.Set<DocumentContentCacheEntryEntity>().ToArrayAsync());
            }

            estate.Blob.FailUploads = false;
            await using var read = await estate.Reader.OpenAsync(estate.Request, CancellationToken.None);
            Assert.Equal(bytes, await ReadAsync(read.Content));
            Assert.Equal(1, estate.Box.Downloads);
            await using var verify = await estate.Database.CreateContextAsync();
            Assert.Single(await verify.Set<DocumentContentCacheEntryEntity>().ToArrayAsync());
        }
    }

    /// <summary>
    /// The publisher checks the bytes it is given against the hash and length
    /// it was given, so a caller's mistake never becomes a cache copy that a
    /// later read would refuse.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BytesThatDoNotMatchTheirHashOrLengthAreNotPublished(bool wrongLength)
    {
        var bytes = "bytes the caller filed"u8.ToArray();
        var logger = new RecordingLogger();
        var estate = await Estate.CreateDocumentAsync(bytes, logger: logger);
        await using (estate)
        {
            var publisher = estate.Reader;
            var request = estate.Request;

            if (wrongLength)
            {
                await using var stream = new MemoryStream(bytes, writable: false);
                await publisher.PublishAsync(
                    KeyOf(request), stream, request.ExpectedSha256, bytes.Length + 1, CancellationToken.None);
            }
            else
            {
                await publisher.PublishAsync(
                    KeyOf(request), "different bytes here"u8.ToArray(), request.ExpectedSha256, CancellationToken.None);
            }

            Assert.Equal([LogLevel.Warning], logger.Levels);
            Assert.Equal(0, estate.Blob.UploadCount);
            await using var db = await estate.Database.CreateContextAsync();
            Assert.Empty(await db.Set<DocumentContentCacheEntryEntity>().ToArrayAsync());
        }
    }

    /// <summary>
    /// Filings of one work item run three at a time, so the publisher holds no
    /// state between calls: each publish uses its own context and one entry
    /// results per version.
    /// </summary>
    [Fact]
    public async Task PublishesOfDifferentVersionsMayRunAtTheSameTime()
    {
        var estate = await Estate.CreateDocumentAsync("one of three"u8.ToArray());
        await using (estate)
        {
            var container = new NamedCacheContainer();
            await using var scope = estate.Database.CreateAsyncScope();
            var publisher = new CachedDocumentContentStore(
                scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>(),
                container,
                new BoxContentClient(BoxOptions(), new HttpClient(estate.Box), new Header(), estate.Clock),
                estate.Clock);
            var versions = new List<(Guid VersionId, byte[] Bytes)>();
            await using (var db = await estate.Database.CreateContextAsync())
            {
                var document = await db.Set<CaseDocumentEntity>().SingleAsync();
                for (var index = 0; index < 3; index++)
                {
                    var bytes = Encoding.UTF8.GetBytes($"concurrent filed version {index}");
                    var version = new DocumentVersionEntity
                    {
                        Id = Guid.NewGuid(), DocumentId = document.Id, Version = 10 + index,
                        FileName = $"filed-{index}.bin", MediaType = "application/octet-stream",
                        ContentLength = bytes.Length, Sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
                        BoxFileId = $"box-file-{index}", BoxVersionId = $"box-version-{index}",
                        CustodyStatus = DocumentCustodyStatus.Confirmed, CreatedAtUtc = DateTimeOffset.UtcNow,
                        CreatedBy = "test"
                    };
                    db.Add(version);
                    versions.Add((version.Id, bytes));
                }
                await db.SaveChangesAsync();
            }

            await Task.WhenAll(versions.Select(item => publisher.PublishAsync(
                DocumentContentCacheKey.ForVersion(item.VersionId),
                item.Bytes,
                Convert.ToHexString(SHA256.HashData(item.Bytes)).ToLowerInvariant(),
                CancellationToken.None)));

            await using var verify = await estate.Database.CreateContextAsync();
            var entries = await verify.Set<DocumentContentCacheEntryEntity>().ToArrayAsync();
            Assert.Equal(
                versions.Select(item => item.VersionId).Order(),
                entries.Select(entry => entry.DocumentVersionId!.Value).Order());
            Assert.Equal(3, container.UploadCount);
        }
    }

    /// <summary>
    /// A completion that filed a file without holding its bytes has the
    /// publisher read the copy intake retained. That copy is Azure storage:
    /// Box is asked for nothing, and the first read is a hit.
    /// </summary>
    [Fact]
    public async Task ARetainedIntakeCopyIsPublishedFromTheRetainedCopyAndNotFromBox()
    {
        var bytes = "photograph retained by intake"u8.ToArray();
        var estate = await Estate.CreateDocumentAsync(bytes);
        await using (estate)
        {
            estate.Box.Unavailable = true;
            var publisher = await estate.RetainedCopyPublisherAsync(
                new RetainedCopies(new() { ["retained/photo"] = bytes }));

            await publisher.PublishRetainedIntakeCopyAsync(
                DocumentContentCacheKey.ForVersion(estate.Request.VersionId!.Value),
                "retained/photo",
                estate.Request.ExpectedSha256,
                bytes.LongLength,
                CancellationToken.None);

            Assert.Equal(1, estate.Blob.UploadCount);
            await using var read = await estate.Reader.OpenAsync(estate.Request, CancellationToken.None);
            Assert.Equal(bytes, await ReadAsync(read.Content));
            Assert.Equal(0, estate.Box.Downloads);
        }
    }

    /// <summary>
    /// A retained copy that is missing, or that no longer holds the bytes that
    /// were filed, publishes nothing and fails nothing.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ARetainedCopyThatIsMissingOrDiffersIsNotPublished(bool differs)
    {
        var bytes = "photograph filed to Box"u8.ToArray();
        var logger = new RecordingLogger();
        var estate = await Estate.CreateDocumentAsync(bytes, logger: logger);
        await using (estate)
        {
            var copies = new Dictionary<string, byte[]>();
            if (differs)
            {
                copies["retained/photo"] = "another photograph"u8.ToArray();
            }
            var publisher = await estate.RetainedCopyPublisherAsync(new RetainedCopies(copies), logger);

            await publisher.PublishRetainedIntakeCopyAsync(
                DocumentContentCacheKey.ForVersion(estate.Request.VersionId!.Value),
                "retained/photo",
                estate.Request.ExpectedSha256,
                bytes.LongLength,
                CancellationToken.None);

            Assert.Equal([LogLevel.Warning], logger.Levels);
            Assert.Equal(0, estate.Blob.UploadCount);
            await using var db = await estate.Database.CreateContextAsync();
            Assert.Empty(await db.Set<DocumentContentCacheEntryEntity>().ToArrayAsync());
        }
    }

    private static DocumentContentCacheKey KeyOf(ReadLogicalDocumentVersionRequest request) =>
        request.VersionId is { } versionId
            ? DocumentContentCacheKey.ForVersion(versionId)
            : DocumentContentCacheKey.ForIntakeAsset(request.IntakeAssetId!.Value);

    [Fact]
    public async Task APlainThumbnailIsKeptThirtyDaysAndAPreparedOneADay()
    {
        var estate = await Estate.CreateDocumentAsync("thumbnail lifetimes"u8.ToArray());
        await using (estate)
        {
            var versionId = estate.Request.VersionId!.Value;
            var prepared = CaseDocumentThumbnails.VariantToken(CaseAssetRotation.Clockwise90, null);
            await using var scope = estate.Database.CreateAsyncScope();
            var cache = new DocumentThumbnailCache(
                scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>(),
                new CacheContainer(estate.Blob),
                estate.Clock);

            await cache.WriteAsync(versionId, "plain rendering"u8.ToArray(), CancellationToken.None);
            await using (var db = await estate.Database.CreateContextAsync())
            {
                var plain = await db.Set<DocumentContentCacheEntryEntity>().SingleAsync();
                Assert.Equal(DocumentThumbnailCache.Variant, plain.Variant);
                Assert.Equal(estate.Clock.GetUtcNow().AddDays(30), plain.ExpiresAtUtc);
                db.Remove(plain);
                await db.SaveChangesAsync();
            }
            estate.Blob.Content = null;

            await cache.WriteAsync(versionId, prepared, "prepared rendering"u8.ToArray(), CancellationToken.None);
            await using var verify = await estate.Database.CreateContextAsync();
            var entry = await verify.Set<DocumentContentCacheEntryEntity>().SingleAsync();
            Assert.Equal(prepared, entry.Variant);
            Assert.Equal(estate.Clock.GetUtcNow().AddHours(24), entry.ExpiresAtUtc);
        }
    }

    /// <summary>
    /// The Worker's sweep makes the plain thumbnail of a newly filed image as
    /// the system worker, and a version that now has one is not a candidate
    /// again. One that cannot be rendered is not offered again for a day.
    /// </summary>
    [Fact]
    public async Task TheThumbnailSweepMakesEachNewImageThumbnailOnceAndLeavesUnrenderableOnesForADay()
    {
        var image = TransparentPng(width: 960, height: 480);
        var estate = await Estate.CreateDocumentAsync(image, "image/png");
        await using (estate)
        {
            await using (var db = await estate.Database.CreateContextAsync())
            {
                (await db.Set<DocumentVersionEntity>().SingleAsync()).CreatedAtUtc =
                    estate.Clock.GetUtcNow().AddHours(-1);
                await db.SaveChangesAsync();
            }
            await using var scope = estate.Database.CreateAsyncScope();
            var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
            var cache = new DocumentThumbnailCache(factory, new CacheContainer(estate.Blob), estate.Clock);
            var source = new ImmediateSource(image);
            var sweep = new PrepareDocumentThumbnails(
                new EfDocumentThumbnailCandidates(factory, estate.Clock),
                new CaseDocumentThumbnailReader(source, cache));

            var first = await sweep.ExecuteAsync(5, CancellationToken.None);

            Assert.Equal(new PrepareDocumentThumbnailsResult(1, 1, 0, 0, null), first);
            Assert.Equal(ActorKind.SystemWorker, Assert.Single(source.Actors).Kind);
            await using (var db = await estate.Database.CreateContextAsync())
            {
                var thumbnail = await db.Set<DocumentContentCacheEntryEntity>().SingleAsync();
                Assert.Equal(DocumentThumbnailCache.Variant, thumbnail.Variant);
                Assert.Equal(estate.Request.VersionId, thumbnail.DocumentVersionId);
            }
            estate.Clock.Advance(TimeSpan.FromDays(2));
            Assert.Equal(0, (await sweep.ExecuteAsync(5, CancellationToken.None)).Candidates);

            // Bytes the renderer cannot decode: tried once, then left for a day.
            await using (var db = await estate.Database.CreateContextAsync())
            {
                db.RemoveRange(db.Set<DocumentContentCacheEntryEntity>());
                await db.SaveChangesAsync();
            }
            var unrenderable = new PrepareDocumentThumbnails(
                new EfDocumentThumbnailCandidates(factory, estate.Clock),
                new CaseDocumentThumbnailReader(new ImmediateSource("not an image"u8.ToArray()), cache));
            Assert.Equal(
                new PrepareDocumentThumbnailsResult(1, 0, 1, 0, null),
                await unrenderable.ExecuteAsync(5, CancellationToken.None));
            Assert.Equal(0, (await unrenderable.ExecuteAsync(5, CancellationToken.None)).Candidates);
            estate.Clock.Advance(TimeSpan.FromDays(1).Add(TimeSpan.FromMinutes(1)));
            Assert.Equal(1, (await unrenderable.ExecuteAsync(5, CancellationToken.None)).Candidates);
        }
    }

    private static BoxCustodyOptions BoxOptions() => BoxCustodyOptions.Create(
        "https://api.box.com/2.0/", "https://upload.box.com/api/2.0/", "405543781910",
        """{"boxAppSettings":{"clientID":"x","appAuth":{"publicKeyID":"x","privateKey":"x","passphrase":"x"}},"enterpriseID":"x"}""",
        "x", "holding");

    private static async Task<byte[]> ReadAsync(Stream stream)
    {
        using var output = new MemoryStream();
        await stream.CopyToAsync(output);
        return output.ToArray();
    }

    private static async Task<Measurement> MeasureReadAsync(
        CachedDocumentContentStore reader,
        ReadLogicalDocumentVersionRequest request,
        byte[] expected)
    {
        var allocatedBefore = GC.GetTotalAllocatedBytes(precise: false);
        var started = Stopwatch.GetTimestamp();
        await using var content = await reader.OpenAsync(request, default);
        Assert.Equal(expected, await ReadAsync(content.Content));
        return new(
            Stopwatch.GetElapsedTime(started),
            Math.Max(0, GC.GetTotalAllocatedBytes(precise: false) - allocatedBefore));
    }

    private void WriteMeasurements(string phase, Measurement[] values, long contentLength)
    {
        var elapsed = values.Select(value => value.Elapsed.TotalMilliseconds).Order().ToArray();
        var allocated = values.Select(value => value.AllocatedBytes).Order().ToArray();
        output.WriteLine(
            "cache-{0}: sample-count={1}; content-bytes={2}; elapsed-ms-p50={3:F3}; elapsed-ms-p95={4:F3}; process-allocated-bytes-p50={5}; process-allocated-bytes-p95={6}",
            phase,
            values.Length,
            contentLength,
            Percentile(elapsed, 0.50),
            Percentile(elapsed, 0.95),
            Percentile(allocated, 0.50),
            Percentile(allocated, 0.95));
    }

    private static T Percentile<T>(T[] sorted, double percentile) =>
        sorted[(int)Math.Ceiling(percentile * sorted.Length) - 1];

    private sealed record Measurement(TimeSpan Elapsed, long AllocatedBytes);

    private static byte[] JpegWithOrientation(int width, int height, ushort orientation)
    {
        using var bitmap = new SKBitmap(
            new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque));
        using (var canvas = new SKCanvas(bitmap))
        using (var paint = new SKPaint())
        {
            var halfWidth = width / 2f;
            var halfHeight = height / 2f;
            paint.Color = SKColors.Red;
            canvas.DrawRect(0, 0, halfWidth, halfHeight, paint);
            paint.Color = SKColors.Green;
            canvas.DrawRect(halfWidth, 0, width - halfWidth, halfHeight, paint);
            paint.Color = SKColors.Blue;
            canvas.DrawRect(0, halfHeight, halfWidth, height - halfHeight, paint);
            paint.Color = SKColors.Yellow;
            canvas.DrawRect(halfWidth, halfHeight, width - halfWidth, height - halfHeight, paint);
        }
        using var image = SKImage.FromBitmap(bitmap);
        using var jpeg = image.Encode(SKEncodedImageFormat.Jpeg, quality: 100);
        var encoded = jpeg.ToArray();
        byte[] exif =
        [
            0xff, 0xe1, 0x00, 0x22,
            (byte)'E', (byte)'x', (byte)'i', (byte)'f', 0x00, 0x00,
            0x49, 0x49, 0x2a, 0x00, 0x08, 0x00, 0x00, 0x00,
            0x01, 0x00,
            0x12, 0x01, 0x03, 0x00, 0x01, 0x00, 0x00, 0x00,
            (byte)orientation, (byte)(orientation >> 8), 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00
        ];
        var oriented = new byte[encoded.Length + exif.Length];
        Buffer.BlockCopy(encoded, 0, oriented, 0, 2);
        Buffer.BlockCopy(exif, 0, oriented, 2, exif.Length);
        Buffer.BlockCopy(encoded, 2, oriented, exif.Length + 2, encoded.Length - 2);
        return oriented;
    }

    private static void AssertColorClose(SKColor actual, SKColor expected)
    {
        const int tolerance = 25;
        Assert.Equal(byte.MaxValue, actual.Alpha);
        Assert.InRange((int)actual.Red, Math.Max(0, expected.Red - tolerance), Math.Min(byte.MaxValue, expected.Red + tolerance));
        Assert.InRange((int)actual.Green, Math.Max(0, expected.Green - tolerance), Math.Min(byte.MaxValue, expected.Green + tolerance));
        Assert.InRange((int)actual.Blue, Math.Max(0, expected.Blue - tolerance), Math.Min(byte.MaxValue, expected.Blue + tolerance));
    }

    private static byte[] TransparentPng(int width, int height)
    {
        using var bitmap = new SKBitmap(
            new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        bitmap.Erase(SKColors.Transparent);
        using var image = SKImage.FromBitmap(bitmap);
        using var png = image.Encode(SKEncodedImageFormat.Png, quality: 100);
        return png.ToArray();
    }

    private sealed class Estate : IAsyncDisposable
    {
        private Estate(LocalDbTestDatabase database, CachedDocumentContentStore reader,
            CacheBlob blob, BoxHandler box, MutableTimeProvider clock,
            ReadLogicalDocumentVersionRequest request,
            IAsyncDisposable? readerScope = null)
        {
            Database = database; Reader = reader; Blob = blob; Box = box; Clock = clock;
            Request = request; this.readerScope = readerScope;
        }
        private readonly IAsyncDisposable? readerScope;
        public LocalDbTestDatabase Database { get; }
        public CachedDocumentContentStore Reader { get; }
        public CacheBlob Blob { get; }
        public BoxHandler Box { get; }
        public MutableTimeProvider Clock { get; }
        public ReadLogicalDocumentVersionRequest Request { get; }
        public Guid StaffId { get; private init; }

        public static async Task<Estate> CreateAsync(byte[] bytes)
        {
            var blob=new CacheBlob();
            var container=new CacheContainer(blob);
            var box=new BoxHandler(bytes);
            var options=BoxCustodyOptions.Create("https://api.box.com/2.0/","https://upload.box.com/api/2.0/",
                "405543781910","""{"boxAppSettings":{"clientID":"x","appAuth":{"publicKeyID":"x","privateKey":"x","passphrase":"x"}},"enterpriseID":"x"}""","x","holding");
            var clock=new MutableTimeProvider(new DateTimeOffset(2031,1,1,0,0,0,TimeSpan.Zero));
            var database = await LocalDbTestDatabase.CreateAsync(configureServices: services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(clock);
                services.AddProductionDocumentStorage(_ => container, _ => false, _ => options);
                services.RemoveAll<IBoxAuthorizationHeaderProvider>();
                services.AddSingleton<IBoxAuthorizationHeaderProvider>(new Header());
                services.AddHttpClient(nameof(BoxContentClient))
                    .ConfigurePrimaryHttpMessageHandler(() => box);
            });
            var receiptId = Guid.NewGuid(); var assetId = Guid.NewGuid();
            var staffId = Guid.NewGuid();

            var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            await using (var db = await database.CreateContextAsync())
            {
                db.Add(new IntakeReceiptEntity
                {
                    Id=receiptId, SourceFileName="source.bin", MediaType="application/octet-stream",
                    SourceLength=bytes.Length, SourceHash=hash, SourceChannel="manual_upload",
                    ExternalReceiptToken=$"cache:{receiptId:N}", ReceivedAtUtc=DateTimeOffset.UtcNow,
                    ProcessedAtUtc=DateTimeOffset.UtcNow, SourceReaderKey="test", SourceReaderVersion="1",
                    Decision="unidentified", DecisionReason="Test.", EvidenceJson="[]", FieldsJson="[]",
                    OcrCandidatesJson="[]"
                });
                db.Add(new PegasusIdentityUser
                {
                    Id = staffId,
                    UserName = $"cache-{staffId:N}@example.invalid",
                    NormalizedUserName = $"CACHE-{staffId:N}@EXAMPLE.INVALID",
                    Email = $"cache-{staffId:N}@example.invalid",
                    NormalizedEmail = $"CACHE-{staffId:N}@EXAMPLE.INVALID",
                    IsEnabled = true,
                    SecurityStamp = Guid.NewGuid().ToString("N"),
                    ConcurrencyStamp = Guid.NewGuid().ToString("N")
                });
                var roleId = await db.Roles.Where(role => role.Name == StaffRoleNames.Engineer).Select(role => role.Id).SingleAsync();
                db.Add(new IdentityUserRole<Guid> { UserId = staffId, RoleId = roleId });
                db.Add(new IntakeAssetEntity
                {
                    Id=assetId, IntakeReceiptId=receiptId, SourceLabel="source", FileName="source.bin",
                    MediaType="application/octet-stream", Kind="source", Disposition="source",
                    ContentLength=bytes.Length, ContentHash=hash, StorageKey="staging",
                    BoxFileId="box-file-1", BoxVersionId="box-version-1", BoxParentFolderId="holding",
                    CustodyStatus="confirmed"
                });
                await db.SaveChangesAsync();
            }
            var scope = database.CreateAsyncScope();
            var logicalReader=scope.ServiceProvider.GetRequiredService<IReadLogicalDocumentVersion>();
            var reader=Assert.IsType<CachedDocumentContentStore>(logicalReader);
            Assert.Same(reader, scope.ServiceProvider.GetRequiredService<IDocumentContentCacheCleanup>());
            return new Estate(database,reader,blob,box,clock,
                new(ActionActor.Staff(staffId,[StaffRole.Engineer]),null,null,assetId,null,receiptId,hash,bytes.Length),
                scope)
                { StaffId = staffId };
        }

        public static async Task<Estate> CreateDocumentAsync(
            byte[] bytes,
            string mediaType = "application/octet-stream",
            ILogger<CachedDocumentContentStore>? logger = null)
        {
            var database = await LocalDbTestDatabase.CreateAsync();
            var staffId = Guid.NewGuid();

            var receiptId = Guid.NewGuid();
            var caseId = Guid.NewGuid();
            var documentId = Guid.NewGuid();
            var versionId = Guid.NewGuid();
            var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            await using (var db = await database.CreateContextAsync())
            {
                var principal = await SeededPrincipals.QdosAsync(db);
                db.Add(new PegasusIdentityUser
                {
                    Id = staffId,
                    UserName = $"cache-{staffId:N}@example.invalid",
                    NormalizedUserName = $"CACHE-{staffId:N}@EXAMPLE.INVALID",
                    IsEnabled = true,
                    SecurityStamp = Guid.NewGuid().ToString("N"),
                    ConcurrencyStamp = Guid.NewGuid().ToString("N")
                });
                var roleId = await db.Roles.Where(role => role.Name == StaffRoleNames.Engineer).Select(role => role.Id).SingleAsync();
                db.Add(new IdentityUserRole<Guid> { UserId = staffId, RoleId = roleId });
                db.Add(new IntakeReceiptEntity
                {
                    Id = receiptId, SourceFileName = "source.bin", MediaType = "application/octet-stream",
                    SourceLength = bytes.Length, SourceHash = hash, SourceChannel = "manual_upload",
                    ExternalReceiptToken = $"cache-document:{receiptId:N}", ReceivedAtUtc = DateTimeOffset.UtcNow,
                    ProcessedAtUtc = DateTimeOffset.UtcNow, SourceReaderKey = "test", SourceReaderVersion = "1",
                    Decision = "case_created", DecisionReason = "Test.", EvidenceJson = "[]", FieldsJson = "[]",
                    OcrCandidatesJson = "[]"
                });
                db.Add(new CaseEntity
                {
                    Id = caseId, PrincipalId = principal.Id, SequenceLineageId = principal.SequenceLineageId,
                    Year = 2031, Sequence = 91, Reference = "QDOS091", Type = "inspection",
                    InitialState = "NotReady", CustodyState = "confirmed", OriginIntakeReceiptId = receiptId,
                    CustodyRootRemoteId = "holding", CreatedAtUtc = DateTimeOffset.UtcNow,
                    ConcurrencyToken = Guid.NewGuid()
                });
                db.Add(new CaseDocumentEntity
                {
                    Id = documentId, CaseId = caseId, Ordinal = 1, SourceOccurrenceIdentity = "document-cache"
                });
                db.Add(new DocumentVersionEntity
                {
                    Id = versionId, DocumentId = documentId, Version = 1, FileName = "source.bin",
                    MediaType = mediaType, ContentLength = bytes.Length, Sha256 = hash,
                    BoxFileId = "box-file-1", BoxVersionId = "box-version-1",
                    CustodyStatus = DocumentCustodyStatus.Confirmed, CreatedAtUtc = DateTimeOffset.UtcNow,
                    CreatedBy = "test", IsCurrent = true
                });
                await db.SaveChangesAsync();
            }
            await using var scope = database.CreateAsyncScope();
            var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>();
            var blob = new CacheBlob();
            var box = new BoxHandler(bytes);
            var options = BoxCustodyOptions.Create(
                "https://api.box.com/2.0/", "https://upload.box.com/api/2.0/", "405543781910",
                """{"boxAppSettings":{"clientID":"x","appAuth":{"publicKeyID":"x","privateKey":"x","passphrase":"x"}},"enterpriseID":"x"}""",
                "x", "holding");
            var clock = new MutableTimeProvider(new DateTimeOffset(2031, 1, 1, 0, 0, 0, TimeSpan.Zero));
            var reader = new CachedDocumentContentStore(
                factory, new CacheContainer(blob), new BoxContentClient(options, new HttpClient(box), new Header(), clock), clock,
                logger: logger);
            return new Estate(database, reader, blob, box, clock,
                new(ActionActor.Staff(staffId, [StaffRole.Engineer]), documentId, versionId, null,
                    caseId, null, hash, bytes.Length))
                { StaffId = staffId };
        }
        /// <summary>
        /// A store over this estate's database, cache blob and Box that can read
        /// the copies intake retained.
        /// </summary>
        public async Task<CachedDocumentContentStore> RetainedCopyPublisherAsync(
            IIntakeArtifactStore retained, ILogger<CachedDocumentContentStore>? logger = null)
        {
            await using var scope = Database.CreateAsyncScope();
            return new CachedDocumentContentStore(
                scope.ServiceProvider.GetRequiredService<IDbContextFactory<PegasusDbContext>>(),
                new CacheContainer(Blob),
                new BoxContentClient(BoxOptions(), new HttpClient(Box), new Header(), Clock),
                Clock,
                logger: logger,
                intakeArtifacts: retained);
        }

        public async ValueTask DisposeAsync()
        {
            if (readerScope is not null) await readerScope.DisposeAsync();
            await Database.DisposeAsync();
        }
    }

    /// <summary>The copies intake retained, by storage key.</summary>
    private sealed class RetainedCopies(Dictionary<string, byte[]> copies) : IIntakeArtifactStore
    {
        public Task<string> StoreAsync(
            string contentHash, ReadOnlyMemory<byte> content, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<StagedArtifactInventoryItem> StageAsync(
            Guid stagedReceiptId, string contentHash, Stream content, long contentLength,
            DateTimeOffset firstSeenAtUtc, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ReadOnlyMemory<byte>?> ReadAsync(string storageKey, CancellationToken cancellationToken) =>
            Task.FromResult(copies.TryGetValue(storageKey, out var bytes) ? (ReadOnlyMemory<byte>?)bytes : null);
    }

    private sealed class GatedThumbnailSource(byte[] content) : IReadLogicalDocumentVersion
    {
        private readonly TaskCompletionSource firstRead = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int reads;

        public Task FirstRead => firstRead.Task;

        public int Reads => Volatile.Read(ref reads);

        public void Release() => release.TrySetResult();

        public async Task<LogicalDocumentContent> OpenAsync(
            ReadLogicalDocumentVersionRequest request,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref reads);
            firstRead.TrySetResult();
            await release.Task.WaitAsync(cancellationToken);
            return new LogicalDocumentContent(
                new MemoryStream(content, writable: false),
                request.DocumentId,
                request.VersionId,
                request.IntakeAssetId,
                request.ExpectedSha256,
                request.ExpectedContentLength,
                "source.png",
                "image/png");
        }
    }

    /// <summary>The verified full bytes, answered at once, with who asked.</summary>
    private sealed class ImmediateSource(byte[] content) : IReadLogicalDocumentVersion
    {
        public List<ActionActor> Actors { get; } = [];

        public Task<LogicalDocumentContent> OpenAsync(
            ReadLogicalDocumentVersionRequest request,
            CancellationToken cancellationToken)
        {
            Actors.Add(request.Actor);
            return Task.FromResult(new LogicalDocumentContent(
                new MemoryStream(content, writable: false),
                request.DocumentId,
                request.VersionId,
                request.IntakeAssetId,
                request.ExpectedSha256,
                content.LongLength,
                "source.png",
                "image/png"));
        }
    }

    /// <summary>Every SQL command a context sends, whatever kind.</summary>
    private sealed class CountingCommandInterceptor : DbCommandInterceptor
    {
        private int count;

        public int Count => Volatile.Read(ref count);

        public void Reset() => Interlocked.Exchange(ref count, 0);

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref count);
            return ValueTask.FromResult(result);
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref count);
            return ValueTask.FromResult(result);
        }

        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<object> result,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref count);
            return ValueTask.FromResult(result);
        }
    }

    private sealed class CancellingReadStream(CancellationTokenSource cancellation) : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => 1;

        public override long Position
        {
            get => 0;
            set => throw new NotSupportedException();
        }

        public override void Flush() => throw new NotSupportedException();

        public override Task FlushAsync(CancellationToken cancellationToken) =>
            Task.FromException(new NotSupportedException());

        public override int Read(byte[] buffer, int offset, int count) =>
            throw new InvalidOperationException("The thumbnail renderer must use the cancellable read path.");

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            cancellation.Cancel();
            return ValueTask.FromCanceled<int>(cancellation.Token);
        }

        public override Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class CacheContainer(CacheBlob blob) : BlobContainerClient
    {
        public override BlobClient GetBlobClient(string blobName) { blob.BlobName=blobName; return blob; }
    }

    /// <summary>A container that keeps one blob for each name it is asked for.</summary>
    private sealed class NamedCacheContainer : BlobContainerClient
    {
        private readonly Dictionary<string, CacheBlob> blobs = [];

        public int UploadCount
        {
            get { lock (blobs) { return blobs.Values.Sum(blob => blob.UploadCount); } }
        }

        public override BlobClient GetBlobClient(string blobName)
        {
            lock (blobs)
            {
                if (!blobs.TryGetValue(blobName, out var blob))
                {
                    blob = new CacheBlob { BlobName = blobName };
                    blobs[blobName] = blob;
                }
                return blob;
            }
        }
    }

    private sealed class RecordingLogger : ILogger<CachedDocumentContentStore>
    {
        private readonly Lock guard = new();
        private readonly List<LogLevel> levels = [];

        public IReadOnlyList<LogLevel> Levels
        {
            get
            {
                lock (guard)
                {
                    return [.. levels];
                }
            }
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            lock (guard)
            {
                levels.Add(logLevel);
            }
        }
    }

    private sealed class CacheBlob : BlobClient
    {
        private ETag etag = new("\"1\"");
        public string BlobName { get; set; }="";
        public byte[]? Content { get; set; }
        public int DeleteCount { get; private set; }
        public int UploadCount { get; private set; }
        public bool DeleteResult { get; set; } = true;
        public bool MissingContentIsNotFound { get; set; }
        public bool DeletePreconditionFails { get; set; }
        /// <summary>Answers every upload with the store being unavailable.</summary>
        public bool FailUploads { get; set; }
        public void ReplaceContent(byte[] content) { Content = content; etag = new ETag("\"2\""); }
        public override string Name=>BlobName;
        public override Task<Response<BlobContentInfo>> UploadAsync(Stream content,BlobUploadOptions options,CancellationToken token=default)
        {
            if (FailUploads)
            {
                return Task.FromException<Response<BlobContentInfo>>(
                    new RequestFailedException(503, "The blob store is unavailable."));
            }
            lock (this)
            {
                if (Content is not null)
                {
                    // What Azure answers an If-None-Match: * upload that lost the race.
                    return Task.FromException<Response<BlobContentInfo>>(
                        new RequestFailedException(409, "The specified blob already exists.", "BlobAlreadyExists", null));
                }
                using var m=new MemoryStream();
                content.CopyTo(m);
                Content=m.ToArray();
                UploadCount++;
                return Task.FromResult(Response.FromValue(
                    BlobsModelFactory.BlobContentInfo(etag,DateTimeOffset.UtcNow,null,null,0),
                    new StubResponse()));
            }
        }
        public override Task<Response<BlobProperties>> GetPropertiesAsync(BlobRequestConditions? conditions=null,CancellationToken token=default)=>
            Task.FromResult(Response.FromValue(BlobsModelFactory.BlobProperties(contentLength:Content?.LongLength??0,eTag:etag,metadata:new Dictionary<string,string>{{"sha256",Convert.ToHexString(SHA256.HashData(Content??[])).ToLowerInvariant()}}),new StubResponse()));
        public override Task<Response<BlobDownloadStreamingResult>> DownloadStreamingAsync(BlobDownloadOptions? options=null,CancellationToken token=default)
        {
            if (Content is null && MissingContentIsNotFound)
            {
                return Task.FromException<Response<BlobDownloadStreamingResult>>(
                    new RequestFailedException(404, "missing"));
            }
            return Task.FromResult(Response.FromValue(
                BlobsModelFactory.BlobDownloadStreamingResult(new MemoryStream(Content ?? [])),
                new StubResponse()));
        }
        public override Task<Response<bool>> DeleteIfExistsAsync(DeleteSnapshotsOption option=default,BlobRequestConditions? conditions=null,CancellationToken token=default)
        {
            DeleteCount++;
            if (DeletePreconditionFails || (conditions?.IfMatch is { } expected && expected != etag))
            {
                return Task.FromException<Response<bool>>(new RequestFailedException(412, "condition"));
            }
            Content=null;
            return Task.FromResult(Response.FromValue(DeleteResult,new StubResponse()));
        }
    }
    private sealed class BoxHandler(byte[] bytes) : HttpMessageHandler
    {
        private byte[] served = bytes;
        /// <summary>Serves these bytes from now on, whatever custody recorded.</summary>
        public void Serve(byte[] other) => served = other;
        public int Downloads { get; private set; } public string? RequestedVersion { get; private set; } public bool Unavailable { get; set; }
        /// <summary>The folder Box reports as the file's parent: the holding folder unless a test files it elsewhere.</summary>
        public string FileParent { get; set; } = "holding";
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            var path=request.RequestUri!.AbsolutePath;
            if(path.EndsWith("/content",StringComparison.Ordinal)){ Downloads++; RequestedVersion=System.Web.HttpUtility.ParseQueryString(request.RequestUri.Query)["version"]; if(Unavailable)return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)); return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(served)}); }
            var body=path.Contains("/files/",StringComparison.Ordinal)
                ? $$$"""{"id":"box-file-1","type":"file","parent":{"id":"{{{FileParent}}}"}}"""
                : path.Contains($"/folders/{FileParent}",StringComparison.Ordinal)
                    ? $$$"""{"id":"{{{FileParent}}}","type":"folder","parent":{"id":"405543781910"}}"""
                    : """{"id":"405543781910","type":"folder"}""";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(body,Encoding.UTF8,"application/json")});
        }
    }
    private sealed class Header : IBoxAuthorizationHeaderProvider { public Task<string> GetAuthorizationHeaderAsync(CancellationToken token)=>Task.FromResult("Bearer x"); public Task<bool> RenewIfDueAsync(CancellationToken token)=>Task.FromResult(false); }
    private sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow()=>now; public void Advance(TimeSpan value)=>now=now.Add(value); }
    private sealed class StubResponse : Response
    {
        public override int Status=>200; public override string ReasonPhrase=>"OK"; public override Stream? ContentStream{get;set;} public override string ClientRequestId{get;set;}="";
        public override void Dispose(){} protected override bool ContainsHeader(string name)=>false; protected override IEnumerable<HttpHeader> EnumerateHeaders()=>[]; protected override bool TryGetHeader(string name,out string value){value="";return false;} protected override bool TryGetHeaderValues(string name,out IEnumerable<string> values){values=[];return false;}
    }
}
