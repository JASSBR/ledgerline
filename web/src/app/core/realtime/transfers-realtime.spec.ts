import { TestBed } from '@angular/core/testing';
import { HubConnectionState } from '@microsoft/signalr';
import { TransferStatusChanged } from '../../banking/models';
import { ToastService } from '../toast';
import { TRANSFERS_HUB_CONNECTION, TransfersRealtime } from './transfers-realtime';

function fakeHub() {
  const handlers = new Map<string, (payload: TransferStatusChanged) => void>();
  const hooks: Record<string, () => void> = {};
  return {
    state: HubConnectionState.Disconnected,
    start: vi.fn(async function (this: { state: HubConnectionState }) {
      this.state = HubConnectionState.Connected;
    }),
    stop: vi.fn(async () => undefined),
    on: (name: string, handler: (payload: TransferStatusChanged) => void) =>
      handlers.set(name, handler),
    onreconnecting: (callback: () => void) => (hooks['reconnecting'] = callback),
    onreconnected: (callback: () => void) => (hooks['reconnected'] = callback),
    onclose: (callback: () => void) => (hooks['close'] = callback),
    push: (payload: TransferStatusChanged) => handlers.get('transferChanged')!(payload),
    hooks,
  };
}

const change = (
  status: TransferStatusChanged['status'],
  reason: string | null = null,
): TransferStatusChanged => ({
  transferId: 't1',
  ownerId: 'alice',
  status,
  reason,
  occurredAt: '2026-10-02T10:00:00Z',
});

describe('TransfersRealtime', () => {
  function setup(hub = fakeHub()) {
    TestBed.configureTestingModule({
      providers: [{ provide: TRANSFERS_HUB_CONNECTION, useValue: () => hub }],
    });
    return {
      realtime: TestBed.inject(TransfersRealtime),
      toasts: TestBed.inject(ToastService),
      hub,
    };
  }

  it('connects once and tracks the connection state', async () => {
    const { realtime, hub } = setup();
    await realtime.connect();
    await realtime.connect();

    expect(hub.start).toHaveBeenCalledTimes(1);
    expect(realtime.connected()).toBe(true);
    hub.hooks['reconnecting']();
    expect(realtime.connected()).toBe(false);
    hub.hooks['reconnected']();
    expect(realtime.connected()).toBe(true);
    hub.hooks['close']();
    expect(realtime.connected()).toBe(false);
  });

  it('stays usable when the hub is unreachable', async () => {
    const hub = fakeHub();
    hub.start.mockRejectedValueOnce(new Error('offline'));
    const { realtime } = setup(hub);
    await realtime.connect();
    expect(realtime.connected()).toBe(false);
  });

  it('publishes every change and toasts only outcomes the user must notice', () => {
    const { realtime, toasts, hub } = setup();

    hub.push(change('Capturing'));
    expect(realtime.lastChange()?.status).toBe('Capturing');
    expect(toasts.toasts()).toHaveLength(0);

    hub.push(change('Completed'));
    hub.push(change('PendingReview'));
    hub.push(change('Failed', 'Insufficient funds'));
    expect(toasts.toasts().map((toast) => toast.tone)).toEqual(['success', 'info', 'error']);
    expect(toasts.toasts()[2].message).toBe('Insufficient funds');
  });

  it('disconnects', async () => {
    const { realtime, hub } = setup();
    await realtime.disconnect();
    expect(hub.stop).toHaveBeenCalled();
  });
});
