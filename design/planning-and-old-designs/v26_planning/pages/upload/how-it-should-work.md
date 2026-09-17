# Upload — how it should work

Decisions landing on Upload from other pages' planning. Its own planning is still to come.

| From | Decision | Where on this page |
| --- | --- | --- |
| Received file D3 | a file that could not be read says "Could not be read" with the reason in operator words on its confirmation row; the person re-uploads; no link to a receipt | confirmation |
| Received file D2 | attach-with-override confirms on the Case's Files (Add evidence with the override reason); reversal is Remove with reason on the Case's Files. Neither opens a receipt. | confirmation actions |
| Received file Q3 | a file that could not be read also becomes an Unidentified item, linked from its confirmation row | confirmation |

## One upload is one group

Decided 13 September 2026. This is what the live contract already says (FRD-02: "a multi-file image submission from a manual upload … is one evidence group, not a set of independent images: a damage close-up carrying no registration must not detach itself from an overview image submitted with it, and the group, never an individual image, is the unit that reaches an association, a pre-Case Image intake registration, or an Unidentified outcome"). The mockup is wrong and is corrected:

- The files of one upload are one group with one destination. Five photographs of a vehicle are one vehicle; a registration read on any one of them applies to the group. Not every photograph shows the plate, and that is expected.
- The confirmation shows **one decision for the group**: attached to Case X, registered as vehicle images under registration Y, or Unidentified. It never shows files dispersed across different Cases.
- A member that could not be read does not split the group. It is flagged on its row ("Could not be read") and travels with the group to the same destination; if no member could be read, the group is one Unidentified item.
- The mockup's "Decided" state (some accepted, some failed, spread over destinations) is replaced by the single group decision with per-member read status underneath.
