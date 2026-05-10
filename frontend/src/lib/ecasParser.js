import * as pdfjsLib from 'pdfjs-dist';
import workerUrl from 'pdfjs-dist/build/pdf.worker.min.mjs?url';

pdfjsLib.GlobalWorkerOptions.workerSrc = workerUrl;

function localToday() {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

// Extract text lines from all pages, preserving reading order via y-coordinate grouping
async function extractLines(pdf) {
  const lines = [];
  for (let pageNum = 1; pageNum <= pdf.numPages; pageNum++) {
    const page = await pdf.getPage(pageNum);
    const textContent = await page.getTextContent();

    const lineMap = new Map();
    for (const item of textContent.items) {
      if (!item.str?.trim()) continue;
      const y = Math.round(item.transform[5]);
      if (!lineMap.has(y)) lineMap.set(y, []);
      lineMap.get(y).push({ x: item.transform[4], text: item.str });
    }

    // PDF y-axis: higher value = higher on page → sort descending
    const sortedYs = [...lineMap.keys()].sort((a, b) => b - a);
    for (const y of sortedYs) {
      const text = lineMap.get(y)
        .sort((a, b) => a.x - b.x)
        .map(i => i.text)
        .join(' ')
        .trim();
      if (text) lines.push(text);
    }
  }
  return lines;
}

function parseEcasLines(lines) {
  const rows = [];
  const errors = [];
  const seen = new Set(); // deduplicate by ISIN

  let currentIsin = null;
  let currentFundName = null;

  for (let i = 0; i < lines.length; i++) {
    const line = lines[i];

    // Detect ISIN line — handles both "ISIN: INF..." and "ISIN : INF..."
    const isinMatch = line.match(/ISIN\s*:\s*([A-Z]{2}[A-Z0-9]{10})/i);
    if (isinMatch) {
      currentIsin = isinMatch[1].toUpperCase();
      currentFundName = null;

      // Fund name: look back up to 6 lines for first non-metadata line
      for (let j = i - 1; j >= Math.max(0, i - 6); j--) {
        const prev = lines[j].trim();
        if (!prev || prev.length < 6) continue;
        // Skip metadata / header lines
        if (/^(Folio|KYC|Nominee|Registrar|Opening|Market|Date|Sub-?Total|Total|AMC\s*:|Scheme\s*Code|ISIN)/i.test(prev)) continue;
        // Skip pure-number or separator lines
        if (/^[\d\s.,/()|\-]+$/.test(prev)) continue;
        if (/^[-=*_]{3,}/.test(prev)) continue;
        currentFundName = prev.replace(/\s+/g, ' ').trim();
        break;
      }
      continue;
    }

    if (!currentIsin || !currentFundName) continue;

    // NSDL format: "Closing Unit Balance : 237.555"
    const closingMatch = line.match(/Closing\s+Unit\s+Balance\s*[:\s]+([0-9,]+\.?[0-9]*)/i);
    if (closingMatch) {
      const units = parseFloat(closingMatch[1].replace(/,/g, ''));
      if (units > 0 && !seen.has(currentIsin)) {
        seen.add(currentIsin);

        // Look ahead up to 8 lines for avg cost
        let avgCost = null;
        for (let k = i + 1; k < Math.min(lines.length, i + 8); k++) {
          if (/ISIN\s*:/i.test(lines[k]) || /^Folio/i.test(lines[k])) break;
          const m = lines[k].match(/Avg\.?\s*Cost\s*:\s*(?:INR\s*|₹\s*|Rs\.?\s*)?([0-9,]+\.?[0-9]*)/i);
          if (m) { avgCost = parseFloat(m[1].replace(/,/g, '')) || null; break; }
        }

        const region = /^IN[F1E]/i.test(currentIsin) ? 'INDIA' : 'GLOBAL';
        rows.push({ name: currentFundName, isin: currentIsin, units, avgCost, date: localToday(), region });
      }
      currentIsin = null;
      currentFundName = null;
      continue;
    }

    // CDSL format: "UNITS : 237.555"
    const cdslMatch = line.match(/^UNITS?\s*:\s*([0-9,]+\.?[0-9]*)/i);
    if (cdslMatch && !seen.has(currentIsin)) {
      const units = parseFloat(cdslMatch[1].replace(/,/g, ''));
      if (units > 0) {
        seen.add(currentIsin);
        const region = /^IN[F1E]/i.test(currentIsin) ? 'INDIA' : 'GLOBAL';
        rows.push({ name: currentFundName, isin: currentIsin, units, avgCost: null, date: localToday(), region });
      }
      currentIsin = null;
      currentFundName = null;
    }
  }

  if (!rows.length) {
    errors.push('No active holdings found. Ensure this is a valid ECAS/CAS statement with active (non-zero) holdings.');
  }

  return { rows, errors, sourceType: 'ecas' };
}

export async function parseEcasPdf(file, password) {
  const arrayBuffer = await file.arrayBuffer();

  let pdf;
  try {
    pdf = await pdfjsLib.getDocument({ data: arrayBuffer, password: password || '' }).promise;
  } catch (err) {
    // Re-throw PasswordException as-is so caller can distinguish wrong-password from parse error
    throw err;
  }

  const lines = await extractLines(pdf);
  if (!lines.length) throw new Error('Could not extract text from this PDF.');
  return parseEcasLines(lines);
}
