# Contacts

- **Mockup route:** `pegasus_administration_v28.html` (`ad-area=contacts`) in [`../../../current/`](../../../current/README.md)
- **Live source:** `src/Pegasus.Web/Pages/Administration/Contacts/Index.cshtml`, `Contacts/Edit.cshtml`

- [**How it works**](how-it-works.md)

## Screenshots

- [s43-admin-contacts-1580.png](../../../current/v28-shots/s43-admin-contacts-1580.png) · [1440](../../../current/v28-shots/s43-admin-contacts-1440.png) · [760](../../../current/v28-shots/s43-admin-contacts-760.png)

## Notes

- `Contacts/Edit.cshtml` is its own live route (`/Administration/Contacts/Edit/{id}`);
  this capture folds it into the same `ad-area=contacts` area as a second
  panel set, switched by the strip's `adContactsView` control (Directory /
  "Wingrove Insurance (edit)"), since the whole lane is one file. The
  `adContactEdit` control separately toggles that panel's own
  viewing/editing fieldset, matching `Model.IsEditing` live.
- The edit fixture is Wingrove Insurance, a Principal, so it demonstrates the
  Principal-only sections (Principal details, Case guidance, Principal
  settings, Report generation, Default inspection location, Provider API,
  Replace principal code). The "Linked principals" role-association UI,
  which only renders for a *non*-Principal contact's own non-Principal role
  checkboxes, has no fixture here and is not captured — it needs a second
  contact (a Claim Source, Repairer, Storage or Third Party Engineer) edited
  with at least one Principal already on file, which this lane's scope did
  not extend to. Noted rather than approximated.
- The "Add contact" dialog's five role cards, and the directory table's Open
  links for every row except Wingrove, post the demo toast rather than
  opening a second fully built create/edit flow — one representative flow
  (Wingrove's Principal edit) is built in full; the rest are noted as
  present-but-stubbed here.
