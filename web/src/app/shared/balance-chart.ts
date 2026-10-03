import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

export interface BalancePoint {
  readonly at: string;
  readonly balance: number;
}

const WIDTH = 600;
const HEIGHT = 140;
const PADDING = 6;

/**
 * Running balance as a step line: a balance only changes when an entry posts, so a straight segment between two
 * postings would show money that never existed. Plain SVG, no chart library.
 */
@Component({
  selector: 'app-balance-chart',
  template: `
    @if (geometry(); as g) {
      <svg
        [attr.viewBox]="'0 0 ' + width + ' ' + height"
        preserveAspectRatio="none"
        role="img"
        [attr.aria-label]="label()"
      >
        <defs>
          <linearGradient id="balance-fill" x1="0" x2="0" y1="0" y2="1">
            <stop offset="0%" stop-color="var(--accent)" stop-opacity="0.28" />
            <stop offset="100%" stop-color="var(--accent)" stop-opacity="0" />
          </linearGradient>
        </defs>
        <path [attr.d]="g.area" fill="url(#balance-fill)" />
        <path
          [attr.d]="g.line"
          fill="none"
          stroke="var(--accent)"
          stroke-width="2"
          vector-effect="non-scaling-stroke"
        />
      </svg>
    }
  `,
  styles: `
    :host {
      display: block;
    }
    svg {
      display: block;
      width: 100%;
      height: 140px;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class BalanceChart {
  readonly points = input.required<readonly BalancePoint[]>();
  readonly label = input('');

  protected readonly width = WIDTH;
  protected readonly height = HEIGHT;

  protected readonly geometry = computed(() => {
    const points = this.points();
    if (points.length < 2) return null;

    const times = points.map((point) => new Date(point.at).getTime());
    const balances = points.map((point) => point.balance);
    const [start, end] = [Math.min(...times), Math.max(...times)];
    const [low, high] = [Math.min(0, ...balances), Math.max(...balances)];
    const x = (time: number) =>
      PADDING + ((time - start) / (end - start || 1)) * (WIDTH - 2 * PADDING);
    const y = (balance: number) =>
      HEIGHT - PADDING - ((balance - low) / (high - low || 1)) * (HEIGHT - 2 * PADDING);

    let line = `M${x(times[0])},${y(balances[0])}`;
    for (let i = 1; i < points.length; i++) {
      line += ` H${x(times[i])} V${y(balances[i])}`;
    }
    const area = `${line} V${HEIGHT} H${x(times[0])} Z`;
    return { line, area };
  });
}
