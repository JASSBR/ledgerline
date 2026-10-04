import {
  ChangeDetectionStrategy,
  Component,
  LOCALE_ID,
  computed,
  inject,
  input,
} from '@angular/core';

/**
 * A headline amount, the way banking apps set it: euros at full size, cents and currency smaller, so the eye
 * reads the magnitude first. Formatting follows the locale (fr: "6 400,00 €", en: "€6,400.00").
 */
@Component({
  selector: 'app-money',
  template: `<span class="money" [attr.aria-label]="label()"
    ><span aria-hidden="true">
      @for (part of parts(); track $index) {
        <span [class.minor]="part.minor">{{ part.value }}</span>
      }
    </span></span
  >`,
  styles: `
    .money {
      font-variant-numeric: tabular-nums;
      letter-spacing: -0.035em;
      white-space: nowrap;
    }
    .minor {
      font-size: 0.55em;
      letter-spacing: -0.01em;
      opacity: 0.6;
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Money {
  readonly value = input.required<number>();
  /** Prefix positive amounts with "+" (credits in a feed). */
  readonly signed = input(false);

  private readonly format = new Intl.NumberFormat(inject(LOCALE_ID), {
    style: 'currency',
    currency: 'EUR',
  });

  protected readonly label = computed(() => this.text());
  protected readonly parts = computed(() => {
    const parts = this.format.formatToParts(this.value());
    const prefix = this.signed() && this.value() > 0 ? [{ value: '+', minor: false }] : [];
    let afterDecimal = false;
    return [
      ...prefix,
      ...parts.map((part) => {
        if (part.type === 'decimal') afterDecimal = true;
        const minor = afterDecimal || part.type === 'currency' || part.type === 'literal';
        return { value: part.value, minor: minor && part.type !== 'minusSign' };
      }),
    ];
  });

  private text(): string {
    const formatted = this.format.format(this.value());
    return this.signed() && this.value() > 0 ? `+${formatted}` : formatted;
  }
}
