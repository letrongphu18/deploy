using TMD.Models;
using Microsoft.EntityFrameworkCore;
using Hangfire;
using Hangfire.Common;
using System.Threading.Tasks; // ✅ FIX: Explicit using for Task
using Hangfire;
namespace AIHUBOS.Services
{
	/// <summary>
	/// Service tự động xóa audit logs cũ hơn X ngày (mặc định 60 ngày = 2 tháng)
	/// Chạy hàng ngày lúc 2:00 AM
	/// </summary>
	public interface IAuditCleanupService
	{
		System.Threading.Tasks.Task CleanupOldAuditLogsAsync(int daysToKeep = 60); // ✅ FIX: Explicit Task
		System.Threading.Tasks.Task<CleanupResult> GetCleanupStatusAsync(); // ✅ FIX: Explicit Task
	}

	public class AuditCleanupService : IAuditCleanupService
	{
		private readonly IDbContextFactory<AihubSystemContext> _contextFactory;
		private readonly ILogger<AuditCleanupService> _logger;

		public AuditCleanupService(
			IDbContextFactory<AihubSystemContext> contextFactory,
			ILogger<AuditCleanupService> logger)
		{
			_contextFactory = contextFactory;
			_logger = logger;
		}

		/// <summary>
		/// Xóa tất cả audit logs cũ hơn số ngày được chỉ định
		/// </summary>
		public async System.Threading.Tasks.Task CleanupOldAuditLogsAsync(int daysToKeep = 60) // ✅ FIX: Explicit Task
		{
			try
			{
				var cutoffDate = DateTime.Now.AddDays(-daysToKeep);

				_logger.LogInformation(
					"[AuditCleanup] 🧹 Bắt đầu xóa audit logs cũ hơn {CutoffDate} (giữ lại {DaysToKeep} ngày)",
					cutoffDate.ToString("dd/MM/yyyy"), daysToKeep);

				await using var context = await _contextFactory.CreateDbContextAsync();

				// ✅ TỰ ĐỘNG DISABLE CHANGE TRACKING (tăng performance)
				context.ChangeTracker.AutoDetectChangesEnabled = false;
				context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;

				// ✅ ĐẾM SỐ RECORD SẼ XÓA
				var logsToDelete = await context.AuditLogs
					.Where(a => a.Timestamp.HasValue && a.Timestamp.Value < cutoffDate)
					.CountAsync();

				if (logsToDelete == 0)
				{
					_logger.LogInformation("[AuditCleanup] ℹ️ Không có audit logs cũ để xóa");
					return; // ✅ FIX: Return void-like behavior
				}

				_logger.LogInformation("[AuditCleanup] 📊 Sẽ xóa {LogCount} bản ghi", logsToDelete);

				// ✅ XÓA DỮ LIỆU (batch processing để tránh timeout)
				const int batchSize = 1000;
				int totalDeleted = 0;

				while (true)
				{
					var batch = await context.AuditLogs
						.Where(a => a.Timestamp.HasValue && a.Timestamp.Value < cutoffDate)
						.Take(batchSize)
						.ToListAsync();

					if (batch.Count == 0)
						break;

					context.AuditLogs.RemoveRange(batch);
					var deletedCount = await context.SaveChangesAsync();
					totalDeleted += deletedCount;

					_logger.LogInformation("[AuditCleanup] Đã xóa batch: {DeletedCount} bản ghi (tổng: {TotalDeleted})",
						deletedCount, totalDeleted);
				}

				// ✅ TRUNCATE IDENTITY SEED NẾU CẦN (tùy chọn)
				if (totalDeleted > 0)
				{
					try
					{
						var totalRecords = await context.AuditLogs.CountAsync();
						_logger.LogInformation(
							"[AuditCleanup] ✅ Hoàn tất! Đã xóa {TotalDeleted} bản ghi. Còn lại: {RemainingRecords}",
							totalDeleted, totalRecords);
					}
					catch (Exception ex)
					{
						_logger.LogWarning(ex, "[AuditCleanup] Không thể lấy số lượng records còn lại");
					}
				}
			}
			catch (DbUpdateException dbEx)
			{
				_logger.LogError(dbEx, "[AuditCleanup] ❌ Database error khi xóa audit logs");
				throw;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "[AuditCleanup] ❌ Lỗi khi xóa audit logs cũ");
				throw;
			}
		}

		/// <summary>
		/// Lấy thông tin trạng thái của cleanup
		/// </summary>
		public async System.Threading.Tasks.Task<CleanupResult> GetCleanupStatusAsync() // ✅ FIX: Explicit Task
		{
			try
			{
				await using var context = await _contextFactory.CreateDbContextAsync();

				var now = DateTime.Now;
				var twoMonthsAgo = now.AddDays(-60);
				var sixMonthsAgo = now.AddDays(-180);

				var totalLogs = await context.AuditLogs.CountAsync();
				var logsLast2Months = await context.AuditLogs
					.CountAsync(a => a.Timestamp.HasValue && a.Timestamp.Value >= twoMonthsAgo);
				var logsLast6Months = await context.AuditLogs
					.CountAsync(a => a.Timestamp.HasValue && a.Timestamp.Value >= sixMonthsAgo);
				var logsOlderThan2Months = await context.AuditLogs
					.CountAsync(a => a.Timestamp.HasValue && a.Timestamp.Value < twoMonthsAgo);

				var oldestLog = await context.AuditLogs
					.OrderBy(a => a.Timestamp)
					.FirstOrDefaultAsync();

				var newestLog = await context.AuditLogs
					.OrderByDescending(a => a.Timestamp)
					.FirstOrDefaultAsync();

				return new CleanupResult
				{
					TotalRecords = totalLogs,
					RecordsLast2Months = logsLast2Months,
					RecordsLast6Months = logsLast6Months,
					RecordsOlderThan2Months = logsOlderThan2Months,
					OldestLogDate = oldestLog?.Timestamp,
					NewestLogDate = newestLog?.Timestamp,
					Status = "OK",
					LastCleanupDate = DateTime.Now // Có thể lưu vào DB nếu cần
				};
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "[AuditCleanup] Error getting cleanup status");
				return new CleanupResult { Status = "ERROR", ErrorMessage = ex.Message };
			}
		}
	}

	/// <summary>
	/// Model kết quả cleanup
	/// </summary>
	public class CleanupResult
	{
		public int TotalRecords { get; set; }
		public int RecordsLast2Months { get; set; }
		public int RecordsLast6Months { get; set; }
		public int RecordsOlderThan2Months { get; set; }
		public DateTime? OldestLogDate { get; set; }
		public DateTime? NewestLogDate { get; set; }
		public string Status { get; set; } = "UNKNOWN";
		public string? ErrorMessage { get; set; }
		public DateTime LastCleanupDate { get; set; } = DateTime.Now;

		public string GetDatabaseSizeInfo()
		{
			// Ước tính (mỗi record ~500 bytes)
			var estimatedSizeMB = (TotalRecords * 500) / (1024.0 * 1024.0);
			return $"{estimatedSizeMB:F2} MB";
		}
	}
}