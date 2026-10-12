using System.Globalization;
using Pegasus.Core.Cases;

namespace Pegasus.Core.Intake.ThirdPartyReports;

/// <summary>
/// Where a capture PDF's vehicle identity lands on the Case its photographs
/// joined (operator, 12 October 2026): it fills the make, model, mileage and
/// accident date only where the Case holds neither an extracted fact nor a
/// confirmed value (the vehicle lookup's rule,
/// <see cref="Pegasus.Core.Vehicle.VehicleLookupFillPolicy.Fills"/>), and the VIN only where
/// the work holds none (Glass's VIN rule,
/// <see cref="Pegasus.Core.Assessment.GlassVinFillPolicy"/>). Nothing already on the Case
/// is replaced, and the registration is never written: it is what paired the
/// photographs.
/// </summary>
public static class VehicleCaptureFill
{
    /// <summary>The Automation actor a filled VIN is recorded as.</summary>
    public const string RecorderId = "vehicle-capture";

    public const string PolicyKey = "vehicle-capture-fill";
    public const int PolicyVersion = 1;

    /// <summary>The Case-data values the reading offers, keyed by field name.</summary>
    public static IReadOnlyDictionary<string, string> CaseDataValues(VehicleCaptureReading reading)
    {
        ArgumentNullException.ThrowIfNull(reading);
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(reading.Make))
        {
            values[CaseDataFieldNames.VehicleMake] = reading.Make;
        }

        if (!string.IsNullOrWhiteSpace(reading.Model))
        {
            values[CaseDataFieldNames.VehicleModel] = reading.Model;
        }

        if (reading.Mileage is { } mileage)
        {
            values[CaseDataFieldNames.VehicleMileage] = mileage.ToString(CultureInfo.InvariantCulture);
        }

        if (reading.AccidentDate is { } accidentDate)
        {
            values[CaseDataFieldNames.IncidentDate] =
                accidentDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        return values;
    }
}

/// <summary>
/// Writes a capture reading's values onto a Case as system work. Anything
/// that stops the fill is logged and the photographs stay filed without it.
/// </summary>
public interface IFillCaseFromVehicleCapture
{
    Task FillAsync(Guid caseId, VehicleCaptureReading reading, CancellationToken cancellationToken);
}

/// <summary>
/// Fills a Case from the capture PDF its receipt carried, once the receipt's
/// evidence is on that Case: an automatic or staff association, or an Image
/// intake merged into the Case later. A receipt with no capture reading
/// fills nothing.
/// </summary>
public sealed class FillCaseFromVehicleCapture(
    IIntakeReceiptQueries receipts,
    RecordedVehicleCaptureReading readings,
    IFillCaseFromVehicleCapture fill)
{
    public async Task ExecuteAsync(Guid caseId, Guid receiptId, CancellationToken cancellationToken)
    {
        VehicleCaptureReading? reading;
        try
        {
            var receipt = await receipts.GetAsync(receiptId, cancellationToken);
            reading = receipt is null ? null : await readings.FindAsync(receipt, cancellationToken);
        }
        catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
        {
            // The fill is a convenience over evidence already filed; a fault
            // reading the recording never fails the filing.
            return;
        }

        if (reading is not null)
        {
            await fill.FillAsync(caseId, reading, cancellationToken);
        }
    }
}
