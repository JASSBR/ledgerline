import { ScreeningStatus, TransferStatus } from './models';

export const TRANSFER_STATUS_LABELS: Readonly<Record<TransferStatus, string>> = {
  Reserving: $localize`:@@status.reserving:Réservation des fonds`,
  Screening: $localize`:@@status.screening:Contrôle anti-fraude`,
  PendingReview: $localize`:@@status.pendingReview:En revue par un analyste`,
  Capturing: $localize`:@@status.capturing:Exécution`,
  Releasing: $localize`:@@status.releasing:Libération des fonds`,
  Completed: $localize`:@@status.completed:Exécuté`,
  Rejected: $localize`:@@status.rejected:Refusé`,
  Failed: $localize`:@@status.failed:Échoué`,
};

export const SCREENING_STATUS_LABELS: Readonly<Record<ScreeningStatus, string>> = {
  Cleared: $localize`:@@screening.cleared:Validé automatiquement`,
  Blocked: $localize`:@@screening.blocked:Bloqué par les règles`,
  PendingReview: $localize`:@@screening.pending:En attente de revue`,
  ApprovedByAnalyst: $localize`:@@screening.approved:Approuvé par un analyste`,
  RejectedByAnalyst: $localize`:@@screening.rejectedByAnalyst:Rejeté par un analyste`,
};

/** The happy path, for the progress stepper; compensation and failures branch off it. */
export const TRANSFER_PATH: readonly TransferStatus[] = [
  'Reserving',
  'Screening',
  'Capturing',
  'Completed',
];

/** Fraud rule ids are stable API values; their names are the UI's. */
export const RULE_LABELS: Readonly<Record<string, string>> = {
  'blocked-beneficiary': $localize`:@@rule.blocked:Bénéficiaire sur liste noire`,
  'single-transfer-limit': $localize`:@@rule.limit:Plafond par virement`,
  'large-amount': $localize`:@@rule.large:Montant élevé`,
  velocity: $localize`:@@rule.velocity:Rafale de virements`,
  'new-beneficiary': $localize`:@@rule.newBeneficiary:Nouveau bénéficiaire`,
};
