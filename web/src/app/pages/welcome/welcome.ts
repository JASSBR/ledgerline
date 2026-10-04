import { CurrencyPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { environment } from '../../../environments/environment';
import { TRANSFER_STATUS_LABELS } from '../../banking/labels';
import { Auth } from '../../core/auth/auth';
import { otherLocaleLink } from '../../core/locale';
import { warmUpBackends } from '../../core/warm-up';
import { Avatar } from '../../shared/avatar';
import { Icon, IconName } from '../../shared/icon';
import { Money } from '../../shared/money';

interface DemoUser {
  readonly username: string;
  readonly name: string;
  readonly role: string;
  readonly summary: string;
}

/** Same password for every demo user, shown on screen: the point is to try OIDC, not to guard fictitious money. */
export const DEMO_PASSWORD = 'ledgerline-demo';

@Component({
  selector: 'app-welcome',
  imports: [Avatar, Icon, Money, CurrencyPipe],
  templateUrl: './welcome.html',
  styleUrl: './welcome.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Welcome {
  private readonly auth = inject(Auth);

  constructor() {
    warmUpBackends();
  }

  protected readonly repositoryUrl = environment.repositoryUrl;
  protected readonly otherLocale = otherLocaleLink();
  protected readonly password = DEMO_PASSWORD;
  protected readonly pending = signal<string | null>(null);
  /** The saga a large transfer goes through, as the preview on the left shows it. */
  protected readonly previewSteps = (['Reserving', 'Screening', 'PendingReview'] as const).map(
    (status) => ({ label: TRANSFER_STATUS_LABELS[status] }),
  );

  protected readonly users: readonly DemoUser[] = [
    {
      username: 'alice',
      name: 'Alice Martin',
      role: $localize`:@@user.customer:Cliente`,
      summary: $localize`:@@user.alice:Compte courant et livret : essayez un gros virement, il part en revue.`,
    },
    {
      username: 'bob',
      name: 'Bob Durand',
      role: $localize`:@@user.customerM:Client`,
      summary: $localize`:@@user.bob:Un compte courant bien provisionné.`,
    },
    {
      username: 'chloe',
      name: 'Chloé Bernard',
      role: $localize`:@@user.customer:Cliente`,
      summary: $localize`:@@user.chloe:640 € seulement : un virement de 1 000 € est refusé, les fonds sont rendus.`,
    },
    {
      username: 'olivia',
      name: 'Olivia Roux',
      role: $localize`:@@user.operator:Analyste conformité`,
      summary: $localize`:@@user.olivia:Décide des virements suspects, consulte le journal et la balance.`,
    },
  ];

  protected readonly highlights: readonly { icon: IconName; title: string; text: string }[] = [
    {
      icon: 'book',
      title: $localize`:@@welcome.ledgerTitle:Comptabilité en partie double`,
      text: $localize`:@@welcome.ledgerText:Chaque euro vient de quelque part : la balance générale est toujours à zéro.`,
    },
    {
      icon: 'activity',
      title: $localize`:@@welcome.sagaTitle:Saga avec compensation`,
      text: $localize`:@@welcome.sagaText:Réservation, contrôle anti-fraude, exécution ou libération des fonds, à travers trois services.`,
    },
    {
      icon: 'key',
      title: $localize`:@@welcome.idempotencyTitle:Idempotence de bout en bout`,
      text: $localize`:@@welcome.idempotencyText:Double clic, timeout, rejeu réseau : un virement n'est jamais exécuté deux fois.`,
    },
    {
      icon: 'clock',
      title: $localize`:@@welcome.eventsTitle:Event sourcing`,
      text: $localize`:@@welcome.eventsText:Le solde à n'importe quelle date de valeur, rejoué depuis les événements.`,
    },
  ];

  protected readonly stack = [
    '.NET 10',
    'Marten',
    'Wolverine',
    'RabbitMQ',
    'PostgreSQL',
    'Keycloak',
    'YARP',
    'Aspire',
    'Angular 22',
    'SignalStore',
    'Kubernetes',
    'Terraform',
  ];

  protected enter(user: DemoUser): void {
    this.pending.set(user.username);
    void this.auth.login(user.username).catch(() => this.pending.set(null));
  }
}
