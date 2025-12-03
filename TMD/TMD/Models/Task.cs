using System;
using System.Collections.Generic;

namespace TMD.Models;

public partial class Task
{
    public int TaskId { get; set; }

    public string TaskName { get; set; } = null!;

    public string? Description { get; set; }

    public string? Platform { get; set; }

    public bool? IsActive { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? Deadline { get; set; }

    public string? Priority { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public int? ProjectId { get; set; }

    public string TaskType { get; set; } = null!;

    public int? ParentTaskId { get; set; }

    public int? OrderIndex { get; set; }

    public virtual ICollection<Task> InverseParentTask { get; set; } = new List<Task>();

    public virtual Task? ParentTask { get; set; }

    public virtual Project? Project { get; set; }

    public virtual ICollection<UserTask> UserTasks { get; set; } = new List<UserTask>();
}
