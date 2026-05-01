namespace GWT.Application;

/// <summary>
/// Derives the ISO 4217 currency code for a fund from its ticker suffix, Yahoo Finance
/// exchange code, or exchange timezone — in that priority order.
/// </summary>
public static class CurrencyHelper
{
    /// <param name="ticker">Yahoo Finance / AMFI ticker (e.g. "EWA.SN", "AMFI-152053").</param>
    /// <param name="timezone">IANA timezone of the exchange (optional fallback).</param>
    /// <param name="exchangeCode">Raw Yahoo Finance exchange code, e.g. "SN", "NMS" (optional second-tier fallback).</param>
    public static string GetCurrency(string? ticker, string? timezone = null, string? exchangeCode = null)
    {
        if (!string.IsNullOrEmpty(ticker))
        {
            // AMFI India funds
            if (ticker.StartsWith("AMFI-", StringComparison.OrdinalIgnoreCase)) return "INR";

            // ── Europe ───────────────────────────────────────────────────────
            if (ticker.EndsWith(".L",  StringComparison.OrdinalIgnoreCase)) return "GBP";   // LSE
            if (ticker.EndsWith(".IL", StringComparison.OrdinalIgnoreCase)) return "GBP";   // LSE (pence denominated)
            if (ticker.EndsWith(".PA", StringComparison.OrdinalIgnoreCase)) return "EUR";   // Euronext Paris
            if (ticker.EndsWith(".AS", StringComparison.OrdinalIgnoreCase)) return "EUR";   // Euronext Amsterdam
            if (ticker.EndsWith(".DE", StringComparison.OrdinalIgnoreCase)) return "EUR";   // Xetra Frankfurt
            if (ticker.EndsWith(".MI", StringComparison.OrdinalIgnoreCase)) return "EUR";   // Borsa Italiana
            if (ticker.EndsWith(".BR", StringComparison.OrdinalIgnoreCase)) return "EUR";   // Euronext Brussels
            if (ticker.EndsWith(".MC", StringComparison.OrdinalIgnoreCase)) return "EUR";   // BME Madrid
            if (ticker.EndsWith(".SW", StringComparison.OrdinalIgnoreCase)) return "CHF";   // SIX Swiss
            if (ticker.EndsWith(".LS", StringComparison.OrdinalIgnoreCase)) return "EUR";   // Euronext Lisbon
            if (ticker.EndsWith(".AT", StringComparison.OrdinalIgnoreCase)) return "EUR";   // Athens
            if (ticker.EndsWith(".OL", StringComparison.OrdinalIgnoreCase)) return "NOK";   // Oslo
            if (ticker.EndsWith(".ST", StringComparison.OrdinalIgnoreCase)) return "SEK";   // Stockholm
            if (ticker.EndsWith(".HE", StringComparison.OrdinalIgnoreCase)) return "EUR";   // Helsinki
            if (ticker.EndsWith(".CO", StringComparison.OrdinalIgnoreCase)) return "DKK";   // Copenhagen
            if (ticker.EndsWith(".IS", StringComparison.OrdinalIgnoreCase)) return "TRY";   // Istanbul
            if (ticker.EndsWith(".WA", StringComparison.OrdinalIgnoreCase)) return "PLN";   // Warsaw
            if (ticker.EndsWith(".PR", StringComparison.OrdinalIgnoreCase)) return "CZK";   // Prague

            // ── North America ─────────────────────────────────────────────────
            if (ticker.EndsWith(".TO", StringComparison.OrdinalIgnoreCase)) return "CAD";   // TSX
            if (ticker.EndsWith(".V",  StringComparison.OrdinalIgnoreCase)) return "CAD";   // TSX Venture
            if (ticker.EndsWith(".CN", StringComparison.OrdinalIgnoreCase)) return "CAD";   // CSE

            // ── Latin America ─────────────────────────────────────────────────
            if (ticker.EndsWith(".SN", StringComparison.OrdinalIgnoreCase)) return "CLP";   // Santiago (Chile)
            if (ticker.EndsWith(".MX", StringComparison.OrdinalIgnoreCase)) return "MXN";   // Mexico
            if (ticker.EndsWith(".SA", StringComparison.OrdinalIgnoreCase)) return "BRL";   // Bovespa (Brazil)
            if (ticker.EndsWith(".BA", StringComparison.OrdinalIgnoreCase)) return "ARS";   // Buenos Aires

            // ── Asia-Pacific ──────────────────────────────────────────────────
            if (ticker.EndsWith(".AX", StringComparison.OrdinalIgnoreCase)) return "AUD";   // ASX
            if (ticker.EndsWith(".NZ", StringComparison.OrdinalIgnoreCase)) return "NZD";   // NZX
            if (ticker.EndsWith(".T",  StringComparison.OrdinalIgnoreCase)) return "JPY";   // TSE Tokyo
            if (ticker.EndsWith(".SI", StringComparison.OrdinalIgnoreCase)) return "SGD";   // SGX
            if (ticker.EndsWith(".HK", StringComparison.OrdinalIgnoreCase)) return "HKD";   // HKEX
            if (ticker.EndsWith(".SS", StringComparison.OrdinalIgnoreCase)) return "CNY";   // Shanghai
            if (ticker.EndsWith(".SZ", StringComparison.OrdinalIgnoreCase)) return "CNY";   // Shenzhen
            if (ticker.EndsWith(".TW", StringComparison.OrdinalIgnoreCase)) return "TWD";   // TWSE
            if (ticker.EndsWith(".TWO",StringComparison.OrdinalIgnoreCase)) return "TWD";   // TPEX
            if (ticker.EndsWith(".KS", StringComparison.OrdinalIgnoreCase)) return "KRW";   // KRX Korea
            if (ticker.EndsWith(".KQ", StringComparison.OrdinalIgnoreCase)) return "KRW";   // KOSDAQ
            if (ticker.EndsWith(".BK", StringComparison.OrdinalIgnoreCase)) return "THB";   // Bangkok
            if (ticker.EndsWith(".KL", StringComparison.OrdinalIgnoreCase)) return "MYR";   // Kuala Lumpur
            if (ticker.EndsWith(".JK", StringComparison.OrdinalIgnoreCase)) return "IDR";   // Jakarta
            if (ticker.EndsWith(".PS", StringComparison.OrdinalIgnoreCase)) return "PHP";   // Philippine SE

            // ── India ─────────────────────────────────────────────────────────
            if (ticker.EndsWith(".BO", StringComparison.OrdinalIgnoreCase)) return "INR";   // BSE
            if (ticker.EndsWith(".NS", StringComparison.OrdinalIgnoreCase)) return "INR";   // NSE

            // ── Middle East / Africa ──────────────────────────────────────────
            if (ticker.EndsWith(".TA", StringComparison.OrdinalIgnoreCase)) return "ILS";   // Tel Aviv
            if (ticker.EndsWith(".JO", StringComparison.OrdinalIgnoreCase)) return "ZAR";   // Johannesburg
        }

        // ── Second tier: Yahoo Finance raw exchange code ──────────────────────
        // Used when the ticker has no recognisable suffix (e.g. bare "EWA" returned
        // for a Santiago-listed instrument with exchange="SN").
        if (!string.IsNullOrEmpty(exchangeCode))
        {
            var currency = ExchangeCodeToCurrency(exchangeCode);
            if (currency is not null) return currency;
        }

        // ── Third tier: exchange timezone ─────────────────────────────────────
        return timezone switch
        {
            "Asia/Kolkata"      => "INR",
            "Europe/London"     => "GBP",
            "Europe/Paris"      => "EUR",
            "Europe/Berlin"     => "EUR",
            "Europe/Amsterdam"  => "EUR",
            "America/Toronto"   => "CAD",
            "America/Vancouver" => "CAD",
            "Australia/Sydney"  => "AUD",
            "Asia/Tokyo"        => "JPY",
            "Asia/Singapore"    => "SGD",
            "Asia/Hong_Kong"    => "HKD",
            _                   => "USD",
        };
    }

