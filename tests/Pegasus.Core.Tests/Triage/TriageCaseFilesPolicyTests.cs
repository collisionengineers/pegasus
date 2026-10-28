using Pegasus.Core.Documents;
using Pegasus.Core.Triage;

namespace Pegasus.Core.Tests.Triage;

public sealed class TriageCaseFilesPolicyTests
{
    [Fact]
    public void EveryFileIsCopiedOnceExceptOneTheCaseAlreadyHolds()
    {
        var email = new TriageCaseFile(Guid.NewGuid(), "aa01", DocumentSemanticRole.OriginalSource);
        var photo = new TriageCaseFile(Guid.NewGuid(), "BB02", DocumentSemanticRole.Image);
        var samePhoto = new TriageCaseFile(Guid.NewGuid(), "bb02", DocumentSemanticRole.Image);
        var pdf = new TriageCaseFile(Guid.NewGuid(), "cc03", DocumentSemanticRole.Instruction);

        var copied = TriageCaseFilesPolicy.SelectFilesToCopy([email, photo, samePhoto, pdf], ["CC03"]);

        Assert.Equal([email, photo], copied);
        Assert.True(TriageCaseFilesPolicy.CompletesImages(copied));
        Assert.False(TriageCaseFilesPolicy.CompletesImages([email, pdf]));
    }
}
