namespace Pegasus.Web.Presentation;

/// <summary>
/// Marks a Razor Page whose unnamed GET never renders the shell: it answers a
/// redirect, a file, a refusal or a page with no layout or the sign-in layout.
/// <see cref="ShellPageFilter"/> starts the shell's reads before every other
/// unnamed GET handler, so a page that carries this marker pays for none of
/// them, before or after its handler. Its POSTs and named handlers are not
/// covered; <see cref="INeverDrawsShell"/> covers them too.
/// </summary>
public interface IShellFreeGet;

/// <summary>
/// Marks a Razor Page whose layout is null or the sign-in layout, so no handler
/// of it, GET or POST, ever draws the shell: <see cref="ShellPageFilter"/> reads
/// nothing for it before or after any handler. This includes a POST or a status
/// code re-executed through the error pages, which answer a page with the
/// request's original method.
/// </summary>
public interface INeverDrawsShell : IShellFreeGet;
