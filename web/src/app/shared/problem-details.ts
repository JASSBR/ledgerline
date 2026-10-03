import { HttpErrorResponse } from '@angular/common/http';

/** RFC 9457 problem details, as returned by every Ledgerline service. */
interface ProblemDetails {
  readonly title?: string;
  readonly detail?: string;
  readonly code?: string;
  readonly errors?: Readonly<Record<string, readonly string[]>>;
}

/**
 * The services speak stable error codes; the UI owns the wording, in the user's language.
 * An unknown code falls back to the server's (English) description, so a new rule is never silent.
 */
const MESSAGES: Readonly<Record<string, string>> = {
  'payments.amount_invalid': $localize`:@@error.amountInvalid:Le montant doit être positif, avec au plus deux décimales, et inférieur à 1 000 000 €.`,
  'payments.label_too_long': $localize`:@@error.labelTooLong:Le libellé est limité à 140 caractères (norme SEPA).`,
  'payments.idempotency_key_missing': $localize`:@@error.idempotencyMissing:Clé d'idempotence manquante.`,
  'payments.idempotency_key_reused': $localize`:@@error.idempotencyReused:Cette clé d'idempotence a déjà servi pour un autre virement.`,
  'payments.source_account_not_found': $localize`:@@error.sourceAccount:Ce compte à débiter n'existe pas parmi les vôtres.`,
  'payments.beneficiary_unknown': $localize`:@@error.beneficiaryUnknown:Aucun compte de la banque ne porte cet IBAN.`,
  'payments.same_account': $localize`:@@error.sameAccount:Le bénéficiaire est le compte débité.`,
  'payments.transfer_not_found': $localize`:@@error.transferNotFound:Ce virement n'existe pas.`,
  'ledger.amount_not_positive': $localize`:@@error.amountPositive:Le montant doit être strictement positif.`,
  'ledger.account_not_found': $localize`:@@error.accountNotFound:Ce compte n'existe pas.`,
  'ledger.freeze_reason_required': $localize`:@@error.freezeReason:Indiquez le motif du gel.`,
  'ledger.already_frozen': $localize`:@@error.alreadyFrozen:Ce compte est déjà gelé.`,
  'ledger.not_frozen': $localize`:@@error.notFrozen:Ce compte n'est pas gelé.`,
  'ledger.account_frozen': $localize`:@@error.accountFrozen:Ce compte est gelé : les paiements sortants sont bloqués.`,
  'fraud.screening_not_found': $localize`:@@error.screeningNotFound:Aucun contrôle n'existe pour ce virement.`,
  'fraud.already_decided': $localize`:@@error.alreadyDecided:Ce virement n'attend plus de revue : un autre analyste a déjà décidé.`,
  'fraud.comment_required': $localize`:@@error.commentRequired:Un commentaire est obligatoire pour rejeter un virement.`,
};

/** Flattens an API error into human-readable messages: every violated business rule, or the problem title. */
export function problemMessages(error: unknown): string[] {
  if (!(error instanceof HttpErrorResponse)) {
    return [$localize`:@@error.unexpected:Erreur inattendue.`];
  }
  if (error.status === 0) {
    return [$localize`:@@error.network:Le service est injoignable. Vérifiez votre connexion.`];
  }
  if (error.status === 429) {
    return [
      $localize`:@@error.rateLimited:Trop de demandes en peu de temps. Réessayez dans une minute.`,
    ];
  }
  const problem = (error.error ?? {}) as ProblemDetails;
  const fieldMessages = Object.entries(problem.errors ?? {}).flatMap(
    ([code, descriptions]) => MESSAGES[code] ?? descriptions,
  );
  if (fieldMessages.length > 0) {
    return fieldMessages;
  }
  return [
    (problem.code && MESSAGES[problem.code]) ||
      problem.detail ||
      problem.title ||
      $localize`:@@error.status:Erreur ${error.status}:status:.`,
  ];
}
