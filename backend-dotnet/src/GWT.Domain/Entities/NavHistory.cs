namespace GWT.Domain.Entities;

/// <summary>
/// Immutable daily NAV snapshot — one row per (fund, date).
/// Used for XIRR calculation and historical charting.
/// </summary>
public class NavHistory
{
    public Guid Id { get; set; }
    public string FundId { get; set; } = string.Empty;
    public decimal Nav { get; set; }
    public DateOnly NavDate { get; set; }

    public FundMeta Fund { get; set; } = null!;
}
