using Microsoft.EntityFrameworkCore;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

/// <summary>
/// What the context does to its tracker before every save: each added Case
/// gets its primary work and each added or modified token entity gets a new
/// concurrency token. The step runs on the tracker alone, so nothing here
/// opens a connection; <see cref="ConcurrencyTokenPersistenceTests"/> is the
/// end-to-end proof through SaveChangesAsync.
/// </summary>
public sealed class PegasusDbContextSaveChangesTests
{
    private const string ConnectionString = "Server=tcp:127.0.0.1,1;Database=Unused;Encrypt=False";

    private static readonly DateTimeOffset FixedUtcNow =
        new(2031, 5, 6, 10, 30, 0, TimeSpan.Zero);

    private static readonly Guid InitialToken = Guid.Parse("5b0c1c7e-6f39-4f6e-9d0e-2a4f4c1b8e11");

    [Fact]
    public void AnAddedCaseWithoutAPrimaryWorkGetsOneWhoseIdIsTheCaseId()
    {
        using var context = CreateContext();
        var added = NewCase(FixedUtcNow);
        var modified = NewCase(FixedUtcNow.AddDays(1));
        context.Cases.Add(added);
        context.Cases.Attach(modified);
        context.Entry(modified).State = EntityState.Modified;

        context.PrepareChangesForSave();

        var work = Assert.Single(context.ChangeTracker.Entries<CaseWorkEntity>());
        Assert.Equal(EntityState.Added, work.State);
        Assert.Equal(added.Id, work.Entity.Id);
        Assert.Equal(added.Id, work.Entity.CaseId);
        Assert.Same(added, work.Entity.Case);
        Assert.Equal(CaseWorkKinds.Primary, work.Entity.Kind);
        Assert.Equal(added.CreatedAtUtc, work.Entity.CreatedAtUtc);
        AssertFreshToken(added.ConcurrencyToken);
        AssertFreshToken(modified.ConcurrencyToken);
    }

    [Fact]
    public void ACaseWhoseWorkIsAlreadyTrackedGetsNoSecondWork()
    {
        using var context = CreateContext();
        var states = new[]
        {
            EntityState.Added,
            EntityState.Unchanged,
            EntityState.Modified,
            EntityState.Deleted
        };
        var tracked = states.Select(state =>
        {
            var caseEntity = NewCase(FixedUtcNow);
            var work = NewPrimaryWork(caseEntity);
            context.Cases.Add(caseEntity);
            context.Entry(work).State = state;
            return (Case: caseEntity, Work: work, State: state);
        }).ToList();

        context.PrepareChangesForSave();

        var entries = context.ChangeTracker.Entries<CaseWorkEntity>().ToList();
        Assert.Equal(states.Length, entries.Count);
        foreach (var (caseEntity, work, state) in tracked)
        {
            var entry = Assert.Single(entries, candidate => candidate.Entity.CaseId == caseEntity.Id);
            Assert.Same(work, entry.Entity);
            Assert.Equal(state, entry.State);
        }
    }

    [Fact]
    public void OnlyAddedAndModifiedTokenEntitiesGetAFreshToken()
    {
        using var context = CreateContext();
        var presets = context.Set<CaseListPresetEntity>();
        var added = NewPreset();
        var edited = NewPreset();
        var untouched = NewPreset();
        var removed = NewPreset();
        presets.Add(added);
        presets.AttachRange(edited, untouched, removed);
        presets.Remove(removed);
        edited.Name = "Edited";

        context.PrepareChangesForSave();

        AssertFreshToken(added.ConcurrencyToken);
        AssertFreshToken(edited.ConcurrencyToken);
        Assert.Equal(InitialToken, untouched.ConcurrencyToken);
        Assert.Equal(InitialToken, removed.ConcurrencyToken);
        Assert.Equal(EntityState.Modified, context.Entry(edited).State);
        Assert.Equal(EntityState.Unchanged, context.Entry(untouched).State);
        Assert.Equal(EntityState.Deleted, context.Entry(removed).State);
    }

    [Fact]
    public void AnEmptyOrUnchangedTrackerIsLeftAsItWas()
    {
        using (var empty = CreateContext())
        {
            empty.PrepareChangesForSave();

            Assert.Empty(empty.ChangeTracker.Entries());
        }

        using var context = CreateContext();
        var caseEntity = NewCase(FixedUtcNow);
        var work = NewPrimaryWork(caseEntity);
        var preset = NewPreset();
        context.Cases.Attach(caseEntity);
        context.CaseWorks.Attach(work);
        context.Set<CaseListPresetEntity>().Attach(preset);

        context.PrepareChangesForSave();

        Assert.Equal(3, context.ChangeTracker.Entries().Count());
        Assert.Equal(EntityState.Unchanged, context.Entry(caseEntity).State);
        Assert.Equal(EntityState.Unchanged, context.Entry(work).State);
        Assert.Equal(EntityState.Unchanged, context.Entry(preset).State);
        Assert.Equal(InitialToken, caseEntity.ConcurrencyToken);
        Assert.Equal(InitialToken, preset.ConcurrencyToken);
    }

    private static PegasusDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<PegasusDbContext>();
        PegasusSqlServer.Configure(options, ConnectionString);
        return new PegasusDbContext(options.Options);
    }

    private static CaseEntity NewCase(DateTimeOffset createdAtUtc) => new()
    {
        Id = Guid.NewGuid(),
        Reference = "QDOS31001",
        Type = CaseTypeCodes.Inspection,
        CustodyState = "pending",
        CreatedAtUtc = createdAtUtc,
        ConcurrencyToken = InitialToken
    };

    private static CaseWorkEntity NewPrimaryWork(CaseEntity caseEntity) => new()
    {
        Id = caseEntity.Id,
        CaseId = caseEntity.Id,
        Kind = CaseWorkKinds.Primary,
        CreatedAtUtc = caseEntity.CreatedAtUtc
    };

    private static CaseListPresetEntity NewPreset() => new()
    {
        Id = Guid.NewGuid(),
        Name = "Preset",
        ColumnKeysJson = "[]",
        UpdatedBy = "save-changes-test",
        UpdatedAtUtc = FixedUtcNow,
        ConcurrencyToken = InitialToken
    };

    private static void AssertFreshToken(Guid token)
    {
        Assert.NotEqual(Guid.Empty, token);
        Assert.NotEqual(InitialToken, token);
    }
}
