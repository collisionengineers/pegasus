using Pegasus.Core.Identity;
using Pegasus.Core.Workflow;

namespace Pegasus.Core.ImageIntake;

public sealed class RegisterImageIntake(IImageIntakeStore store) : IRegisterImageIntake
{
    private readonly IImageIntakeStore _store = store ?? throw new ArgumentNullException(nameof(store));

    public async Task<ImageIntakeRecord> ExecuteAsync(
        RegisterImageIntakeRequest request,
        CancellationToken cancellationToken)
    {
        ImageIntakeLifecycleRules.ValidateRegister(request);
        var replay = await _store.ProbeRegisterReplayAsync(request, cancellationToken);
        if (replay is not null)
        {
            return replay.Result;
        }

        return await _store.RegisterAsync(request, cancellationToken);
    }
}

public sealed class LinkImageIntakeCase(IImageIntakeStore store) : ILinkImageIntakeCase
{
    private readonly IImageIntakeStore _store = store ?? throw new ArgumentNullException(nameof(store));

    public async Task ExecuteAsync(ImageIntakeCaseLinkRequest request, CancellationToken cancellationToken)
    {
        ImageIntakeLifecycleRules.ValidateCaseLink(request);
        var current = await ImageIntakeLifecycleRules.GetRequiredAsync(
            _store,
            request.ImageIntakeId,
            cancellationToken);
        if (current.Record.LinkedCaseId is not null)
        {
            throw new InvalidOperationException(
                "An image intake has at most one current case association; unlink it first.");
        }

        await _store.LinkCaseAsync(request, cancellationToken);
    }
}

public sealed class UnlinkImageIntakeCase(IImageIntakeStore store) : IUnlinkImageIntakeCase
{
    private readonly IImageIntakeStore _store = store ?? throw new ArgumentNullException(nameof(store));

    public async Task ExecuteAsync(ImageIntakeCaseLinkRequest request, CancellationToken cancellationToken)
    {
        ImageIntakeLifecycleRules.ValidateCaseLink(request);
        var current = await ImageIntakeLifecycleRules.GetRequiredAsync(
            _store,
            request.ImageIntakeId,
            cancellationToken);
        if (current.Record.LinkedCaseId != request.CaseId)
        {
            throw new InvalidOperationException(
                "Only the currently associated case can be unlinked from an image intake.");
        }

        await _store.UnlinkCaseAsync(request, cancellationToken);
    }
}

public static class ImageIntakeLifecycleRules
{
    public static async Task<ImageIntakeDetail> GetRequiredAsync(
        IImageIntakeQueries queries,
        Guid imageIntakeId,
        CancellationToken cancellationToken)
    {
        if (imageIntakeId == Guid.Empty)
        {
            throw new ArgumentException("An image intake identifier is required.", nameof(imageIntakeId));
        }

        return await queries.GetAsync(imageIntakeId, cancellationToken)
            ?? throw new KeyNotFoundException($"Image intake '{imageIntakeId}' was not found.");
    }

    /// <summary>
    /// A Case is eligible for Image-intake association only before report
    /// delivery: an editable pre-report workflow state and no retained
    /// report-sent evidence. Terminal and post-report states are never
    /// eligible.
    /// </summary>
    public static bool IsCaseEligibleForAssociation(
        CaseLifecycleState state,
        bool hasReportSentEvidence) =>
        !hasReportSentEvidence
        && state is CaseLifecycleState.NotReady
            or CaseLifecycleState.Held
            or CaseLifecycleState.Review
            or CaseLifecycleState.ReportPreparation;

    public static void ValidateRegister(RegisterImageIntakeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Origin, nameof(request));
        ValidateOrigin(request.Origin);
        ValidateNormalizedRegistration(request.NormalizedVehicleRegistration);
        ArgumentNullException.ThrowIfNull(request.Actor, nameof(request));
        StaffAuthorization.Require(request.Actor, StaffAccessRight.PerformCasework);
        ValidateOperation(request.OperationKey);
        RequireText(request.Reason, "A reason is required.", 500, nameof(request));
    }

    public static void ValidateCaseLink(ImageIntakeCaseLinkRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ImageIntakeId == Guid.Empty)
        {
            throw new ArgumentException("An image intake identifier is required.", nameof(request));
        }

        if (request.ExpectedImageIntakeVersion < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "The expected image intake version cannot be negative.");
        }

        if (request.CaseId == Guid.Empty)
        {
            throw new ArgumentException("A case identifier is required.", nameof(request));
        }

        if (request.ExpectedCaseVersion < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "The expected case version cannot be negative.");
        }

        ArgumentNullException.ThrowIfNull(request.Actor);
        StaffAuthorization.Require(request.Actor, StaffAccessRight.PerformCasework);
        ValidateOperation(request.OperationKey);
        RequireText(request.Reason, "A reason is required.", 500, nameof(request));
        RequireText(
            request.CaseEditLeaseToken,
            "An active case edit lease token is required.",
            128,
            nameof(request));
    }

    internal static void ValidateNormalizedRegistration(string registration)
    {
        RequireText(registration, "A normalized vehicle registration is required.", 20, nameof(registration));
        if (registration.Any(character => !(char.IsAsciiLetterUpper(character) || char.IsAsciiDigit(character))))
        {
            throw new ArgumentException(
                "The vehicle registration must be uppercase ASCII letters and digits with no separators.",
                nameof(registration));
        }
    }

    private static void ValidateOrigin(ImageIntakeOrigin origin)
    {
        if (origin.ReceiptId == Guid.Empty)
        {
            throw new ArgumentException("An originating intake receipt is required.", nameof(origin));
        }

        ArgumentNullException.ThrowIfNull(origin.SourceIdentity, nameof(origin));
        if (!Enum.IsDefined(origin.SourceIdentity.Channel))
        {
            throw new ArgumentOutOfRangeException(nameof(origin), "The intake source channel is invalid.");
        }

        RequireText(
            origin.SourceIdentity.ExternalReceiptToken,
            "The source receipt token is required.",
            200,
            nameof(origin));
        RequireText(origin.SourceHash, "The source hash is required.", 64, nameof(origin));
        if (origin.EvaluationRevisionId == Guid.Empty)
        {
            throw new ArgumentException("A completed intake evaluation revision is required.", nameof(origin));
        }

        if (origin.SourceHash.Length != 64
            || origin.SourceHash.Any(character => !char.IsAsciiHexDigit(character)))
        {
            throw new ArgumentException("The source hash must be a SHA-256 hexadecimal value.", nameof(origin));
        }
    }

    private static void ValidateOperation(string operationKey) =>
        RequireText(operationKey, "An operation key is required.", 100, nameof(operationKey));

    private static void RequireText(string value, string message, int maximumLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(message, parameterName);
        }

        if (value.Length > maximumLength)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                $"The value cannot exceed {maximumLength} characters.");
        }
    }
}
