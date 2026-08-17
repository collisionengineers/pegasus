import { Pager } from '@pegasus/design-system';

/** First page: Previous is omitted entirely, never rendered disabled. */
export const FirstPage = () => <Pager label="Case result pages" context="Page 1 · showing 25" nextHref="#" />;

/** A middle page: both links present around the context text. */
export const MiddlePage = () => <Pager label="Case result pages" context="Page 3 of 7" previousHref="#" nextHref="#" />;

/** Last page: Next is omitted. */
export const LastPage = () => <Pager label="Case result pages" context="Page 7 of 7" previousHref="#" />;
