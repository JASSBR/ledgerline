import { Transfer, TransferStatus, isTerminal } from './models';

export type StageState = 'done' | 'current' | 'upcoming';
export type StageOwner = 'ledger' | 'fraud' | 'analyst' | 'payments';

export interface Stage {
  readonly status: TransferStatus;
  readonly state: StageState;
  readonly owner: StageOwner;
  readonly at: string | null;
  readonly detail: string | null;
}

/** Which service does the work at each step of the saga — what the timeline is meant to make visible. */
const OWNERS: Readonly<Record<TransferStatus, StageOwner>> = {
  Reserving: 'ledger',
  Screening: 'fraud',
  PendingReview: 'analyst',
  Capturing: 'ledger',
  Releasing: 'ledger',
  Completed: 'payments',
  Rejected: 'payments',
  Failed: 'payments',
};

/** What is still ahead on the happy path from a given status; nothing is predicted once compensation started. */
const AHEAD: Readonly<Partial<Record<TransferStatus, readonly TransferStatus[]>>> = {
  Reserving: ['Screening', 'Capturing', 'Completed'],
  Screening: ['Capturing', 'Completed'],
  PendingReview: ['Capturing', 'Completed'],
  Capturing: ['Completed'],
};

/** The steps the transfer went through (from its timeline), followed by the ones still expected. */
export function transferStages(transfer: Transfer): Stage[] {
  const reached: Stage[] = transfer.steps.map((step, index) => ({
    status: step.status,
    state: index === transfer.steps.length - 1 && !isTerminal(step.status) ? 'current' : 'done',
    owner: OWNERS[step.status],
    at: step.at,
    detail: step.detail,
  }));
  const ahead: Stage[] = (AHEAD[transfer.status] ?? []).map((status) => ({
    status,
    state: 'upcoming',
    owner: OWNERS[status],
    at: null,
    detail: null,
  }));
  return [...reached, ...ahead];
}

/** Wall-clock time from request to final outcome, in milliseconds (null while in flight). */
export function settlementTime(transfer: Transfer): number | null {
  if (!isTerminal(transfer.status) || transfer.steps.length === 0) return null;
  const last = transfer.steps[transfer.steps.length - 1];
  return new Date(last.at).getTime() - new Date(transfer.requestedAt).getTime();
}
