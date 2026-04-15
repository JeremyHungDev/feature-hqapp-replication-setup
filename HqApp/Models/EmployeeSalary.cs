namespace HqApp.Models;

public class EmployeeSalary
{
    public int Id { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public decimal MonthlySalary { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
