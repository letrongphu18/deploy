using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using AIHUBOS.Hubs;
using AIHUBOS.Models;
using System.Linq;
using System.Collections.Generic;
using TaskModel = AIHUBOS.Models.Task;
using SystemTask = System.Threading.Tasks.Task;

namespace AIHUBOS.Services
{
	public interface INotificationService
	{
		// ✅ CŨ - GIỮ NGUYÊN ĐỂ TƯƠNG THÍCH
		SystemTask SendToUserAsync(int userId, string title, string message, string type = "info", string? link = null);
		SystemTask SendToDepartmentAsync(int departmentId, string title, string message, string type = "info", string? link = null);
		SystemTask SendToAdminsAsync(string title, string message, string type = "info", string? link = null);
		SystemTask SendBroadcastAsync(string title, string message, string type = "info", string? link = null);

		// ✅ MỚI - DYNAMIC ROLE-BASED
		SystemTask SendToRoleAsync(string roleName, string title, string message, string type = "info", string? link = null);
		SystemTask SendToRolesAsync(List<string> roleNames, string title, string message, string type = "info", string? link = null);

		// ✅ EXISTING READ METHODS
		System.Threading.Tasks.Task<List<UserNotification>> GetUserNotificationsAsync(int userId, int skip = 0, int take = 20);
		System.Threading.Tasks.Task<int> GetUnreadCountAsync(int userId);
		SystemTask MarkAsReadAsync(int userNotificationId);
		SystemTask MarkAllAsReadAsync(int userId);
	}

	public class NotificationService : INotificationService
	{
		private readonly AihubSystemContext _context;
		private readonly IHubContext<NotificationHub> _hubContext;

		public NotificationService(AihubSystemContext context, IHubContext<NotificationHub> hubContext)
		{
			_context = context;
			_hubContext = hubContext;
		}

		// ============================================
		// Helper: thêm UserNotification cho list userIds nếu chưa tồn tại
		// ============================================
		private async System.Threading.Tasks.Task AddUserNotificationsIfNotExistAsync(Notification notification, List<int> userIds)
		{
			if (userIds == null || userIds.Count == 0)
				return;

			var existing = await _context.UserNotifications
				.Where(un => un.NotificationId == notification.NotificationId && userIds.Contains(un.UserId))
				.Select(un => un.UserId)
				.ToListAsync();

			var toAdd = userIds.Except(existing).ToList();

			foreach (var uid in toAdd)
			{
				_context.UserNotifications.Add(new UserNotification
				{
					NotificationId = notification.NotificationId,
					UserId = uid,
					IsRead = false
				});
			}
		}

		// ============================================
		// ✅ DYNAMIC ROLE-BASED NOTIFICATION (single role)
		// - đảm bảo Admin(s) cũng nhận
		// - tránh duplicate UserNotification
		// ============================================
		public async System.Threading.Tasks.Task SendToRoleAsync(string roleName, string title, string message, string type = "info", string? link = null)
		{
			// Create notification record
			var notification = new Notification
			{
				Title = title,
				Message = message,
				Type = type,
				Link = link,
				CreatedAt = DateTime.UtcNow,
				IsBroadcast = false
			};

			_context.Notifications.Add(notification);
			await _context.SaveChangesAsync();

			// 1) Lấy userId của role mục tiêu
			var roleUserIds = await _context.Users
				.Include(u => u.Role)
				.Where(u => u.Role != null && u.Role.RoleName == roleName && u.IsActive == true)
				.Select(u => u.UserId)
				.ToListAsync();

			// 2) Lấy admin userIds (dynamic: nếu có nhiều role admin, thay đổi logic ở đây)
			var adminUserIds = await _context.Users
				.Include(u => u.Role)
				.Where(u => u.Role != null && u.Role.RoleName == "Admin" && u.IsActive == true)
				.Select(u => u.UserId)
				.ToListAsync();

			// 3) Kết hợp và thêm UserNotification (không duplicate)
			var allTargetUserIds = roleUserIds.Union(adminUserIds).ToList();
			await AddUserNotificationsIfNotExistAsync(notification, allTargetUserIds);
			await _context.SaveChangesAsync();

			// 4) Gửi SignalR tới role group và tới Role_Admin để admin online nhận realtime
			await _hubContext.Clients.Group($"Role_{roleName}").SendAsync("ReceiveMessage", title, message, type, link ?? "");
			await _hubContext.Clients.Group($"Role_Admin").SendAsync("ReceiveMessage", title, message, type, link ?? "");
		}

		// ============================================
		// ✅ GỬI ĐẾN NHIỀU ROLE CÙNG LÚC (single notification record)
		// - tạo 1 Notification duy nhất, thêm UserNotification cho tất cả user thuộc các role + admin(s)
		// - gửi SignalR tới từng role group và Role_Admin
		// ============================================
		public async System.Threading.Tasks.Task SendToRolesAsync(List<string> roleNames, string title, string message, string type = "info", string? link = null)
		{
			if (roleNames == null || roleNames.Count == 0)
				return;

			// Create single notification record for all roles
			var notification = new Notification
			{
				Title = title,
				Message = message,
				Type = type,
				Link = link,
				CreatedAt = DateTime.UtcNow,
				IsBroadcast = false
			};

			_context.Notifications.Add(notification);
			await _context.SaveChangesAsync();

			// Lấy tất cả user thuộc các role
			var roleUserIds = await _context.Users
				.Include(u => u.Role)
				.Where(u => u.Role != null && roleNames.Contains(u.Role.RoleName) && u.IsActive == true)
				.Select(u => u.UserId)
				.ToListAsync();

			// Lấy admin userIds
			var adminUserIds = await _context.Users
				.Include(u => u.Role)
				.Where(u => u.Role != null && u.Role.RoleName == "Admin" && u.IsActive == true)
				.Select(u => u.UserId)
				.ToListAsync();

			var allTargetUserIds = roleUserIds.Union(adminUserIds).ToList();

			// Thêm UserNotification cho tất cả (không duplicate)
			await AddUserNotificationsIfNotExistAsync(notification, allTargetUserIds);
			await _context.SaveChangesAsync();

			// Gửi SignalR tới từng role group
			foreach (var rn in roleNames)
			{
				await _hubContext.Clients.Group($"Role_{rn}").SendAsync("ReceiveMessage", title, message, type, link ?? "");
			}

			// Gửi tới admin group
			await _hubContext.Clients.Group($"Role_Admin").SendAsync("ReceiveMessage", title, message, type, link ?? "");
		}

