using CollisionDocNet.Core;

namespace CollisionDocNet.Core.Tests;

[TestClass]
public sealed class ResourceBudgetTests
{
    [TestMethod]
    [DataRow(ResourceKind.InputBytes)]
    [DataRow(ResourceKind.DecodedBytes)]
    [DataRow(ResourceKind.Objects)]
    [DataRow(ResourceKind.TextCharacters)]
    [DataRow(ResourceKind.Assets)]
    [DataRow(ResourceKind.AssetBytes)]
    public void TryCharge_AtLimitAcceptsThenRejectsWithoutChangingSnapshot(ResourceKind kind)
    {
        var budget = new ResourceBudget(CreateLimits(maximum: 2));

        Assert.IsTrue(budget.TryCharge(kind, 2));
        ResourceBudgetSnapshot atLimit = budget.GetSnapshot();
        Assert.IsFalse(budget.TryCharge(kind, 1));

        Assert.AreEqual(atLimit, budget.GetSnapshot());
    }

    [TestMethod]
    public void TryCharge_NegativeAmount_Throws()
    {
        var budget = new ResourceBudget(CreateLimits());

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => budget.TryCharge(ResourceKind.InputBytes, -1));
    }

    [TestMethod]
    public void TryObserveNestingDepth_RecordsMaximumAndRejectsExcess()
    {
        var budget = new ResourceBudget(CreateLimits(maximum: 5));

        Assert.IsTrue(budget.TryObserveNestingDepth(3));
        Assert.IsTrue(budget.TryObserveNestingDepth(2));
        Assert.IsFalse(budget.TryObserveNestingDepth(6));

        Assert.AreEqual(3, budget.GetSnapshot().MaximumNestingDepth);
    }

    [TestMethod]
    public void DefaultLimits_UseCurrentCollisionSpikeInputLimit()
    {
        ResourceLimits limits = ResourceLimits.CreateCollisionSpikeDefault();

        Assert.AreEqual(10 * 1024 * 1024, limits.MaxInputBytes);
        Assert.AreEqual(ResourceLimits.CollisionSpikeTenMegabytePolicy, limits.PolicyId);
    }

    [TestMethod]
    public void Constructor_InputTooLargeForManagedMaterialization_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new ResourceLimits(
                "test/1",
                (long)Array.MaxLength + 1,
                1,
                1,
                1,
                1,
                1,
                1,
                TimeSpan.FromSeconds(1)));
    }

    private static ResourceLimits CreateLimits(int maximum = 10) =>
        new(
            "test/1",
            maximum,
            maximum,
            maximum,
            maximum,
            maximum,
            maximum,
            maximum,
            TimeSpan.FromSeconds(1));
}
