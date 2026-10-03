/** Spaces and case are presentation: "fr76 9999 …" and "FR769999…" are the same IBAN. */
export function normalizeIban(value: string): string {
  return value.replace(/\s+/g, '').toUpperCase();
}

/** ISO 13616 check: move the country code and check digits to the end, letters to numbers, remainder mod 97 is 1. */
export function isValidIban(value: string): boolean {
  const iban = normalizeIban(value);
  if (!/^[A-Z]{2}\d{2}[A-Z0-9]{11,30}$/.test(iban)) return false;
  const rearranged = iban.slice(4) + iban.slice(0, 4);
  let remainder = 0;
  for (const char of rearranged) {
    const digits = /\d/.test(char) ? char : String(char.charCodeAt(0) - 55);
    for (const digit of digits) remainder = (remainder * 10 + Number(digit)) % 97;
  }
  return remainder === 1;
}

export function formatIban(value: string): string {
  return normalizeIban(value).replace(/(.{4})(?=.)/g, '$1 ');
}
