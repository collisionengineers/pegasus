import { FieldCard, FieldGrid } from '@pegasus/design-system';

/** The extracted fields of an instruction as hairline-separated cards, one carrying a conflicting value. */
export const ExtractedFields = () => (
  <div style={{ maxWidth: 960 }}>
    <FieldGrid>
      <FieldCard title="Registration" detail="Extracted · 12 Aug 09:14">
        LM19 KXR
      </FieldCard>
      <FieldCard title="Claim number" detail="Extracted · 12 Aug 09:14">
        AX/44/210983
      </FieldCard>
      <FieldCard title="Accident date" detail="E-mail says 4 Aug 2026" conflict>
        6 Aug 2026
      </FieldCard>
    </FieldGrid>
  </div>
);

/** Two full rows: six fields of a Direct Line instruction, all agreeing. */
export const TwoRows = () => (
  <div style={{ maxWidth: 960 }}>
    <FieldGrid>
      <FieldCard title="Principal" detail="Principal">
        Direct Line
      </FieldCard>
      <FieldCard title="Registration" detail="Extracted">
        YD68 TFA
      </FieldCard>
      <FieldCard title="Claim number" detail="Extracted">
        DL/2026/118420
      </FieldCard>
      <FieldCard title="Claimant" detail="Extracted">
        R. Patel
      </FieldCard>
      <FieldCard title="Accident date" detail="Extracted">
        3 Aug 2026
      </FieldCard>
      <FieldCard title="Received" detail="E-mail">
        14 Aug 08:52
      </FieldCard>
    </FieldGrid>
  </div>
);
