import { AuthCard, AuthShell, Input, PrimaryAction } from '@pegasus/design-system';

/** The navless, centred, full-height paper ground with the sign-in card on it. */
export const SignInScreen = () => (
  <AuthShell>
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
  </AuthShell>
);
