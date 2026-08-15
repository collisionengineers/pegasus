import { Button, Field, FilterBar, FormGrid, Input, Select } from '@pegasus/design-system';

/** One line of common filters: keyword, stage, then Search (dark) and Clear (plain). Labels are visually hidden. */
export const CaseFilters = () => (
  <div style={{ maxWidth: 960 }}>
    <FilterBar title="Filter cases">
      <label htmlFor="case-query" className="vh">
        Case/PO or keyword
      </label>
      <Input id="case-query" name="query" placeholder="Case/PO, registration, claimant or claim number" />
      <label htmlFor="case-state" className="vh">
        Case stage
      </label>
      <Select id="case-state" name="state" defaultValue="">
        <option value="">Any stage</option>
        <option>Not ready</option>
        <option>Review</option>
        <option>Held</option>
        <option>Completed</option>
      </Select>
      <Button variant="dark" type="submit">
        Search
      </Button>
      <Button href="#">Clear</Button>
    </FilterBar>
  </div>
);

/** The rarely used fields behind the `More filters` disclosure, shown open as a FormGrid. */
export const WithMoreFiltersOpen = () => (
  <div style={{ maxWidth: 960 }}>
    <FilterBar
      title="Filter cases"
      moreOpen
      more={
        <FormGrid>
          <Field label="Registration" htmlFor="f-registration">
            <Input id="f-registration" name="registration" defaultValue="LM19 KXR" />
          </Field>
          <Field label="Received on" htmlFor="f-received">
            <Input id="f-received" name="received" type="date" />
          </Field>
        </FormGrid>
      }
    >
      <label htmlFor="case-query-2" className="vh">
        Case/PO or keyword
      </label>
      <Input id="case-query-2" name="query" defaultValue="Okafor" />
      <label htmlFor="case-state-2" className="vh">
        Case stage
      </label>
      <Select id="case-state-2" name="state" defaultValue="Review">
        <option value="">Any stage</option>
        <option>Not ready</option>
        <option>Review</option>
        <option>Held</option>
      </Select>
      <Button variant="dark" type="submit">
        Search
      </Button>
      <Button href="#">Clear</Button>
    </FilterBar>
  </div>
);
