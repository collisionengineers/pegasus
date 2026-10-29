using Pegasus.Core.Cases;
using Pegasus.Core.Identity;
using Pegasus.Core.Intake;
using Pegasus.Infrastructure.Persistence;

namespace Pegasus.IntegrationTests;

/// <summary>
/// A Case created from an e-mail takes its contact from the sender and the
/// sender's signature, only where the instruction left the field empty
/// (operator, 28 October 2026; R462).
/// </summary>
public sealed class SenderContactSnapshotTests
{
    private const string Sender = "claims.handler@principal.example";

    private static readonly InstructionEmailSender Email = new(
        Sender,
        "Claire Handler",
        """
        Please inspect the vehicle.

        Kind regards
        Claire Handler
        Tel: 0161 904 4660
        """);

    [Fact]
    public void TheSenderFillsTheEmptyContactFields()
    {
        var snapshot = CreateSnapshot(Route(Sender), Email);

        var name = Field(snapshot, CaseDataFieldNames.ContactName);
        Assert.Equal("Claire Handler", name.Value);
        Assert.Equal(CaseDataCodes.Fact, name.ValueKind);
        Assert.Equal(CaseDataCodes.MailRoute, name.SourceKind);
        Assert.Equal(SenderContactFill.PolicyKey, name.PolicyKey);
        Assert.Equal(CaseDataCodes.MailRoute, Field(snapshot, CaseDataFieldNames.ContactEmailAddress).SourceKind);
        Assert.Equal(Sender, Field(snapshot, CaseDataFieldNames.ContactEmailAddress).Value);
        var phone = Field(snapshot, CaseDataFieldNames.ContactPhoneNumber);
        Assert.Equal("0161 904 4660", phone.Value);
        Assert.Equal(CaseDataCodes.IntakeEvidence, phone.SourceKind);
    }

    [Fact]
    public void TheInstructionsOwnContactRanksFirst()
    {
        var snapshot = CreateSnapshot(Route(Sender), Email, declaredContactName: "Declared Handler");

        var name = Field(snapshot, CaseDataFieldNames.ContactName);
        Assert.Equal("Declared Handler", name.Value);
        Assert.NotEqual(SenderContactFill.PolicyKey, name.PolicyKey);
        Assert.Equal(Sender, Field(snapshot, CaseDataFieldNames.ContactEmailAddress).Value);
    }

    [Fact]
    public void AReceiptWithNoRouteSenderFillsNoContact()
    {
        var snapshot = CreateSnapshot(route: null, Email);

        Assert.DoesNotContain(snapshot.Fields, field => field.FieldName is
            CaseDataFieldNames.ContactName or
            CaseDataFieldNames.ContactEmailAddress or
            CaseDataFieldNames.ContactPhoneNumber);
    }

    private static CaseDataFieldEntity Field(CaseDataSnapshotEntity snapshot, string fieldName) =>
        Assert.Single(snapshot.Fields, field => field.FieldName == fieldName);

    private static IntakeMailRouteDecisionEntity Route(string effectiveSender) => new()
    {
        Disposition = "accepted",
        RouteOwnerCode = "QDOS",
        RouteKind = "direct_principal",
        PrincipalCode = "QDOS",
        PredicatesJson = "[]",
        Reason = "sender-contact-probe",
        PolicyKey = PrincipalMailRoutePolicy.Key,
        PolicyVersion = PrincipalMailRoutePolicy.Version,
        TransportIdentitiesJson = "[]",
        OriginalIdentitiesJson = "[]",
        EffectiveSenderAddress = effectiveSender,
        EffectiveSenderSourceLabel = "outer message"
    };

    private static CaseDataSnapshotEntity CreateSnapshot(
        IntakeMailRouteDecisionEntity? route,
        InstructionEmailSender email,
        string? declaredContactName = null)
    {
        var receiptId = Guid.NewGuid();
        InstructionReviewField[] fields = declaredContactName is null
            ? []
            : [new("Contact name", declaredContactName,
                [new(declaredContactName, IntakeEvidenceSource.PdfContent, "contact-block")], false, false)];
        var receipt = new IntakeReceiptEntity
        {
            Id = receiptId,
            SourceFileName = "sender-contact-probe.eml",
            MediaType = "message/rfc822",
            SourceHash = "source-hash",
            SourceChannel = "mailbox",
            ExternalReceiptToken = "sender-contact-probe",
            SourceReaderKey = "structural-probe",
            SourceReaderVersion = "1",
            ExtractionPolicyKey = QdosInstructionExtractionPolicy.Key,
            ExtractionPolicyVersion = QdosInstructionExtractionPolicy.Version,
            Decision = "case_created",
            DecisionReason = "sender-contact-probe",
            EvidenceJson = "{\"version\":1,\"data\":[]}",
            OcrCandidatesJson = "{\"version\":1,\"data\":[]}",
            FieldsJson = EfIntakeReceiptStore.SerializeFields(fields),
            InstructionDraft = new() { SuggestedPrincipalCode = "QDOS", FileHandlerName = declaredContactName },
            MailRouteDecision = route
        };
        var accepted = new CaseEntity
        {
            Id = Guid.NewGuid(),
            OriginIntakeReceiptId = receiptId,
            Reference = "sender-contact-probe",
            Type = "inspection",
            InitialState = "not_ready",
            CustodyState = "pending"
        };

        return CaseDataSnapshotFactory.Create(
            accepted,
            receipt,
            new(
                receiptId,
                1,
                ActionActor.SystemWorker("system-worker:intake-processing"),
                "sender-contact-probe",
                CaseType.Inspection,
                "QDOS",
                new(true, false),
                new(false, "sender-contact-probe", 1),
                new Pegasus.Core.Workflow.CaseWorkflowConfiguration("sender-contact-probe", 1),
                CaseInspectionMode.PhysicalAddress),
            DateTimeOffset.UtcNow,
            email);
    }
}
