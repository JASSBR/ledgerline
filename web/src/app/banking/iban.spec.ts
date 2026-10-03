import { formatIban, isValidIban, normalizeIban } from './iban';

describe('IBAN', () => {
  it.each([
    'FR7699999000012000000000112',
    'fr76 9999 9000 0120 0000 0000 112',
    'DE89370400440532013000',
    'GB82WEST12345698765432',
  ])('accepts %s', (iban) => expect(isValidIban(iban)).toBe(true));

  it.each([
    ['a wrong check digit', 'FR7699999000012000000000113'],
    ['a typo in the account number', 'FR7699999000012000000000121'],
    ['a truncated value', 'FR7699999'],
    ['letters where digits belong', 'FRXX99999000012000000000112'],
    ['an empty value', ''],
  ])('rejects %s', (_, iban) => expect(isValidIban(iban)).toBe(false));

  it('normalizes spaces and case, and groups by four for display', () => {
    expect(normalizeIban(' fr76 9999 9000 0120 0000 0000 112 ')).toBe(
      'FR7699999000012000000000112',
    );
    expect(formatIban('FR7699999000012000000000112')).toBe('FR76 9999 9000 0120 0000 0000 112');
  });
});
