import { ButtonRow, Field, FormGrid, FormPanel, Input, PrimaryAction, Select, Textarea } from '@pegasus/design-system';

/** The Upload form: section label, one file input with its stated requirement, and the page's single red action. */
export const UploadForm = () => (
  <FormPanel title="Upload a document" form={{ encType: 'multipart/form-data' }}>
    <label htmlFor="upload-file">
      Drag a file here, or browse
      <span className="field-hint">E-mail, Word document, PDF or image — up to 25 MB.</span>
    </label>
    <Input id="upload-file" name="Upload" type="file" />
    <PrimaryAction>Upload</PrimaryAction>
  </FormPanel>
);

/** A `wide` panel carrying a `FormGrid` of case details, as on New case. */
export const WideWithFormGrid = () => (
  <FormPanel title="Details" wide form={{}}>
    <p>What was read from the file, for you to confirm or change. Every value is retained with who put it there.</p>
    <FormGrid>
      <Field label="Claimant" htmlFor="fp-claimant">
        <Input id="fp-claimant" name="claimantName" defaultValue="J. Okafor" />
      </Field>
      <Field label="Claim number" htmlFor="fp-claim">
        <Input id="fp-claim" name="claimNumber" defaultValue="AX/44/210983" />
      </Field>
      <Field label="Registration" htmlFor="fp-reg">
        <Input id="fp-reg" name="vehicleRegistration" defaultValue="LM19 KXR" />
      </Field>
      <Field label="Incident date" htmlFor="fp-incident">
        <Input id="fp-incident" name="incidentDate" type="date" defaultValue="2026-08-06" />
      </Field>
      <Field label="Inspection mode" htmlFor="fp-mode">
        <Select id="fp-mode" name="inspectionMode" defaultValue="image">
          <option value="">Not recorded</option>
          <option value="image">Image Based Assessment</option>
          <option value="physical">Physical inspection</option>
        </Select>
      </Field>
      <Field label="Notes for the engineer" htmlFor="fp-notes" wide>
        <Textarea id="fp-notes" name="notes" rows={3} placeholder="Anything the instruction did not carry." />
      </Field>
    </FormGrid>
    <ButtonRow>
      <PrimaryAction>Create case</PrimaryAction>
    </ButtonRow>
  </FormPanel>
);

/** No `form` prop: the panel is a plain section, here holding a one-time value shown read-only. */
export const PlainSection = () => (
  <FormPanel title="Copy this secret now">
    <p>It is shown once and is not available from case history.</p>
    <Input readOnly value="req_7Kq2mZ9vX4pL8nT3" aria-label="One-time request secret" />
  </FormPanel>
);
