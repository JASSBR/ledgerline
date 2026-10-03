import { MovementLine } from './models';

const HEADER = ['date', 'account', 'reference', 'counterparty', 'amount'];

/**
 * Spreadsheet-ready export. French Excel expects ";" between fields and "," as decimal mark; with "," fields it puts
 * the whole line in one column. The BOM makes it read the file as UTF-8 (accents in names and labels).
 */
export function movementsCsv(lines: readonly MovementLine[], locale: string): string {
  const french = locale.startsWith('fr');
  const separator = french ? ';' : ',';
  const amount = (value: number) =>
    french ? value.toFixed(2).replace('.', ',') : value.toFixed(2);
  const rows = lines.map((line) => [
    line.valueDate.slice(0, 10),
    line.accountName,
    line.reference,
    line.counterparty,
    amount(line.amount),
  ]);
  return (
    '﻿' +
    [HEADER, ...rows]
      .map((row) => row.map((field) => quote(field, separator)).join(separator))
      .join('\r\n')
  );
}

function quote(field: string, separator: string): string {
  // Also neutralises spreadsheet formulas: a label starting with "=" must stay text (CSV injection).
  const safe = /^[=+\-@]/.test(field) && !/^-?\d/.test(field) ? `'${field}` : field;
  const needsQuotes = safe.includes(separator) || /["\r\n]/.test(safe);
  return needsQuotes ? `"${safe.replaceAll('"', '""')}"` : safe;
}
