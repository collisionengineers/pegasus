# Report sending panel: states

Captured in the mockup. Each Principal's rules are the ones `20261007090000_PrincipalReportSendingRules` seeds from the Report Sending SOP v5.

| `?state=` | Principal and what its rules show | Capture |
| --- | --- | --- |
| `kerr-edit` | KERR: the default rules (reply all, fee note separate, report images on, no rules) | `current/captured/contact-kerr-edit.html` |
| `dfd` | DFD: three Claim Source rules: Car 2 Go adds a Cc, SMC adds two, CarClaims stops ("CarClaims job. This needs checking with Andy before it is sent.") | `current/captured/contact-dfd-read.html` |
| `ax` | AX: two fixed Send to addresses, one Cc, fee note not separate, garage figures, and an All rule of two Bodyshop mentions conditions that removes the Cc | `current/captured/contact-ax-read.html` |
| `mp` | MP: two fixed Send to addresses, report without vehicle images, vehicle images document and figure breakdown required | `current/captured/contact-mp-read.html` |
| `pch` | PCH: Send to only one address, Audatex attached, attachment name "{reg} Initial" / "{reg} Supplementary" | `current/captured/contact-pch-read.html` |
| `rjs` | RJS: an Any rule (Instruction mentions "Luton", or Claim Source CS) adding two Cc, and an ACSP Claim Source rule adding one | `current/captured/contact-rjs-read.html` |
| `qdos` | QDOS: one Cc, fee note not separate, garage figures | `current/captured/contact-qdos-read.html` |

Reachable in the source but not captured:

- A refused save: the page's validation summaries (in the Contact form and in Principal settings, above this panel) name the field or "Rule N" and the problem, and the panel keeps what was typed.
- An inactive Principal: the panel is not drawn.
- The page without script: every condition line shows all of its value fields until a kind is chosen.
