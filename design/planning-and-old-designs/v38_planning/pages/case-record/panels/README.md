# Case record panels (Repair Spec)

Temporary review artifact; see the [page README](../README.md).

The Repair Spec section, top to bottom, and what the round does to each part.

| Part | Today (12 October 2026) | Round |
| --- | --- | --- |
| Head | Import, the Glass's slot, Send to AI, More (New repair spec, Print Repair Spec, Versions (n), Compare), Expand, Edit | as today |
| Spec tabs and origin line | one tab per spec with its route tag and Current or Draft; "Populated … by …, from … · n lines · labour rate £x/h" | as today; under P3 the Case holds one spec, since the import landed on it, and the origin line names the import |
| Use repair spec / Duplicate / Discard | editing only | as today |
| Header cells | Repair spec name, Labour rate (£/h) with the rate-card select and the typed rate, Regional uplift, Other costs (£), VAT %, Repairer VAT status — six cells in a four-column grid | as today (**P5** adds the bar; `rate:bar` shows the bar alone for comparison) |
| Grid | Type, Description, Part no., Qty, Unit (£), Hours, Paint h, Material (£), Source, delete; every column a box on every type; Core's off-pattern cells amber | **P1**: a cell the type does not price is a dash; a value sitting in one keeps its amber box |
| Delete all lines, Add line | editing only | as today |
| Labour rate bar | — | **P5**: Labour rate (£/h) select and figure, Regional uplift tick and chip |
| Target % of value | slider from 1 % to the spec's own share, floors, Preview, Apply, Scaled, Remove scaling | as today |
| Contract repair | tick, "agreed total sum £" read from Decisions, mismatch chip, the sentence | **P4**: the sum is a box in the bar that previews the scaling as it is typed; the mismatch chip rescales on press |
| Discounts %, VAT charged on | as today | as today |
| Supplementary | at the foot, with two specs: Changes vs (the other specs), Reason, Explain the change on the report; the diff and the statement once chosen | **P3**: at the top of the section, born of an import onto the existing spec: changes vs the sent version, the cost movement, full comparison, one line per change, Explain, reason, Dismiss; the statement once Explain is ticked |
| Work-lists and roll-up | as today | as today |
