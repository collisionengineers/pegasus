# Wireframe — Received item review

One container: header, action bar, tabs. The former two-column content/action-rail split is
retired (ui-standards §4 rule 13).

## Legend

```
[Inbox*]       active nav item (this page is reached from Inbox rows)
(chip:xxx)     state chip; amber=Needs sorting/Missing, red=Blocked,
               green=Case created (linked), grey=neutral
               ("Ready to review" is not an intake chip — it is the operator label for
               the Review Case stage; see the premise correction in alteration-plan.md)
[Button]       secondary button    [[Button]]  primary (red) button
<link>         text link           ~muted~     secondary/muted text
[____]         text input          [v]         select    [x]/[ ]  checkbox
#              container border    ─────       hairline rule
```

## Main state — Needs sorting

This is the state that actually reaches this screen. Definitive authorised intake creates its
case at processing time and never lands here pending (`requirements.md:251`); only ambiguous
or unidentified material does, as `Needs sorting` (`operator-notes.md:204`). **Create case** is
the ambiguity-resolution path (`INT-26`, manual creation through the same business rules) — not
a gate that every intake passes through.

```
┌──────────────────────────────────────────────────────────────────────────────┐
│ COLLISION ENGINEERS | Pegasus   Dashboard [Inbox*] Upload Queues Cases       │
│                                 Administration        alex · Change pw · Out │
├──────────────────────────────────────────────────────────────────────────────┤
│  Inbox › sample-instruction.pdf                                              │
│  ###########################  CONTAINER  ##################################  │
│  # Received item (chip:amber Needs sorting)  sample-instruction.pdf ·      # │
│  #   Uploaded · 04 Aug 2026 16:30                        <Back to Inbox>   # │
│  #--------------------------------------------------------------------------#│
│  # [[Create case]] [Block] [Link to a case] [More v]  | ✓ Case type chosen # │ <- sticky
│  #                                                      ○ Principal ident. # │
│  #--------------------------------------------------------------------------#│
│  # | Details* | Files and images (3) | How this was read (5) |             # │
│  #--------------------------------------------------------------------------#│
│  #  Principal          [Principal A_______]  Claimant name  [Sample Cl...] # │
│  #   ~Suggested from the sender's domain~     ~From page 1 of the instr.~   # │
│  #  Claim number       [_____] (chip:amber   Vehicle reg    [AB12 CDE___]  # │
│  #                             Missing)       ~From page 1~                # │
│  #  Vehicle make       [_____] (chip:amber   Vehicle model  [_____] (chip: # │
│  #                             Missing)                      amber Missing)# │
│  #  Vehicle mileage    [__________]          Accident circ. [Rear-end...]  # │
│  #  Date of incident   [27/02/2025]          Instruction dt [04/08/2026]   # │
│  #  Inspection address [__________]          Inspection date [dd/mm/yyyy]  # │
│  #   ~No inspection address was found. Enter or confirm one.~              # │
│  #                                                     [Save corrections]  # │
│  ############################################################################ │
└──────────────────────────────────────────────────────────────────────────────┘
```

The readiness checklist that used to sit in the rail is now the right-hand end of the action
bar — the operator sees what is outstanding without leaving the field they are filling in.

## Create case — a dialog, not a rail panel

```
        +------------------------------------------------------------+
        |  Create case                                         ( x ) |
        |------------------------------------------------------------|
        |  Principal   [Principal A_________________]                |
        |  Case type   [Inspection                 v]                |
        |  Reason      [___________________________]                 |
        |  ✓ Case type chosen                                        |
        |  ○ Principal identified                                    |
        |  i The case reference is allocated on creation and cannot  |
        |    be changed.                                             |
        |                       ( Cancel )   (( Create case ))       |
        +------------------------------------------------------------+
```

The two "evidence is complete and confirmed" checkboxes are deleted. Incomplete ordinary detail
is not a bar to allocation — the new case enters `Not ready` and its detail is chased there
(`requirements.md:251`). Genuine fail-closed conditions (limits, principal identity, standalone
Audit evidence) refuse with a reason via **Block**, not via an unticked checkbox.

## Alternate state — vehicle images (image-only upload)

```
│  # Received item (chip:grey Vehicle images)  sample-image.jpg ·            # │
│  #   Uploaded · 04 Aug 2026 16:40                        <Back to Inbox>   # │
│  #--------------------------------------------------------------------------#│
│  # [[Register images]] [Block] [More v]                                    # │
│  #--------------------------------------------------------------------------#│
│  # | Details* | Files and images (1) | How this was read (1) |             # │
│  #--------------------------------------------------------------------------#│
│  #  Vehicle registration [AB12 CDE________]                                # │
│  #  ~Registering keeps these images filed under the registration until a   # │
│  #  ~case claims them.~                                                    # │
│  #  Reading results                                                        # │
│  #  ─────                                                                  # │
│  #  No readable registration · 04 Aug 2026 16:40                           # │
```

## Header variant — case created

The ordinary outcome for a definitive instruction, reached without any operator action on this
screen: the case already existed when the item first appeared in the Inbox.

```
│  # Received item (chip:green Case 26001)  sample-instruction.pdf ·         # │
│  #   Uploaded · 04 Aug 2026 16:30                        <Back to Inbox>   # │
│  #--------------------------------------------------------------------------#│
│  # [[Open case 26001]]                                                     # │
│  #--------------------------------------------------------------------------#│
│  # | Details* (read-only) | Files and images (3) | How this was read (5) | # │
```
