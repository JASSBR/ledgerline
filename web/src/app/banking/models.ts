// Mirrors the services' contracts. Enums travel as names. Amounts are decimal euros on the wire (cents inside the Ledger).

export type AccountKind = 'Customer' | 'Internal';

export interface Hold {
  readonly transferId: string;
  readonly amount: number;
  readonly reference: string;
}

export interface Account {
  readonly id: string;
  readonly iban: string;
  readonly ibanFormatted: string;
  readonly name: string;
  readonly kind: AccountKind;
  readonly balance: number;
  readonly available: number;
  readonly held: number;
  readonly holds: readonly Hold[];
}

export interface StatementLine {
  readonly entryId: string;
  readonly valueDate: string;
  readonly reference: string;
  readonly amount: number;
  readonly counterparty: string;
  readonly balanceAfter: number;
}

export interface BalanceAsOf {
  readonly accountId: string;
  readonly asOf: string;
  readonly balance: number;
  readonly available: number;
}

export interface IbanLookup {
  readonly iban: string;
  readonly name: string;
  readonly exists: boolean;
}

export interface JournalLine {
  readonly accountId: string;
  readonly accountName: string;
  readonly debit: number;
  readonly credit: number;
}

export interface JournalEntry {
  readonly id: string;
  readonly reference: string;
  readonly postedAt: string;
  readonly lines: readonly JournalLine[];
  readonly balanced: boolean;
}

export interface TrialBalance {
  readonly accounts: readonly {
    readonly accountId: string;
    readonly name: string;
    readonly kind: AccountKind;
    readonly balance: number;
  }[];
  readonly total: number;
  readonly balanced: boolean;
  readonly journalEntries: number;
}

export const TRANSFER_STATUSES = [
  'Reserving',
  'Screening',
  'PendingReview',
  'Capturing',
  'Releasing',
  'Completed',
  'Rejected',
  'Failed',
] as const;
export type TransferStatus = (typeof TRANSFER_STATUSES)[number];

export interface TransferStep {
  readonly status: TransferStatus;
  readonly at: string;
  readonly detail: string | null;
}

export interface Transfer {
  readonly id: string;
  readonly fromAccountId: string;
  readonly fromName: string;
  readonly toIban: string;
  readonly toName: string;
  readonly amount: number;
  readonly label: string;
  readonly status: TransferStatus;
  readonly reason: string | null;
  readonly requestedAt: string;
  readonly steps: readonly TransferStep[];
}

export interface TransferRequest {
  readonly fromAccountId: string;
  readonly toIban: string;
  readonly amount: number;
  readonly label: string | null;
}

export interface TransferStatusChanged {
  readonly transferId: string;
  readonly ownerId: string;
  readonly status: TransferStatus;
  readonly reason: string | null;
  readonly occurredAt: string;
}

export type ScreeningStatus =
  'Cleared' | 'Blocked' | 'PendingReview' | 'ApprovedByAnalyst' | 'RejectedByAnalyst';

export interface RuleHit {
  readonly rule: string;
  readonly outcome: 'Clear' | 'Review' | 'Block';
  readonly explanation: string;
}

export interface Screening {
  readonly transferId: string;
  readonly fromAccount: string;
  readonly toIban: string;
  readonly toName: string | null;
  readonly amount: number;
  readonly requestedAt: string;
  readonly decision: 'Clear' | 'Review' | 'Block';
  readonly status: ScreeningStatus;
  readonly hits: readonly RuleHit[];
  readonly decidedBy: string | null;
  readonly decidedAt: string | null;
  readonly comment: string | null;
}

export function isTerminal(status: TransferStatus): boolean {
  return status === 'Completed' || status === 'Rejected' || status === 'Failed';
}
