namespace Pegasus.Core.Identity;

/// <summary>
/// Names the application boundary being authorised. Business-state preconditions remain owned by
/// their feature use cases and are evaluated after this actor boundary succeeds.
/// </summary>
public enum StaffAccessRight
{
    AccessStaffApplication,
    PerformCasework,
    ManageStaffAccounts,
    AssignStaffRoles,
    ManageOrganizationsAndPrincipals,
    ManageWorkflowConfiguration,
    ManageApprovedMailboxes,
    ManageApprovedOutlookCategories,
    ManageAutomationClients,
    ViewOperationalReports,
    ExecuteSystemWork,
    SubmitRequestUpload,
    SubmitProviderInstruction
}

/// <summary>
/// The single Core role boundary shared by Web, Worker and later authenticated transports.
/// Unknown actor/permission combinations fail closed.
/// </summary>
public static class StaffAuthorization
{
    public static bool IsAuthorized(ActionActor actor, StaffAccessRight permission)
    {
        ArgumentNullException.ThrowIfNull(actor);

        return permission switch
        {
            StaffAccessRight.AccessStaffApplication => actor.Kind == ActorKind.Staff,

            // The Automation Actor is granted only the ordinary operational
            // casework surface (ADR-0011). Every management, configuration,
            // credential, system-work, and request-upload right below stays
            // denied for it, and unknown combinations fail closed.
            StaffAccessRight.PerformCasework =>
                actor.Kind is ActorKind.Staff or ActorKind.Automation,

            StaffAccessRight.ManageStaffAccounts or
            StaffAccessRight.AssignStaffRoles or
            StaffAccessRight.ManageOrganizationsAndPrincipals or
            StaffAccessRight.ManageWorkflowConfiguration or
            StaffAccessRight.ManageApprovedMailboxes or
            StaffAccessRight.ManageApprovedOutlookCategories or
            StaffAccessRight.ManageAutomationClients or
            StaffAccessRight.ViewOperationalReports =>
                actor.Kind == ActorKind.Staff && actor.IsInRole(StaffRole.Administrator),

            StaffAccessRight.ExecuteSystemWork => actor.Kind == ActorKind.SystemWorker,
            StaffAccessRight.SubmitRequestUpload => actor.Kind == ActorKind.RequestLink,
            // The Provider API actor (API-01) may only submit its own
            // Principal's instructions and read its own receipts; every staff,
            // management and system-work right above stays denied for it.
            StaffAccessRight.SubmitProviderInstruction => actor.Kind == ActorKind.Provider,
            _ => false
        };
    }

    public static void Require(ActionActor actor, StaffAccessRight permission)
    {
        if (!IsAuthorized(actor, permission))
        {
            throw new StaffAuthorizationException(permission);
        }
    }
}

public sealed class StaffAuthorizationException : Exception
{
    public StaffAuthorizationException(StaffAccessRight permission)
        : base("The current actor is not authorized to perform this operation.")
    {
        Permission = permission;
    }

    public StaffAccessRight Permission { get; }
}
