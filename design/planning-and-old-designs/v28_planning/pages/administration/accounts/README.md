# Staff accounts & roles

- **Mockup route:** `pegasus_administration_v28.html` (`ad-area=accounts`) in [`../../../current/`](../../../current/README.md)
- **Live source:** `src/Pegasus.Web/Pages/Administration/Accounts/Index.cshtml`

- [**How it works**](how-it-works.md)

## Screenshots

- [s37-admin-accounts-1580.png](../../../current/v28-shots/s37-admin-accounts-1580.png) · [1440](../../../current/v28-shots/s37-admin-accounts-1440.png) · [760](../../../current/v28-shots/s37-admin-accounts-760.png)

## Notes

- Only the Sam Whitlock row's Settings and Manage login controls open real
  dialogs (`settings-sam-whitlock`, `glass-credential-dialog`); Priya Anand's
  and David Okafor's Settings/Manage login controls show the "Demo: not
  wired in this mockup" toast instead of a second full dialog build, since
  the dialog content and mechanics are identical per account. Noted rather
  than silently faked.
- The live "Take over" concurrent-edit variant (offered only when another
  browser window holds the same account's edit lease) and the lease
  heartbeat/beacon forms that keep it alive are not modelled — this baseline
  has no server to race against. Every dialog opens straight into its normal
  Edit/Settings state.
- The account settings dialog's Role → Sign-off Engineer eligibility toggle
  (Administrator/Engineer can hold sign-off fields, User cannot) is wired
  live in `administration.js`, mirroring `wwwroot/js/accounts.js`.
- Glass's login state for Sam Whitlock's row is a strip control
  (`adGlass`: Not set / Set / Disabled) so all three live states of
  `StaffAccountRow.GlassLabel` are reachable.
