import { TestBed } from '@angular/core/testing';
import { RelativeTimePipe } from './relative-time.pipe';

describe('RelativeTimePipe', () => {
  const now = new Date('2026-10-02T12:00:00Z');

  it('formats recent moments in French', () => {
    const pipe = TestBed.runInInjectionContext(() => new RelativeTimePipe());
    expect(pipe.transform('2026-10-02T11:55:00Z', now)).toBe('il y a 5 minutes');
    expect(pipe.transform('2026-10-01T12:00:00Z', now)).toBe('hier');
    expect(pipe.transform(null, now)).toBe('');
  });
});
