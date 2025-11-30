using System;
using System.Collections.Generic;

namespace AIHUBOS.Models;

public partial class Department
{
    public int DepartmentId { get; set; }

    public string DepartmentName { get; set; } = null!;

    public string? Description { get; set; }

    public bool? IsActive { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public int? LeaderId { get; set; }

    public virtual User? Leader { get; set; }

    public virtual ICollection<Team> Teams { get; set; } = new List<Team>();

    public virtual ICollection<User> Users { get; set; } = new List<User>();

    public virtual ICollection<WorkScheduleException> WorkScheduleExceptions { get; set; } = new List<WorkScheduleException>();
}
