using BenchmarkDotNet.Attributes;
using CollisionDocNet.Extraction;
using CollisionDocNet.Model;
using CollisionDocNet.Storage.Detection;

namespace CollisionDocNet.Performance;

[MemoryDiagnoser]
public class DetectionBenchmarks
{
    private readonly SyntheticInputSet _inputs = SyntheticInputSet.Create();

    [Benchmark]
    public FormatDetectionResult DetectPdfOneMegabyte() => Detect(_inputs.PdfOneMegabyte, "evidence.pdf");

    [Benchmark]
    public FormatDetectionResult DetectDoc() => Detect(_inputs.Doc, "evidence.doc");

    [Benchmark]
    public FormatDetectionResult DetectDocxOneMegabyte() => Detect(_inputs.DocxOneMegabyte, "evidence.docx");

    [Benchmark]
    public FormatDetectionResult DetectMsg() => Detect(_inputs.Msg, "evidence.msg");

    [Benchmark]
    public FormatDetectionResult DetectEmlOneMegabyte() => Detect(_inputs.EmlOneMegabyte, "evidence.eml");

    private static FormatDetectionResult Detect(byte[] input, string fileName) =>
        FileFormatDetector.Detect(input, fileName, limits: SyntheticInputSet.DetectionLimits);
}

[MemoryDiagnoser]
public class DispatcherBenchmarks
{
    private readonly SyntheticInputSet _inputs = SyntheticInputSet.Create();

    [Benchmark]
    public ValueTask<ExtractionResult> ExtractPdf() => Extract(_inputs.Pdf, "evidence.pdf", "application/pdf");

    [Benchmark]
    public ValueTask<ExtractionResult> ExtractDoc() => Extract(_inputs.Doc, "evidence.doc", "application/msword");

    [Benchmark]
    public ValueTask<ExtractionResult> ExtractDocx() => Extract(_inputs.Docx, "evidence.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document");

    [Benchmark]
    public ValueTask<ExtractionResult> ExtractMsg() => Extract(_inputs.Msg, "evidence.msg", "application/vnd.ms-outlook");

    [Benchmark]
    public ValueTask<ExtractionResult> ExtractEml() => Extract(_inputs.Eml, "evidence.eml", "message/rfc822");

    private static ValueTask<ExtractionResult> Extract(byte[] input, string fileName, string mediaType) =>
        DocumentExtractor.ExtractAsync(input, "synthetic-performance-input", fileName, mediaType);
}
