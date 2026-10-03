import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { Transfer, TransferRequest } from './models';

/** Commands only: reads are httpResource declarations next to the views that render them. */
@Injectable({ providedIn: 'root' })
export class BankApi {
  private readonly http = inject(HttpClient);

  /**
   * The idempotency key is created once per form, not per click: a double-click, a retry after a timeout or a network
   * replay all reach the server with the same key and get the same transfer back.
   */
  requestTransfer(request: TransferRequest, idempotencyKey: string): Observable<Transfer> {
    return this.http.post<Transfer>('/api/payments/transfers', request, {
      headers: new HttpHeaders({ 'Idempotency-Key': idempotencyKey }),
    });
  }

  decide(transferId: string, approve: boolean, comment: string | null): Observable<void> {
    return this.http.post<void>(`/api/fraud/screenings/${transferId}/decision`, {
      approve,
      comment,
    });
  }

  deposit(accountId: string, amount: number, reference: string): Observable<void> {
    return this.http.post<void>(`/api/ledger/accounts/${accountId}/deposits`, {
      amount,
      reference,
    });
  }
}
