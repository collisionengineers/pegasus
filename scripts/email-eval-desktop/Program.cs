using Pegasus.Core.Intake;
using Pegasus.Infrastructure.Intake;

namespace Pegasus.EmailEvaluation.Desktop;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        var repositoryRoot = FindRepositoryRoot(AppContext.BaseDirectory)
            ?? throw new InvalidOperationException("The Pegasus repository root could not be located.");
        var catalog = CategoryCatalog.Load(repositoryRoot);
        var workflow = new EmailEvaluationWorkflow(
            new MimeKitPdfPigOpenXmlIntakeSourceReader(TimeProvider.System),
            new QdosInstructionExtractionPolicy(),
            catalog);
        Application.Run(new MainForm(workflow));
    }

    private static string? FindRepositoryRoot(string start)
    {
        var directory = new DirectoryInfo(start);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "docs", "reference", "CollisionSPikeCurrenttree.txt")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
