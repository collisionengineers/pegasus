using CollisionDocNet.Core;
using CollisionDocNet.Storage.Opc;

namespace CollisionDocNet.Writer.OpenXml;

public sealed record DocxExtractionOptions
{
    public const string Specification = "ECMA-376-5ed:2016;OPC:2021;MCE:2015";
    public const string Extractor = "collisiondocnet-docx/0.1";

    public ResourceLimits ResourceLimits { get; init; } = ResourceLimits.CreateCollisionSpikeDefault();

    public OpcLimits OpcLimits { get; init; } = OpcLimits.Default;

    /// <summary>
    /// Supplies the monotonic clock used for the operation deadline. Production callers
    /// normally retain <see cref="TimeProvider.System"/>; tests may inject a controlled clock.
    /// </summary>
    public TimeProvider TimeProvider { get; init; } = TimeProvider.System;
}
