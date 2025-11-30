using System;
using System.Collections.Generic;

namespace AIHUBOS.Models;

public partial class VwKpidashboard
{
    public int UserId { get; set; }

    public string FullName { get; set; } = null!;

    public string? Email { get; set; }

    public string? DepartmentName { get; set; }

    public string? RoleName { get; set; }

    public int? ThisMonthWorkDays { get; set; }

    public int? ThisMonthLateDays { get; set; }

    public decimal? ThisMonthTotalHours { get; set; }

    public int? TotalTasks { get; set; }

    public int? CompletedTasks { get; set; }

    public int? InProgressTasks { get; set; }

    public bool? IsActive { get; set; }

    public DateTime? LastLoginAt { get; set; }
}
