import { Button, ButtonRow, PrimaryAction, SecondaryAction } from '@pegasus/design-system';

/** A form footer: the primary submit leads, the hairline companion follows. */
export const FormFooter = () => (
  <ButtonRow>
    <PrimaryAction>Save changes</PrimaryAction>
    <SecondaryAction type="button">Cancel</SecondaryAction>
  </ButtonRow>
);

/** Right-aligned, as in a dialog footer. */
export const EndAligned = () => (
  <div style={{ maxWidth: 520, border: '1px solid #e3e0dc', padding: 16 }}>
    <p style={{ margin: '0 0 12px' }}>Reopen case CE-2026-01432? A reason is required.</p>
    <ButtonRow end>
      <SecondaryAction type="button">Cancel</SecondaryAction>
      <PrimaryAction>Reopen case</PrimaryAction>
    </ButtonRow>
  </div>
);

/** Compact bar buttons share the same row; sectionGap adds space above when the row follows a form section. */
export const CompactActions = () => (
  <ButtonRow sectionGap>
    <Button>Record finding</Button>
    <Button>Assign to me</Button>
    <Button variant="dark" disabled condition="Available once a finding is recorded">
      Complete
    </Button>
  </ButtonRow>
);
