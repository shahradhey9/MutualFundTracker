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
            // UK
            if (ticker.EndsWith(".L",  StringComparison.OrdinalIgnoreCase)) return "GBP";   // LSE
            if (ticker.EndsWith(".IL", StringComparison.OrdinalIgnoreCase)) return "GBP";   // LSE (pence)
            // Euronext
            if (ticker.EndsWith(".PA", StringComparison.OrdinalIgnoreCase)) return "EUR";   // Euronext Paris
            if (ticker.EndsWith(".AS", StringComparison.OrdinalIgnoreCase)) return "EUR";   // Euronext Amsterdam
            if (ticker.EndsWith(".BR", StringComparison.OrdinalIgnoreCase)) return "EUR";   // Euronext Brussels
            if (ticker.EndsWith(".LS", StringComparison.OrdinalIgnoreCase)) return "EUR";   // Euronext Lisbon
            if (ticker.EndsWith(".IR", StringComparison.OrdinalIgnoreCase)) return "EUR";   // Euronext Dublin
            // Germany (Xetra + regional)
            if (ticker.EndsWith(".DE", StringComparison.OrdinalIgnoreCase)) return "EUR";   // Xetra (primary)
            if (ticker.EndsWith(".F",  StringComparison.OrdinalIgnoreCase)) return "EUR";   // Frankfurt
            if (ticker.EndsWith(".BE", StringComparison.OrdinalIgnoreCase)) return "EUR";   // Berlin
            if (ticker.EndsWith(".SG", StringComparison.OrdinalIgnoreCase)) return "EUR";   // Stuttgart
            if (ticker.EndsWith(".MU", StringComparison.OrdinalIgnoreCase)) return "EUR";   // Munich
            if (ticker.EndsWith(".DU", StringComparison.OrdinalIgnoreCase)) return "EUR";   // Düsseldorf
            if (ticker.EndsWith(".HA", StringComparison.OrdinalIgnoreCase)) return "EUR";   // Hannover
            if (ticker.EndsWith(".HM", StringComparison.OrdinalIgnoreCase)) return "EUR";   // Hamburg
            // Other EUR-zone
            if (ticker.EndsWith(".MI", StringComparison.OrdinalIgnoreCase)) return "EUR";   // Borsa Italiana
            if (ticker.EndsWith(".MC", StringComparison.OrdinalIgnoreCase)) return "EUR";   // BME Madrid
            if (ticker.EndsWith(".AT", StringComparison.OrdinalIgnoreCase)) return "EUR";   // Athens
            if (ticker.EndsWith(".HE", StringComparison.OrdinalIgnoreCase)) return "EUR";   // Nasdaq Helsinki
            if (ticker.EndsWith(".VI", StringComparison.OrdinalIgnoreCase)) return "EUR";   // Wiener Börse (Austria)
            if (ticker.EndsWith(".ZG", StringComparison.OrdinalIgnoreCase)) return "EUR";   // Zagreb (Croatia — EUR since 2023)
            if (ticker.EndsWith(".TL", StringComparison.OrdinalIgnoreCase)) return "EUR";   // Nasdaq Tallinn (Estonia)
            if (ticker.EndsWith(".RG", StringComparison.OrdinalIgnoreCase)) return "EUR";   // Nasdaq Riga (Latvia)
            if (ticker.EndsWith(".VS", StringComparison.OrdinalIgnoreCase)) return "EUR";   // Nasdaq Vilnius (Lithuania)
            // Non-EUR European
            if (ticker.EndsWith(".SW", StringComparison.OrdinalIgnoreCase)) return "CHF";   // SIX Swiss
            if (ticker.EndsWith(".OL", StringComparison.OrdinalIgnoreCase)) return "NOK";   // Oslo Børs
            if (ticker.EndsWith(".ST", StringComparison.OrdinalIgnoreCase)) return "SEK";   // Nasdaq Stockholm
            if (ticker.EndsWith(".CO", StringComparison.OrdinalIgnoreCase)) return "DKK";   // Nasdaq Copenhagen
            if (ticker.EndsWith(".IS", StringComparison.OrdinalIgnoreCase)) return "TRY";   // Borsa Istanbul
            if (ticker.EndsWith(".WA", StringComparison.OrdinalIgnoreCase)) return "PLN";   // WSE Warsaw
            if (ticker.EndsWith(".PR", StringComparison.OrdinalIgnoreCase)) return "CZK";   // Prague
            if (ticker.EndsWith(".BUD",StringComparison.OrdinalIgnoreCase)) return "HUF";   // Budapest
            if (ticker.EndsWith(".RO", StringComparison.OrdinalIgnoreCase)) return "RON";   // Bucharest
            if (ticker.EndsWith(".IC", StringComparison.OrdinalIgnoreCase)) return "ISK";   // Iceland (Nasdaq Iceland)

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
            if (ticker.EndsWith(".AX", StringComparison.OrdinalIgnoreCase)) return "AUD";   // ASX (Australia)
            if (ticker.EndsWith(".NZ", StringComparison.OrdinalIgnoreCase)) return "NZD";   // NZX (New Zealand)
            if (ticker.EndsWith(".T",  StringComparison.OrdinalIgnoreCase)) return "JPY";   // TSE Tokyo
            if (ticker.EndsWith(".SI", StringComparison.OrdinalIgnoreCase)) return "SGD";   // SGX
            if (ticker.EndsWith(".HK", StringComparison.OrdinalIgnoreCase)) return "HKD";   // HKEX
            if (ticker.EndsWith(".SS", StringComparison.OrdinalIgnoreCase)) return "CNY";   // Shanghai
            if (ticker.EndsWith(".SZ", StringComparison.OrdinalIgnoreCase)) return "CNY";   // Shenzhen
            if (ticker.EndsWith(".TW", StringComparison.OrdinalIgnoreCase)) return "TWD";   // TWSE
            if (ticker.EndsWith(".TWO",StringComparison.OrdinalIgnoreCase)) return "TWD";   // TPEX (OTC Taiwan)
            if (ticker.EndsWith(".KS", StringComparison.OrdinalIgnoreCase)) return "KRW";   // KRX Korea
            if (ticker.EndsWith(".KQ", StringComparison.OrdinalIgnoreCase)) return "KRW";   // KOSDAQ
            if (ticker.EndsWith(".BK", StringComparison.OrdinalIgnoreCase)) return "THB";   // SET Bangkok
            if (ticker.EndsWith(".KL", StringComparison.OrdinalIgnoreCase)) return "MYR";   // Bursa Malaysia
            if (ticker.EndsWith(".JK", StringComparison.OrdinalIgnoreCase)) return "IDR";   // IDX Jakarta
            if (ticker.EndsWith(".PS", StringComparison.OrdinalIgnoreCase)) return "PHP";   // PSE Philippines
            if (ticker.EndsWith(".VN", StringComparison.OrdinalIgnoreCase)) return "VND";   // HOSE Vietnam
            if (ticker.EndsWith(".KA", StringComparison.OrdinalIgnoreCase)) return "PKR";   // PSX Karachi (Pakistan)
            if (ticker.EndsWith(".CM", StringComparison.OrdinalIgnoreCase)) return "LKR";   // CSE Colombo (Sri Lanka)

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
        // US
        "NMS" or "NGM" or "NCM" or "NYQ" or "PCX" or "BTS" or "OTC" or "PINK" or "PNK" => "USD",
        // UK
        "LSE" or "IOB"                                              => "GBP",
        // Euronext
        "PA"  or "SBF"                                              => "EUR",   // Paris
        "AS"                                                        => "EUR",   // Amsterdam
        "BRU"                                                       => "EUR",   // Brussels
        "LIS" or "ALXL"                                             => "EUR",   // Lisbon
        "EIR" or "DUB"                                              => "EUR",   // Dublin
        // Germany
        "XETRA" or "GER" or "EBS" or "FRA" or "XFRA"              => "EUR",   // Xetra / Frankfurt
        "BER"  or "XBER"                                            => "EUR",   // Berlin
        "STU"  or "XSTU"                                            => "EUR",   // Stuttgart
        "MUN"  or "XMUN"                                            => "EUR",   // Munich
        "DUS"  or "XDUS"                                            => "EUR",   // Düsseldorf
        "HAN"  or "XHAN"                                            => "EUR",   // Hannover
        "HAM"  or "XHAM"                                            => "EUR",   // Hamburg
        // Other EUR-zone
        "MIL"  or "BIT"                                             => "EUR",   // Milan
        "MC"   or "MAD" or "BME"                                    => "EUR",   // Madrid
        "ATH"  or "ATHS"                                            => "EUR",   // Athens
        "VIE"  or "WBO" or "WBAG"                                   => "EUR",   // Vienna
        "ZAG"  or "ZSE"                                             => "EUR",   // Zagreb
        "TLN"  or "TLX"                                             => "EUR",   // Tallinn
        "RIX"                                                       => "EUR",   // Riga
        "VLN"                                                       => "EUR",   // Vilnius
        "HEL"                                                       => "EUR",   // Helsinki
        // Non-EUR European
        "SW"   or "SIX"                                             => "CHF",
        "OSL"  or "OB"                                              => "NOK",
        "STO"  or "XSTO"                                            => "SEK",
        "CPH"  or "XCPH"                                            => "DKK",
        "IST"  or "BIST"                                            => "TRY",
        "WAR"  or "WSE"                                             => "PLN",
        "PRA"  or "PSE"  or "XPRA"                                  => "CZK",
        "BUD"  or "BSE"  or "XBUD"                                  => "HUF",
        "BUH"  or "BVB"  or "XBSE"                                  => "RON",
        "ICE"  or "ICS"  or "XICE"                                  => "ISK",
        // Americas
        "TO"   or "TSX"  or "TSX-V" or "NEO"                        => "CAD",
        "SN"   or "SGO"                                              => "CLP",
        "MX"   or "BMV"                                              => "MXN",
        "SAO"  or "BVSP" or "B3"                                     => "BRL",
        "BA"   or "BCBA"                                             => "ARS",
        // Asia-Pacific
        "ASX"                                                        => "AUD",
        "NZE"  or "NZX"                                              => "NZD",
        "TYO"  or "JPX"  or "OSA"                                    => "JPY",
        "SES"                                                        => "SGD",
        "HKG"  or "HKSE" or "HKEX"                                   => "HKD",
        "SHH"  or "SHZ"                                              => "CNY",
        "TAI"  or "TWO"  or "TWSE"                                   => "TWD",
        "KSC"  or "KOE"  or "KRX"                                    => "KRW",
        "SET"  or "BKK"                                              => "THB",
        "KLSE" or "MYX"                                              => "MYR",
        "IDX"  or "JKT"                                              => "IDR",
        "PSX"  or "KAR"                                              => "PKR",
        "CSE"  or "CMB"                                              => "LKR",
        "HCM"  or "HNX"  or "HOSE"                                   => "VND",
        // India
        "NSE"  or "NSI"  or "BSE"  or "BOM"                          => "INR",
        // Middle East / Africa
        "TLV"  or "TASE"                                             => "ILS",
        "JSE"  or "JNB"                                              => "ZAR",
        "ADX"  or "ABU"                                              => "AED",   // Abu Dhabi
        "DFM"  or "DXB"                                              => "AED",   // Dubai
        "QSE"  or "DSM"                                              => "QAR",   // Qatar
        "EGX"  or "CAI"                                              => "EGP",   // Egypt
        _                                                            => null,
    };
}
