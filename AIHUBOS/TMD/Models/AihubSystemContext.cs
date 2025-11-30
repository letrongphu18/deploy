using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace AIHUBOS.Models;

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

    public virtual DbSet<VwSalaryCalculation> VwSalaryCalculations { get; set; }

    public virtual DbSet<VwTaskPerformance> VwTaskPerformances { get; set; }

    public virtual DbSet<VwTasksWithDeadline> VwTasksWithDeadlines { get; set; }

    public virtual DbSet<VwTodayAttendance> VwTodayAttendances { get; set; }

    public virtual DbSet<VwUserDetail> VwUserDetails { get; set; }

    public virtual DbSet<WorkScheduleException> WorkScheduleExceptions { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseSqlServer("Server=(local)\\MSSQLSERVER02;Database=aihub_system;Trusted_Connection=True;TrustServerCertificate=True;");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Attendance>(entity =>
        {
            entity.HasKey(e => e.AttendanceId).HasName("PK__Attendan__8B69261CFC1A2038");

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
                .HasConstraintName("FK__Attendanc__UserI__2EDAF651");
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.AuditLogId).HasName("PK__AuditLog__EB5F6CBD0A0604CC");

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
                .HasConstraintName("FK__AuditLogs__UserI__32AB8735");
        });

        modelBuilder.Entity<Department>(entity =>
        {
            entity.HasKey(e => e.DepartmentId).HasName("PK__Departme__B2079BED5523DA78");

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
            entity.HasKey(e => e.KpihistoryId).HasName("PK__KPIHisto__846C1BB3604F2E59");

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
            entity.HasKey(e => e.LateRequestId).HasName("PK__LateRequ__6814EE074A02B0E7");

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
            entity.HasKey(e => e.LeaveRequestId).HasName("PK__LeaveReq__609421EE91D0CDBF");

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
            entity.HasKey(e => e.LoginHistoryId).HasName("PK__LoginHis__2773EA9F7FD6FE09");

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
                .HasConstraintName("FK__LoginHist__UserI__37703C52");
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.NotificationId).HasName("PK__Notifica__20CF2E1248FF5E01");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.Link).HasMaxLength(500);
            entity.Property(e => e.Message).HasMaxLength(1000);
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.Type).HasMaxLength(50);
        });

        modelBuilder.Entity<OvertimeRequest>(entity =>
        {
            entity.HasKey(e => e.OvertimeRequestId).HasName("PK__Overtime__F97D0DCAA9117636");

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
            entity.HasKey(e => e.ResetId).HasName("PK__Password__783CF04DAFAF33CE");

            entity.ToTable("PasswordResetHistory");

            entity.Property(e => e.Ipaddress)
                .HasMaxLength(50)
                .HasColumnName("IPAddress");
            entity.Property(e => e.OldPasswordHash).HasMaxLength(255);
            entity.Property(e => e.ResetReason).HasMaxLength(500);
            entity.Property(e => e.ResetTime).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.ResetByUser).WithMany(p => p.PasswordResetHistoryResetByUsers)
                .HasForeignKey(d => d.ResetByUserId)
                .HasConstraintName("FK__PasswordR__Reset__3A4CA8FD");

            entity.HasOne(d => d.User).WithMany(p => p.PasswordResetHistoryUsers)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__PasswordR__UserI__3B40CD36");
        });

        modelBuilder.Entity<PasswordResetOtp>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Password__3214EC0763694607");

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
            entity.HasKey(e => e.Id).HasName("PK__Password__3214EC0788EBB4ED");

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
            entity.HasKey(e => e.Id).HasName("PK__Password__3214EC0792472EA4");

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

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.RoleId).HasName("PK__Roles__8AFACE1A6FFE762B");

            entity.HasIndex(e => e.RoleName, "UQ__Roles__8A2B6160EE09EB9D").IsUnique();

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.RoleName).HasMaxLength(50);
        });

        modelBuilder.Entity<SalaryAdjustment>(entity =>
        {
            entity.HasKey(e => e.AdjustmentId).HasName("PK__SalaryAd__E60DB89397E1F75A");

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
                .HasConstraintName("FK__SalaryAdj__Adjus__3F115E1A");

            entity.HasOne(d => d.ApprovedByNavigation).WithMany(p => p.SalaryAdjustmentApprovedByNavigations)
                .HasForeignKey(d => d.ApprovedBy)
                .HasConstraintName("FK__SalaryAdj__Appro__40058253");

            entity.HasOne(d => d.Attendance).WithMany(p => p.SalaryAdjustments)
                .HasForeignKey(d => d.AttendanceId)
                .HasConstraintName("FK__SalaryAdj__Atten__40F9A68C");

            entity.HasOne(d => d.User).WithMany(p => p.SalaryAdjustmentUsers)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK__SalaryAdj__UserI__41EDCAC5");
        });

        modelBuilder.Entity<SalaryConfigCategory>(entity =>
        {
            entity.HasKey(e => e.CategoryId).HasName("PK__SalaryCo__19093A0BB427FF54");

            entity.HasIndex(e => e.CategoryCode, "UQ__SalaryCo__371BA9553FBE53F9").IsUnique();

            entity.Property(e => e.CategoryCode).HasMaxLength(50);
            entity.Property(e => e.CategoryName).HasMaxLength(100);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
        });

        modelBuilder.Entity<SalaryConfigHistory>(entity =>
        {
            entity.HasKey(e => e.HistoryId).HasName("PK__SalaryCo__4D7B4ABD356233AE");

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
                .HasConstraintName("FK__SalaryCon__Chang__42E1EEFE");

            entity.HasOne(d => d.Config).WithMany(p => p.SalaryConfigHistories)
                .HasForeignKey(d => d.ConfigId)
                .HasConstraintName("FK__SalaryCon__Confi__43D61337");
        });

        modelBuilder.Entity<SalaryConfiguration>(entity =>
        {
            entity.HasKey(e => e.ConfigId).HasName("PK__SalaryCo__C3BC335CAAA72430");

            entity.HasIndex(e => e.CategoryId, "IX_SalaryConfigurations_CategoryId");

            entity.HasIndex(e => e.IsActive, "IX_SalaryConfigurations_IsActive");

            entity.HasIndex(e => e.ConfigCode, "UQ__SalaryCo__5488CE681A24F599").IsUnique();

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
                .HasConstraintName("FK__SalaryCon__Categ__44CA3770");

            entity.HasOne(d => d.CreatedByNavigation).WithMany(p => p.SalaryConfigurationCreatedByNavigations)
                .HasForeignKey(d => d.CreatedBy)
                .HasConstraintName("FK__SalaryCon__Creat__45BE5BA9");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.SalaryConfigurationUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK__SalaryCon__Updat__46B27FE2");
        });

        modelBuilder.Entity<SystemSetting>(entity =>
        {
            entity.HasKey(e => e.SettingId).HasName("PK__SystemSe__54372B1DB15485C8");

            entity.HasIndex(e => e.Category, "IX_SystemSettings_Category");

            entity.HasIndex(e => new { e.Category, e.ApplyMethod }, "IX_SystemSettings_Category_ApplyMethod").HasFilter("([IsActive]=(1))");

            entity.HasIndex(e => e.IsActive, "IX_SystemSettings_IsActive");

            entity.HasIndex(e => e.IsEnabled, "IX_SystemSettings_IsEnabled");

            entity.HasIndex(e => e.Priority, "IX_SystemSettings_Priority")
                .IsDescending()
                .HasFilter("([IsActive]=(1) AND [IsEnabled]=(1))");

            entity.HasIndex(e => e.SettingKey, "UQ__SystemSe__01E719ADD915F5B4").IsUnique();

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
                .HasConstraintName("FK__SystemSet__Updat__47A6A41B");
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
            entity.HasKey(e => e.TaskId).HasName("PK__Tasks__7C6949B1CD1A420C");

            entity.HasIndex(e => e.Deadline, "IX_Tasks_Deadline").HasFilter("([Deadline] IS NOT NULL)");

            entity.HasIndex(e => new { e.IsActive, e.Deadline }, "IX_Tasks_IsActive_Deadline").HasFilter("([Deadline] IS NOT NULL)");

            entity.HasIndex(e => e.Priority, "IX_Tasks_Priority");

            entity.HasIndex(e => e.UpdatedAt, "IX_Tasks_UpdatedAt");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Platform).HasMaxLength(200);
            entity.Property(e => e.Priority)
                .HasMaxLength(20)
                .HasDefaultValue("Medium");
            entity.Property(e => e.TaskName).HasMaxLength(200);
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime");
        });

        modelBuilder.Entity<Team>(entity =>
        {
            entity.HasKey(e => e.TeamId).HasName("PK__Teams__123AE7996987189A");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.TeamName).HasMaxLength(100);

            entity.HasOne(d => d.Department).WithMany(p => p.Teams)
                .HasForeignKey(d => d.DepartmentId)
                .HasConstraintName("FK__Teams__Departmen__7C1A6C5A");

            entity.HasOne(d => d.TeamLeadUser).WithMany(p => p.Teams)
                .HasForeignKey(d => d.TeamLeadUserId)
                .HasConstraintName("FK__Teams__TeamLeadU__7D0E9093");
        });

        modelBuilder.Entity<TeamMember>(entity =>
        {
            entity.HasKey(e => e.TeamMemberId).HasName("PK__TeamMemb__C7C092E5827837B1");

            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.JoinedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.Team).WithMany(p => p.TeamMembers)
                .HasForeignKey(d => d.TeamId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__TeamMembe__TeamI__01D345B0");

            entity.HasOne(d => d.User).WithMany(p => p.TeamMembers)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__TeamMembe__UserI__02C769E9");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__Users__1788CC4C56E8178B");

            entity.HasIndex(e => e.DepartmentId, "IX_Users_DepartmentId");

            entity.HasIndex(e => new { e.IsActive, e.DepartmentId }, "IX_Users_IsActive_DepartmentId");

            entity.HasIndex(e => e.RoleId, "IX_Users_RoleId");

            entity.HasIndex(e => e.Username, "IX_Users_Username");

            entity.HasIndex(e => e.Username, "UQ__Users__536C85E43FCBEE68").IsUnique();

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
                .HasConstraintName("FK__Users__Departmen__489AC854");

            entity.HasOne(d => d.Role).WithMany(p => p.Users)
                .HasForeignKey(d => d.RoleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Users__RoleId__498EEC8D");
        });

        modelBuilder.Entity<UserNotification>(entity =>
        {
            entity.HasKey(e => e.UserNotificationId).HasName("PK__UserNoti__EB2986294F44D7A6");

            entity.HasIndex(e => e.IsRead, "IX_UserNotifications_IsRead");

            entity.HasIndex(e => e.UserId, "IX_UserNotifications_UserId");

            entity.HasOne(d => d.Notification).WithMany(p => p.UserNotifications)
                .HasForeignKey(d => d.NotificationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserNotifications_Notifications");
        });

        modelBuilder.Entity<UserSalarySetting>(entity =>
        {
            entity.HasKey(e => e.UserSalaryId).HasName("PK__UserSala__528D2F42E4B5DCDC");

            entity.HasIndex(e => e.UserId, "UQ__UserSala__1788CC4D793850F2").IsUnique();

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
                .HasConstraintName("FK__UserSalar__Creat__4A8310C6");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.UserSalarySettingUpdatedByNavigations)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("FK__UserSalar__Updat__4B7734FF");

            entity.HasOne(d => d.User).WithOne(p => p.UserSalarySettingUser)
                .HasForeignKey<UserSalarySetting>(d => d.UserId)
                .HasConstraintName("FK__UserSalar__UserI__4C6B5938");
        });

        modelBuilder.Entity<UserTask>(entity =>
        {
            entity.HasKey(e => e.UserTaskId).HasName("PK__UserTask__4EF5961FDBDC8DD1");

            entity.HasIndex(e => e.Status, "IX_UserTasks_Status");

            entity.HasIndex(e => new { e.Status, e.TesterId }, "IX_UserTasks_Status_TesterId");

            entity.HasIndex(e => e.TesterId, "IX_UserTasks_TesterId");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.ReportLink).HasMaxLength(500);
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("TODO");

            entity.HasOne(d => d.Task).WithMany(p => p.UserTasks)
                .HasForeignKey(d => d.TaskId)
                .HasConstraintName("FK__UserTasks__TaskI__4D5F7D71");

            entity.HasOne(d => d.Tester).WithMany(p => p.UserTaskTesters)
                .HasForeignKey(d => d.TesterId)
                .HasConstraintName("FK_UserTasks_Tester");

			// ✅ SAU (ĐÚNG)
			entity.HasOne(d => d.User)
				.WithMany(p => p.UserTasks)  // ✅ ĐỔI THÀNH UserTasks
				.HasForeignKey(d => d.UserId)
				.OnDelete(DeleteBehavior.Cascade)
				.HasConstraintName("FK__UserTasks__UserId__...");
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

        modelBuilder.Entity<VwTaskPerformance>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_TaskPerformance");

            entity.Property(e => e.Avatar).HasMaxLength(500);
            entity.Property(e => e.DepartmentName).HasMaxLength(100);
            entity.Property(e => e.FullName).HasMaxLength(100);
            entity.Property(e => e.Platform).HasMaxLength(200);
            entity.Property(e => e.ReportLink).HasMaxLength(500);
            entity.Property(e => e.TaskName).HasMaxLength(200);
        });

        modelBuilder.Entity<VwTasksWithDeadline>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_TasksWithDeadline");

            entity.Property(e => e.DeadlineStatus)
                .HasMaxLength(13)
                .IsUnicode(false);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Platform).HasMaxLength(200);
            entity.Property(e => e.Priority).HasMaxLength(20);
            entity.Property(e => e.TaskId).ValueGeneratedOnAdd();
            entity.Property(e => e.TaskName).HasMaxLength(200);
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
            entity.HasKey(e => e.ExceptionId).HasName("PK__WorkSche__26981D884334D51C");

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
                .HasConstraintName("FK__WorkSched__Creat__4F47C5E3");

            entity.HasOne(d => d.Department).WithMany(p => p.WorkScheduleExceptions)
                .HasForeignKey(d => d.DepartmentId)
                .HasConstraintName("FK__WorkSched__Depar__503BEA1C");

            entity.HasOne(d => d.User).WithMany(p => p.WorkScheduleExceptionUsers)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK__WorkSched__UserI__51300E55");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
