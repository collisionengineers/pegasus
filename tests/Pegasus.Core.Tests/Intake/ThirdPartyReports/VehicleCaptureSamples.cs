using Pegasus.Core.Intake;

namespace Pegasus.Core.Tests.Intake.ThirdPartyReports;

/// <summary>
/// A Tractable capture PDF as the production reader (PdfPig content order)
/// reads it, transcribed from the local samples: the July layout prints an
/// Accident Date and a Case ID, the August layout a Claim Id and no accident
/// date. Image sizes are the samples' own.
/// </summary>
internal static class VehicleCaptureSamples
{
    public const string MailLabel = "mail.eml";
    public const string PdfLabel = MailLabel + ", attachment 1: LINE_LEVEL_ESTIMATE.pdf";

    public const string JulyFirstPage = """
        collisionengineers
        desk@collisionengineers.co.uk
        https://collisionengineers.co.uk/
        Accident Date: Fri Jul 17 2026 Submission date: Fri Jul 17 2026 Case ID: 9992bf95-964d-4d6f-93c7-fa6cdf1bea67
        Case Details
        Initial repair estimate: £4,032.57 (excluding tax)
        Vehicle Information
        Producer Toyota
        Model Prius
        Type -
        Year of Production 2017
        VIN JTDKB3FU703577122
        Registration Number WP67YBO
        Registration Date -
        Mileage 251,525
        Vehicle Value £2,780.00
        Repair Summary
        Part Costs £1,762.57
        Powered by Page 1 of 3
        """;

    public const string AugustFirstPage = """
        collisionengineers
        desk@collisionengineers.co.uk
        https://collisionengineers.co.uk/
        Submission date: Thu Aug 06 2026 Claim Id: DK11SXP
        Case Details
        Initial repair estimate: £812.80 (excluding tax)
        Vehicle Information
        Producer Audi
        Model A3
        Type -
        Year of Production 2011
        VIN WAUZZZ8P1BA141110
        Registration Number Dk11SXP
        Registration Date -
        Mileage 78,904
        Vehicle Value £4,991.00
        Repair Summary
        Powered by 1
        """;

    public const string EstimatePage = """
        Preliminary AI Estimate
        Totals Rate Hours Totals (£)
        Labour 767.00
        Total excluding tax 2,620.91
        Powered by 2
        """;

    public const string ImagesPage = """
        Submitted Vehicle Images
        Powered by Page 3 of 3
        """;

    /// <summary>
    /// An e-mail carrying one capture PDF: page 1 has the letterhead logo and
    /// the page icon, page 3 the icon, a small photograph, a large one and a
    /// byte-identical repeat of the large one.
    /// </summary>
    public static IntakeSourceReadResult Mail(
        string firstPage = JulyFirstPage,
        string estimatePage = EstimatePage) =>
        new(
            IntakeSourceReadStatus.Readable,
            [
                new(IntakeEvidenceSource.EmailBody, MailLabel + ", message body", "New completed lead"),
                Page(1, firstPage),
                Page(2, estimatePage),
                Page(3, ImagesPage)
            ],
            [new(IntakeEvidenceSource.Sender, "noreply@tractable.ai")],
            [],
            RequiresOcr: false,
            Assets:
            [
                new(PdfLabel, "LINE_LEVEL_ESTIMATE.pdf", "application/pdf", new byte[200_000],
                    IntakeAssetKind.Attachment, IntakeAssetDisposition.Attachment),
                Image(1, 1, "image/png", 43_507, 1016, 565),
                Image(1, 2, "image/png", 1_104, 70, 65),
                Image(3, 1, "image/jpeg", 30_236, 800, 600),
                Image(3, 2, "image/jpeg", 128_044, 800, 1067),
                Image(3, 3, "image/jpeg", 128_044, 800, 1067),
                Image(3, 4, "image/png", 1_104, 70, 65)
            ]);

    public static string ImageLabel(int page, int image) => $"{PdfLabel}, page {page}, image {image}";

    private static IntakeContentFragment Page(int page, string text) =>
        new(IntakeEvidenceSource.PdfContent, $"{PdfLabel}, page {page}", text, IntakeSourceLocator.ForPage(page));

    private static IntakeAssetCandidate Image(int page, int image, string mediaType, int bytes, int width, int height) =>
        new(
            ImageLabel(page, image),
            $"page-{page}-image-{image}{(mediaType == "image/jpeg" ? ".jpg" : ".png")}",
            mediaType,
            new byte[bytes],
            IntakeAssetKind.EmbeddedImage,
            IntakeAssetDisposition.Embedded,
            PageNumber: page,
            WidthPixels: width,
            HeightPixels: height);
}
