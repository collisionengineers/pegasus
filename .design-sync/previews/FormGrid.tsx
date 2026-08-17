import { Field, FormGrid, Input, Select, Textarea } from '@pegasus/design-system';

/** Auto-fit grid of labelled fields; the Textarea spans the row with `Field wide`. */
export const CaseDetails = () => (
  <FormGrid>
    <Field label="Claimant" htmlFor="fg-claimant">
      <Input id="fg-claimant" name="claimantName" defaultValue="J. Okafor" />
    </Field>
    <Field label="Registration" htmlFor="fg-reg" hint="As printed on the V5C.">
      <Input id="fg-reg" name="vehicleRegistration" defaultValue="YD68 TFA" aria-describedby="fg-reg-hint" />
    </Field>
    <Field label="Principal" htmlFor="fg-principal">
      <Select id="fg-principal" name="principal" defaultValue="Direct Line">
        <option>AXA</option>
        <option>Direct Line</option>
        <option>Aviva</option>
        <option>LV=</option>
        <option>Admiral</option>
      </Select>
    </Field>
    <Field label="Reason" htmlFor="fg-reason" wide>
      <Textarea id="fg-reason" name="reason" rows={3} placeholder="Why the recorded values are being changed." />
    </Field>
  </FormGrid>
);

/** Two date fields and a wide address, with `sectionGap` beneath an earlier block. */
export const InspectionWithSectionGap = () => (
  <FormGrid sectionGap>
    <Field label="Inspection date" htmlFor="fg-inspection-date">
      <Input id="fg-inspection-date" name="inspectionDate" type="date" defaultValue="2026-08-19" />
    </Field>
    <Field label="Inspection deadline" htmlFor="fg-inspection-deadline">
      <Input id="fg-inspection-deadline" name="inspectionDeadline" type="date" defaultValue="2026-08-26" />
    </Field>
    <Field
      label="Inspection address"
      htmlFor="fg-address"
      wide
      hint="Physical mode only; a recorded address never implies attendance."
    >
      <Input
        id="fg-address"
        name="inspectionAddress"
        defaultValue="Unit 4, Riverside Body Repairs, Leeds LS10 1AB"
        aria-describedby="fg-address-hint"
      />
    </Field>
  </FormGrid>
);
