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
    ReviewStaffAccess,
    AssignStaffRoles,
    ManageOrganizationsAndPrincipals,
    ManageWorkflowConfiguration,
    ManageApprovedMailboxes,
    ExecuteSystemWork,
    SubmitRequestUpload
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
            StaffAccessRight.AccessStaffApplication or StaffAccessRight.PerformCasework =>
                actor.Kind == ActorKind.Staff,

            StaffAccessRight.ManageStaffAccounts or
            StaffAccessRight.ReviewStaffAccess or
            StaffAccessRight.AssignStaffRoles or
            StaffAccessRight.ManageOrganizationsAndPrincipals or
            StaffAccessRight.ManageWorkflowConfiguration or
            StaffAccessRight.ManageApprovedMailboxes =>
                actor.Kind == ActorKind.Staff && actor.IsInRole(StaffRole.Administrator),

            StaffAccessRight.ExecuteSystemWork => actor.Kind == ActorKind.SystemWorker,
            StaffAccessRight.SubmitRequestUpload => actor.Kind == ActorKind.RequestLink,
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
