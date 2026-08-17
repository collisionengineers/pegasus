import { cloneElement, isValidElement } from 'react';
import type { FieldsetHTMLAttributes, FormHTMLAttributes, HTMLAttributes, InputHTMLAttributes, ReactNode, SelectHTMLAttributes, TextareaHTMLAttributes } from 'react';
import { cx } from '../cx';

export interface FormPanelProps extends Omit<HTMLAttributes<HTMLElement>, 'title'> {
  /** The section label (`.section-label`, uppercase muted) naming the form. */
  title?: ReactNode;
  /** Removes the 45rem max width. */
  wide?: boolean;
  /** Form attributes; when set, children render inside a `<form>` with the 12px grid gap. */
  form?: FormHTMLAttributes<HTMLFormElement>;
  children: ReactNode;
}

/** `.panel.form-panel` — the standard form section: title, then a `<form>` laid out as a 12px grid. */
export function FormPanel({ title, wide, form, className, children, ...rest }: FormPanelProps) {
  return (
    <section className={cx('panel', 'form-panel', wide && 'form-panel--wide', className)} {...rest}>
      {title ? <h2 className="section-label">{title}</h2> : null}
      {form ? (
        <form
          {...form}
          onSubmit={(e) => {
            if (form.onSubmit) form.onSubmit(e);
            else e.preventDefault();
          }}
        >
          {children}
        </form>
      ) : (
        children
      )}
    </section>
  );
}

export interface FormGridProps extends HTMLAttributes<HTMLDivElement> {
  sectionGap?: boolean;
}

/** `.form-grid` — auto-fit field grid (min 240px per field); use `Field wide` to span. */
export function FormGrid({ sectionGap, className, ...rest }: FormGridProps) {
  return <div className={cx('form-grid', sectionGap && 'section-gap', className)} {...rest} />;
}

export interface FieldProps extends Omit<HTMLAttributes<HTMLDivElement>, 'children'> {
  /** The label text. */
  label: ReactNode;
  /** `id` of the control. The label's `htmlFor` points at it, and a single element child is cloned with `id` and `aria-describedby` for the hint/error. */
  htmlFor: string;
  /** One requirement, stated once, beside the field it governs (`.field-hint`). */
  hint?: ReactNode;
  /** Field-level validation message (`.field-validation-error`). */
  error?: ReactNode;
  /** Spans the whole `FormGrid` row (`.field-wide`). */
  wide?: boolean;
  /** The control: `Input`, `Select`, `Textarea`. */
  children: ReactNode;
}

/** A labelled control cell for `FormGrid`: label, control, hint, validation — the `.form-grid > div` shape. */
export function Field({ label, htmlFor, hint, error, wide, className, children, ...rest }: FieldProps) {
  const describedBy = [hint ? `${htmlFor}-hint` : null, error ? `${htmlFor}-error` : null].filter(Boolean).join(' ') || undefined;
  const control = isValidElement<{ id?: string; 'aria-describedby'?: string; 'aria-invalid'?: boolean }>(children)
    ? cloneElement(children, {
        id: children.props.id ?? htmlFor,
        'aria-describedby': children.props['aria-describedby'] ?? describedBy,
        'aria-invalid': error ? true : children.props['aria-invalid'],
      })
    : children;
  return (
    <div className={cx(wide && 'field-wide', className)} {...rest}>
      <label htmlFor={htmlFor}>{label}</label>
      {control}
      {hint ? (
        <small id={`${htmlFor}-hint`} className="field-hint">
          {hint}
        </small>
      ) : null}
      {error ? (
        <span id={`${htmlFor}-error`} className="field-validation-error">
          {error}
        </span>
      ) : null}
    </div>
  );
}

/** `<input>` — 34px tall, hairline `line-strong` border, 5px radius; readonly/disabled recess onto paper. */
export function Input(props: InputHTMLAttributes<HTMLInputElement>) {
  return <input {...props} />;
}

/** `<select>` — same treatment as `Input`. */
export function Select(props: SelectHTMLAttributes<HTMLSelectElement>) {
  return <select {...props} />;
}

/** `<textarea>` — 5rem minimum, vertical resize. */
export function Textarea(props: TextareaHTMLAttributes<HTMLTextAreaElement>) {
  return <textarea {...props} />;
}

export interface ChoiceProps extends Omit<InputHTMLAttributes<HTMLInputElement>, 'type' | 'children'> {
  /** `checkbox` or `radio`. */
  type?: 'checkbox' | 'radio';
  children: ReactNode;
}

/** `label.choice` — a checkbox or radio with its text on one line, red accent colour. */
export function Choice({ type = 'checkbox', children, className, ...rest }: ChoiceProps) {
  return (
    <label className={cx('choice', className)}>
      <input type={type} {...rest} />
      <span>{children}</span>
    </label>
  );
}

export interface ChoiceGroupProps extends FieldsetHTMLAttributes<HTMLFieldSetElement> {
  /** Uppercase muted legend, e.g. `Roles for j.patel` or `Rate class`. */
  legend: ReactNode;
  /** Stack choices in a column (`.role-choices--stacked`). */
  stacked?: boolean;
  children: ReactNode;
}

/** `fieldset.role-choices` — a bordered group of `Choice`s with a legend; wraps in a row, or stacks. */
export function ChoiceGroup({ legend, stacked, className, children, ...rest }: ChoiceGroupProps) {
  return (
    <fieldset className={cx('role-choices', stacked && 'role-choices--stacked', className)} {...rest}>
      <legend>{legend}</legend>
      {children}
    </fieldset>
  );
}

/** `.role-form` — the narrow in-table administration form (choices, reason, save). */
export function RoleForm({ className, onSubmit, ...rest }: FormHTMLAttributes<HTMLFormElement>) {
  return (
    <form
      className={cx('role-form', className)}
      method="post"
      onSubmit={(e) => {
        if (onSubmit) onSubmit(e);
        else e.preventDefault();
      }}
      {...rest}
    />
  );
}

export interface RowConfirmProps extends Omit<FormHTMLAttributes<HTMLFormElement>, 'children'> {
  /** The disclosure button text, e.g. `Withdraw link`. */
  summary: ReactNode;
  /** Label for the reason input (default `Reason`). */
  reasonLabel?: ReactNode;
  /** `id` for the reason input. */
  reasonId: string;
  /** The confirming button text (dark `.btn`). */
  confirm: ReactNode;
  /** Render the disclosure open (for previews / already-confirming rows). */
  open?: boolean;
}

/**
 * `<details>` + `.row-confirm` — an action that needs a reason confirms in
 * the row it belongs to: a `.btn` summary, then Reason + a dark confirm button.
 */
export function RowConfirm({ summary, reasonLabel = 'Reason', reasonId, confirm, open, className, onSubmit, ...rest }: RowConfirmProps) {
  return (
    <details open={open}>
      <summary className="btn">{summary}</summary>
      <form
        method="post"
        className={cx('row-confirm', className)}
        onSubmit={(e) => {
          if (onSubmit) onSubmit(e);
          else e.preventDefault();
        }}
        {...rest}
      >
        <label htmlFor={reasonId}>{reasonLabel}</label>
        <input id={reasonId} name="reason" maxLength={500} required />
        <button className="btn btn--dark" type="submit">
          {confirm}
        </button>
      </form>
    </details>
  );
}
