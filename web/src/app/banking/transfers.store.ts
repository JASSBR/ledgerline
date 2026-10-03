import { HttpClient } from '@angular/common/http';
import { computed, effect, inject, untracked } from '@angular/core';
import {
  patchState,
  signalStore,
  withComputed,
  withHooks,
  withMethods,
  withState,
} from '@ngrx/signals';
import { firstValueFrom } from 'rxjs';
import { TransfersRealtime } from '../core/realtime/transfers-realtime';
import { Transfer, isTerminal } from './models';

interface TransfersState {
  readonly transfers: readonly Transfer[];
  readonly loading: boolean;
  readonly error: boolean;
}

/**
 * The transfers the user can see, kept current by the SignalR feed: a status notification refetches only the
 * transfer it concerns and patches it in place (or prepends it when it is new). Components read, never fetch.
 */
export const TransfersStore = signalStore(
  { providedIn: 'root' },
  withState<TransfersState>({ transfers: [], loading: false, error: false }),
  withComputed(({ transfers }) => ({
    inFlight: computed(() => transfers().filter((transfer) => !isTerminal(transfer.status))),
    pendingReview: computed(() =>
      transfers().filter((transfer) => transfer.status === 'PendingReview'),
    ),
  })),
  withMethods((store, http = inject(HttpClient)) => ({
    async load(): Promise<void> {
      patchState(store, { loading: true, error: false });
      try {
        patchState(store, {
          transfers: await firstValueFrom(http.get<Transfer[]>('/api/payments/transfers')),
          loading: false,
        });
      } catch {
        patchState(store, { loading: false, error: true });
      }
    },
    async refresh(id: string): Promise<void> {
      const fresh = await firstValueFrom(http.get<Transfer>(`/api/payments/transfers/${id}`));
      const known = store.transfers().some((transfer) => transfer.id === id);
      patchState(store, {
        transfers: known
          ? store.transfers().map((transfer) => (transfer.id === id ? fresh : transfer))
          : [fresh, ...store.transfers()],
      });
    },
    upsert(transfer: Transfer): void {
      const others = store.transfers().filter((existing) => existing.id !== transfer.id);
      patchState(store, { transfers: [transfer, ...others] });
    },
    byId(id: string) {
      return computed(() => store.transfers().find((transfer) => transfer.id === id));
    },
  })),
  withHooks({
    onInit(store, realtime = inject(TransfersRealtime)) {
      effect(() => {
        const change = realtime.lastChange();
        if (change) untracked(() => void store.refresh(change.transferId).catch(() => undefined));
      });
    },
  }),
);
