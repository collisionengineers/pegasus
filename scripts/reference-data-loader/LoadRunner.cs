using Pegasus.Core.Address;
using Pegasus.Core.Cases;
using Pegasus.Core.Identity;

namespace Pegasus.ReferenceDataLoader;

/// <summary>
/// Writes a plan in its order: (a) the Principals, (b) every other contact
/// with its whole link set, (c) the inspection locations. A row that fails
/// is recorded and the rest continue.
/// </summary>
internal static class LoadRunner
{
    internal static async Task<LoadOutcome> ApplyAsync(
        PlannedLoad planned,
        LoadSettings settings,
        LoadPorts ports,
        CancellationToken cancellationToken)
    {
        var idsByCode = new Dictionary<string, Guid>(planned.PrincipalIdsByCode, StringComparer.OrdinalIgnoreCase);
        var rejects = new List<RowReject>();
        var results = new List<WriteResult>();
        var written = 0;
        foreach (var write in planned.Plan.ContactWrites)
        {
            if (!TryResolve(write.Links, idsByCode, out var links))
            {
                Refuse(write.File, write.RowId, write.OperationKey, "a linked Principal was not saved.");
                continue;
            }

            try
            {
                var saved = await ports.ContactWriter.SaveAsync(ToRequest(write, settings.Actor, links), cancellationToken);
                if (write.Create && write.Fields.PrincipalCode is { } code)
                {
                    idsByCode[code] = saved.OwnPrincipalIds![0];
                }

                results.Add(new(write.OperationKey, write.Create ? "created" : "updated", saved.Version));
                written++;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                Refuse(write.File, write.RowId, write.OperationKey, Describe(exception));
            }
        }

        var created = 0;
        var updated = 0;
        var kept = 0;
        foreach (var write in planned.Plan.LocationWrites)
        {
            if (!TryResolve(write.Principals, idsByCode, out var principals))
            {
                Refuse(ReferenceFiles.InspectionLocations, write.RowId, write.OperationKey, "a linked Principal was not saved.");
                continue;
            }

            try
            {
                var saved = await ports.LocationWriter.SaveAsync(
                    new(
                        settings.Actor,
                        write.Name,
                        write.Address,
                        write.Postcode,
                        Active: true,
                        ReferenceFiles.LocationSource,
                        write.RowId,
                        settings.ReviewedAtUtc,
                        principals,
                        write.OperationKey),
                    cancellationToken);
                switch (saved.Outcome)
                {
                    case InspectionLocationSaveOutcome.Created:
                        created++;
                        written++;
                        break;
                    case InspectionLocationSaveOutcome.Updated:
                        updated++;
                        written++;
                        break;
                    default:
                        // A stored row reviewed no earlier than this one is kept (FRD-06).
                        kept++;
                        break;
                }

                results.Add(new(write.OperationKey, saved.Outcome.ToString(), saved.Version));
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                Refuse(ReferenceFiles.InspectionLocations, write.RowId, write.OperationKey, Describe(exception));
            }
        }

        return new(written, new(created, updated, kept), rejects, results);

        void Refuse(string file, string rowId, string operationKey, string reason)
        {
            rejects.Add(new(file, rowId, reason));
            results.Add(new(operationKey, "rejected", null));
        }
    }

    private static SaveContactRequest ToRequest(ContactWrite write, ActionActor actor, Guid[] links) => new(
        actor,
        write.OrganizationId,
        write.ExpectedVersion,
        write.Fields.Name,
        write.Fields.ContactPerson,
        write.Fields.Email,
        write.Fields.Telephone,
        write.Fields.Address,
        write.Fields.Postcode,
        write.Fields.Active,
        ReferenceFiles.RoleOf(write.Kind),
        write.Fields.PrincipalCode,
        write.Fields.InspectionMode is { } mode
            ? PrincipalInspectionModePolicy.Parse(mode)
            : CaseInspectionMode.PhysicalAddress,
        links,
        write.OperationKey,
        write.Fields.NotesOnEveryCase);

    private static bool TryResolve(IReadOnlyList<string> codes, Dictionary<string, Guid> idsByCode, out Guid[] ids)
    {
        ids = new Guid[codes.Count];
        for (var index = 0; index < codes.Count; index++)
        {
            if (!idsByCode.TryGetValue(codes[index], out ids[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static string Describe(Exception exception) => exception is ContactDirectoryException directory
        ? directory.Error.ToString()
        : exception.Message;
}
