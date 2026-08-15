import { ActionList, Button } from '@pegasus/design-system';

/** A wrapping row of inline actions. */
export const InlineActions = () => (
  <ActionList>
    <li>
      <Button href="#">Open case</Button>
    </li>
    <li>
      <Button href="#">Assign to me</Button>
    </li>
    <li>
      <Button href="#">Record finding</Button>
    </li>
  </ActionList>
);

/** The same row carrying facts rather than actions. */
export const InlineFacts = () => (
  <ActionList>
    <li>
      <b>AXA</b>
    </li>
    <li>LM19 KXR</li>
    <li>J. Okafor</li>
  </ActionList>
);
