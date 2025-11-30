// Services/NotificationService.cs
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using AIHUBOS.Hubs;
using AIHUBOS.Models;
using TaskModel = AIHUBOS.Models.Task; // ✅ Alias cho Model Task
using SystemTask = System.Threading.Tasks.Task; // ✅ Alias cho System Task

namespace AIHUBOS.Services
{
	public interface INotificationService
	{
		SystemTask SendToUserAsync(int userId, string title, string message, string type = "info", string? link = null);
		SystemTask SendToDepartmentAsync(int departmentId, string title, string message, string type = "info", string? link = null);
		SystemTask SendToAdminsAsync(string title, string message, string type = "info", string? link = null);
		SystemTask SendBroadcastAsync(string title, string message, string type = "info", string? link = null);
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

		public async SystemTask SendToUserAsync(int userId, string title, string message, string type = "info", string? link = null)
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

			// Send via SignalR
			await _hubContext.Clients.Group($"User_{userId}").SendAsync(
				"ReceiveMessage",
				title,
				message,
				type,
				link ?? ""
			);
		}

		public async SystemTask SendToDepartmentAsync(int departmentId, string title, string message, string type = "info", string? link = null)
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

			await _hubContext.Clients.Group($"Dept_{departmentId}").SendAsync(
				"ReceiveMessage",
				title,
				message,
				type,
				link ?? ""
			);
		}

		public async SystemTask SendToAdminsAsync(string title, string message, string type = "info", string? link = null)
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

			var adminUserIds = await _context.Users
				.Include(u => u.Role)
				.Where(u => u.Role.RoleName == "Admin" && u.IsActive == true)
				.Select(u => u.UserId)
				.ToListAsync();

			foreach (var userId in adminUserIds)
			{
				_context.UserNotifications.Add(new UserNotification
				{
					NotificationId = notification.NotificationId,
					UserId = userId,
					IsRead = false
				});
			}

			await _context.SaveChangesAsync();

			await _hubContext.Clients.Group("Admins").SendAsync(
				"ReceiveMessage",
				title,
				message,
				type,
				link ?? ""
			);
		}

		public async SystemTask SendBroadcastAsync(string title, string message, string type = "info", string? link = null)
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

			await _hubContext.Clients.All.SendAsync(
				"ReceiveMessage",
				title,
				message,
				type,
				link ?? ""
			);
		}

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

		public async SystemTask MarkAsReadAsync(int userNotificationId)
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

		public async SystemTask MarkAllAsReadAsync(int userId)
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