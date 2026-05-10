namespace GWT.Domain.Entities;

public class Goal
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string GoalType { get; set; } = string.Empty;
    public decimal TargetAmount { get; set; }
    public DateTime EndDate { get; set; }
    public string Priority { get; set; } = "MEDIUM";
    public decimal InflationRate { get; set; }
    public int TargetDebtPct { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public ICollection<GoalFund> GoalFunds { get; set; } = [];
}
