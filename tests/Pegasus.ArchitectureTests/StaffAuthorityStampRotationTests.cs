using System.Text.RegularExpressions;

namespace Pegasus.ArchitectureTests;

/// <summary>
/// The staff cookie check trusts the principal a sign-in built until the
/// account's security stamp changes; it never rebuilds it. So every change to
/// a staff account's roles, claims, lockout, enabled state or password must
/// rotate the stamp. These are the only places allowed to make such a change,
/// and each one rotates the stamp with it.
/// </summary>
public sealed partial class StaffAuthorityStampRotationTests
{
    private static readonly string[] AllowedFiles =
    [
        // Disable, enable, role change, forced logout and password reset each
        // rotate the stamp; creation makes an account no cookie names yet.
        "src/Pegasus.Infrastructure/Persistence/EfStaffAccountAdministration.cs",
        // Identity's password change and reset rotate the stamp themselves.
        "src/Pegasus.Infrastructure/Persistence/EfStaffPasswordChange.cs",
        // The verification account's convergence resets its password in the
        // same transaction as its role; bootstrap creates new accounts.
        "src/Pegasus.Web/Program.cs",
        // The local offline fixture sets a new stamp with every correction.
        "src/Pegasus.Web/Authentication/DevelopmentOfflineInitialization.cs",
    ];

    [Fact]
    public void OnlyTheKnownOwnersChangeStaffAuthority()
    {
        var root = FindRepositoryRoot();
        var offenders = Directory
            .EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .Where(path => !path.Contains("/Migrations/", StringComparison.Ordinal)
                && !path.Contains("/obj/", StringComparison.Ordinal)
                && !path.Contains("/bin/", StringComparison.Ordinal))
            .Where(path => AuthorityMutation().IsMatch(File.ReadAllText(Path.Combine(root, path))))
            .Except(AllowedFiles, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            "These files change staff roles, claims, lockout, enabled state or passwords outside "
            + "the owners that rotate the security stamp: " + string.Join(", ", offenders));
    }

    [Fact]
    public void EachOwnerRotatesTheStamp()
    {
        var root = FindRepositoryRoot();
        Assert.All(AllowedFiles, relativePath =>
            Assert.Matches(StampRotation(), File.ReadAllText(Path.Combine(root, relativePath))));
    }

    [GeneratedRegex(
        @"[Uu]serManager\s*\.\s*(AddToRoles?Async|RemoveFromRoles?Async|AddClaims?Async|RemoveClaims?Async|ReplaceClaimAsync"
        + @"|SetLockoutEnabledAsync|SetLockoutEndDateAsync|ResetPasswordAsync|ChangePasswordAsync"
        + @"|AddPasswordAsync|RemovePasswordAsync)\("
        + @"|\b(UserRoles|UserClaims|RoleClaims)\s*\.\s*(Add|AddRange|Remove|RemoveRange)\("
        + @"|\.(IsEnabled|PasswordHash|LockoutEnd|LockoutEnabled)\s*=[^=>]"
        + @"|AspNetUserRoles|AspNetUserClaims",
        RegexOptions.CultureInvariant)]
    private static partial Regex AuthorityMutation();

    [GeneratedRegex(
        @"UpdateSecurityStampAsync\(|[Uu]serManager\s*\.\s*(ResetPasswordAsync|ChangePasswordAsync)\(|\.SecurityStamp\s*=[^=>]",
        RegexOptions.CultureInvariant)]
    private static partial Regex StampRotation();

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Pegasus.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the Pegasus repository root.");
    }
}
