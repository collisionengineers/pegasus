using System.Text.Json;
using Pegasus.Core.Cases;
using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake;

public sealed class InstructionReviewFieldTests
{
    [Theory]
    [InlineData("Claim number", CaseDataFieldNames.ClaimNumber)]
    [InlineData("Claim reference", CaseDataFieldNames.ClaimNumber)]
    [InlineData("Date of incident", CaseDataFieldNames.IncidentDate)]
    [InlineData("Incident date", CaseDataFieldNames.IncidentDate)]
    [InlineData("Vehicle make", CaseDataFieldNames.VehicleMake)]
    [InlineData("Vehicle make and model", CaseDataFieldNames.VehicleMake)]
    [InlineData("Claimant mobile telephone", CaseDataFieldNames.ClaimantContactNumber)]
    [InlineData("Claimant home telephone", CaseDataFieldNames.ClaimantContactNumber)]
    [InlineData("Repairer name", CaseDataFieldNames.RepairerName)]
    [InlineData("Repairer address", CaseDataFieldNames.RepairerAddress)]
    [InlineData("Insurer policy number", null)]
    public void CurrentProfileBindingsResolveWithoutChangingPrintedEvidence(string name, string? expected)
    {
        var field = new InstructionReviewField(name, null, [], false, false);
        var before = JsonSerializer.Serialize(field);

        Assert.Equal(expected, field.ToCaseDataFieldName());

        Assert.Equal(name, field.Name);
        Assert.Equal(before, JsonSerializer.Serialize(field));
        Assert.DoesNotContain("CaseDataFieldName", before, StringComparison.Ordinal);
    }
}
