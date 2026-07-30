namespace Pegasus.Core.Access;

public enum StaffRole
{
    User,
    Engineer,
    Administrator
}

public sealed record StaffActor(Guid Id, string UserName, string DisplayName, StaffRole Role)
{
    public bool IsAdministrator => Role == StaffRole.Administrator;
    public bool IsEngineer => Role is StaffRole.Engineer or StaffRole.Administrator;
}

public interface IStaffActorAccessor
{
    StaffActor? Current { get; }
}
