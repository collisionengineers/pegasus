# Normalized Companion: Backup of Conditional Formatting 260202.txt

- Raw source: `docs/reference/raw/collisionrelateddocs/collision_releated/Backup of Conditional Formatting 260202.txt`
- SHA256: `4c5169b1abf1c4332cd785f4353dbc5dd4910b0253e2b1669109b95d518cf506`
- Extraction method: native-text
- Extraction confidence: complete
- Blocker: None recorded

This companion is a derivative working copy. The raw source remains the source of truth.

---

```text
Highlight 4 week old dates:
=AND(ISNUMBER($C1), $C1<=TODAY(), TODAY()-$C1>=28)
Applies to:
=C:C
Stop if true:
Ticked


Highlight 3 week old dates:
=AND(ISNUMBER($C1), $C1<=TODAY(), TODAY()-$C1>=21, TODAY()-$C1<28)
Applies to:
=C:C
Stop if true:
Ticked


Highlight current or past dates:
=AND(ISNUMBER(I1), I1 <= TODAY())
Applies to:
=I:I
Stop if true:
Unticked


Highlight repeated regs:
=AND(SUBSTITUTE(D1," ","")<>"",SUMPRODUCT(--(SUBSTITUTE($D$7:$D$822," ","")=SUBSTITUTE(D1," ","")))>1)
Applies to:
=D:D
Stop if true:
Unticked
```
