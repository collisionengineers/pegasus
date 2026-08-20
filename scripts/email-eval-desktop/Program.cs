using Pegasus.Core.Intake;
using Pegasus.Infrastructure.Intake;

namespace Pegasus.EmailEvaluation.Desktop;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        var workflow = new EmailEvaluationWorkflow(
            new MimeKitPdfPigOpenXmlIntakeSourceReader(TimeProvider.System),
            new QdosInstructionExtractionPolicy(),
            new QdosMailClassificationPolicy(),
            CategoryCatalog.Load());
        Application.Run(new MainForm(workflow));
    }
}
