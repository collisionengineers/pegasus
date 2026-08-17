import { Field, FormGrid, Select } from '@pegasus/design-system';

/** A select with a settled default; same 34px hairline treatment as `Input`. */
export const WithOptions = () => (
  <FormGrid style={{ maxWidth: 320 }}>
    <Field label="Inspection mode" htmlFor="sel-mode">
      <Select id="sel-mode" name="inspectionMode" defaultValue="image">
        <option value="">Not recorded</option>
        <option value="image">Image Based Assessment</option>
        <option value="physical">Physical inspection</option>
      </Select>
    </Field>
  </FormGrid>
);

/** Disabled: recessed onto paper, value still legible. */
export const Disabled = () => (
  <FormGrid style={{ maxWidth: 320 }}>
    <Field label="Principal" htmlFor="sel-principal">
      <Select id="sel-principal" name="principal" defaultValue="Direct Line" disabled>
        <option>AXA</option>
        <option>Direct Line</option>
        <option>Aviva</option>
      </Select>
    </Field>
  </FormGrid>
);
