/**
 * csvParser.js
 *
 * Parses uploaded portfolio CSV files.
 * Handles various column name conventions used by brokers.
 *
 * Expected columns (flexible naming):
 *   Fund Name   → "fund name", "scheme name", "name", "fund", "security"
 *   Units       → "units", "quantity", "qty", "shares", "balance units"
 *   Buy Price   → "buy price", "avg cost", "average cost", "purchase price",
 *                 "cost basis", "nav", "avg nav", "purchase nav"
 *   Purchase Date → "date", "purchase date", "buy date", "transaction date"
 *   Region      → "region", "type" (optional — auto-detected if absent)
 */

// Aliases for each logical column
const ALIASES = {
  name: ['fund name', 'scheme name', 'fund', 'name', 'security name', 'security', 'scheme'],
  units: ['units', 'quantity', 'qty', 'shares', 'balance units', 'unit balance', 'holding units'],
  avgCost: [
    'buy price', 'avg cost', 'average cost', 'purchase price',
    'cost basis', 'avg nav', 'average nav', 'purchase nav',
    'nav', 'cost per unit', 'price',
  ],
  date: ['purchase date', 'buy date', 'date', 'transaction date', 'investment date'],
  region: ['region', 'type', 'market', 'exchange'],
};

function parseCSVLine(line) {
  const result = [];
  let current = '';
  let inQuotes = false;
  for (let i = 0; i < line.length; i++) {
    const ch = line[i];
    if (ch === '"') {
      inQuotes = !inQuotes;
    } else if (ch === ',' && !inQuotes) {
      result.push(current.trim());
      current = '';
    } else {
      current += ch;
    }
  }
  result.push(current.trim());
  return result;
}

function findColumn(headers, aliases) {
  const lower = headers.map(h => h.toLowerCase().trim());
  for (const alias of aliases) {
    const idx = lower.indexOf(alias);
    if (idx !== -1) return idx;
  }
  // Partial match fallback
  for (const alias of aliases) {
    const idx = lower.findIndex(h => h.includes(alias) || alias.includes(h));
    if (idx !== -1) return idx;
  }
  return -1;
}

function localToday() {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

function parseDate(raw) {
  if (!raw) return localToday();
  // Try common formats: DD/MM/YYYY, MM/DD/YYYY, YYYY-MM-DD, DD-MM-YYYY
  const cleaned = raw.trim();

  // Already ISO
  if (/^\d{4}-\d{2}-\d{2}$/.test(cleaned)) return cleaned;

  // DD/MM/YYYY or DD-MM-YYYY
  const dmy = cleaned.match(/^(\d{1,2})[\/\-](\d{1,2})[\/\-](\d{4})$/);
  if (dmy) {
    const [, d, m, y] = dmy;
    return `${y}-${m.padStart(2,'0')}-${d.padStart(2,'0')}`;
  }

  // MM/DD/YYYY
  const mdy = cleaned.match(/^(\d{1,2})\/(\d{1,2})\/(\d{2,4})$/);
  if (mdy) {
    const [, m, d, y] = mdy;
    const year = y.length === 2 ? '20' + y : y;
    return `${year}-${m.padStart(2,'0')}-${d.padStart(2,'0')}`;
  }

  // Try native Date parse as last resort — use local date parts to avoid UTC rollover
  const parsed = new Date(cleaned);
  if (!isNaN(parsed)) {
    return `${parsed.getFullYear()}-${String(parsed.getMonth() + 1).padStart(2, '0')}-${String(parsed.getDate()).padStart(2, '0')}`;
  }

  return localToday();
}

function guessRegion(name) {
  const n = name.toLowerCase();
  // Common Indian fund keywords
  if (/\b(fund|flexi|elss|bluechip|midcap|smallcap|nifty|sensex|gilt|liquid|arbitrage|balanced|hybrid|sbi|hdfc|icici|axis|kotak|mirae|nippon|parag|quant|uti|dsp|tata|franklin|canara|edelweiss|motilal|aditya)\b/.test(n)) {
    return 'INDIA';
  }
  // Common global ticker patterns or keywords
  if (/\b(etf|voo|qqq|spy|vti|iwm|schd|vgt|arkk|ishares|vanguard|spdr|schwab|fidelity|invesco)\b/.test(n)) {
    return 'GLOBAL';
  }
  return 'INDIA'; // default — most users adding via CSV are tracking Indian MFs
}

export function parseCSV(text) {
  const lines = text.split('\n').map(l => l.trim()).filter(Boolean);
  if (lines.length < 2) throw new Error('CSV must have a header row and at least one data row');

  const headers = parseCSVLine(lines[0]);

  // Find column indices
  const cols = {};
  for (const [key, aliases] of Object.entries(ALIASES)) {
    cols[key] = findColumn(headers, aliases);
  }

  if (cols.name === -1) {
    throw new Error('Could not find a "Fund Name" column. Please ensure your CSV has a column named "Fund Name", "Scheme Name", or "Name".');
  }
  if (cols.units === -1) {
    throw new Error('Could not find a "Units" column. Please ensure your CSV has a column named "Units", "Quantity", or "Qty".');
  }

  const rows = [];
  const errors = [];

  for (let i = 1; i < lines.length; i++) {
    const cells = parseCSVLine(lines[i]);
    if (cells.every(c => !c)) continue; // skip blank rows

    const name = cells[cols.name]?.trim();
    if (!name) continue;

    const unitsRaw = cols.units !== -1 ? cells[cols.units]?.replace(/,/g, '') : '';
    const units = parseFloat(unitsRaw);
    if (isNaN(units) || units <= 0) {
      errors.push(`Row ${i + 1}: invalid units "${unitsRaw}" for "${name}" — skipped`);
      continue;
    }

    const avgCostRaw = cols.avgCost !== -1 ? cells[cols.avgCost]?.replace(/,/g, '').replace(/[₹$£]/g, '') : '';
    const avgCost = parseFloat(avgCostRaw) || null;

    const dateRaw = cols.date !== -1 ? cells[cols.date] : '';
    const date = parseDate(dateRaw);

    const regionRaw = cols.region !== -1 ? cells[cols.region]?.trim().toUpperCase() : '';
    const region = ['INDIA', 'GLOBAL'].includes(regionRaw) ? regionRaw : guessRegion(name);

    rows.push({ name, units, avgCost, date, region, rowIndex: i + 1 });
  }

  if (!rows.length) throw new Error('No valid rows found in the CSV.');

  return { rows, errors, headers, colMap: cols };
}

export const SAMPLE_CSV = `Fund Name,Units,Buy Price,Purchase Date,Region
Parag Parikh Flexi Cap Fund Direct Growth,150.000,68.50,15/01/2023,INDIA
HDFC Mid-Cap Opportunities Fund Direct Growth,80.000,95.20,10/03/2022,INDIA
Axis Small Cap Fund Direct Growth,200.000,45.30,20/06/2023,INDIA
Vanguard S&P 500 ETF,12.000,380.00,05/02/2023,GLOBAL
Invesco QQQ Trust,8.000,360.00,12/08/2023,GLOBAL`;
