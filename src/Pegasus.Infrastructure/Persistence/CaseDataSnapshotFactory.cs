using System.Globalization;
using Pegasus.Core.Address;
using Pegasus.Core.Cases;
using Pegasus.Core.Intake;

namespace Pegasus.Infrastructure.Persistence;

internal static class CaseDataSnapshotFactory
{
    public static CaseDataSnapshotEntity Create(
        CaseEntity caseEntity,
        IntakeReceiptEntity receipt,
        CaseAcceptanceRequest request,
        DateTimeOffset acceptedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(caseEntity);
        ArgumentNullException.ThrowIfNull(receipt);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.CompletenessEvaluation);

        if (caseEntity.OriginIntakeReceiptId != receipt.Id
            || request.IntakeReceiptId != receipt.Id)
        {
            throw new InvalidDataException(
                "The accepted case origin does not match the intake receipt snapshot.");
        }

        if (request.AcceptedInspectionDeadline != receipt.InstructionDraft?.InspectionDate)
        {
            throw new InvalidOperationException(
                "The accepted inspection deadline must match the reviewed intake evidence.");
        }

        var snapshot = new CaseDataSnapshotEntity
        {
            CaseId = caseEntity.Id,
            Case = caseEntity,
            OriginIntakeReceiptId = receipt.Id,
            OriginSourceChannel = receipt.SourceChannel,
            OriginExternalReceiptToken = receipt.ExternalReceiptToken,
            OriginSourceHash = receipt.SourceHash,
            OriginReceivedAtUtc = receipt.ReceivedAtUtc,
            SourceReaderKey = receipt.SourceReaderKey,
            SourceReaderVersion = receipt.SourceReaderVersion,
            ExtractionPolicyKey = receipt.ExtractionPolicyKey,
            ExtractionPolicyVersion = receipt.ExtractionPolicyVersion,
            CompletenessPolicyKey = request.CompletenessEvaluation.PolicyKey,
            CompletenessPolicyVersion = request.CompletenessEvaluation.PolicyVersion,
            CompletenessPolicySatisfied = request.CompletenessEvaluation.SatisfiesPolicy,
            AcceptedAtUtc = acceptedAtUtc
        };

