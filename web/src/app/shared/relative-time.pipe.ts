import { LOCALE_ID, Pipe, PipeTransform, inject } from '@angular/core';

const STEPS: readonly [Intl.RelativeTimeFormatUnit, number][] = [
  ['second', 60],
  ['minute', 60],
  ['hour', 24],
  ['day', 30],
  ['month', 12],
];

/** "il y a 5 minutes" / "5 minutes ago", in the build's locale. */
@Pipe({ name: 'relativeTime' })
export class RelativeTimePipe implements PipeTransform {
  private readonly formatter = new Intl.RelativeTimeFormat(inject(LOCALE_ID), { numeric: 'auto' });

  transform(value: string | Date | null | undefined, now: Date = new Date()): string {
    if (!value) return '';
    let delta = (new Date(value).getTime() - now.getTime()) / 1000;
    for (const [unit, size] of STEPS) {
      if (Math.abs(delta) < size) return this.formatter.format(Math.round(delta), unit);
      delta /= size;
    }
    return this.formatter.format(Math.round(delta), 'year');
  }
}
