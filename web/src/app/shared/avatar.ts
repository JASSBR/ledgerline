import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

const HUES = [222, 262, 168, 24, 330, 199];

@Component({
  selector: 'app-avatar',
  template: `<span
    class="avatar"
    [style.--hue]="hue()"
    [style.--size.px]="size()"
    [attr.aria-label]="name()"
    role="img"
    >{{ initials() }}</span
  >`,
  styles: `
    .avatar {
      display: inline-grid;
      place-items: center;
      width: var(--size);
      height: var(--size);
      border-radius: 50%;
      font-size: calc(var(--size) * 0.38);
      font-weight: 700;
      letter-spacing: 0.02em;
      color: hsl(var(--hue) 70% 30%);
      background: hsl(var(--hue) 80% 90%);
      flex-shrink: 0;
    }
    :host-context([data-theme='dark']) .avatar {
      color: hsl(var(--hue) 80% 85%);
      background: hsl(var(--hue) 40% 25%);
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Avatar {
  readonly name = input.required<string>();
  readonly size = input(32);
  protected readonly initials = computed(() =>
    this.name()
      .split(/\s+/)
      .filter(Boolean)
      .slice(0, 2)
      .map((part) => part[0]!.toUpperCase())
      .join(''),
  );
  // Stable colour per person, so the same handler is recognisable across the timeline and the feed.
  protected readonly hue = computed(
    () => HUES[[...this.name()].reduce((sum, c) => sum + c.charCodeAt(0), 0) % HUES.length],
  );
}
