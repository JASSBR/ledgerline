import { TestBed } from '@angular/core/testing';
import { BalanceChart } from './balance-chart';

describe('BalanceChart', () => {
  function render(points: { at: string; balance: number }[]) {
    const fixture = TestBed.createComponent(BalanceChart);
    fixture.componentRef.setInput('points', points);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('draws a step line: a balance only changes when an entry posts', () => {
    const element = render([
      { at: '2026-09-01', balance: 0 },
      { at: '2026-09-10', balance: 100 },
      { at: '2026-09-20', balance: 50 },
    ]);
    const line = element.querySelectorAll('path')[1].getAttribute('d')!;
    expect(line).toMatch(/^M[\d.]+,[\d.]+( H[\d.]+ V[\d.]+){2}$/);
  });

  it('draws nothing with fewer than two points', () => {
    expect(render([{ at: '2026-09-01', balance: 10 }]).querySelector('svg')).toBeNull();
  });
});
