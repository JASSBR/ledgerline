import { Injectable, InjectionToken, inject, signal } from '@angular/core';
import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr';
import { TRANSFER_STATUS_LABELS } from '../../banking/labels';
import { TransferStatusChanged } from '../../banking/models';
import { API_BASE_URL } from '../api-base-url';
import { Auth } from '../auth/auth';
import { ToastService } from '../toast';

export type TransfersHubConnection = Pick<
  HubConnection,
  'state' | 'start' | 'stop' | 'on' | 'onreconnecting' | 'onreconnected' | 'onclose'
>;

export const TRANSFERS_HUB_CONNECTION = new InjectionToken<() => TransfersHubConnection>(
  'TRANSFERS_HUB_CONNECTION',
  {
    providedIn: 'root',
    factory: () => {
      const auth = inject(Auth);
      const baseUrl = inject(API_BASE_URL);
      return () =>
        new HubConnectionBuilder()
          .withUrl(`${baseUrl}/hubs/transfers`, {
            accessTokenFactory: () => auth.accessToken() ?? '',
          })
          .withAutomaticReconnect()
          .configureLogging(LogLevel.Warning)
          .build();
    },
  },
);

/** One connection to the Payments hub. Screens react to `lastChange`; terminal outcomes also raise a toast. */
@Injectable({ providedIn: 'root' })
export class TransfersRealtime {
  private readonly toasts = inject(ToastService);
  private readonly connection = inject(TRANSFERS_HUB_CONNECTION)();

  readonly lastChange = signal<TransferStatusChanged | null>(null);
  readonly connected = signal(false);

  constructor() {
    this.connection.on('transferChanged', (change: TransferStatusChanged) => this.receive(change));
    this.connection.onreconnecting(() => this.connected.set(false));
    this.connection.onreconnected(() => this.connected.set(true));
    this.connection.onclose(() => this.connected.set(false));
  }

  async connect(): Promise<void> {
    if (this.connection.state !== HubConnectionState.Disconnected) return;
    try {
      await this.connection.start();
      this.connected.set(true);
    } catch {
      // Live updates are a comfort: every screen still works on plain HTTP.
      this.connected.set(false);
    }
  }

  async disconnect(): Promise<void> {
    await this.connection.stop();
  }

  private receive(change: TransferStatusChanged): void {
    this.lastChange.set(change);
    if (
      change.status === 'Completed' ||
      change.status === 'Rejected' ||
      change.status === 'Failed' ||
      change.status === 'PendingReview'
    ) {
      this.toasts.show({
        tone:
          change.status === 'Completed'
            ? 'success'
            : change.status === 'PendingReview'
              ? 'info'
              : 'error',
        title: TRANSFER_STATUS_LABELS[change.status],
        message: change.reason ?? undefined,
      });
    }
  }
}
