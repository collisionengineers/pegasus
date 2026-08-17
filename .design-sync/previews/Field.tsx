import { Field, FormGrid, Input, Select } from '@pegasus/design-system';

/** Label above the control — the `.form-grid > div` shape. */
export const LabelAndInput = () => (
  <FormGrid style={{ maxWidth: 360 }}>
    <Field label="Claim number" htmlFor="f-claim">
      <Input id="f-claim" name="claimNumber" defaultValue="AX/44/210983" />
    </Field>
  </FormGrid>
);

/** One requirement, stated once, beside the field it governs. */
export const WithHint = () => (
  <FormGrid style={{ maxWidth: 360 }}>
    <Field label="Retail value (£)" htmlFor="f-retail" hint="Required. Chosen from the guide evidence above.">
      <Input id="f-retail" name="retail" type="number" min={0} step="0.01" inputMode="decimal" aria-describedby="f-retail-hint" />
    </Field>
  </FormGrid>
);

/** A field-level validation message in the red-dark tone under the control. */
export const WithError = () => (
  <FormGrid style={{ maxWidth: 360 }}>
    <Field label="Registration" htmlFor="f-reg" error="Enter a UK registration, for example LM19 KXR.">
      <Input id="f-reg" name="vehicleRegistration" defaultValue="LM19-KXR" aria-invalid="true" />
    </Field>
  </FormGrid>
);

/** `wide` spans the whole grid row; the neighbours share the columns above it. */
export const WideInGrid = () => (
  <FormGrid>
    <Field label="Principal" htmlFor="f-principal">
      <Select id="f-principal" name="principal" defaultValue="Aviva">
        <option>AXA</option>
        <option>Aviva</option>
        <option>LV=</option>
      </Select>
    </Field>
    <Field label="Incident date" htmlFor="f-incident">
      <Input id="f-incident" name="incidentDate" type="date" defaultValue="2026-08-03" />
    </Field>
    <Field label="Inspection address" htmlFor="f-address" wide>
      <Input id="f-address" name="inspectionAddress" defaultValue="Image Based Assessment" readOnly />
    </Field>
  </FormGrid>
);
