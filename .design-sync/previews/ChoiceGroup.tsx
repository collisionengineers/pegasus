import { Choice, ChoiceGroup } from '@pegasus/design-system';

/** A row of role checkboxes for one account, as in the Roles administration table. */
export const RolesRow = () => (
  <ChoiceGroup legend="Roles for j.patel" style={{ maxWidth: 420 }}>
    <Choice name="SelectedRoles" value="Administrator">
      Administrator
    </Choice>
    <Choice name="SelectedRoles" value="Engineer" defaultChecked>
      Engineer
    </Choice>
    <Choice name="SelectedRoles" value="User" defaultChecked>
      User
    </Choice>
  </ChoiceGroup>
);

/** `stacked` radios for a single-choice setting. */
export const StackedRadios = () => (
  <ChoiceGroup legend="Rate class" stacked style={{ maxWidth: 320 }}>
    <Choice type="radio" name="rateClass" value="standard" defaultChecked>
      Standard
    </Choice>
    <Choice type="radio" name="rateClass" value="prestige">
      Prestige
    </Choice>
  </ChoiceGroup>
);
