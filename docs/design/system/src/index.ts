/**
 * @pegasus/design-system — React bindings for the Pegasus operator interface.
 *
 * Every component renders the real markup and class names of
 * `src/Pegasus.Web/wwwroot/css/site.css`; the stylesheet itself ships as
 * `dist/styles.css` and must be loaded once for anything here to be styled.
 */
export { cx } from './cx';
export type { StateName, Tone } from './cx';

export { Icon, ICON_PATHS } from './components/Icon';
export type { IconName, IconProps } from './components/Icon';

export { StatusChip, toneForState } from './components/StatusChip';
export type { StatusChipProps } from './components/StatusChip';

export { Button, PrimaryAction, SecondaryAction, ButtonRow, BackLink, Gated, SendToClaudeButton } from './components/Actions';
export type { ButtonProps, ActionProps, ButtonRowProps, BackLinkProps, GatedProps, SendToClaudeButtonProps } from './components/Actions';

export { Panel, DashboardGrid, SplitMain, ReviewGrid, WorkbenchGrid, Eyebrow, SectionLabel, Blockhead, Lede, EmptyState, SrOnly } from './components/Layout';
export type { WorkbenchGridProps, EyebrowProps, SectionLabelProps, BlockheadProps } from './components/Layout';

export {
  StatusCard,
  Notice,
  AcceptanceBoundary,
  Refresh,
  FreshnessBanner,
  ValidationSummary,
  FailureDetail,
  Blocker,
  BlockerList,
  Provenance,
} from './components/Feedback';
export type {
  StatusCardProps,
  AcceptanceBoundaryProps,
  RefreshProps,
  RefreshStatus,
  FreshnessBannerProps,
  ValidationSummaryProps,
  BlockerProps,
  ProvenanceProps,
} from './components/Feedback';

export {
  MetricStrip,
  Metric,
  QueueGrid,
  QueueCard,
  TileGrid,
  MetricTile,
  QueueFilters,
  QueueList,
  QueueListRow,
  AdminWorkspaces,
  AdminCard,
} from './components/Metrics';
export type {
  MetricStripProps,
  MetricProps,
  QueueCardProps,
  MetricTileProps,
  QueueFilter,
  QueueFiltersProps,
  QueueListRowProps,
  AdminCardProps,
} from './components/Metrics';

export {
  Record,
  RecordHead,
  RecordBar,
  RecordBody,
  Tabs,
  Subtabs,
  SectionTabs,
  Crumb,
  Facts,
  DataRow,
  DetailList,
  EvidenceList,
  EvidenceFigure,
  ProposalDiff,
  FieldGrid,
  FieldCard,
} from './components/Record';
export type {
  RecordProps,
  RecordHeadProps,
  RecordBarProps,
  TabItem,
  TabsProps,
  SubtabsProps,
  SectionTabsProps,
  CrumbProps,
  FactGroup,
  FactsProps,
  DataRowProps,
  DetailListProps,
  EvidenceListProps,
  EvidenceFigureProps,
  ProposalDiffProps,
  FieldCardProps,
} from './components/Record';

export { AppNav, AppShell, PageHeading, BRAND_LOGO } from './components/Shell';
export type { NavItem, AppNavProps, AppShellProps, PageHeadingProps } from './components/Shell';

export { DataTable, TableWrap, Pager, FilterBar, PlainList, ActionList } from './components/Tables';
export type { TableColumn, DataTableProps, PagerProps, FilterBarProps } from './components/Tables';

export { FormPanel, FormGrid, Field, Input, Select, Textarea, Choice, ChoiceGroup, RoleForm, RowConfirm } from './components/Forms';
export type { FormPanelProps, FormGridProps, FieldProps, ChoiceProps, ChoiceGroupProps, RowConfirmProps } from './components/Forms';

export { ReasonDialog } from './components/Overlay';
export type { ReasonDialogProps } from './components/Overlay';

export { AuthShell, AuthCard, AuthCardActions, SupportReference } from './components/Auth';
export type { AuthCardProps, SupportReferenceProps } from './components/Auth';
