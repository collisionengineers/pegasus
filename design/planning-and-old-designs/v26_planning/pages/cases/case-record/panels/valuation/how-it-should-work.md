# Valuation — how it should work

Decided with the operator on 13 September 2026.

- **Valuation month** sits at the top of the section while editing: a month-and-year field (no day). It defaults to the current month and is the guide month written on every valuation taken from it.
- **One button per source runs the valuation**: Glass's, Brego, Super CAP and AI market research. Pressing one fetches the figures for the Valuation month, shows the source's card as pending ("Looking up · Jun 2026", "Researching · …") and then fills the card with Retail, Trade, the guide month, the mileage and today's date, logs a Notes line, and re-runs the calculation when that source is the basis. A source is fetched again for a different month by changing the month and pressing again.
- **Add valuation** stays for a figure typed by hand (the live dialog: Source, Guide month, Mileage, Date, Time, Retail, Trade) and for sources with no connection, such as Cazana.
- Sources that are not connected keep their seam card; a button is absent, not disabled.
- Stage 2 (decided 13 September): the Glass's, Brego and Super CAP buttons are present but inert until a provider connection exists; AI market research creates the existing Market research AI job and shows the card as pending until the job completes (inline, no notification and no Work Centre row); a fetched valuation for the same source and month replaces the earlier card.
