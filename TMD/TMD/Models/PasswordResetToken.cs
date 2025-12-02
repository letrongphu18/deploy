using System;
using System.Collections.Generic;

namespace TMD.Models;

public partial class PasswordResetToken
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string TokenCode { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime ResendAvailableAt { get; set; }

    public int FailedAttempts { get; set; }

    public bool IsUsed { get; set; }

    public DateTime? LockoutUntil { get; set; }

    public virtual User User { get; set; } = null!;
}
