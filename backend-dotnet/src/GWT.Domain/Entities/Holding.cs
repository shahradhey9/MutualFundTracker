namespace GWT.Domain.Entities;

/// <summary>
/// A user's position in a single fund.
/// The unique constraint (UserId, FundId) enforces auto-consolidation at the DB level.
/// </summary>
public class Holding
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string FundId { get; set; } = string.Empty;
    public decimal Units { get; set; }
    public decimal? AvgCost { get; set; }
    public DateTime PurchaseAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public FundMeta Fund { get; set; } = null!;
}
