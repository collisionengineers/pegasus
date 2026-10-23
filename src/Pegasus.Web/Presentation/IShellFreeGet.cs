namespace Pegasus.Web.Presentation;

/// <summary>
/// Marks a Razor Page whose unnamed GET never renders the shell: it answers a
/// redirect, a file, a refusal or a page with no layout or the sign-in layout.
/// <see cref="ShellPageFilter"/> starts the shell's reads before every other
/// unnamed GET handler, so a page that carries this marker pays for none of
/// them, before or after its handler. Its POSTs and named handlers are not
/// covered.
/// </summary>
public interface IShellFreeGet;
