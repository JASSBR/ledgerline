import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Account, Transfer, TransferStatusChanged } from './banking/models';
import { Auth, CurrentUser } from './core/auth/auth';
import { TransfersRealtime } from './core/realtime/transfers-realtime';

export const ALICE: CurrentUser = {
  id: 'a11ce000-0000-4000-8000-000000000001',
  username: 'alice',
  name: 'Alice Martin',
  roles: ['customer'],
};
export const OLIVIA: CurrentUser = {
  id: '0ae7a700-0000-4000-8000-000000000004',
  username: 'olivia',
  name: 'Olivia Roux',
  roles: ['operator'],
};

export function fakeAuth(user: CurrentUser | null = ALICE) {
  const current = signal<CurrentUser | null>(user);
  const fake = {
    user: current,
    isAuthenticated: () => current() !== null,
    isOperator: () => current()?.roles.includes('operator') ?? false,
    accessToken: () => (current() ? 'token' : null),
    login: vi.fn(async () => undefined),
    logout: vi.fn(async () => undefined),
  };
  return { fake, provider: { provide: Auth, useValue: fake } };
}

/** Stands in for the SignalR connection: tests push notifications by setting lastChange. */
export function fakeRealtime() {
  const fake = {
    lastChange: signal<TransferStatusChanged | null>(null),
    connected: signal(true),
    connect: async () => undefined,
    disconnect: async () => undefined,
  };
  return { fake, provider: { provide: TransfersRealtime, useValue: fake } };
}

export function transfer(overrides: Partial<Transfer> = {}): Transfer {
  return {
    id: 't1',
    fromAccountId: 'acc-1',
    fromName: 'Alice Martin — Compte courant',
    toIban: 'FR7699999000012000000000112',
    toName: 'Bob Durand — Compte courant',
    amount: 120,
    label: 'Dîner',
    status: 'Screening',
    reason: null,
    requestedAt: '2026-10-02T10:00:00Z',
    steps: [
      { status: 'Reserving', at: '2026-10-02T10:00:00.100Z', detail: null },
      { status: 'Screening', at: '2026-10-02T10:00:00.300Z', detail: null },
    ],
    ...overrides,
  };
}

export function account(overrides: Partial<Account> = {}): Account {
  return {
    id: 'acc-1',
    iban: 'FR7699999000011000000000162',
    ibanFormatted: 'FR76 9999 9000 0110 0000 0000 162',
    name: 'Alice Martin — Compte courant',
    kind: 'Customer',
    balance: 6400,
    available: 6400,
    held: 0,
    holds: [],
    ...overrides,
  };
}

/**
 * Lets promise callbacks run, then flushes signal effects. Needed where whenStable() can't be used:
 * it also waits for in-flight HTTP requests, which a test must flush itself first.
 */
export async function settle(): Promise<void> {
  await new Promise((resolve) => setTimeout(resolve));
  TestBed.tick();
}
