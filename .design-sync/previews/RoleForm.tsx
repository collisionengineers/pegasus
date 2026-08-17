import { ButtonRow, Choice, ChoiceGroup, Input, PrimaryAction, RoleForm } from '@pegasus/design-system';

/** The in-table role assignment: choices, a required reason, then Save roles. */
export const AssignRoles = () => (
  <RoleForm style={{ maxWidth: 420 }}>
    <ChoiceGroup legend="Roles for j.patel">
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
    <label>
      Reason
      <Input name="Reason" required maxLength={1000} />
    </label>
    <ButtonRow>
      <PrimaryAction>Save roles</PrimaryAction>
    </ButtonRow>
  </RoleForm>
);

/** The same form with a reason already typed, granting Administrator. */
export const WithReason = () => (
  <RoleForm style={{ maxWidth: 420 }}>
    <ChoiceGroup legend="Roles for a.mensah">
      <Choice name="SelectedRoles" value="Administrator" defaultChecked>
        Administrator
      </Choice>
      <Choice name="SelectedRoles" value="Engineer">
        Engineer
      </Choice>
      <Choice name="SelectedRoles" value="User" defaultChecked>
        User
      </Choice>
    </ChoiceGroup>
    <label>
      Reason
      <Input name="Reason" required maxLength={1000} defaultValue="Covering administration while R. Hughes is on leave." />
    </label>
    <ButtonRow>
      <PrimaryAction>Save roles</PrimaryAction>
    </ButtonRow>
  </RoleForm>
);
