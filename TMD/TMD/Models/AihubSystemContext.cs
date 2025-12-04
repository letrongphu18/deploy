using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace TMD.Models;

public partial class AihubSystemContext : DbContext
{
    public AihubSystemContext()
    {
    }

    public AihubSystemContext(DbContextOptions<AihubSystemContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Attendance> Attendances { get; set; }

    public virtual DbSet<AuditLog> AuditLogs { get; set; }

    public virtual DbSet<ChatMessage> ChatMessages { get; set; }

    public virtual DbSet<Conversation> Conversations { get; set; }

    public virtual DbSet<Department> Departments { get; set; }

    public virtual DbSet<Kpihistory> Kpihistories { get; set; }

    public virtual DbSet<LateRequest> LateRequests { get; set; }

    public virtual DbSet<LeaveRequest> LeaveRequests { get; set; }

    public virtual DbSet<LoginHistory> LoginHistories { get; set; }

    public virtual DbSet<Notification> Notifications { get; set; }

    public virtual DbSet<OvertimeRequest> OvertimeRequests { get; set; }

    public virtual DbSet<PasswordResetHistory> PasswordResetHistories { get; set; }

    public virtual DbSet<PasswordResetOtp> PasswordResetOtps { get; set; }

    public virtual DbSet<PasswordResetToken> PasswordResetTokens { get; set; }

    public virtual DbSet<PasswordResetToken1> PasswordResetTokens1 { get; set; }

    public virtual DbSet<Project> Projects { get; set; }

    public virtual DbSet<ProjectMember> ProjectMembers { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<SalaryAdjustment> SalaryAdjustments { get; set; }

    public virtual DbSet<SalaryConfigCategory> SalaryConfigCategories { get; set; }

    public virtual DbSet<SalaryConfigHistory> SalaryConfigHistories { get; set; }

    public virtual DbSet<SalaryConfiguration> SalaryConfigurations { get; set; }

    public virtual DbSet<SystemSetting> SystemSettings { get; set; }

    public virtual DbSet<SystemSettingsBackup20251123> SystemSettingsBackup20251123s { get; set; }

    public virtual DbSet<Task> Tasks { get; set; }

    public virtual DbSet<Team> Teams { get; set; }

    public virtual DbSet<TeamMember> TeamMembers { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserNotification> UserNotifications { get; set; }

    public virtual DbSet<UserSalarySetting> UserSalarySettings { get; set; }

    public virtual DbSet<UserTask> UserTasks { get; set; }

    public virtual DbSet<VwActiveSalarySetting> VwActiveSalarySettings { get; set; }

    public virtual DbSet<VwKpidashboard> VwKpidashboards { get; set; }

    public virtual DbSet<VwPendingRequestsSummary> VwPendingRequestsSummaries { get; set; }

    public virtual DbSet<VwProjectOverview> VwProjectOverviews { get; set; }

    public virtual DbSet<VwSalaryCalculation> VwSalaryCalculations { get; set; }

    public virtual DbSet<VwTodayAttendance> VwTodayAttendances { get; set; }

    public virtual DbSet<VwUserDetail> VwUserDetails { get; set; }

    public virtual DbSet<WorkScheduleException> WorkScheduleExceptions { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseSqlServer("Server=tmd-server-system.database.windows.net;Database=aihub_system;User Id=tmd_admin;Password=MyP@ssword123!;TrustServerCertificate=False;Encrypt=True;MultipleActiveResultSets=True;");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Attendance>(entity =>
        {
            entity.HasKey(e => e.AttendanceId).HasName("PK__Attendan__8B69261C3E6D9DC0");

            entity.HasIndex(e => new { e.UserId, e.WorkDate }, "IX_Attendances_UserId_WorkDate");

            entity.HasIndex(e => new { e.WorkDate, e.IsLate }, "IX_Attendances_WorkDate_IsLate");

            entity.Property(e => e.ActualWorkHours).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.ApprovedOvertimeHours).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.CheckInAddress).HasMaxLength(500);
            entity.Property(e => e.CheckInIpaddress)
                .HasMaxLength(50)
                .HasColumnName("CheckInIPAddress");
            entity.Property(e => e.CheckInLatitude).HasColumnType("decimal(10, 8)");
            entity.Property(e => e.CheckInLongitude).HasColumnType("decimal(11, 8)");
            entity.Property(e => e.CheckInNotes).HasMaxLength(1000);
            entity.Property(e => e.CheckOutAddress).HasMaxLength(500);
            entity.Property(e => e.CheckOutIpaddress)
                .HasMaxLength(50)
                .HasColumnName("CheckOutIPAddress");
            entity.Property(e => e.CheckOutLatitude).HasColumnType("decimal(10, 8)");
            entity.Property(e => e.CheckOutLongitude).HasColumnType("decimal(11, 8)");
            entity.Property(e => e.CheckOutNotes).HasMaxLength(1000);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.DeductionAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.DeductionHours).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.IsLate).HasDefaultValue(false);
            entity.Property(e => e.IsWithinGeofence).HasDefaultValue(true);
            entity.Property(e => e.SalaryMultiplier)
                .HasDefaultValue(1.0m)
                .HasColumnType("decimal(5, 2)");
            entity.Property(e => e.StandardWorkHours)
                .HasDefaultValue(8m)
                .HasColumnType("decimal(5, 2)");
            entity.Property(e => e.TotalHours).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime");

            entity.HasOne(d => d.LateRequest).WithMany(p => p.Attendances)
                .HasForeignKey(d => d.LateRequestId)
                .HasConstraintName("FK_Attendances_LateRequest");

            entity.HasOne(d => d.OvertimeRequest).WithMany(p => p.Attendances)
                .HasForeignKey(d => d.OvertimeRequestId)
                .HasConstraintName("FK_Attendances_OvertimeRequest");

            entity.HasOne(d => d.ScheduleException).WithMany(p => p.Attendances)
                .HasForeignKey(d => d.ScheduleExceptionId)
                .HasConstraintName("FK_Attendances_ScheduleException");

            entity.HasOne(d => d.User).WithMany(p => p.Attendances)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK__Attendanc__UserI__00DF2177");
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.AuditLogId).HasName("PK__AuditLog__EB5F6CBDB3E108F7");

            entity.HasIndex(e => new { e.UserId, e.Timestamp }, "IX_AuditLogs_UserId_Timestamp");

            entity.Property(e => e.Action).HasMaxLength(50);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.EntityName).HasMaxLength(100);
            entity.Property(e => e.Ipaddress)
                .HasMaxLength(50)
                .HasColumnName("IPAddress");
            entity.Property(e => e.Location).HasMaxLength(200);
            entity.Property(e => e.Timestamp).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.UserAgent).HasMaxLength(500);

            entity.HasOne(d => d.User).WithMany(p => p.AuditLogs)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK__AuditLogs__UserI__02C769E9");
        });

        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.HasKey(e => e.MessageId).HasName("PK__ChatMess__C87C0C9C2856B10C");

            entity.ToTable(tb => tb.HasTrigger("trg_UpdateLastMessageAt"));

            entity.HasIndex(e => e.ConversationId, "IX_ChatMessages_ConversationId");

            entity.HasIndex(e => e.IsRead, "IX_ChatMessages_IsRead").HasFilter("([IsRead]=(0))");

            entity.HasIndex(e => e.SenderId, "IX_ChatMessages_SenderId");

            entity.HasIndex(e => e.SentAt, "IX_ChatMessages_SentAt").IsDescending();

            entity.Property(e => e.AttachmentType).HasMaxLength(50);
            entity.Property(e => e.AttachmentUrl).HasMaxLength(500);
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.IsRead).HasDefaultValue(false);
            entity.Property(e => e.MessageContent).HasMaxLength(4000);
            entity.Property(e => e.ReadAt).HasColumnType("datetime");
            entity.Property(e => e.SentAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.Conversation).WithMany(p => p.ChatMessages)
                .HasForeignKey(d => d.ConversationId)
                .HasConstraintName("FK_ChatMessages_Conversation");

            entity.HasOne(d => d.Sender).WithMany(p => p.ChatMessages)
                .HasForeignKey(d => d.SenderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ChatMessages_Sender");
        });

        modelBuilder.Entity<Conversation>(entity =>
        {
            entity.HasKey(e => e.ConversationId).HasName("PK__Conversa__C050D87758B9A498");

            entity.HasIndex(e => e.LastMessageAt, "IX_Conversations_LastMessageAt").IsDescending();

            entity.HasIndex(e => e.User1Id, "IX_Conversations_User1Id");

            entity.HasIndex(e => e.User2Id, "IX_Conversations_User2Id");

            entity.HasIndex(e => new { e.User1Id, e.User2Id }, "UQ_Conversations").IsUnique();

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.IsArchived).HasDefaultValue(false);
            entity.Property(e => e.LastMessageAt).HasColumnType("datetime");

            entity.HasOne(d => d.User1).WithMany(p => p.ConversationUser1s)
                .HasForeignKey(d => d.User1Id)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Conversations_User1");

            entity.HasOne(d => d.User2).WithMany(p => p.ConversationUser2s)
                .HasForeignKey(d => d.User2Id)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Conversations_User2");
        });

        modelBuilder.Entity<Department>(entity =>
        {
            entity.HasKey(e => e.DepartmentId).HasName("PK__Departme__B2079BED48851AA0");

            entity.HasIndex(e => e.LeaderId, "IX_Department_LeaderId").HasFilter("([LeaderId] IS NOT NULL)");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.DepartmentName).HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.IsActive).HasDefaultValue(true);

            entity.HasOne(d => d.Leader).WithMany(p => p.Departments)
                .HasForeignKey(d => d.LeaderId)
                .HasConstraintName("FK_Department_Leader");
        });

        modelBuilder.Entity<Kpihistory>(entity =>
        {
            entity.HasKey(e => e.KpihistoryId).HasName("PK__KPIHisto__846C1BB3D5FB2AE8");

            entity.ToTable("KPIHistory");

            entity.HasIndex(e => new { e.UserId, e.CalculationDate }, "IX_KPIHistory_UserId_CalculationDate");

            entity.Property(e => e.KpihistoryId).HasColumnName("KPIHistoryId");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Kpiscore)
                .HasColumnType("decimal(5, 2)")
                .HasColumnName("KPIScore");
            entity.Property(e => e.LateRate).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.OvertimeHours).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.TaskCompletionRate).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.TotalHours).HasColumnType("decimal(10, 2)");

            entity.HasOne(d => d.User).WithMany(p => p.Kpihistories)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_KPIHistory_Users");
        });

        modelBuilder.Entity<LateRequest>(entity =>
        {
            entity.HasKey(e => e.LateRequestId).HasName("PK__LateRequ__6814EE07A29D6B21");

            entity.HasIndex(e => e.Status, "IX_LateRequests_Status");

            entity.HasIndex(e => new { e.UserId, e.RequestDate }, "IX_LateRequests_UserId_RequestDate");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.ProofDocument).HasMaxLength(500);
            entity.Property(e => e.Reason).HasMaxLength(1000);
            entity.Property(e => e.ReviewNote).HasMaxLength(500);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("Pending");

            entity.HasOne(d => d.ReviewedByNavigation).WithMany(p => p.LateRequestReviewedByNavigations)
                .HasForeignKey(d => d.ReviewedBy)
                .HasConstraintName("FK_LateRequests_Reviewer");

            entity.HasOne(d => d.User).WithMany(p => p.LateRequestUsers)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_LateRequests_User");
        });

        modelBuilder.Entity<LeaveRequest>(entity =>
        {
            entity.HasKey(e => e.LeaveRequestId).HasName("PK__LeaveReq__609421EE960EC52E");

            entity.HasIndex(e => e.StartDate, "IX_LeaveRequests_StartDate");

            entity.HasIndex(e => new { e.UserId, e.Status }, "IX_LeaveRequests_UserId_Status");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.LeaveType).HasMaxLength(50);
            entity.Property(e => e.ProofDocument).HasMaxLength(500);
            entity.Property(e => e.Reason).HasMaxLength(1000);
            entity.Property(e => e.ReviewNote).HasMaxLength(500);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("Pending");
            entity.Property(e => e.TotalDays).HasColumnType("decimal(5, 2)");

            entity.HasOne(d => d.ReviewedByNavigation).WithMany(p => p.LeaveRequestReviewedByNavigations)
                .HasForeignKey(d => d.ReviewedBy)
                .HasConstraintName("FK_LeaveRequests_Reviewer");

            entity.HasOne(d => d.User).WithMany(p => p.LeaveRequestUsers)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_LeaveRequests_User");
        });

        modelBuilder.Entity<LoginHistory>(entity =>
        {
            entity.HasKey(e => e.LoginHistoryId).HasName("PK__LoginHis__2773EA9FE59C58D8");

            entity.ToTable("LoginHistory");

            entity.HasIndex(e => e.UserId, "IX_LoginHistory_UserId");

            entity.Property(e => e.Browser).HasMaxLength(100);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Device).HasMaxLength(100);
            entity.Property(e => e.FailReason).HasMaxLength(200);
            entity.Property(e => e.Ipaddress)
                .HasMaxLength(50)
                .HasColumnName("IPAddress");
            entity.Property(e => e.IsSuccess).HasDefaultValue(true);
            entity.Property(e => e.Location).HasMaxLength(200);
            entity.Property(e => e.UserAgent).HasMaxLength(500);
            entity.Property(e => e.Username).HasMaxLength(50);

            entity.HasOne(d => d.User).WithMany(p => p.LoginHistories)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK__LoginHist__UserI__09746778");
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.NotificationId).HasName("PK__Notifica__20CF2E12229B0369");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Link).HasMaxLength(500);
            entity.Property(e => e.Message).HasMaxLength(1000);
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.Type).HasMaxLength(50);
        });

        modelBuilder.Entity<OvertimeRequest>(entity =>
        {
            entity.HasKey(e => e.OvertimeRequestId).HasName("PK__Overtime__F97D0DCAB46A25AB");

            entity.HasIndex(e => e.ExpiryDate, "IX_OvertimeRequests_ExpiryDate");

            entity.HasIndex(e => new { e.IsExpired, e.Status }, "IX_OvertimeRequests_IsExpired_Status");

            entity.HasIndex(e => new { e.UserId, e.WorkDate }, "IX_OvertimeRequests_UserId_WorkDate");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsExpired).HasDefaultValue(false);
            entity.Property(e => e.OvertimeHours).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.Reason).HasMaxLength(1000);
            entity.Property(e => e.ReviewNote).HasMaxLength(500);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("Pending");
            entity.Property(e => e.TaskDescription).HasMaxLength(1000);

            entity.HasOne(d => d.ReviewedByNavigation).WithMany(p => p.OvertimeRequestReviewedByNavigations)
                .HasForeignKey(d => d.ReviewedBy)
                .HasConstraintName("FK_OvertimeRequests_Reviewer");

            entity.HasOne(d => d.User).WithMany(p => p.OvertimeRequestUsers)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_OvertimeRequests_User");
        });

        modelBuilder.Entity<PasswordResetHistory>(entity =>
        {
            entity.HasKey(e => e.ResetId).HasName("PK__Password__783CF04DE6257ED0");

            entity.ToTable("PasswordResetHistory");

            entity.Property(e => e.Ipaddress)
                .HasMaxLength(50)
                .HasColumnName("IPAddress");
            entity.Property(e => e.OldPasswordHash).HasMaxLength(255);
            entity.Property(e => e.ResetReason).HasMaxLength(500);
            entity.Property(e => e.ResetTime).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.ResetByUser).WithMany(p => p.PasswordResetHistoryResetByUsers)
                .HasForeignKey(d => d.ResetByUserId)
                .HasConstraintName("FK__PasswordR__Reset__0D44F85C");

            entity.HasOne(d => d.User).WithMany(p => p.PasswordResetHistoryUsers)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__PasswordR__UserI__0C50D423");
        });

        modelBuilder.Entity<PasswordResetOtp>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Password__3214EC07FD9B0141");

            entity.ToTable("PasswordResetOTPs");

            entity.HasIndex(e => e.Email, "IX_PasswordResetOTPs_Email");

            entity.HasIndex(e => e.OtpCode, "IX_PasswordResetOTPs_OtpCode");

            entity.HasIndex(e => e.UserId, "IX_PasswordResetOTPs_UserId");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Email).HasMaxLength(100);
            entity.Property(e => e.ExpiryTime).HasColumnType("datetime");
            entity.Property(e => e.OtpCode).HasMaxLength(6);
            entity.Property(e => e.UsedAt).HasColumnType("datetime");

            entity.HasOne(d => d.User).WithMany(p => p.PasswordResetOtps)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK_PasswordResetOTPs_Users");
        });

        modelBuilder.Entity<PasswordResetToken>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Password__3214EC07FEB0E7FE");

            entity.ToTable("PasswordResetToken");

            entity.Property(e => e.CreatedAt).HasColumnType("datetime");
            entity.Property(e => e.ExpiresAt).HasColumnType("datetime");
            entity.Property(e => e.LockoutUntil).HasColumnType("datetime");
            entity.Property(e => e.ResendAvailableAt).HasColumnType("datetime");
            entity.Property(e => e.TokenCode).HasMaxLength(6);

            entity.HasOne(d => d.User).WithMany(p => p.PasswordResetTokens)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PasswordResetToken_User");
        });

        modelBuilder.Entity<PasswordResetToken1>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Password__3214EC07B821CF83");

            entity.ToTable("PasswordResetTokens");

            entity.HasIndex(e => e.Token, "IX_PasswordResetTokens_Token");

            entity.HasIndex(e => e.UserId, "IX_PasswordResetTokens_UserId");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.ExpiryTime).HasColumnType("datetime");
            entity.Property(e => e.Token).HasMaxLength(500);
            entity.Property(e => e.UsedAt).HasColumnType("datetime");

            entity.HasOne(d => d.User).WithMany(p => p.PasswordResetToken1s)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PasswordResetTokens_Users");
        });

        modelBuilder.Entity<Project>(entity =>
        {
            entity.HasIndex(e => e.LeaderId, "IX_Projects_LeaderId");

            entity.HasIndex(e => e.Status, "IX_Projects_Status");

            entity.HasIndex(e => e.ProjectCode, "UQ_Projects_ProjectCode").IsUnique();

            entity.Property(e => e.Budget).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Priority)
                .HasMaxLength(20)
                .HasDefaultValue("Medium");
            entity.Property(e => e.Progress)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(5, 2)");
            entity.Property(e => e.ProjectCode).HasMaxLength(50);
            entity.Property(e => e.ProjectName).HasMaxLength(200);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("Planning");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.ProjectCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK_Projects_Creator");

            entity.HasOne(d => d.Department).WithMany(p => p.Projects)
                .HasForeignKey(d => d.DepartmentId)
                .HasConstraintName("FK_Projects_Department");

            entity.HasOne(d => d.Leader).WithMany(p => p.ProjectLeaders)
                .HasForeignKey(d => d.LeaderId)
                .HasConstraintName("FK_Projects_Leader");
        });

        modelBuilder.Entity<ProjectMember>(entity =>
        {
            entity.HasIndex(e => e.ProjectId, "IX_ProjectMembers_ProjectId");

            entity.HasIndex(e => new { e.ProjectId, e.UserId }, "UQ_ProjectMember").IsUnique();

            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.JoinedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Role).HasMaxLength(50);

            entity.HasOne(d => d.Project).WithMany(p => p.ProjectMembers)
                .HasForeignKey(d => d.ProjectId)
                .HasConstraintName("FK_ProjectMembers_Project");

            entity.HasOne(d => d.User).WithMany(p => p.ProjectMembers)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ProjectMembers_User");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.RoleId).HasName("PK__Roles__8AFACE1A32FA3244");

            entity.HasIndex(e => e.RoleName, "UQ__Roles__8A2B6160077C5D61").IsUnique();

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.RoleName).HasMaxLength(50);
        });

        modelBuilder.Entity<SalaryAdjustment>(entity =>
        {
            entity.HasKey(e => e.AdjustmentId).HasName("PK__SalaryAd__E60DB893C534584D");

            entity.HasIndex(e => e.AdjustmentType, "IX_SalaryAdjustments_AdjustmentType");

            entity.HasIndex(e => new { e.UserId, e.WorkDate }, "IX_SalaryAdjustments_UserId_WorkDate");

            entity.Property(e => e.AdjustedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.AdjustmentType).HasMaxLength(50);
            entity.Property(e => e.Amount)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Category).HasMaxLength(100);
            entity.Property(e => e.Hours)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(5, 2)");
            entity.Property(e => e.IsApproved).HasDefaultValue(true);
            entity.Property(e => e.Notes).HasMaxLength(2000);
            entity.Property(e => e.Reason).HasMaxLength(1000);

            entity.HasOne(d => d.AdjustedByNavigation).WithMany(p => p.SalaryAdjustmentAdjustedByNavigations)
                .HasForeignKey(d => d.AdjustedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__SalaryAdj__Adjus__12FDD1B2");

            entity.HasOne(d => d.ApprovedByNavigation).WithMany(p => p.SalaryAdjustmentApprovedByNavigations)
                .HasForeignKey(d => d.ApprovedBy)
                .HasConstraintName("FK__SalaryAdj__Appro__1209AD79");

            entity.HasOne(d => d.Attendance).WithMany(p => p.SalaryAdjustments)
                .HasForeignKey(d => d.AttendanceId)
                .HasConstraintName("FK__SalaryAdj__Atten__11158940");

            entity.HasOne(d => d.User).WithMany(p => p.SalaryAdjustmentUsers)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK__SalaryAdj__UserI__13F1F5EB");
        });

        modelBuilder.Entity<SalaryConfigCategory>(entity =>
        {
            entity.HasKey(e => e.CategoryId).HasName("PK__SalaryCo__19093A0B73384BD9");

            entity.HasIndex(e => e.CategoryCode, "UQ__SalaryCo__371BA955635D701B").IsUnique();

            entity.Property(e => e.CategoryCode).HasMaxLength(50);
            entity.Property(e => e.CategoryName).HasMaxLength(100);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
        });

        modelBuilder.Entity<SalaryConfigHistory>(entity =>
        {
            entity.HasKey(e => e.HistoryId).HasName("PK__SalaryCo__4D7B4ABD4F36C860");

            entity.ToTable("SalaryConfigHistory");

            entity.HasIndex(e => e.ChangedBy, "IX_SalaryConfigHistory_ChangedBy");

            entity.HasIndex(e => e.ConfigId, "IX_SalaryConfigHistory_ConfigId");

            entity.Property(e => e.ChangedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.NewValue).HasMaxLength(1000);
            entity.Property(e => e.OldValue).HasMaxLength(1000);
            entity.Property(e => e.Reason).HasMaxLength(500);

            entity.HasOne(d => d.ChangedByNavigation).WithMany(p => p.SalaryConfigHistories)
                .HasForeignKey(d => d.ChangedBy)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__SalaryCon__Chang__15DA3E5D");

            entity.HasOne(d => d.Config).WithMany(p => p.SalaryConfigHistories)
                .HasForeignKey(d => d.ConfigId)
                .HasConstraintName("FK__SalaryCon__Confi__14E61A24");
        });

        modelBuilder.Entity<SalaryConfiguration>(entity =>
        {
            entity.HasKey(e => e.ConfigId).HasName("PK__SalaryCo__C3BC335CBD43BE58");

            entity.HasIndex(e => e.CategoryId, "IX_SalaryConfigurations_CategoryId");

            entity.HasIndex(e => e.IsActive, "IX_SalaryConfigurations_IsActive");

            entity.HasIndex(e => e.ConfigCode, "UQ__SalaryCo__5488CE684E5A1576").IsUnique();

            entity.Property(e => e.ConfigCode).HasMaxLength(100);
            entity.Property(e => e.ConfigName).HasMaxLength(200);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.DefaultValue).HasMaxLength(1000);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.DisplayLabel).HasMaxLength(200);
            entity.Property(e => e.DisplayOrder).HasDefaultValue(0);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.IsEditable).HasDefaultValue(true);
            entity.Property(e => e.MaxValue).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.MinValue).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Unit).HasMaxLength(50);
            entity.Property(e => e.Value).HasMaxLength(1000);
            entity.Property(e => e.ValueType).HasMaxLength(50);

            entity.HasOne(d => d.Category).WithMany(p => p.SalaryConfigurations)
                .HasForeignKey(d => d.CategoryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__SalaryCon__Categ__16CE6296");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.SalaryConfigurationCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK__SalaryCon__Creat__17C286CF");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.SalaryConfigurationUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK__SalaryCon__Updat__18B6AB08");
        });

        modelBuilder.Entity<SystemSetting>(entity =>
        {
            entity.HasKey(e => e.SettingId).HasName("PK__SystemSe__54372B1DC388C255");

            entity.HasIndex(e => e.Category, "IX_SystemSettings_Category");

            entity.HasIndex(e => new { e.Category, e.ApplyMethod }, "IX_SystemSettings_Category_ApplyMethod").HasFilter("([IsActive]=(1))");

            entity.HasIndex(e => e.IsActive, "IX_SystemSettings_IsActive");

            entity.HasIndex(e => e.IsEnabled, "IX_SystemSettings_IsEnabled");

            entity.HasIndex(e => e.Priority, "IX_SystemSettings_Priority")
                .IsDescending()
                .HasFilter("([IsActive]=(1) AND [IsEnabled]=(1))");

            entity.HasIndex(e => e.SettingKey, "UQ__SystemSe__01E719AD97EB829B").IsUnique();

            entity.Property(e => e.ApplyMethod)
                .HasMaxLength(20)
                .HasDefaultValue("Add");
            entity.Property(e => e.Category).HasMaxLength(100);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.DataType).HasMaxLength(50);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.DisplayName).HasMaxLength(200);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.IsEnabled).HasDefaultValue(true);
            entity.Property(e => e.MaxValue).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.MinValue).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Priority).HasDefaultValue(0);
            entity.Property(e => e.SettingKey).HasMaxLength(100);
            entity.Property(e => e.SettingValue).HasMaxLength(1000);
            entity.Property(e => e.Unit).HasMaxLength(20);

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.SystemSettings)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK__SystemSet__Updat__19AACF41");
        });

        modelBuilder.Entity<SystemSettingsBackup20251123>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("SystemSettings_Backup_20251123");

            entity.Property(e => e.ApplyMethod).HasMaxLength(20);
            entity.Property(e => e.Category).HasMaxLength(100);
            entity.Property(e => e.DataType).HasMaxLength(50);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.DisplayName).HasMaxLength(200);
            entity.Property(e => e.SettingId).ValueGeneratedOnAdd();
            entity.Property(e => e.SettingKey).HasMaxLength(100);
            entity.Property(e => e.SettingValue).HasMaxLength(1000);
        });

        modelBuilder.Entity<Task>(entity =>
        {
            entity.HasKey(e => e.TaskId).HasName("PK__Tasks__7C6949B179A7C693");

            entity.HasIndex(e => e.Deadline, "IX_Tasks_Deadline").HasFilter("([Deadline] IS NOT NULL)");

            entity.HasIndex(e => new { e.IsActive, e.Deadline }, "IX_Tasks_IsActive_Deadline").HasFilter("([Deadline] IS NOT NULL)");

            entity.HasIndex(e => e.Priority, "IX_Tasks_Priority");

            entity.HasIndex(e => e.ProjectId, "IX_Tasks_ProjectId");

            entity.HasIndex(e => e.TaskType, "IX_Tasks_TaskType");

            entity.HasIndex(e => e.UpdatedAt, "IX_Tasks_UpdatedAt");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Platform).HasMaxLength(200);
            entity.Property(e => e.Priority)
                .HasMaxLength(20)
                .HasDefaultValue("Medium");
            entity.Property(e => e.TaskName).HasMaxLength(200);
            entity.Property(e => e.TaskType)
                .HasMaxLength(20)
                .HasDefaultValue("Standalone");
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime");

            entity.HasOne(d => d.ParentTask).WithMany(p => p.InverseParentTask)
                .HasForeignKey(d => d.ParentTaskId)
                .HasConstraintName("FK_Tasks_ParentTask");

            entity.HasOne(d => d.Project).WithMany(p => p.Tasks)
                .HasForeignKey(d => d.ProjectId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_Tasks_Projects");
        });

        modelBuilder.Entity<Team>(entity =>
        {
            entity.HasKey(e => e.TeamId).HasName("PK__Teams__123AE799FB116721");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.TeamName).HasMaxLength(100);

            entity.HasOne(d => d.Department).WithMany(p => p.Teams)
                .HasForeignKey(d => d.DepartmentId)
                .HasConstraintName("FK__Teams__Departmen__1C873BEC");

            entity.HasOne(d => d.TeamLeadUser).WithMany(p => p.Teams)
                .HasForeignKey(d => d.TeamLeadUserId)
                .HasConstraintName("FK__Teams__TeamLeadU__1D7B6025");
        });

        modelBuilder.Entity<TeamMember>(entity =>
        {
            entity.HasKey(e => e.TeamMemberId).HasName("PK__TeamMemb__C7C092E5F70D3D98");

            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.JoinedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.Team).WithMany(p => p.TeamMembers)
                .HasForeignKey(d => d.TeamId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__TeamMembe__TeamI__1A9EF37A");

            entity.HasOne(d => d.User).WithMany(p => p.TeamMembers)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__TeamMembe__UserI__1B9317B3");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__Users__1788CC4C97C7A690");

            entity.HasIndex(e => e.DepartmentId, "IX_Users_DepartmentId");

            entity.HasIndex(e => new { e.IsActive, e.DepartmentId }, "IX_Users_IsActive_DepartmentId");

            entity.HasIndex(e => e.RoleId, "IX_Users_RoleId");

            entity.HasIndex(e => e.Username, "IX_Users_Username");

            entity.HasIndex(e => e.Username, "UQ__Users__536C85E49D27A24A").IsUnique();

            entity.Property(e => e.Avatar).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Email).HasMaxLength(100);
            entity.Property(e => e.FullName).HasMaxLength(100);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.PasswordHash).HasMaxLength(255);
            entity.Property(e => e.PhoneNumber).HasMaxLength(20);
            entity.Property(e => e.Username).HasMaxLength(50);

            entity.HasOne(d => d.Department).WithMany(p => p.Users)
                .HasForeignKey(d => d.DepartmentId)
                .HasConstraintName("FK__Users__Departmen__2057CCD0");

            entity.HasOne(d => d.Role).WithMany(p => p.Users)
                .HasForeignKey(d => d.RoleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Users__RoleId__1F63A897");
        });

        modelBuilder.Entity<UserNotification>(entity =>
        {
            entity.HasKey(e => e.UserNotificationId).HasName("PK__UserNoti__EB2986291E9E1394");

            entity.HasIndex(e => e.IsRead, "IX_UserNotifications_IsRead");

            entity.HasIndex(e => e.UserId, "IX_UserNotifications_UserId");

            entity.HasOne(d => d.Notification).WithMany(p => p.UserNotifications)
                .HasForeignKey(d => d.NotificationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserNotifications_Notifications");
        });

        modelBuilder.Entity<UserSalarySetting>(entity =>
        {
            entity.HasKey(e => e.UserSalaryId).HasName("PK__UserSala__528D2F425D4CB598");

            entity.HasIndex(e => e.UserId, "UQ__UserSala__1788CC4D9CC479CC").IsUnique();

            entity.Property(e => e.AllowanceAmount)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.BaseSalary)
                .HasDefaultValue(5000000m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.DefaultOvertimeRate)
                .HasDefaultValue(1.5m)
                .HasColumnType("decimal(5, 2)");
            entity.Property(e => e.HourlyRate).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.SalaryType)
                .HasMaxLength(20)
                .HasDefaultValue("Monthly");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.UserSalarySettingCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK__UserSalar__Creat__22401542");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.UserSalarySettingUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK__UserSalar__Updat__214BF109");

            entity.HasOne(d => d.User).WithOne(p => p.UserSalarySettingUser)
                .HasForeignKey<UserSalarySetting>(d => d.UserId)
                .HasConstraintName("FK__UserSalar__UserI__2334397B");
        });

        modelBuilder.Entity<UserTask>(entity =>
        {
            entity.HasKey(e => e.UserTaskId).HasName("PK__UserTask__4EF5961F76B2B797");

            entity.ToTable(tb => tb.HasTrigger("trg_UserTask_UpdateProjectProgress"));

            entity.HasIndex(e => e.Status, "IX_UserTasks_Status");

            entity.HasIndex(e => new { e.Status, e.TesterId }, "IX_UserTasks_Status_TesterId");

            entity.HasIndex(e => e.TesterId, "IX_UserTasks_TesterId");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.ReopenReason).HasMaxLength(500);
            entity.Property(e => e.ReportLink).HasMaxLength(500);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("TODO");

            entity.HasOne(d => d.Task).WithMany(p => p.UserTasks)
                .HasForeignKey(d => d.TaskId)
                .HasConstraintName("FK__UserTasks__TaskI__2610A626");

            entity.HasOne(d => d.Tester).WithMany(p => p.UserTaskTesters)
                .HasForeignKey(d => d.TesterId)
                .HasConstraintName("FK_UserTasks_Tester");

            entity.HasOne(d => d.User).WithMany(p => p.UserTaskUsers)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK__UserTasks__UserI__24285DB4");
        });

        modelBuilder.Entity<VwActiveSalarySetting>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_ActiveSalarySettings");

            entity.Property(e => e.ApplyMethod).HasMaxLength(20);
            entity.Property(e => e.Category).HasMaxLength(100);
            entity.Property(e => e.DataType).HasMaxLength(50);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.DisplayName).HasMaxLength(200);
            entity.Property(e => e.MaxValue).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.MinValue).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.SettingId).ValueGeneratedOnAdd();
            entity.Property(e => e.SettingKey).HasMaxLength(100);
            entity.Property(e => e.SettingValue).HasMaxLength(1000);
            entity.Property(e => e.Unit).HasMaxLength(20);
        });

        modelBuilder.Entity<VwKpidashboard>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_KPIDashboard");

            entity.Property(e => e.DepartmentName).HasMaxLength(100);
            entity.Property(e => e.Email).HasMaxLength(100);
            entity.Property(e => e.FullName).HasMaxLength(100);
            entity.Property(e => e.RoleName).HasMaxLength(50);
            entity.Property(e => e.ThisMonthTotalHours).HasColumnType("decimal(38, 2)");
        });

        modelBuilder.Entity<VwPendingRequestsSummary>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_PendingRequestsSummary");

            entity.Property(e => e.RequestType)
                .HasMaxLength(15)
                .IsUnicode(false);
        });

        modelBuilder.Entity<VwProjectOverview>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_ProjectOverview");

            entity.Property(e => e.DepartmentName).HasMaxLength(100);
            entity.Property(e => e.LeaderName).HasMaxLength(100);
            entity.Property(e => e.Priority).HasMaxLength(20);
            entity.Property(e => e.Progress).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.ProjectCode).HasMaxLength(50);
            entity.Property(e => e.ProjectName).HasMaxLength(200);
            entity.Property(e => e.Status).HasMaxLength(20);
        });

        modelBuilder.Entity<VwSalaryCalculation>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_SalaryCalculation");

            entity.Property(e => e.AutoDeduction).HasColumnType("decimal(38, 2)");
            entity.Property(e => e.BaseSalary).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.DailySalaryTotal).HasColumnType("numeric(38, 8)");
            entity.Property(e => e.DepartmentName).HasMaxLength(100);
            entity.Property(e => e.Email).HasMaxLength(100);
            entity.Property(e => e.FullName).HasMaxLength(100);
            entity.Property(e => e.MonthlyAllowance).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.OvertimeSalary).HasColumnType("numeric(38, 12)");
            entity.Property(e => e.SalaryType).HasMaxLength(20);
            entity.Property(e => e.TotalOvertimeHours).HasColumnType("decimal(38, 2)");
            entity.Property(e => e.TotalWorkHours).HasColumnType("decimal(38, 2)");
        });

        modelBuilder.Entity<VwTodayAttendance>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_TodayAttendance");

            entity.Property(e => e.Avatar).HasMaxLength(500);
            entity.Property(e => e.CheckInAddress).HasMaxLength(500);
            entity.Property(e => e.CheckOutAddress).HasMaxLength(500);
            entity.Property(e => e.DepartmentName).HasMaxLength(100);
            entity.Property(e => e.FullName).HasMaxLength(100);
            entity.Property(e => e.TotalHours).HasColumnType("decimal(5, 2)");
        });

        modelBuilder.Entity<VwUserDetail>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_UserDetails");

            entity.Property(e => e.Avatar).HasMaxLength(500);
            entity.Property(e => e.DepartmentName).HasMaxLength(100);
            entity.Property(e => e.Email).HasMaxLength(100);
            entity.Property(e => e.FullName).HasMaxLength(100);
            entity.Property(e => e.PhoneNumber).HasMaxLength(20);
            entity.Property(e => e.RoleName).HasMaxLength(50);
            entity.Property(e => e.Username).HasMaxLength(50);
        });

        modelBuilder.Entity<WorkScheduleException>(entity =>
        {
            entity.HasKey(e => e.ExceptionId).HasName("PK__WorkSche__26981D88485D7C8E");

            entity.HasIndex(e => e.UserId, "IX_WorkScheduleExceptions_UserId");

            entity.HasIndex(e => e.WorkDate, "IX_WorkScheduleExceptions_WorkDate");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.ExceptionType)
                .HasMaxLength(50)
                .HasDefaultValue("Normal");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.OvertimeMultiplier).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.SalaryMultiplier)
                .HasDefaultValue(1.0m)
                .HasColumnType("decimal(5, 2)");
            entity.Property(e => e.StandardHours).HasColumnType("decimal(5, 2)");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.WorkScheduleExceptionCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK__WorkSched__Creat__28ED12D1");

            entity.HasOne(d => d.Department).WithMany(p => p.WorkScheduleExceptions)
                .HasForeignKey(d => d.DepartmentId)
                .HasConstraintName("FK__WorkSched__Depar__27F8EE98");

            entity.HasOne(d => d.User).WithMany(p => p.WorkScheduleExceptionUsers)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK__WorkSched__UserI__2704CA5F");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
