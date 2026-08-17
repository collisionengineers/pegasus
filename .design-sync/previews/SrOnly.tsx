import { SrOnly } from '@pegasus/design-system';

/** Visually hidden text for assistive technology — the span below the caption is intentionally invisible. */
export const HiddenLabel = () => (
  <div>
    <p>The text below is visually hidden:</p>
    <SrOnly>Cases awaiting review, sorted by received date</SrOnly>
  </div>
);
