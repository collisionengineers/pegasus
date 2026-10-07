namespace Pegasus.Core.Cases;

/// <summary>
/// The length of each instruction field the case store keeps. Case data, the
/// instruction draft table and the Principal API all bound the same values,
/// so they read the bound from here rather than restating the number.
/// </summary>
public static class CaseDataLimits
{
    public const int PersonName = 300;
    public const int Telephone = 100;
    public const int EmailAddress = 320;
    public const int Address = 1000;
    public const int ClaimNumber = 100;
    public const int VehicleRegistration = 20;
    public const int VehicleText = 100;
    public const int MileageUnit = 40;
    public const int AccidentCircumstances = 2000;
    public const int VatStatus = 100;
}