    /// <summary>Maps Yahoo Finance raw exchange codes to ISO 4217 currency codes.</summary>
    private static string? ExchangeCodeToCurrency(string code) => code.ToUpperInvariant() switch
    {
        // US (no suffix in ticker)
        "NMS" or "NGM" or "NCM" or "NYQ" or "PCX" or "BTS" or "OTC" or "PINK" or "PNK" => "USD",
        // Europe
        "LSE" or "IOB"                    => "GBP",
        "PA"  or "SBF"                    => "EUR",   // Euronext Paris
        "AS"                              => "EUR",   // Euronext Amsterdam
        "XETRA" or "GER" or "EBS"        => "EUR",   // Xetra / Frankfurt
        "MIL"                             => "EUR",   // Milan
        "BRU"                             => "EUR",   // Brussels
        "MC"  or "MAD"                    => "EUR",   // Madrid
        "SW"                              => "CHF",
        "OSL"                             => "NOK",
        "STO"                             => "SEK",
        "CPH"                             => "DKK",
        "IST"                             => "TRY",
        // Americas
        "TO"  or "TSX" or "TSX-V"        => "CAD",
        "SN"  or "SGO"                    => "CLP",   // Santiago
        "MX"  or "BMV"                    => "MXN",
        "SAO" or "BVSP"                   => "BRL",
        "BA"  or "BCBA"                   => "ARS",
        // Asia-Pacific
        "ASX"                             => "AUD",
        "NZE"                             => "NZD",
        "TYO" or "JPX"                    => "JPY",
        "SES"                             => "SGD",
        "HKG" or "HKSE"                   => "HKD",
        "SHH" or "SHZ"                    => "CNY",
        "TAI" or "TWO"                    => "TWD",
        "KSC" or "KOE"                    => "KRW",
        "SET"                             => "THB",
        "KLSE"                            => "MYR",
        "IDX" or "JKT"                    => "IDR",
        "PSE"                             => "PHP",
        // India
        "BSE" or "NSE" or "NSI"           => "INR",
        // Middle East / Africa
        "TLV"                             => "ILS",
        "JSE"                             => "ZAR",
        _                                 => null,
    };
}
