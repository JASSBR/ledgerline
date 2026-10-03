import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { environment } from '../../environments/environment';
import { TransfersStore } from '../banking/transfers.store';
import { Auth } from '../core/auth/auth';
import { homeFor } from '../core/auth/guards';
import { otherLocaleLink } from '../core/locale';
import { TransfersRealtime } from '../core/realtime/transfers-realtime';
import { ThemeService } from '../core/theme';
import { Avatar } from '../shared/avatar';
import { Icon } from '../shared/icon';

@Component({
  selector: 'app-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, Avatar, Icon],
  templateUrl: './shell.html',
  styleUrl: './shell.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Shell {
  protected readonly auth = inject(Auth);
  protected readonly realtime = inject(TransfersRealtime);
  protected readonly theme = inject(ThemeService);
  protected readonly transfers = inject(TransfersStore);

  protected readonly repositoryUrl = environment.repositoryUrl;
  protected readonly otherLocale = otherLocaleLink();
  protected readonly home = homeFor(this.auth.isOperator());
  protected readonly menuOpen = signal(false);

  constructor() {
    void this.realtime.connect();
    void this.transfers.load();
    inject(DestroyRef).onDestroy(() => void this.realtime.disconnect());
  }
}
