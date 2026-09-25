# State presets

Each option has the same query presets: `select` (empty picker), `chosen`
(eleven files), `storing`, `processing`, `decision` (proposed Case),
`registered` (one Image reference), `mixed` (one unreadable file), `single`
(one registered image), `failed`, `discarded`, `no-match` (lookup open), `multiple` (two viable Cases),
and `attached` (confirmed destination). The strip switches states.

All files and outcomes are synthetic. Processing and storage are simulated;
the live Worker and custody stores are not called.

## New Upload designs: state coverage

The new A–E files expose 20 query presets through Mockup controls:
`select`, `chosen`, `uploading`, `processing`, `ready`, `multiple`, `no-match`,
`search-error`, `mixed`, `unreadable`, `upload-error`, `conflict`, `single`,
`document`, `duplicate`, `attached`, `discarded`, `incomplete`, `confirm`, `discard`.

[Numbered captures at 1580, 1440 and 760 px](../../../current/v30-upload-new-shots/README.md).
Selection begins without an empty file panel. Unavailable data is never represented
as an empty successful result. Mixed and all-unreadable outcomes are distinct.
