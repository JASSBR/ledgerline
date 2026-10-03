import { Routes } from '@angular/router';
import { customerGuard, guestGuard, operatorGuard, signedInGuard } from './core/auth/guards';

export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    title: 'Ledgerline — démo',
    canActivate: [guestGuard],
    loadComponent: () => import('./pages/welcome/welcome').then((m) => m.Welcome),
  },
  {
    path: 'callback',
    loadComponent: () => import('./pages/callback/callback').then((m) => m.Callback),
  },
  {
    path: '',
    canActivate: [signedInGuard],
    loadComponent: () => import('./layout/shell').then((m) => m.Shell),
    children: [
      {
        path: 'accounts',
        title: 'Comptes · Ledgerline',
        canActivate: [customerGuard],
        loadComponent: () => import('./pages/accounts/accounts').then((m) => m.Accounts),
      },
      {
        path: 'accounts/:id',
        title: 'Compte · Ledgerline',
        loadComponent: () =>
          import('./pages/account-detail/account-detail').then((m) => m.AccountDetail),
      },
      {
        path: 'movements',
        title: 'Mouvements · Ledgerline',
        loadComponent: () => import('./pages/movements/movements').then((m) => m.Movements),
      },
      {
        path: 'transfers/new',
        title: 'Nouveau virement · Ledgerline',
        canActivate: [customerGuard],
        loadComponent: () => import('./pages/transfer-new/transfer-new').then((m) => m.TransferNew),
      },
      {
        path: 'transfers',
        title: 'Virements · Ledgerline',
        loadComponent: () => import('./pages/transfers/transfers').then((m) => m.Transfers),
      },
      {
        path: 'transfers/:id',
        title: 'Virement · Ledgerline',
        loadComponent: () =>
          import('./pages/transfer-detail/transfer-detail').then((m) => m.TransferDetail),
      },
      {
        path: 'ops',
        canActivate: [operatorGuard],
        children: [
          {
            path: 'reviews',
            title: 'Revue anti-fraude · Ledgerline',
            loadComponent: () => import('./pages/reviews/reviews').then((m) => m.Reviews),
          },
          {
            path: 'journal',
            title: 'Journal · Ledgerline',
            loadComponent: () => import('./pages/journal/journal').then((m) => m.Journal),
          },
          {
            path: 'trial-balance',
            title: 'Balance générale · Ledgerline',
            loadComponent: () =>
              import('./pages/trial-balance/trial-balance').then((m) => m.TrialBalance),
          },
        ],
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
