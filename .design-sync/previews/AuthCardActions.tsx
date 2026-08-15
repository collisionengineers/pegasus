import { AuthCard, AuthCardActions, PrimaryAction, SecondaryAction } from '@pegasus/design-system';

/** Stacked full-width actions inside an AuthCard: the primary first, then the way back. */
export const AccessDenied = () => (
  <div style={{ background: '#f7f6f4', padding: 24 }}>
    <AuthCard title="You do not have access to that page" fault>
      <p>Your account does not include the role that page needs. Ask your administrator if you think it should.</p>
      <AuthCardActions>
        <PrimaryAction href="#">Return to Dashboard</PrimaryAction>
        <SecondaryAction href="#">Sign in as someone else</SecondaryAction>
      </AuthCardActions>
    </AuthCard>
  </div>
);
