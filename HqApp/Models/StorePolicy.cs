namespace HqApp.Models;

public class StorePolicy
{
    public int Id { get; set; }
    public string PolicyName { get; set; } = string.Empty;
    public decimal DiscountRate { get; set; }
    public TimeOnly OpeningHour { get; set; }
    public TimeOnly ClosingHour { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
