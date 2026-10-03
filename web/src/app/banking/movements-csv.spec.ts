import { MovementLine } from './models';
import { movementsCsv } from './movements-csv';

const line = (overrides: Partial<MovementLine> = {}): MovementLine => ({
  entryId: 'e1',
  accountId: 'acc-1',
  accountName: 'Alice — Compte courant',
  valueDate: '2026-09-30T08:00:00Z',
  reference: 'Loyer',
  amount: -900.5,
  counterparty: 'Bob',
  ...overrides,
});

describe('movementsCsv', () => {
  it('uses semicolons and decimal commas for French spreadsheets', () => {
    const csv = movementsCsv([line()], 'fr-FR');

    expect(csv.startsWith('﻿')).toBe(true);
    expect(csv.split('\r\n')[1]).toBe('2026-09-30;Alice — Compte courant;Loyer;Bob;-900,50');
  });

  it('uses commas and decimal points in English', () => {
    expect(movementsCsv([line({ amount: 12 })], 'en-US').split('\r\n')[1]).toBe(
      '2026-09-30,Alice — Compte courant,Loyer,Bob,12.00',
    );
  });

  it('quotes separators and defuses formulas', () => {
    const row = movementsCsv([line({ reference: '=HYPERLINK("x")', counterparty: 'A; B' })], 'fr')
      .split('\r\n')[1]
      .split(';');

    expect(row[2]).toBe(`"'=HYPERLINK(""x"")"`);
    expect(row.slice(3, 5).join(';')).toBe('"A; B"');
  });
});
