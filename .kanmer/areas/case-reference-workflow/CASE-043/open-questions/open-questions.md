# Open questions — CASE-043

- [ ] Should all ten CASE-043 fields be required for instruction/case
      completeness, or remain ordinary optional Case fields when neither
      supplied material nor the lookup can provide them?
- [ ] Must the automatic DVLA/DVSA lookup populate every listed field when
      extraction lacks it? The existing approved adapter cannot currently
      obtain VIN, body, transmission, first registration, colour, or tax
      expiry.
- [ ] Does CASE-043 also deliver the staff-editable path for the ten fields, or
      does it deliver only the record, both extraction paths, the lookup and
      the projection while the editable surface stays with the Vehicle-section
      lane? Editing them means expanding `CaseEditableData` plus every
      production save caller (`Pages/Cases/Details.cshtml.cs`,
      `Pages/Cases/Shared/_CaseDataHiddenFields.cshtml`,
      `Mcp/AssessmentMcpTools.cs`), because `EfCaseDataStore.SetConfirmed`
      deletes a confirmed value when its parameter is null; that touches the
      capacity-one `Pages/Cases/Shared/*` lock. Not editing them means the four
      fields retired from the Assessment vocabulary by step 1b lose their
      current Engineer edit route until that lane ships.
