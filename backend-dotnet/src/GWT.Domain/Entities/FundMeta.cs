using GWT.Domain.Enums;

namespace GWT.Domain.Entities;

/// <summary>
/// Fund catalogue entry — covers both Indian mutual funds (AMFI) and global ETFs (Yahoo Finance).
/// Id format: "IN-{schemeCode}" for India, "US-{ticker}" for Global.
/// </summary>
public class FundMeta
{
    public string Id { get; set; } = string.Empty;
    public Region Region { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Amc { get; set; } = string.Empty;
    public string Ticker { get; set; } = string.Empty;
    public string? SchemeCode { get; set; }
    public string? Isin { get; set; }
    public string? Category { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Denormalised latest NAV for fast portfolio reads (authoritative source is NavHistory)
    public decimal? LatestNav { get; set; }
    public DateTime? NavDate { get; set; }

    public ICollection<Holding> Holdings { get; set; } = new List<Holding>();
    public ICollection<NavHistory> NavHistories { get; set; } = new List<NavHistory>();
}
