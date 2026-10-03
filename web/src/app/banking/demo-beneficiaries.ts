/**
 * The demo bank's well-known accounts, as a customer's saved payees would be. Their IBANs are fixed by the Ledger's
 * seeder (DemoAccounts.cs); the last one is on the Fraud service's blocklist, to show a blocked payment.
 */
export interface DemoBeneficiary {
  readonly name: string;
  readonly iban: string;
  readonly ownerUsername: string | null;
}

export const DEMO_BENEFICIARIES: readonly DemoBeneficiary[] = [
  { name: 'Alice Martin', iban: 'FR7699999000011000000000162', ownerUsername: 'alice' },
  { name: 'Alice Martin — Livret', iban: 'FR7699999000011000000000259', ownerUsername: 'alice' },
  { name: 'Bob Durand', iban: 'FR7699999000012000000000112', ownerUsername: 'bob' },
  { name: 'Chloé Bernard', iban: 'FR7699999000013000000000159', ownerUsername: 'chloe' },
  { name: 'Société Écran SARL', iban: 'FR7699999000016666666666610', ownerUsername: null },
];
