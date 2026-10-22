namespace Pegasus.Web.Presentation;

/// <summary>
/// Marks a Razor Page whose unnamed GET never renders the shell: it answers a
/// redirect, a file, a refusal or a page with no layout. <see cref="ShellPageFilter"/>
/// starts the shell's reads before every other unnamed GET handler, so a page
/// that carries this marker pays for none of them.
/// </summary>
public interface IShellFreeGet;