        AddProviderFact(snapshot, receipt);
        AddInstructionSuggestions(snapshot, receipt);
        AddResolvedInspection(
            snapshot,
            receipt,
            request.ProviderInspectionMode == CaseInspectionMode.ImageBasedAssessment);
        AddProviderInspectionMode(snapshot, request, acceptedAtUtc);
        AddAcceptedDeadline(snapshot, receipt, request, acceptedAtUtc);
        return snapshot;
    }

    private static void AddProviderInspectionMode(
        CaseDataSnapshotEntity snapshot,
        CaseAcceptanceRequest request,
        DateTimeOffset acceptedAtUtc)
    {
        if (request.ProviderInspectionMode != CaseInspectionMode.ImageBasedAssessment)
        {
            return;
        }

        // The provider setting determines the mode even when the instruction
        // carried a physical location or staff resolved one at intake; those
        // remain as suggestion rows and staff may still override on the case.
        snapshot.Fields.RemoveAll(item =>
            item.FieldName is CaseDataFieldNames.InspectionAddress or CaseDataFieldNames.InspectionMode
            && item.ValueKind == CaseDataCodes.Confirmed);
        var sourceLabel = $"provider setting:{request.PrincipalCode}";
        snapshot.Fields.Add(new()
        {
            CaseId = snapshot.CaseId,
            Snapshot = snapshot,
            FieldName = CaseDataFieldNames.InspectionAddress,
            ValueKind = CaseDataCodes.Confirmed,
            ValueType = CaseDataCodes.Text,
            Value = Ext18InspectionAddressPolicy.ImageBasedAssessment,
            SourceKind = CaseDataCodes.ProviderSetting,
            SourceIdentity = snapshot.OriginIntakeReceiptId.ToString("D"),
            SourceLabel = sourceLabel,
            PolicyKey = ProviderInspectionModePolicy.PolicyKey,
            PolicyVersion = ProviderInspectionModePolicy.PolicyVersion,
            ConfirmedByActor = request.Actor.SubjectId,
            ConfirmedAtUtc = acceptedAtUtc
        });
        snapshot.Fields.Add(new()
        {
            CaseId = snapshot.CaseId,
            Snapshot = snapshot,
            FieldName = CaseDataFieldNames.InspectionMode,
            ValueKind = CaseDataCodes.Confirmed,
            ValueType = CaseDataCodes.InspectionMode,
            Value = ProviderInspectionModePolicy.ImageBasedAssessmentCode,
            SourceKind = CaseDataCodes.ProviderSetting,
            SourceIdentity = snapshot.OriginIntakeReceiptId.ToString("D"),
            SourceLabel = sourceLabel,
            PolicyKey = ProviderInspectionModePolicy.PolicyKey,
            PolicyVersion = ProviderInspectionModePolicy.PolicyVersion,
            ConfirmedByActor = request.Actor.SubjectId,
            ConfirmedAtUtc = acceptedAtUtc
        });
    }

    private static void AddProviderFact(
        CaseDataSnapshotEntity snapshot,
        IntakeReceiptEntity receipt)
    {
        var route = receipt.MailRouteDecision;
        if (route is null
            || !string.Equals(route.Disposition, "accepted", StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(route.WorkProviderCode))
        {
            return;
        }

        RequirePolicy(route.PolicyKey, route.PolicyVersion, "mail-route");
        snapshot.Fields.Add(new()
        {
            CaseId = snapshot.CaseId,
            Snapshot = snapshot,
            FieldName = CaseDataFieldNames.WorkProviderCode,
            ValueKind = CaseDataCodes.Fact,
            ValueType = CaseDataCodes.Text,
            Value = route.WorkProviderCode.Trim(),
            SourceKind = CaseDataCodes.MailRoute,
            SourceIdentity = receipt.Id.ToString("D"),
            SourceLabel = string.IsNullOrWhiteSpace(route.RouteOwnerCode)
                ? "accepted mail route"
                : route.RouteOwnerCode,
            PolicyKey = route.PolicyKey,
            PolicyVersion = route.PolicyVersion
        });
    }

    private static void AddInstructionSuggestions(
        CaseDataSnapshotEntity snapshot,
        IntakeReceiptEntity receipt)
    {
        var draft = receipt.InstructionDraft;
        if (draft is null)
        {
            return;
        }

        var fields = EfIntakeReceiptStore.DeserializeFields(receipt.FieldsJson);
        AddExtractedValue(snapshot, receipt, fields, CaseDataFieldNames.ClaimantName, "Claimant name", CaseDataCodes.Text, draft.ClaimantName);
        AddExtractedValue(snapshot, receipt, fields, CaseDataFieldNames.ClaimNumber, "Claim number", CaseDataCodes.Text, draft.ClaimNumber);
        AddExtractedValue(snapshot, receipt, fields, CaseDataFieldNames.VehicleRegistration, "Vehicle registration", CaseDataCodes.Text, draft.VehicleRegistration);
        AddExtractedValue(snapshot, receipt, fields, CaseDataFieldNames.VehicleMake, "Vehicle make", CaseDataCodes.Text, draft.VehicleMake);
        AddExtractedValue(snapshot, receipt, fields, CaseDataFieldNames.VehicleModel, "Vehicle model", CaseDataCodes.Text, draft.VehicleModel);
        AddExtractedValue(
            snapshot,
            receipt,
            fields,
            CaseDataFieldNames.VehicleMileage,
            "Vehicle mileage",
            CaseDataCodes.Integer,
            draft.VehicleMileage?.ToString(CultureInfo.InvariantCulture));
        AddExtractedValue(snapshot, receipt, fields, CaseDataFieldNames.AccidentCircumstances, "Accident circumstances", CaseDataCodes.Text, draft.AccidentCircumstances);
        AddExtractedValue(
            snapshot,
            receipt,
            fields,
            CaseDataFieldNames.IncidentDate,
            "Date of incident",
            CaseDataCodes.Date,
            Date(draft.DateOfIncident));
        AddExtractedValue(
            snapshot,
            receipt,
            fields,
            CaseDataFieldNames.InstructionDate,
            "Instruction date",
            CaseDataCodes.Date,
            Date(draft.InstructionDate));
        AddExtractedValue(
            snapshot,
            receipt,
            fields,
            CaseDataFieldNames.InspectionDate,
            "Inspection date",
            CaseDataCodes.Date,
            Date(draft.InspectionDate));
        AddExtractedValue(snapshot, receipt, fields, CaseDataFieldNames.ClaimantContactNumber, "Claimant contact number", CaseDataCodes.Text, draft.ClaimantContactNumber);
        AddExtractedValue(snapshot, receipt, fields, CaseDataFieldNames.ClaimantAddress, "Claimant address", CaseDataCodes.Text, draft.ClaimantAddress);
        AddExtractedValue(snapshot, receipt, fields, CaseDataFieldNames.ContactName, "Contact name", CaseDataCodes.Text, draft.FileHandlerName);
        AddExtractedValue(snapshot, receipt, fields, CaseDataFieldNames.ContactEmailAddress, "Contact email", CaseDataCodes.Text, draft.FileHandlerEmailAddress);
        AddExtractedValue(snapshot, receipt, fields, CaseDataFieldNames.ContactPhoneNumber, "Contact phone", CaseDataCodes.Text, draft.FileHandlerPhoneNumber);
        AddExtractedValue(snapshot, receipt, fields, CaseDataFieldNames.VatStatus, "VAT status", CaseDataCodes.Text, draft.VatStatus);
        AddExtractedValue(snapshot, receipt, fields, CaseDataFieldNames.VehicleMileageUnit, "Vehicle mileage unit", CaseDataCodes.Text, draft.VehicleMileageUnit);
        var suggestedInspectionAddress = fields.SingleOrDefault(
                field => string.Equals(
                    field.Name,
                    "Inspection address",
                    StringComparison.Ordinal))
            ?.SuggestedValue
            ?? draft.InspectionAddress;
        AddExtractedValue(
            snapshot,
            receipt,
            fields,
            CaseDataFieldNames.InspectionAddress,
            "Inspection address",
            CaseDataCodes.Text,
            suggestedInspectionAddress);

        if (!string.IsNullOrWhiteSpace(suggestedInspectionAddress))
        {
            AddExtractedValue(
                snapshot,
                receipt,
                fields,
                CaseDataFieldNames.InspectionMode,
                "Inspection address",
                CaseDataCodes.InspectionMode,
                string.Equals(
                    suggestedInspectionAddress,
                    Ext18InspectionAddressPolicy.ImageBasedAssessment,
                    StringComparison.Ordinal)
                    ? "image_based_assessment"
                    : "physical_address");
        }

        var mileageField = fields.SingleOrDefault(
            field => string.Equals(field.Name, "Vehicle mileage", StringComparison.Ordinal));
        if (draft.VehicleMileageUnit is null
            && mileageField?.SuggestedValue is { } suggestedMileage
            && HasExplicitMilesUnit(suggestedMileage))
        {
            AddExtractedValue(
                snapshot,
                receipt,
                fields,
                CaseDataFieldNames.VehicleMileageUnit,
                "Vehicle mileage",
                CaseDataCodes.Text,
                "miles");
        }
    }

    private static void AddResolvedInspection(
        CaseDataSnapshotEntity snapshot,
        IntakeReceiptEntity receipt,
        bool providerIsImageBased)
    {
        if (receipt.InstructionDraft is null)
        {
            return;
        }

        // The same rule the create screen applies, asked of Core rather than
        // composed again here. An Image Based Assessment provider needs nothing
        // settled first, and anything this adds for one is replaced by the
        // provider's own recorded mode below.
        var resolution = InspectionAddressResolutionStore.CreateSnapshot(receipt);
        if (!InspectionAddressResolutionPolicy.SatisfiesCaseCreation(
                resolution.State,
                providerIsImageBased)
            || string.IsNullOrWhiteSpace(resolution.ResolvedValue)
            || resolution.ResolvedByStaffId is not { } staffId
            || resolution.ResolvedAtUtc is not { } resolvedAtUtc)
        {
            return;
        }

        var actor = staffId.ToString("D");
        // Where the value came from, in the terms the case record keeps: an
        // accepted suggestion is the extraction the acceptance confirmed, and
        // both a correction and a supplied address are a person's own words,
        // so both carry staff provenance.
        var sourceKind = resolution.State == InspectionAddressResolutionState.Accepted
            ? CaseDataCodes.CaseAcceptance
            : CaseDataCodes.StaffCorrection;
        var addressLabel = resolution.State switch
        {
            InspectionAddressResolutionState.Corrected => "staff-corrected inspection address",
            InspectionAddressResolutionState.Supplied => "staff-supplied inspection address",
            _ => "accepted inspection address"
        };
        var modeLabel = resolution.State switch
        {
            InspectionAddressResolutionState.Corrected => "staff-corrected inspection mode",
            InspectionAddressResolutionState.Supplied => "staff-supplied inspection mode",
            _ => "accepted inspection mode"
        };
        UpsertConfirmed(
            snapshot,
            CaseDataFieldNames.InspectionAddress,
            CaseDataCodes.Text,
            resolution.ResolvedValue,
            actor,
            resolvedAtUtc,
            Ext18InspectionAddressPolicy.PolicyKey,
            Ext18InspectionAddressPolicy.PolicyVersion,
            sourceKind,
            addressLabel);
        UpsertConfirmed(
            snapshot,
            CaseDataFieldNames.InspectionMode,
            CaseDataCodes.InspectionMode,
            string.Equals(
                resolution.ResolvedValue,
                Ext18InspectionAddressPolicy.ImageBasedAssessment,
                StringComparison.Ordinal)
                ? "image_based_assessment"
                : "physical_address",
            actor,
            resolvedAtUtc,
            Ext18InspectionAddressPolicy.PolicyKey,
            Ext18InspectionAddressPolicy.PolicyVersion,
            sourceKind,
            modeLabel);
    }

    private static void AddAcceptedDeadline(
        CaseDataSnapshotEntity snapshot,
        IntakeReceiptEntity receipt,
        CaseAcceptanceRequest request,
        DateTimeOffset acceptedAtUtc)
    {
        if (request.AcceptedInspectionDeadline is not { } deadline)
        {
            return;
        }

        UpsertConfirmed(
            snapshot,
            CaseDataFieldNames.InspectionDeadline,
            CaseDataCodes.Date,
            deadline.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            request.Actor.SubjectId,
            acceptedAtUtc,
            receipt.ExtractionPolicyKey ?? request.CompletenessEvaluation.PolicyKey,
            receipt.ExtractionPolicyVersion ?? request.CompletenessEvaluation.PolicyVersion);
    }

    private static void AddExtractedValue(
        CaseDataSnapshotEntity snapshot,
        IntakeReceiptEntity receipt,
        IReadOnlyList<InstructionReviewField> fields,
        string fieldName,
        string intakeFieldName,
        string valueType,
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        RequirePolicy(receipt.ExtractionPolicyKey, receipt.ExtractionPolicyVersion, "instruction extraction");
        var field = fields.SingleOrDefault(
            item => string.Equals(item.Name, intakeFieldName, StringComparison.Ordinal));
        if (field is null || field.HasConflict || field.Candidates.Count == 0)
        {
            throw new InvalidDataException(
                $"The accepted intake field '{intakeFieldName}' has no unambiguous source provenance.");
        }

        var candidate = field.Candidates.Count == 1
            ? field.Candidates[0]
            : field.Candidates.FirstOrDefault(item =>
                string.Equals(item.Value, field.SuggestedValue, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidDataException(
                    $"The accepted intake field '{intakeFieldName}' has ambiguous source provenance.");
        snapshot.Fields.Add(new()
        {
            CaseId = snapshot.CaseId,
            Snapshot = snapshot,
            FieldName = fieldName,
            // Operator direction 2026-08-20 (INTK-021): an unambiguous,
            // singly-provenanced extracted value is auto-added as the working
            // value (Fact), not parked as a suggestion awaiting confirmation.
            // Conflicted or ambiguous candidates never reach this method.
            ValueKind = CaseDataCodes.Fact,
            ValueType = valueType,
            Value = value,
            // A value a person keyed is not intake evidence, whatever else is
            // on the field. Recording it as evidence would have the case claim
            // the document said something it never said, and the confirmed row
            // inherits this kind, so the mislabelling would carry through.
            SourceKind = candidate.Source switch
            {
                IntakeEvidenceSource.StaffCorrection => CaseDataCodes.StaffCorrection,
                // FRD-02 names the provider API as a provenance in its own
                // right. A value the instructing Principal stated is neither
                // something a document said nor something a person here keyed.
                IntakeEvidenceSource.ProviderDeclaration => CaseDataCodes.ProviderApi,
                _ => CaseDataCodes.IntakeEvidence
            },
            SourceIdentity = receipt.Id.ToString("D"),
            SourceLabel = $"{candidate.Source}:{candidate.SourceLabel}",
            PolicyKey = receipt.ExtractionPolicyKey!,
            PolicyVersion = receipt.ExtractionPolicyVersion!.Value
        });
    }

    private static void UpsertConfirmed(
        CaseDataSnapshotEntity snapshot,
        string fieldName,
        string valueType,
        string value,
        string actor,
        DateTimeOffset confirmedAtUtc,
        string fallbackPolicyKey,
        int fallbackPolicyVersion,
        string fallbackSourceKind = CaseDataCodes.CaseAcceptance,
        string fallbackSourceLabel = "accepted case review")
    {
        var underlying = snapshot.Fields.SingleOrDefault(
            item => item.FieldName == fieldName
                && item.ValueKind is CaseDataCodes.Fact or CaseDataCodes.Suggestion
                && string.Equals(item.Value, value, StringComparison.OrdinalIgnoreCase));
        snapshot.Fields.RemoveAll(
            item => item.FieldName == fieldName && item.ValueKind == CaseDataCodes.Confirmed);
        snapshot.Fields.Add(new()
        {
            CaseId = snapshot.CaseId,
            Snapshot = snapshot,
            FieldName = fieldName,
            ValueKind = CaseDataCodes.Confirmed,
            ValueType = valueType,
            Value = value,
            SourceKind = underlying?.SourceKind ?? fallbackSourceKind,
            SourceIdentity = underlying?.SourceIdentity ?? snapshot.OriginIntakeReceiptId.ToString("D"),
            SourceLabel = underlying?.SourceLabel ?? fallbackSourceLabel,
            PolicyKey = underlying?.PolicyKey ?? fallbackPolicyKey,
            PolicyVersion = underlying?.PolicyVersion ?? fallbackPolicyVersion,
            ConfirmedByActor = actor,
            ConfirmedAtUtc = confirmedAtUtc
        });
    }

    private static string? Date(DateOnly? value) =>
        value?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static bool HasExplicitMilesUnit(string value)
    {
        var normalized = value.Trim();
        return normalized.EndsWith(" mi", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith(" mile", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith(" miles", StringComparison.OrdinalIgnoreCase);
    }

    private static void RequirePolicy(string? key, int? version, string source)
    {
        if (string.IsNullOrWhiteSpace(key) || version is null or < 1)
        {
            throw new InvalidDataException(
                $"The {source} policy identity is incomplete.");
        }
    }
}
