import { transfer } from '../testing';
import { settlementTime, transferStages } from './transfer-progress';

describe('transferStages', () => {
  it('shows reached steps then the happy path still ahead', () => {
    const stages = transferStages(transfer());

    expect(stages.map((stage) => [stage.status, stage.state])).toEqual([
      ['Reserving', 'done'],
      ['Screening', 'current'],
      ['Capturing', 'upcoming'],
      ['Completed', 'upcoming'],
    ]);
    expect(stages.map((stage) => stage.owner)).toEqual(['ledger', 'fraud', 'ledger', 'payments']);
  });

  it('inserts the analyst review when the rules held the transfer', () => {
    const stages = transferStages(
      transfer({
        status: 'PendingReview',
        steps: [
          { status: 'Reserving', at: '2026-10-02T10:00:00Z', detail: null },
          { status: 'Screening', at: '2026-10-02T10:00:01Z', detail: null },
          { status: 'PendingReview', at: '2026-10-02T10:00:02Z', detail: 'Large amount' },
        ],
      }),
    );

    expect(stages.map((stage) => stage.status)).toEqual([
      'Reserving',
      'Screening',
      'PendingReview',
      'Capturing',
      'Completed',
    ]);
    expect(stages[2]).toMatchObject({ state: 'current', owner: 'analyst', detail: 'Large amount' });
  });

  it('predicts nothing once compensation started, and marks the outcome done', () => {
    const rejected = transfer({
      status: 'Rejected',
      steps: [
        { status: 'Reserving', at: '2026-10-02T10:00:00Z', detail: null },
        { status: 'Screening', at: '2026-10-02T10:00:01Z', detail: null },
        { status: 'Releasing', at: '2026-10-02T10:00:02Z', detail: 'Blocklisted' },
        { status: 'Rejected', at: '2026-10-02T10:00:03Z', detail: null },
      ],
    });

    const stages = transferStages(rejected);

    expect(stages.map((stage) => stage.status)).toEqual([
      'Reserving',
      'Screening',
      'Releasing',
      'Rejected',
    ]);
    expect(stages.every((stage) => stage.state === 'done')).toBe(true);
  });
});

describe('settlementTime', () => {
  it('is the time from request to final outcome', () => {
    const completed = transfer({
      status: 'Completed',
      requestedAt: '2026-10-02T10:00:00.000Z',
      steps: [{ status: 'Completed', at: '2026-10-02T10:00:01.250Z', detail: null }],
    });
    expect(settlementTime(completed)).toBe(1250);
  });

  it('is unknown while the transfer is in flight', () => {
    expect(settlementTime(transfer())).toBeNull();
  });
});
