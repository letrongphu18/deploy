using System;
using System.Collections.Generic;

namespace TMD.Models;

public partial class Kpihistory
{
    public int KpihistoryId { get; set; }

    public int UserId { get; set; }

    public DateOnly CalculationDate { get; set; }

    public DateOnly PeriodStart { get; set; }

    public DateOnly PeriodEnd { get; set; }

    public int? WorkDays { get; set; }

    public int? LateDays { get; set; }

    public decimal? LateRate { get; set; }

    public decimal? TotalHours { get; set; }

    public decimal? OvertimeHours { get; set; }

    public int? CompletedTasks { get; set; }

    public int? TotalTasks { get; set; }

    public decimal? TaskCompletionRate { get; set; }

    public decimal? Kpiscore { get; set; }

    public int? Rank { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual User User { get; set; } = null!;
}
