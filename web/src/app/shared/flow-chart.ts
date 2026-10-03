import {
  ChangeDetectionStrategy,
  Component,
  LOCALE_ID,
  computed,
  inject,
  input,
} from '@angular/core';
import { MonthlyFlow } from '../banking/models';

/**
 * Money in and money out, month by month, as paired bars. HTML bars rather than SVG: the month labels stay crisp
 * text at any width, and each month reads out as one sentence for screen readers.
 */
@Component({
  selector: 'app-flow-chart',
  template: `
    <div class="chart" role="list" [attr.aria-label]="label()">
      @for (month of bars(); track month.key) {
        <div class="month" role="listitem" [attr.aria-label]="month.description">
          <div class="bars" aria-hidden="true">
            <span class="bar in" [style.height.%]="month.in"></span>
            <span class="bar out" [style.height.%]="month.out"></span>
          </div>
          <span class="name" aria-hidden="true">{{ month.name }}</span>
        </div>
      }
    </div>
  `,
  styles: `
    :host {
      display: block;
    }
    .chart {
      display: flex;
      align-items: stretch;
      gap: 0.5rem;
      height: 170px;
      padding-bottom: 0.25rem;
      overflow-x: auto;
      border-bottom: 1px solid var(--text);
    }
    .month {
      flex: 1 0 2.75rem;
      display: grid;
      grid-template-rows: 1fr auto;
      gap: 0.35rem;
    }
    .bars {
      display: flex;
      align-items: flex-end;
      justify-content: center;
      gap: 3px;
      border-bottom: 1px solid var(--rule);
    }
    .bar {
      width: min(40%, 1.1rem);
      min-height: 1px;
      border-radius: 2px 2px 0 0;
      transition: height 0.4s ease;
    }
    .in {
      background: var(--accent);
    }
    .out {
      background: var(--column);
    }
    .name {
      font-family: var(--serif);
      font-style: italic;
      font-size: 0.8rem;
      color: var(--muted);
      text-align: center;
      white-space: nowrap;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FlowChart {
  readonly months = input.required<readonly MonthlyFlow[]>();
  readonly label = input('');

  private readonly locale = inject(LOCALE_ID);
  private readonly monthName = new Intl.DateTimeFormat(this.locale, {
    month: 'short',
    year: '2-digit',
    timeZone: 'UTC',
  });
  private readonly euros = new Intl.NumberFormat(this.locale, {
    style: 'currency',
    currency: 'EUR',
  });

  protected readonly bars = computed(() => {
    const months = this.months();
    const peak = Math.max(1, ...months.flatMap((month) => [month.moneyIn, month.moneyOut]));
    return months.map((month) => {
      const name = this.monthName.format(new Date(`${month.month}-01T00:00:00Z`));
      const moneyIn = this.euros.format(month.moneyIn);
      const moneyOut = this.euros.format(month.moneyOut);
      return {
        key: month.month,
        name,
        in: (month.moneyIn / peak) * 100,
        out: (month.moneyOut / peak) * 100,
        description: $localize`:@@flow.month:${name}:month: : ${moneyIn}:in: entrés, ${moneyOut}:out: sortis`,
      };
    });
  });
}
