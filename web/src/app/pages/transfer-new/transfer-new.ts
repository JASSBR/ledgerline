import { CurrencyPipe } from '@angular/common';
import { httpResource } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
  untracked,
} from '@angular/core';
import {
  FormField,
  form,
  max,
  maxLength,
  min,
  required,
  submit,
  validate,
} from '@angular/forms/signals';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { BankApi } from '../../banking/api';
import { DEMO_BENEFICIARIES, DemoBeneficiary } from '../../banking/demo-beneficiaries';
import { formatIban, isValidIban, normalizeIban } from '../../banking/iban';
import { Account, IbanLookup } from '../../banking/models';
import { TransfersStore } from '../../banking/transfers.store';
import { Auth } from '../../core/auth/auth';
import { Icon } from '../../shared/icon';
import { problemMessages } from '../../shared/problem-details';

interface TransferModel {
  fromAccountId: string;
  toIban: string;
  amount: number;
  label: string;
}

const MAX_AMOUNT = 999_999.99;
const MAX_LABEL = 140;

@Component({
  selector: 'app-transfer-new',
  imports: [CurrencyPipe, FormField, RouterLink, Icon],
  templateUrl: './transfer-new.html',
  styleUrl: './transfer-new.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TransferNew {
  private readonly api = inject(BankApi);
  private readonly auth = inject(Auth);
  private readonly router = inject(Router);
  private readonly store = inject(TransfersStore);

  protected readonly accounts = httpResource<Account[]>(() => '/api/ledger/accounts', {
    defaultValue: [],
  });

  protected readonly model = signal<TransferModel>({
    fromAccountId: '',
    toIban: '',
    amount: 0,
    label: '',
  });

  /**
   * One key per transfer being written, not per click: a double submit or a retry after a timeout is recognised by
   * the server and returns the transfer it already accepted. A new key is drawn only once the form is reset.
   */
  protected readonly idempotencyKey = signal(crypto.randomUUID());
  protected readonly serverErrors = signal<string[]>([]);

  protected readonly source = computed(() =>
    this.accounts.value().find((account) => account.id === this.model().fromAccountId),
  );
  protected readonly beneficiaries = computed(() =>
    DEMO_BENEFICIARIES.filter((beneficiary) => beneficiary.iban !== this.source()?.iban),
  );

  // Verification of payee: the bank tells the payer whose account an IBAN is before any money moves.
  protected readonly payee = httpResource<IbanLookup>(() => {
    const iban = normalizeIban(this.model().toIban);
    return isValidIban(iban) ? { url: '/api/ledger/accounts/lookup', params: { iban } } : undefined;
  });

  protected readonly form = form(this.model, (path) => {
    required(path.fromAccountId, {
      message: $localize`:@@transfer.fromRequired:Choisissez le compte à débiter.`,
    });
    required(path.toIban, {
      message: $localize`:@@transfer.ibanRequired:L'IBAN du bénéficiaire est obligatoire.`,
    });
    validate(path.toIban, ({ value }) =>
      value() && !isValidIban(value())
        ? {
            kind: 'iban',
            message: $localize`:@@transfer.ibanInvalid:IBAN invalide (clé de contrôle incorrecte).`,
          }
        : null,
    );
    min(path.amount, 0.01, {
      message: $localize`:@@transfer.amountPositive:Le montant doit être positif.`,
    });
    max(path.amount, MAX_AMOUNT, {
      message: $localize`:@@transfer.amountMax:Montant maximum : 999 999,99 €.`,
    });
    validate(path.amount, ({ value }) =>
      Math.round(value() * 100) !== value() * 100
        ? { kind: 'cents', message: $localize`:@@transfer.amountCents:Au plus deux décimales.` }
        : null,
    );
    maxLength(path.label, MAX_LABEL, {
      message: $localize`:@@transfer.labelMax:140 caractères maximum (norme SEPA).`,
    });
  });

  constructor() {
    // Pre-select the first account once the list arrives (unless the user already chose).
    effect(() => {
      const first = this.accounts.value()[0];
      if (first && !untracked(this.model).fromAccountId) {
        this.model.update((model) => ({ ...model, fromAccountId: first.id }));
      }
    });
  }

  protected pick(beneficiary: DemoBeneficiary): void {
    this.model.update((model) => ({ ...model, toIban: formatIban(beneficiary.iban) }));
  }

  protected isOwn(beneficiary: DemoBeneficiary): boolean {
    return beneficiary.ownerUsername === this.auth.user()?.username;
  }

  protected async onSubmit(event: Event): Promise<void> {
    event.preventDefault();
    this.serverErrors.set([]);
    await submit(this.form, async () => {
      const { fromAccountId, toIban, amount, label } = this.model();
      try {
        const transfer = await firstValueFrom(
          this.api.requestTransfer(
            { fromAccountId, toIban: normalizeIban(toIban), amount, label: label.trim() || null },
            this.idempotencyKey(),
          ),
        );
        this.store.upsert(transfer);
        await this.router.navigate(['/transfers', transfer.id]);
      } catch (error) {
        this.serverErrors.set(problemMessages(error));
      }
      return undefined;
    });
  }
}