		// ============================================
		// ✅ CŨ - GỬI TỚI 1 USER
		// ============================================
		public async System.Threading.Tasks.Task SendToUserAsync(int userId, string title, string message, string type = "info", string? link = null)
		{
			var notification = new Notification
			{
				Title = title,
				Message = message,
				Type = type,
				Link = link,
				CreatedAt = DateTime.UtcNow,
				IsBroadcast = false
			};

			_context.Notifications.Add(notification);
			await _context.SaveChangesAsync();

			var userNotification = new UserNotification
			{
				NotificationId = notification.NotificationId,
				UserId = userId,
				IsRead = false
			};

			_context.UserNotifications.Add(userNotification);
			await _context.SaveChangesAsync();

			await _hubContext.Clients.Group($"User_{userId}").SendAsync("ReceiveMessage", title, message, type, link ?? "");
		}

		// ============================================
		// ✅ GỬI TỚI PHÒNG BAN
		// ============================================
		public async System.Threading.Tasks.Task SendToDepartmentAsync(int departmentId, string title, string message, string type = "info", string? link = null)
		{
			var notification = new Notification
			{
				Title = title,
				Message = message,
				Type = type,
				Link = link,
				TargetDepartmentId = departmentId,
				CreatedAt = DateTime.UtcNow,
				IsBroadcast = false
			};

			_context.Notifications.Add(notification);
			await _context.SaveChangesAsync();

			var userIds = await _context.Users
				.Where(u => u.DepartmentId == departmentId && u.IsActive == true)
				.Select(u => u.UserId)
				.ToListAsync();

			foreach (var userId in userIds)
			{
				_context.UserNotifications.Add(new UserNotification
				{
					NotificationId = notification.NotificationId,
					UserId = userId,
					IsRead = false
				});
			}

			await _context.SaveChangesAsync();

			await _hubContext.Clients.Group($"Dept_{departmentId}").SendAsync("ReceiveMessage", title, message, type, link ?? "");
		}

		// ============================================
		// ✅ GỬI CHO ADMIN (BACKWARD COMPATIBLE)
		// ============================================
		public async System.Threading.Tasks.Task SendToAdminsAsync(string title, string message, string type = "info", string? link = null)
		{
			// Sử dụng dynamic role-based method
			await SendToRoleAsync("Admin", title, message, type, link);
		}

		// ============================================
		// ✅ BROADCAST TO ALL USERS
		// ============================================
		public async System.Threading.Tasks.Task SendBroadcastAsync(string title, string message, string type = "info", string? link = null)
		{
			var notification = new Notification
			{
				Title = title,
				Message = message,
				Type = type,
				Link = link,
				CreatedAt = DateTime.UtcNow,
				IsBroadcast = true
			};

			_context.Notifications.Add(notification);
			await _context.SaveChangesAsync();

			var allUserIds = await _context.Users
				.Where(u => u.IsActive == true)
				.Select(u => u.UserId)
				.ToListAsync();

			foreach (var userId in allUserIds)
			{
				_context.UserNotifications.Add(new UserNotification
				{
					NotificationId = notification.NotificationId,
					UserId = userId,
					IsRead = false
				});
			}

			await _context.SaveChangesAsync();

			await _hubContext.Clients.Group("AllUsers").SendAsync("ReceiveMessage", title, message, type, link ?? "");
		}

		// ============================================
		// Read helpers
		// ============================================
		public async System.Threading.Tasks.Task<List<UserNotification>> GetUserNotificationsAsync(int userId, int skip = 0, int take = 20)
		{
			return await _context.UserNotifications
				.Include(un => un.Notification)
				.Where(un => un.UserId == userId)
				.OrderByDescending(un => un.Notification.CreatedAt)
				.Skip(skip)
				.Take(take)
				.ToListAsync();
		}

		public async System.Threading.Tasks.Task<int> GetUnreadCountAsync(int userId)
		{
			return await _context.UserNotifications
				.CountAsync(un => un.UserId == userId && !un.IsRead);
		}

		public async System.Threading.Tasks.Task MarkAsReadAsync(int userNotificationId)
		{
			var userNotif = await _context.UserNotifications
				.FirstOrDefaultAsync(un => un.UserNotificationId == userNotificationId);

			if (userNotif != null && !userNotif.IsRead)
			{
				userNotif.IsRead = true;
				userNotif.ReadAt = DateTime.UtcNow;
				await _context.SaveChangesAsync();
			}
		}

		public async System.Threading.Tasks.Task MarkAllAsReadAsync(int userId)
		{
			var unreadNotifs = await _context.UserNotifications
				.Where(un => un.UserId == userId && !un.IsRead)
				.ToListAsync();

			foreach (var notif in unreadNotifs)
			{
				notif.IsRead = true;
				notif.ReadAt = DateTime.UtcNow;
			}

			await _context.SaveChangesAsync();
		}
	}
}
