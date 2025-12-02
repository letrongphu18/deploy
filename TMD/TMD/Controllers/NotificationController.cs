// Controllers/NotificationController.cs
using Microsoft.AspNetCore.Mvc;
using AIHUBOS.Services;

namespace TMD.Controllers
{
	public class NotificationController : Controller
	{
		private readonly INotificationService _notificationService;

		public NotificationController(INotificationService notificationService)
		{
			_notificationService = notificationService;
		}

		[HttpGet]
		public async Task<IActionResult> GetUserNotifications(int skip = 0, int take = 20)
		{
			var userId = HttpContext.Session.GetInt32("UserId");
			if (!userId.HasValue)
				return Json(new { success = false, message = "Unauthorized" });

			try
			{
				var notifications = await _notificationService.GetUserNotificationsAsync(userId.Value, skip, take);

				var result = notifications.Select(un => new
				{
					userNotificationId = un.UserNotificationId,
					notificationId = un.NotificationId,
					title = un.Notification.Title,
					message = un.Notification.Message,
					type = un.Notification.Type,
					link = un.Notification.Link,
					isRead = un.IsRead,
					createdAt = un.Notification.CreatedAt,
					readAt = un.ReadAt
				}).ToList();

				return Json(new { success = true, notifications = result });
			}
			catch (Exception ex)
			{
				return Json(new { success = false, message = ex.Message });
			}
		}

		[HttpGet]
		public async Task<IActionResult> GetUnreadCount()
		{
			var userId = HttpContext.Session.GetInt32("UserId");
			if (!userId.HasValue)
				return Json(new { success = false, count = 0 });

			try
			{
				var count = await _notificationService.GetUnreadCountAsync(userId.Value);
				return Json(new { success = true, count });
			}
			catch (Exception ex)
			{
				return Json(new { success = false, count = 0, message = ex.Message });
			}
		}

		[HttpPost]
		public async Task<IActionResult> MarkAsRead([FromBody] MarkAsReadRequest request)
		{
			try
			{
				await _notificationService.MarkAsReadAsync(request.UserNotificationId);
				return Json(new { success = true, message = "Đánh dấu đã đọc thành công" });
			}
			catch (Exception ex)
			{
				return Json(new { success = false, message = ex.Message });
			}
		}

		[HttpPost]
		public async Task<IActionResult> MarkAllAsRead()
		{
			var userId = HttpContext.Session.GetInt32("UserId");
			if (!userId.HasValue)
				return Json(new { success = false, message = "Unauthorized" });

			try
			{
				await _notificationService.MarkAllAsReadAsync(userId.Value);
				return Json(new { success = true, message = "Đánh dấu tất cả đã đọc thành công" });
			}
			catch (Exception ex)
			{
				return Json(new { success = false, message = ex.Message });
			}
		}

		public class MarkAsReadRequest
		{
			public int UserNotificationId { get; set; }
		}
	}
}