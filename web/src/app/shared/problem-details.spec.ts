import { HttpErrorResponse } from '@angular/common/http';
import { problemMessages } from './problem-details';

const problem = (status: number, error: unknown) => new HttpErrorResponse({ status, error });

describe('problemMessages', () => {
  it('words validation errors from their code, in the UI language', () => {
    const error = problem(400, {
      errors: { 'payments.same_account': ['The beneficiary is the paying account.'] },
    });
    expect(problemMessages(error)).toEqual(['Le bénéficiaire est le compte débité.']);
  });

  it('words business conflicts from the code extension', () => {
    expect(problemMessages(problem(409, { code: 'fraud.already_decided', title: 'x' }))).toEqual([
      "Ce virement n'attend plus de revue : un autre analyste a déjà décidé.",
    ]);
  });

  it("falls back to the server's description for a code the UI does not know yet", () => {
    expect(problemMessages(problem(409, { code: 'ledger.new_rule', title: 'New rule.' }))).toEqual([
      'New rule.',
    ]);
    expect(problemMessages(problem(400, { errors: { 'ledger.other': ['Other rule.'] } }))).toEqual([
      'Other rule.',
    ]);
  });

  it('explains network failures, rate limiting and unknown errors', () => {
    expect(problemMessages(problem(0, null))[0]).toContain('injoignable');
    expect(problemMessages(problem(429, null))[0]).toContain('Trop de demandes');
    expect(problemMessages(problem(500, null))).toEqual(['Erreur 500.']);
    expect(problemMessages(new Error('boom'))).toEqual(['Erreur inattendue.']);
  });
});
