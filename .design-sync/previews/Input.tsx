import { Field, FormGrid, Input } from '@pegasus/design-system';

/** The input treatments stacked in a narrow column: empty, filled, read-only, disabled and a date. */
export const Treatments = () => (
  <FormGrid style={{ maxWidth: 320 }}>
    <Field label="Claimant" htmlFor="in-claimant">
      <Input id="in-claimant" name="claimantName" placeholder="Name as instructed" />
    </Field>
    <Field label="Registration" htmlFor="in-reg">
      <Input id="in-reg" name="vehicleRegistration" defaultValue="LM19 KXR" />
    </Field>
    <Field label="Reference" htmlFor="in-ref">
      <Input id="in-ref" name="reference" value="CE-2026-01432" readOnly />
    </Field>
    <Field label="Principal" htmlFor="in-principal">
      <Input id="in-principal" name="principal" value="AXA" disabled />
    </Field>
    <Field label="Incident date" htmlFor="in-date">
      <Input id="in-date" name="incidentDate" type="date" defaultValue="2026-08-06" />
    </Field>
  </FormGrid>
);
