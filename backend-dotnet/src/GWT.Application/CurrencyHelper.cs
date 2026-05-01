namespace GWT.Application;

/// <summary>
/// Derives the ISO 4217 currency code for a fund from its ticker suffix or exchange timezone.
/// Yahoo Finance uses dot-separated exchange suffixes (e.g. INDIA.L → London → GBP).
/// AMFI tickers use the "AMFI-{schemeCode}" prefix → INR.
/// Falls back to USD for plain NYSE/NASDAQ tickers with no suffix.
/// </summary>
public static class CurrencyHelper
{
    public static string GetCurrency(string? ticker, string? timezone = null)
    {
        if (!string.IsNullOrEmpty(ticker))
        {
            // AMFI India funds
            if (ticker.StartsWith("AMFI-", StringComparison.OrdinalIgnoreCase)) return "INR";

            // Yahoo Finance exchange suffixes (case-insensitive)
            if (ticker.EndsWith(".L",  StringComparison.OrdinalIgnoreCase)) return "GBP";   // LSE
            if (ticker.EndsWith(".PA", StringComparison.OrdinalIgnoreCase)) return "EUR";   // Euronext Paris
            if (ticker.EndsWith(".AS", StringComparison.OrdinalIgnoreCase)) return "EUR";   // Euronext Amsterdam
            if (ticker.EndsWith(".DE", StringComparison.OrdinalIgnoreCase)) return "EUR";   // Xetra Frankfurt
            if (ticker.EndsWith(".MI", StringComparison.OrdinalIgnoreCase)) return "EUR";   // Borsa Italiana
            if (ticker.EndsWith(".BR", StringComparison.OrdinalIgnoreCase)) return "EUR";   // Euronext Brussels
            if (ticker.EndsWith(".SW", StringComparison.OrdinalIgnoreCase)) return "CHF";   // SIX Swiss
            if (ticker.EndsWith(".TO", StringComparison.OrdinalIgnoreCase)) return "CAD";   // TSX
            if (ticker.EndsWith(".V",  StringComparison.OrdinalIgnoreCase)) return "CAD";   // TSX Venture
            if (ticker.EndsWith(".AX", StringComparison.OrdinalIgnoreCase)) return "AUD";   // ASX
            if (ticker.EndsWith(".T",  StringComparison.OrdinalIgnoreCase)) return "JPY";   // TSE Tokyo
            if (ticker.EndsWith(".SI", StringComparison.OrdinalIgnoreCase)) return "SGD";   // SGX
            if (ticker.EndsWith(".HK", StringComparison.OrdinalIgnoreCase)) return "HKD";   // HKEX
            if (ticker.EndsWith(".BO", StringComparison.OrdinalIgnoreCase)) return "INR";   // BSE
            if (ticker.EndsWith(".NS", StringComparison.OrdinalIgnoreCase)) return "INR";   // NSE
        }

        // Fallback: derive from exchange timezone
        return timezone switch
        {
            "Asia/Kolkata"   => "INR",
            "Europe/London"  => "GBP",
            "Europe/Paris"   => "EUR",
            _                => "USD",
        };
    }
}
