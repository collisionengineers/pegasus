using Pegasus.Core.Identity;
using Pegasus.Core.Reports;

namespace Pegasus.Core.Tests.Reports;

public sealed class CaseListPresetsTests
{
    [Fact]
    public async Task APresetIsSavedWithATrimmedNameAndItsColumnsInCatalogueOrder()
    {
        var store = new Store();
        var id = Guid.NewGuid();

        await new SaveCaseListPreset(store).ExecuteAsync(
            new(id, "  Invoicing ", ["agreed_fee.inspection", "case.reference", "case.reference"], 0, Administrator(), Guid.NewGuid().ToString("N")),
            default);

        var saved = Assert.Single(store.Saved);
        Assert.Equal("Invoicing", saved.Name);
        Assert.Equal(["case.reference", "agreed_fee.inspection"], saved.ColumnKeys);
    }

    [Fact]
    public async Task APresetNeedsANameAndAtLeastOneKnownColumn()
    {
        var save = new SaveCaseListPreset(new Store());
        var operation = Guid.NewGuid().ToString("N");

        var noName = await Assert.ThrowsAsync<ArgumentException>(() => save.ExecuteAsync(
            new(Guid.NewGuid(), " ", ["case.reference"], 0, Administrator(), operation), default));
        Assert.Equal("name", noName.ParamName);
        await Assert.ThrowsAsync<ArgumentException>(() => save.ExecuteAsync(
            new(Guid.NewGuid(), new string('x', CaseListPresetPolicy.MaximumNameLength + 1), ["case.reference"], 0, Administrator(), operation), default));
        var noColumns = await Assert.ThrowsAsync<ArgumentException>(() => save.ExecuteAsync(
            new(Guid.NewGuid(), "Empty", [], 0, Administrator(), operation), default));
        Assert.Equal("keys", noColumns.ParamName);
        await Assert.ThrowsAsync<ArgumentException>(() => save.ExecuteAsync(
            new(Guid.NewGuid(), "Unknown", ["report.types"], 0, Administrator(), operation), default));
    }

    [Fact]
    public async Task OnlyAnAdministratorKeepsPresets()
    {
        var engineer = ActionActor.Staff(Guid.NewGuid(), [StaffRole.Engineer]);
        var store = new Store();

        await Assert.ThrowsAsync<StaffAuthorizationException>(() => new ListCaseListPresets(store).ExecuteAsync(engineer, default));
        await Assert.ThrowsAsync<StaffAuthorizationException>(() => new SaveCaseListPreset(store).ExecuteAsync(
            new(Guid.NewGuid(), "Mine", ["case.reference"], 0, engineer, Guid.NewGuid().ToString("N")), default));
        await Assert.ThrowsAsync<StaffAuthorizationException>(() => new RemoveCaseListPreset(store).ExecuteAsync(
            new(Guid.NewGuid(), 1, engineer, Guid.NewGuid().ToString("N")), default));
        Assert.Empty(store.Saved);
    }

    [Fact]
    public async Task ARemovalNamesThePresetAtTheVersionItWasRead()
    {
        var remove = new RemoveCaseListPreset(new Store());

        await Assert.ThrowsAsync<ArgumentException>(() => remove.ExecuteAsync(
            new(Guid.Empty, 1, Administrator(), Guid.NewGuid().ToString("N")), default));
        await Assert.ThrowsAsync<ArgumentException>(() => remove.ExecuteAsync(
            new(Guid.NewGuid(), 0, Administrator(), Guid.NewGuid().ToString("N")), default));
    }

    private static ActionActor Administrator() => ActionActor.Staff(Guid.NewGuid(), [StaffRole.Administrator]);

    private sealed class Store : ICaseListPresetStore
    {
        public List<SaveCaseListPresetRequest> Saved { get; } = [];

        public Task<IReadOnlyList<CaseListPreset>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CaseListPreset>>([]);

        public Task<CaseListPreset> SaveAsync(SaveCaseListPresetRequest request, CancellationToken cancellationToken)
        {
            Saved.Add(request);
            return Task.FromResult(new CaseListPreset(request.PresetId, request.Name, request.ColumnKeys, request.ExpectedVersion + 1, "staff", DateTimeOffset.UnixEpoch));
        }

        public Task<CaseListPreset> RemoveAsync(RemoveCaseListPresetRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Not reached by these tests.");
    }
}
