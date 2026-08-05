namespace Pegasus.Core.Assessment;

public sealed class GetCaseAssessment(ICaseAssessmentStore store) : IGetCaseAssessment
{
    private readonly ICaseAssessmentStore _store =
        store ?? throw new ArgumentNullException(nameof(store));

    public async Task<CaseAssessmentProjection?> ExecuteAsync(
        Guid caseId,
        CancellationToken cancellationToken)
    {
        if (caseId == Guid.Empty)
        {
            throw new ArgumentException("A case identifier is required.", nameof(caseId));
        }

        var projection = await _store.GetAsync(caseId, cancellationToken);
        return projection is null
            ? null
            : projection with { Readiness = AssessmentPolicy.EvaluateReadiness(projection) };
    }
}

public sealed class SaveAssessment(ICaseAssessmentStore store) : ISaveAssessment
{
    private readonly ICaseAssessmentStore _store =
        store ?? throw new ArgumentNullException(nameof(store));

    public async Task<CaseAssessmentProjection> ExecuteAsync(
        SaveAssessmentRequest request,
        CancellationToken cancellationToken)
    {
        var normalized = AssessmentPolicy.ValidateAndNormalize(request);
        var projection = await _store.SaveAsync(normalized, cancellationToken);
        return projection with { Readiness = AssessmentPolicy.EvaluateReadiness(projection) };
    }
}
