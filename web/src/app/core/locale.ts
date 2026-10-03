import { LOCALE_ID, inject } from '@angular/core';

export interface LocaleLink {
  readonly code: 'fr' | 'en';
  readonly label: string;
  readonly href: string;
}

/**
 * Each language is its own build served under /fr/ or /en/ (Angular i18n compiles translations in, no runtime cost).
 * Switching language therefore means loading the other build at the same route.
 */
export function otherLocaleLink(): LocaleLink {
  const current = inject(LOCALE_ID).startsWith('en') ? 'en' : 'fr';
  const target = current === 'fr' ? 'en' : 'fr';
  const path = globalThis.location?.pathname.replace(/^\/(fr|en)(?=\/|$)/, '') || '/';
  return {
    code: target,
    label: target === 'en' ? 'English' : 'Français',
    href: `/${target}${path === '/' ? '/' : path}`,
  };
}
