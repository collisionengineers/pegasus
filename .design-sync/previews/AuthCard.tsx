import { AuthCard, AuthCardActions, Input, PrimaryAction, SecondaryAction, SupportReference } from '@pegasus/design-system';

const paper = { background: '#f7f6f4', padding: 24 } as const;

/** Sign in: the red mark, the H1, labels wrapping their inputs, one full-width primary action. */
export const SignIn = () => (
  <div style={paper}>
    <AuthCard title="Sign in to Pegasus">
      <form onSubmit={(e) => e.preventDefault()}>
        <label>
          Username
          <Input name="UserName" autoComplete="username" />
        </label>
        <label>
          Password
          <Input name="Password" type="password" autoComplete="current-password" />
        </label>
        <PrimaryAction>Sign in</PrimaryAction>
      </form>
    </AuthCard>
  </div>
);

/** Signed out: the one place green is earned on this family — a completed action, then the form to sign back in. */
export const SignedOut = () => (
  <div style={paper}>
    <AuthCard title="You are signed out" done>
      <form onSubmit={(e) => e.preventDefault()}>
        <label>
          Username
          <Input name="UserName" autoComplete="username" />
        </label>
        <label>
          Password
          <Input name="Password" type="password" autoComplete="current-password" />
        </label>
        <PrimaryAction>Sign in</PrimaryAction>
      </form>
    </AuthCard>
  </div>
);

/** A fault: red left rail, one statement of what happened, stacked actions, and the support reference under the hairline. */
export const Fault = () => (
  <div style={paper}>
    <AuthCard
      title="We could not complete that request"
      fault
      foot={
        <>
          Support reference <SupportReference reference="0HN5K2Q9V3R7L:00000012" />
        </>
      }
    >
      <p>What you submitted may not have been saved. Try again, and if it keeps failing, tell your administrator the reference below.</p>
      <AuthCardActions>
        <PrimaryAction href="#">Try again</PrimaryAction>
        <SecondaryAction href="#">Return to Dashboard</SecondaryAction>
      </AuthCardActions>
    </AuthCard>
  </div>
);

/** The wider card for a form with more fields — the password change, with its one requirement stated beside the field. */
export const WidePasswordChange = () => (
  <div style={paper}>
    <AuthCard title="Change password" wide mark={null}>
      <form onSubmit={(e) => e.preventDefault()}>
        <label>
          Current password
          <Input name="CurrentPassword" type="password" autoComplete="current-password" />
        </label>
        <label>
          New password
          <span className="field-hint">At least 8 characters. Any characters are allowed.</span>
          <Input name="NewPassword" type="password" autoComplete="new-password" />
        </label>
        <label>
          Confirm new password
          <Input name="ConfirmPassword" type="password" autoComplete="new-password" />
        </label>
        <PrimaryAction>Change password</PrimaryAction>
      </form>
    </AuthCard>
  </div>
);
