import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { ThemeService } from './core/theme';
import { ToastHost } from './shared/toast-host';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, ToastHost],
  template: `<router-outlet /><app-toast-host />`,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class App {
  // Instantiated at startup so the saved or system theme applies before the first paint of any page.
  protected readonly theme = inject(ThemeService);
}
