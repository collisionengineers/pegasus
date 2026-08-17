import { ValidationSummary } from '@pegasus/design-system';

/** The red-railed summary the form tag helper emits, with its heading. */
export const WithHeading = () => (
  <div style={{ maxWidth: 640 }}>
    <ValidationSummary
      heading="Please correct the following errors:"
      errors={[
        'Vehicle registration is required.',
        'Accident date cannot be after today.',
        'Choose a principal before saving.',
      ]}
    />
  </div>
);

/** List only, no heading. */
export const ListOnly = () => (
  <div style={{ maxWidth: 640 }}>
    <ValidationSummary errors={['A reason is required to reopen this case.', 'Pick a destination stage.']} />
  </div>
);
