import { Injectable, signal } from '@angular/core';

export interface Toast {
  readonly id: number;
  readonly title: string;
  readonly message?: string;
  readonly tone: 'info' | 'success' | 'error';
}

const LIFETIME_MS = 5000;

@Injectable({ providedIn: 'root' })
export class ToastService {
  private nextId = 0;
  readonly toasts = signal<readonly Toast[]>([]);

  show(toast: Omit<Toast, 'id'>): void {
    const id = ++this.nextId;
    this.toasts.update((toasts) => [...toasts.slice(-3), { ...toast, id }]);
    setTimeout(() => this.dismiss(id), LIFETIME_MS);
  }

  dismiss(id: number): void {
    this.toasts.update((toasts) => toasts.filter((toast) => toast.id !== id));
  }
}
