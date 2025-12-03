using System;
using System.Collections.Generic;

namespace TMD.Models;

public partial class VwProjectOverview
{
    public int ProjectId { get; set; }

    public string ProjectCode { get; set; } = null!;

    public string ProjectName { get; set; } = null!;

    public string Status { get; set; } = null!;

    public string? Priority { get; set; }

    public decimal? Progress { get; set; }

    public DateOnly? StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public int? LeaderId { get; set; }

    public string? LeaderName { get; set; }

    public string? DepartmentName { get; set; }

    public int? TotalMembers { get; set; }

    public int? TotalTasks { get; set; }

    public int? CompletedTaskAssignments { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
