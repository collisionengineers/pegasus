using Pegasus.Core.Intake;

namespace Pegasus.Web.Presentation;

/// <summary>
/// One retained source or non-image attachment listed on a pre-Case record.
/// The route is offered only after its exact durable custody version confirms.
/// </summary>
public sealed record RetainedIntakeFile(
    Guid ReceiptId,
    IntakeAssetRecord Asset,
    bool IsSource)
{
    public bool IsStored => Asset.CustodyState == IncomingArtifactCustodyState.Confirmed;

    public string Href => IsSource
        ? $"/Received/{ReceiptId:D}/Source"
        : $"/Received/{ReceiptId:D}/Asset/{Asset.Id:D}";
}

/// <summary>
/// A selected photograph together with the receipt that retained it. Group
/// destinations can therefore present photographs from every completed member
/// without inferring their route from the group's own origin.
/// </summary>
public sealed record RetainedIntakeImage(Guid ReceiptId, IntakeAssetRecord Asset);
