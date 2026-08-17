import { Field, FormGrid, Textarea } from '@pegasus/design-system';

/** Reason field: 5rem minimum, vertical resize, placeholder that names what is wanted. */
export const Reason = () => (
  <FormGrid style={{ maxWidth: 480 }}>
    <Field label="Reason" htmlFor="ta-reason">
      <Textarea id="ta-reason" name="reason" rows={4} placeholder="Why this case is being put on hold." />
    </Field>
  </FormGrid>
);

/** With a recorded value. */
export const WithValue = () => (
  <FormGrid style={{ maxWidth: 480 }}>
    <Field label="Note" htmlFor="ta-note">
      <Textarea
        id="ta-note"
        name="note"
        rows={3}
        defaultValue="Repairer asked for images by 19 Aug; chase again on 21 Aug if nothing arrives."
      />
    </Field>
  </FormGrid>
);
