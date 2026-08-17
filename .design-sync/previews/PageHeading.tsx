import { Button, PageHeading, PrimaryAction, Refresh } from '@pegasus/design-system';

/** The one H1 with the screen's freshness element right-aligned. No lede. */
export const WithRefresh = () => <PageHeading title="Dashboard" refresh={<Refresh updatedAt="14 Aug 09:32" />} />;

/** Eyebrow above the title, and the screen's safe primary action in the actions slot. */
export const EyebrowAndAction = () => (
  <PageHeading eyebrow="Administration" title="Staff accounts" actions={<PrimaryAction href="#">Add staff member</PrimaryAction>} />
);

/** Two compact actions in the actions slot; a stale query shows its chip beside the time. */
export const ActionsAndStaleRefresh = () => (
  <PageHeading
    title="Cases"
    actions={
      <>
        <Button icon="file-text">Export list</Button>
        <Button variant="dark">New case</Button>
      </>
    }
    refresh={<Refresh updatedAt="14 Aug 08:05" status="stale" />}
  />
);

/** Title only — most record-adjacent screens carry nothing on the right. */
export const TitleOnly = () => <PageHeading title="Change password" />;
