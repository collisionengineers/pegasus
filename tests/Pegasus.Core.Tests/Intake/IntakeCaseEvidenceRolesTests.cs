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

    /// <summary>
    /// A file uploaded to a declared Case takes the type chosen for it, Other
    /// when none; a photograph is an Image whatever was chosen (operator, 30
    /// October 2026; R564, R565).
    /// </summary>
    [Theory]
    [InlineData("estimate.pdf", "application/pdf", DocumentSemanticRole.Estimate, DocumentSemanticRole.Estimate)]
    [InlineData("letter.pdf", "application/pdf", null, DocumentSemanticRole.Other)]
    [InlineData("front.jpg", "image/jpeg", DocumentSemanticRole.Estimate, DocumentSemanticRole.Image)]
    [InlineData("rear.png", "image/png", null, DocumentSemanticRole.Image)]
    [InlineData("notes.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", DocumentSemanticRole.Correspondence, DocumentSemanticRole.Correspondence)]
    public void ADeclaredUploadTakesTheChosenTypeOrOther(
        string fileName,
        string mediaType,
        DocumentSemanticRole? chosen,
        DocumentSemanticRole expected)
    {
        Assert.NotNull(fileName);
        Assert.Equal(expected, IntakeCaseEvidenceRoles.ForDeclaredUpload(mediaType, chosen));
    }
}
