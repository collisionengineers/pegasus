import {
  Blocker,
  BlockerList,
  Field,
  FormGrid,
  Input,
  Panel,
  SectionLabel,
  SendToClaudeButton,
  WorkbenchGrid,
} from '@pegasus/design-system';

/** The assessment workbench: readiness rail with blockers and the send control, main column with the section being worked on. */
export const AssessmentWorkbench = () => (
  <WorkbenchGrid
    aside={
      <Panel>
        <SectionLabel>Outstanding</SectionLabel>
        <BlockerList>
          <Blocker title="Vehicle registration">Enter the registration on the Vehicle tab.</Blocker>
          <Blocker title="Pre-accident value">Record the value before sending.</Blocker>
        </BlockerList>
        <div style={{ marginTop: 12 }}>
          <SendToClaudeButton />
        </div>
      </Panel>
    }
  >
    <Panel>
      <SectionLabel>Vehicle</SectionLabel>
      <FormGrid>
        <Field label="Registration" htmlFor="wb-reg" hint="As shown on the V5C.">
          <Input id="wb-reg" defaultValue="LM19 KXR" />
        </Field>
        <Field label="Mileage" htmlFor="wb-miles">
          <Input id="wb-miles" defaultValue="48,210" />
        </Field>
      </FormGrid>
    </Panel>
  </WorkbenchGrid>
);
