# Staff accounts

- **Mockup route:** `pegasus_admin_accounts_v30.html` in [`../../../current/`](../../../current/README.md); `?state=list|settings-self|settings-other|settings-disabled|error|create|temporary-password|delete`, `?layer=baseline|proposal`
- **Live source:** `src/Pegasus.Web/Pages/Administration/Accounts/Index.cshtml`, `.cshtml.cs`, `Administration/Shared/_AdminNav.cshtml`, `wwwroot/js/accounts.js`, `wwwroot/css/site.css` (`account-settings-dialog`, `account-signoff-fields`, `settings-line`)
- **Dialogs:** [`dialogs/`](dialogs/README.md) · **States:** [`states/`](states/README.md) · **Panels:** [`panels/`](panels/README.md)

- [**How it works**](how-it-works.md) · [**How it should work**](how-it-should-work.md)

## Screenshots

Not captured in this pass. `shoot.ps1` names them `accounts-<state>-<width>.png`.

## Notes

The operator's screenshot of 25 September 2026 (the `alex` settings dialog)
is the reference for `?state=settings-self&layer=baseline`.
