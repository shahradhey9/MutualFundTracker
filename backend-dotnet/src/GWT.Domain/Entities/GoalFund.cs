namespace GWT.Domain.Entities;

public class GoalFund
{
    public Guid Id { get; set; }
    public Guid GoalId { get; set; }
    public Guid HoldingId { get; set; }

    public Goal Goal { get; set; } = null!;
    public Holding Holding { get; set; } = null!;
}
