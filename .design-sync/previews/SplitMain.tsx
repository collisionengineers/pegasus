import { Panel, SectionLabel, SplitMain } from '@pegasus/design-system';

/** The list leads at 2fr; the form that adds to it follows at a minimum of 300px. */
export const ListAndForm = () => (
  <SplitMain>
    <Panel>
      <SectionLabel>Approved mailboxes</SectionLabel>
      <ul style={{ margin: 0, paddingLeft: 18 }}>
        <li>claims@axa-instructions.co.uk</li>
        <li>engineering@directline.com</li>
        <li>motor.claims@aviva.co.uk</li>
      </ul>
    </Panel>
    <Panel>
      <SectionLabel>Add a mailbox</SectionLabel>
      <p style={{ margin: 0 }}>Enter the sender address a principal instructs from.</p>
    </Panel>
  </SplitMain>
);
