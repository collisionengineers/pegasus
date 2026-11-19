// Runs before any script of Glass's framing page (acofr.php), in the page's own world.
// The page creates `dialog` in its last script; the editor page inside its first iframe may
// ask for `parent.dialog` before that. This makes `dialog` exist from the first moment it is
// asked for, built by the page's own dialogClass, and keeps that one object when the page's
// last script assigns its own.
(() => {
    if (!/\/ere\/aco\/acofr\.php$/.test(location.pathname)) { return; }
    let made = null;
    Object.defineProperty(window, 'dialog', {
        configurable: true,
        enumerable: true,
        get() {
            if (!made && typeof window.dialogClass === 'function') {
                made = new window.dialogClass('ifrMain', 'ifrHide', 'ifrDialog', true);
            }
            return made || undefined;
        },
        set(value) { if (!made) { made = value; } }
    });
})();
