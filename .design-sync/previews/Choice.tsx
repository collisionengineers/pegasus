import { Choice } from '@pegasus/design-system';

/** Checkboxes, checked and unchecked, red accent. */
export const Checkboxes = () => (
  <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
    <Choice name="instructionComplete" defaultChecked>
      Instructions complete
    </Choice>
    <Choice name="imagesComplete">Images complete</Choice>
  </div>
);

/** Radios in one group, one selected. */
export const Radios = () => (
  <div style={{ display: 'flex', flexDirection: 'column', gap: 8 }}>
    <Choice type="radio" name="rateClass" value="standard" defaultChecked>
      Standard
    </Choice>
    <Choice type="radio" name="rateClass" value="prestige">
      Prestige
    </Choice>
  </div>
);
