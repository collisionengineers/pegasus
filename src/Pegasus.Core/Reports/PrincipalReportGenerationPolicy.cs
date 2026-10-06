namespace Pegasus.Core.Reports;

/// <summary>The report route a Principal selects for newly entering Review cases.</summary>
public enum PrincipalReportGenerationPolicy
{
    Pegasus,
    EvaZip,
    EvaManualApi,
    EvaAutomaticApiOnReview
}

public static class PrincipalReportGenerationPolicyRules
{
    public static bool IsEva(PrincipalReportGenerationPolicy policy) => policy is not PrincipalReportGenerationPolicy.Pegasus;
    public static bool AllowsManualApi(PrincipalReportGenerationPolicy policy) => policy == PrincipalReportGenerationPolicy.EvaManualApi;
    public static bool RequiresAutomaticApiOnReview(PrincipalReportGenerationPolicy policy) => policy == PrincipalReportGenerationPolicy.EvaAutomaticApiOnReview;
}
