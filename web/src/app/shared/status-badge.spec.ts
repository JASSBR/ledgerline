import { TestBed } from '@angular/core/testing';
import { TransferStatus } from '../banking/models';
import { StatusBadge } from './status-badge';

describe('StatusBadge', () => {
  it.each<[TransferStatus, string, string]>([
    ['Screening', 'progress', 'Contrôle anti-fraude'],
    ['PendingReview', 'review', 'En revue par un analyste'],
    ['Completed', 'done', 'Exécuté'],
    ['Failed', 'failed', 'Échoué'],
    ['Rejected', 'failed', 'Refusé'],
  ])('renders %s with the %s tone', (status, tone, label) => {
    const fixture = TestBed.createComponent(StatusBadge);
    fixture.componentRef.setInput('status', status);
    fixture.detectChanges();
    const badge = (fixture.nativeElement as HTMLElement).querySelector('.badge')!;
    expect(badge.getAttribute('data-tone')).toBe(tone);
    expect(badge.textContent).toBe(label);
  });
});
