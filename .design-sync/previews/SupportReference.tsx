import { SupportReference } from '@pegasus/design-system';

/** The request id in code with a Copy button — the content of an AuthCard foot after "Support reference". */
export const Reference = () => (
  <p className="auth-card__foot" style={{ margin: 0, paddingTop: 0, borderTop: 0 }}>
    Support reference <SupportReference reference="0HN5K2Q9V3R7L:00000012" />
  </p>
);
