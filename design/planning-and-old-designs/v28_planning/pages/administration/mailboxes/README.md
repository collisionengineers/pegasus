# Mail settings

- **Mockup route:** `pegasus_administration_v28.html` (`ad-area=mail`) in [`../../../current/`](../../../current/README.md)
- **Live source:** `src/Pegasus.Web/Pages/Administration/Mailboxes.cshtml`

- [**How it works**](how-it-works.md)

## Screenshots

- [s46-admin-mailboxes-1580.png](../../../current/v28-shots/s46-admin-mailboxes-1580.png) · [1440](../../../current/v28-shots/s46-admin-mailboxes-1440.png) · [760](../../../current/v28-shots/s46-admin-mailboxes-760.png)

## Notes

- One mailbox ("instructions@collisionengineers.example") has a fully built
  Settings dialog (`mailbox-settings-instructions`); the other three rows'
  Settings links post the demo toast, since the dialog's fields and
  mechanics are identical per mailbox. The mail-category Review disclosure
  is similarly fully built for one category ("Instructions") and stubbed for
  the other two.
- Approved mailbox addresses are an invented `collisionengineers.example`
  fixture (Pegasus's own mail estate), distinct from the fixture sheet's
  `claims@wingroveinsurance.example`, which is the Principal's own mailbox
  and appears instead in the Intake log and Contacts fixtures — approved
  mailboxes belong to Collision Engineers, not to a Principal.
- The live "Verified encoded-message size limit (bytes)" field label and the
  Logical folders' individual folder names are captured verbatim; the field
  label itself carries the banned word "bytes" in shipped copy — see [Logs'
  Notes](../logs/) for the parallel case with "intake". Part of sign-off item
  B.
- The concurrent-edit "Take over" variant and lease heartbeat/beacon forms
  are not modelled, as elsewhere in this lane.
