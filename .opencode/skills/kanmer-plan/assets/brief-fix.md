# Optional brief overlay — fix

Copy this prompt set into the brief when the ticket corrects existing behaviour.

- **Reproduction:** state the smallest reliable before-fix reproduction and the affected environment/input.
- **Root cause:** name the responsible path and why it produces the observed failure.
- **Regression boundary:** name the adjacent behaviours that must remain unchanged.
- **Negative test:** add an assertion for the failure case that must not recur, not only a happy-path test.
