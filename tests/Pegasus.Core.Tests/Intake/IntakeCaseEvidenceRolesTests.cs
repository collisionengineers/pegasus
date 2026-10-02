using Pegasus.Core.Documents;
using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake;

/// <summary>
/// The role a filed intake file takes on its Case decides the Case page tab it
/// appears under. A photograph is an image even when it is the received file
/// itself, as when a photograph is added to a Case directly.
/// </summary>
public sealed class IntakeCaseEvidenceRolesTests
{
    [Theory]
    [InlineData(IntakeAssetKind.Source, "image/jpeg", DocumentSemanticRole.Image)]
    [InlineData(IntakeAssetKind.Source, "image/png", DocumentSemanticRole.Image)]
    [InlineData(IntakeAssetKind.Source, "message/rfc822", DocumentSemanticRole.OriginalSource)]
    [InlineData(IntakeAssetKind.Source, "application/pdf", DocumentSemanticRole.OriginalSource)]
    [InlineData(IntakeAssetKind.Source, "image/svg+xml", DocumentSemanticRole.OriginalSource)]
    [InlineData(IntakeAssetKind.Attachment, "image/png", DocumentSemanticRole.Image)]
    [InlineData(IntakeAssetKind.Attachment, "application/pdf", DocumentSemanticRole.Correspondence)]
    public void APhotographIsAnImageWhateverItsKind(
        IntakeAssetKind kind,
        string mediaType,
        DocumentSemanticRole expected)
    {
        Assert.Equal(expected, IntakeCaseEvidenceRoles.For(kind, mediaType));
    }
}
