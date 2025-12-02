using System;
using System.Collections.Generic;

namespace TMD.Models;

public partial class Notification
{
    public int NotificationId { get; set; }

    public string Title { get; set; } = null!;

    public string? Message { get; set; }

    public string? Type { get; set; }

    public string? Link { get; set; }

    public DateTime CreatedAt { get; set; }

    public int? CreatedBy { get; set; }

    public int? TargetDepartmentId { get; set; }

    public bool IsBroadcast { get; set; }

    public virtual ICollection<UserNotification> UserNotifications { get; set; } = new List<UserNotification>();
}
