using TMD.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace AIHUBOS.Helpers
{
	public class AuditHelper
	{
		private readonly IDbContextFactory<AihubSystemContext> _contextFactory;
		private readonly IHttpContextAccessor _httpContextAccessor;
		private readonly ILogger<AuditHelper> _logger;

		public AuditHelper(
			IDbContextFactory<AihubSystemContext> contextFactory,
			IHttpContextAccessor httpContextAccessor,
			ILogger<AuditHelper> logger)
		{
			_contextFactory = contextFactory;
			_httpContextAccessor = httpContextAccessor;
			_logger = logger;
		}

		/// <summary>
		/// ✅ GHI LOG CHÍNH - ĐÃ FIX HOÀN TOÀN
		/// </summary>
		public async System.Threading.Tasks.Task LogAsync(
			int? userId,
			string action,
			string entityName,
			int? entityId,
			object? oldValue,
			object? newValue,
			string description)
		{
			try
			{
				// ✅ VALIDATE INPUT
				if (string.IsNullOrWhiteSpace(action))
				{
					_logger.LogWarning("[AuditHelper] Action is null/empty - skipping log");
					return;
				}

				if (string.IsNullOrWhiteSpace(entityName))
				{
					_logger.LogWarning("[AuditHelper] EntityName is null/empty - skipping log");
					return;
				}

				var context = _httpContextAccessor.HttpContext;

				// ✅ LẤY IP ADDRESS CHÍNH XÁC
				var ipAddress = GetClientIpAddress(context);

				// ✅ LẤY USER AGENT
				var userAgent = context?.Request.Headers["User-Agent"].ToString();
				if (string.IsNullOrEmpty(userAgent))
					userAgent = "Unknown";

				// ✅ LẤY LOCATION
				var location = GetLocationInfo(context);

				// ✅ JSON OPTIONS AN TOÀN
				var jsonOptions = new JsonSerializerOptions
				{
					WriteIndented = false, // ✅ Giảm dung lượng
					Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
					DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
				};

				// ✅ SERIALIZE AN TOÀN
				string? oldValueJson = null;
				string? newValueJson = null;

				try
				{
					if (oldValue != null)
					{
						oldValueJson = JsonSerializer.Serialize(oldValue, jsonOptions);

						// ✅ GIỚI HẠN ĐỘ DÀI (tránh database overflow)
						if (oldValueJson.Length > 4000)
						{
							oldValueJson = oldValueJson.Substring(0, 3997) + "...";
							_logger.LogWarning("[AuditHelper] OldValue truncated (too long)");
						}
					}
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "[AuditHelper] Failed to serialize OldValue");
					oldValueJson = "[Serialization Error]";
				}

				try
				{
					if (newValue != null)
					{
						newValueJson = JsonSerializer.Serialize(newValue, jsonOptions);

						// ✅ GIỚI HẠN ĐỘ DÀI
						if (newValueJson.Length > 4000)
						{
							newValueJson = newValueJson.Substring(0, 3997) + "...";
							_logger.LogWarning("[AuditHelper] NewValue truncated (too long)");
						}
					}
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "[AuditHelper] Failed to serialize NewValue");
					newValueJson = "[Serialization Error]";
				}

				// ✅ TẠO AUDIT LOG OBJECT
				var log = new AuditLog
				{
					UserId = userId,
					Action = action.ToUpper().Trim(),
					EntityName = entityName.Trim(),
					EntityId = entityId,
					OldValue = oldValueJson,
					NewValue = newValueJson,
					Description = description?.Trim() ?? "",
					Timestamp = DateTime.Now,
					Ipaddress = ipAddress,
					UserAgent = userAgent,
					Location = location
				};

				// ✅ VALIDATE TRƯỚC KHI LƯU
				if (log.Action.Length > 50)
				{
					_logger.LogWarning("[AuditHelper] Action too long, truncating");
					log.Action = log.Action.Substring(0, 50);
				}

				if (log.EntityName.Length > 100)
				{
					_logger.LogWarning("[AuditHelper] EntityName too long, truncating");
					log.EntityName = log.EntityName.Substring(0, 100);
				}

				if (log.Description != null && log.Description.Length > 1000)
				{
					_logger.LogWarning("[AuditHelper] Description too long, truncating");
					log.Description = log.Description.Substring(0, 997) + "...";
				}

				// ✅ SỬ DỤNG SEPARATE CONTEXT
				await using var auditContext = await _contextFactory.CreateDbContextAsync();

				// ✅ TẮT CHANGE TRACKING (TĂNG PERFORMANCE)
				auditContext.ChangeTracker.AutoDetectChangesEnabled = false;
				auditContext.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;

				auditContext.AuditLogs.Add(log);

				// ✅ LƯU VÀ XỬ LÝ ERROR CỤ THỂ
				var savedCount = await auditContext.SaveChangesAsync();

				if (savedCount > 0)
				{
					_logger.LogInformation(
						"[AuditLog] ✅ Logged: {Action} on {EntityName} (ID: {EntityId}) by User {UserId}",
						action, entityName, entityId, userId);
				}
				else
				{
					_logger.LogWarning("[AuditLog] ⚠️ SaveChanges returned 0 - Log may not be saved");
				}
			}
			catch (DbUpdateException dbEx)
			{
				// ✅ LOG DATABASE ERROR CHI TIẾT
				_logger.LogError(dbEx,
					"[AuditHelper ERROR] ❌ Database error - Action: {Action}, Entity: {EntityName}, User: {UserId}",
					action, entityName, userId);

				if (dbEx.InnerException != null)
				{
					_logger.LogError("[Inner Exception]: {Message}", dbEx.InnerException.Message);
				}
			}
			catch (Exception ex)
			{
				// ✅ LOG GENERAL ERROR
				_logger.LogError(ex,
					"[AuditHelper ERROR] ❌ Failed to log audit - Action: {Action}, Entity: {EntityName}",
					action, entityName);

				if (ex.InnerException != null)
				{
					_logger.LogError("[Inner Exception]: {Message}", ex.InnerException.Message);
				}
			}
		}

		/// <summary>
		/// ✅ LOG CHI TIẾT HƠN (VỚI METADATA BỔ SUNG)
		/// </summary>
		public async System.Threading.Tasks.Task LogDetailedAsync(
			int? userId,
			string action,
			string entityName,
			int? entityId,
			object? oldValue,
			object? newValue,
			string description,
			Dictionary<string, object>? additionalData = null)
		{
			try
			{
				// ✅ THÊM ADDITIONAL DATA VÀO DESCRIPTION
				var detailedDescription = description ?? "";

				if (additionalData != null && additionalData.Count > 0)
				{
					detailedDescription += "\n\n--- Chi tiết bổ sung ---";
					foreach (var item in additionalData)
					{
						detailedDescription += $"\n• {item.Key}: {item.Value}";
					}
				}

				await LogAsync(userId, action, entityName, entityId, oldValue, newValue, detailedDescription);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "[AuditHelper] LogDetailedAsync failed");
			}
		}

		/// <summary>
		/// ✅ LOG FAILED ATTEMPTS
		/// </summary>
		public async System.Threading.Tasks.Task LogFailedAttemptAsync(
			int? userId,
			string action,
			string entityName,
			string reason,
			object? attemptData = null)
		{
			var description = $"❌ Thất bại: {reason}";

			await LogAsync(
				userId,
				$"{action}_FAILED",
				entityName,
				null,
				attemptData,
				null,
				description
			);
		}

		/// <summary>
		/// ✅ LOG VIEW ACTIONS
		/// </summary>
		public async System.Threading.Tasks.Task LogViewAsync(
			int userId,
			string entityName,
			int entityId,
			string description)
		{
			await LogAsync(
				userId,
				"VIEW",
				entityName,
				entityId,
				null,
				null,
				$"👁️ Xem thông tin: {description}"
			);
		}

		// ============================================
		// ✅ HELPER METHODS
		// ============================================

		/// <summary>
		/// Lấy IP Address chính xác (bao gồm cả proxy)
		/// </summary>
		private string GetClientIpAddress(HttpContext? context)
		{
			if (context == null)
				return "Unknown";

			try
			{
				// Kiểm tra X-Forwarded-For (cho proxy/load balancer)
				var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
				if (!string.IsNullOrEmpty(forwardedFor))
				{
					var ips = forwardedFor.Split(',');
					return ips[0].Trim();
				}

				// Kiểm tra X-Real-IP (Nginx)
				var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
				if (!string.IsNullOrEmpty(realIp))
					return realIp.Trim();

				// Fallback: Remote IP
				return context.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "[AuditHelper] Failed to get IP address");
				return "Unknown";
			}
		}

		/// <summary>
		/// Lấy thông tin location
		/// </summary>
		private string GetLocationInfo(HttpContext? context)
		{
			if (context == null)
				return "Unknown";

			try
			{
				// Lấy thông tin từ headers (nếu có)
				var country = context.Request.Headers["CloudFront-Viewer-Country"].FirstOrDefault();
				var city = context.Request.Headers["CloudFront-Viewer-City"].FirstOrDefault();

				if (!string.IsNullOrEmpty(country) && !string.IsNullOrEmpty(city))
					return $"{city}, {country}";

				if (!string.IsNullOrEmpty(country))
					return country;

				return "Unknown";
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "[AuditHelper] Failed to get location");
				return "Unknown";
			}
		}
	}
}