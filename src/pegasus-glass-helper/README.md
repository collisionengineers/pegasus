# Pegasus helper for Glass's

A Chrome (and Edge) extension an Engineer installs, named "Pegasus helper for
Glass's" ([R1170, R1183](../../docs/rulings.md),
[ADR-0091](../../docs/adr/0091-pegasus-sends-the-glass-editor-its-own-requests.md)).
In Glass's editor frame (`acofr.php`) it does three things and nothing else:

- `patch-dialog.js`, in the page's own world before Glass's scripts run, makes
  the framing page's `dialog` exist when the editor frame first asks for it,
  built by Glass's own `dialogClass`. This removes the start-up alert
  "ERROR dialog not found" (FRD-25, issue 1065). It changes none of Glass's
  files or requests.
- `helper.js`, in the extension's own world, asks the service worker to hand
  the frame's own address to Pegasus once, then to poll on the cadence each
  answer names (every 2 seconds while the Engineer has Automation help on,
  every 10 otherwise). When the Automation has added or removed a position,
  Pegasus's answer asks for it, and it reloads the editor's selected list
  (`selected.php`) and reports that on a poll a second later (`seen`), so the
  Automation hears that the window shows the change.
- `background.js`, the service worker, answers only this extension's script in
  the editor frame, reads the frame's address from the sender, and posts only
  to a Pegasus origin listed in `host_permissions`:
  `POST /Integrations/Glass/Help/Key` with the staff cookie, and
  `POST /Integrations/Glass/Help/Poll` without it.

It asks for no extension permission. Its protocol (`PROTOCOL` in
`background.js`) must equal `HelpModel.Protocol`; raise `version` in
`manifest.json` with every change to this folder.
`tests/Pegasus.ArchitectureTests/GlassHelperExtensionContractTests.cs` pins
both and the manifest.

## Loading it unpacked

1. Copy this folder to the PC, for example to `C:\Tools\pegasus-glass-helper`.
2. Open `chrome://extensions` (or `edge://extensions`), turn on
   **Developer mode**, press **Load unpacked** and choose the folder.
3. After a change, copy the folder again and press the extension's reload
   button.

## Manual walks

The extension has no CI. Record each run under `artifacts/` and in one line
of `docs/operations.md`. Never write a key, token, cookie or frame address
into the record.

**E1, offline.** With the replay scripts of 17 November 2026
(`verify-patch.mjs`, `run-trials.mjs --extension <this folder>` at 12 times
CPU slow-down, the editor page answering after 0, 50 and 100 ms): no alert in
at least 30 starts, and the framing page and the editor page hold the one
`dialog` built by Glass's class. Those scripts copy only the content scripts;
copy `background.js` as well, or the folder does not load.

**E2, live, Chrome, on the test instance.**

1. Load the folder. Press **Glass's** on a Case three times over the walk, in
   new and continued sessions. No start-up alert.
2. In `chrome://extensions` open the extension's service worker (**Inspect**)
   and watch its Network panel:
   - `Key` answers **200** with `"key":"held"`. This settles whether Chrome
     sends the SameSite=Strict staff cookie from the service worker: **401**
     means it did not; **403** means the request carried no extension
     `Origin` or was not JSON.
   - `Poll` answers 200 every 10 seconds.
3. The host log says "The Glass's helper handed over the editor key for
   session …" with the session id and nothing else.
4. Restart the instance's Web host with the window open: the next poll says
   `"key":"needed"` and the next `Key` is 200.
5. Close the window: the polls stop.
6. Save & Exit: the next poll answers **409** and the helper stops.

**E3, Edge.** One launch as in E2, step 1 and 2.

**E4, live, Automation help, on the test instance.** The walk is in
FRD-25's acceptance walk, step 14: the Engineer turns help on from the
Case page, the Automation adds and removes positions beside them, and the
Engineer's own Save & Exit brings it all back.
