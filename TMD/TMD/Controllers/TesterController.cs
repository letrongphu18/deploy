using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TMD.Models;
using AIHUBOS.Helpers;
using AIHUBOS.Services;

namespace TMD.Controllers
{
	public class TesterController : Controller
	{
		private readonly AihubSystemContext _context;
		private readonly AuditHelper _auditHelper;
		private readonly INotificationService _notificationService;

		public TesterController(AihubSystemContext context, AuditHelper auditHelper, INotificationService notificationService)
		{
			_context = context;
			_auditHelper = auditHelper;
			_notificationService = notificationService;
		}

		// ============================================
		// 🔐 AUTHORIZATION HELPERS - FIXED
		// ============================================
		private bool IsAuthenticated()
		{
			return HttpContext.Session.GetInt32("UserId") != null;
		}

		private bool IsTester()
		{
			var roleName = HttpContext.Session.GetString("RoleName") ?? "";
			var isTesterStr = HttpContext.Session.GetString("IsTester") ?? "";

			// ✅ Kiểm tra RoleName
			if (roleName.Equals("Tester", StringComparison.OrdinalIgnoreCase))
				return true;

			// ✅ Kiểm tra IsTester flag (cho phép Staff có IsTester=1)
			if (isTesterStr == "1"
				|| isTesterStr.Equals("true", StringComparison.OrdinalIgnoreCase)
				|| isTesterStr.Equals("yes", StringComparison.OrdinalIgnoreCase))
				return true;

			return false;
		}

		// ============================================
		// 📊 TESTER DASHBOARD
		// ============================================
		public async Task<IActionResult> Dashboard()
		{
			if (!IsAuthenticated())
			{
				TempData["Error"] = "Vui lòng đăng nhập để tiếp tục";
				return RedirectToAction("Login", "Account");
			}

			if (!IsTester())
			{
				TempData["Error"] = "Bạn không có quyền truy cập trang này. Chỉ Tester mới được phép.";
				return RedirectToAction("Dashboard", "Staff");
			}

			var testerId = HttpContext.Session.GetInt32("UserId");

			// ✅ CHỈ LẤY TASK ĐƯỢC ASSIGN CHO TESTER NÀY
			var tasksToTest = await _context.UserTasks
				.Include(ut => ut.Task)
				.Include(ut => ut.User)
					.ThenInclude(u => u.Department)
				.Where(ut => ut.Status == "Testing"
						  && ut.Task.IsActive == true
						  && ut.TesterId == testerId)  // ✅ CHỈ LẤY TASK CỦA TESTER NÀY
				.OrderByDescending(ut => ut.UpdatedAt)
				.ToListAsync();

			// STATISTICS
			ViewBag.TotalTesting = tasksToTest.Count;
			ViewBag.OverdueTasks = tasksToTest.Count(ut =>
				ut.Task.Deadline.HasValue &&
				DateTime.Now > ut.Task.Deadline.Value
			);
			ViewBag.HighPriorityTasks = tasksToTest.Count(ut =>
				ut.Task.Priority == "High"
			);

			await _auditHelper.LogViewAsync(
				testerId.Value,
				"UserTask",
				testerId.Value,
				"Xem Tester Dashboard"
			);

			return View(tasksToTest);
		}

		// ============================================
		// 📄 GET TASK DETAIL FOR TESTING
		// ============================================
		[HttpGet]
		public async Task<IActionResult> GetTaskDetail(int userTaskId)
		{
			if (!IsAuthenticated())
				return Json(new { success = false, message = "Vui lòng đăng nhập" });

			if (!IsTester())
				return Json(new { success = false, message = "Không có quyền truy cập. Chỉ Tester mới được phép." });

			try
			{
				var userTask = await _context.UserTasks
					.Include(ut => ut.Task)
					.Include(ut => ut.User)
						.ThenInclude(u => u.Department)
					.FirstOrDefaultAsync(ut => ut.UserTaskId == userTaskId);

				if (userTask == null)
					return Json(new { success = false, message = "Không tìm thấy task" });

				// ✅ CHỈ CHO PHÉP XEM TASK Ở TRẠNG THÁI "Testing"
				if (userTask.Status != "Testing")
					return Json(new { success = false, message = "Task không ở trạng thái chờ test" });

				var task = userTask.Task;
				bool isOverdue = task.Deadline.HasValue && DateTime.Now > task.Deadline.Value;

				return Json(new
				{
					success = true,
					task = new
					{
						userTaskId = userTask.UserTaskId,
						taskName = task.TaskName,
						description = task.Description ?? "Không có mô tả",
						platform = task.Platform ?? "N/A",
						priority = task.Priority ?? "Medium",
						deadline = task.Deadline.HasValue ? task.Deadline.Value.ToString("dd/MM/yyyy HH:mm") : "Không có deadline",
						reportLink = userTask.ReportLink ?? "",
						status = userTask.Status,
						assignedTo = userTask.User.FullName,
						assignedToEmail = userTask.User.Email,
						department = userTask.User.Department?.DepartmentName ?? "N/A",
						isOverdue = isOverdue,
						createdAt = userTask.CreatedAt.HasValue ? userTask.CreatedAt.Value.ToString("dd/MM/yyyy HH:mm") : "",
						updatedAt = userTask.UpdatedAt.HasValue ? userTask.UpdatedAt.Value.ToString("dd/MM/yyyy HH:mm") : "Chưa cập nhật"
					}
				});
			}
			catch (Exception ex)
			{
				return Json(new { success = false, message = $"Lỗi: {ex.Message}" });
			}
		}

		// ============================================
		// ✅ REVIEW TASK: DONE / REOPEN
		// ============================================
		[HttpPost]
		public async Task<IActionResult> ReviewTask([FromBody] ReviewTaskRequest request)
		{
			if (!IsAuthenticated())
				return Json(new { success = false, message = "Vui lòng đăng nhập" });

			if (!IsTester())
				return Json(new { success = false, message = "Không có quyền thực hiện. Chỉ Tester mới được phép." });

			var testerId = HttpContext.Session.GetInt32("UserId").Value;

			var userTask = await _context.UserTasks
				.Include(ut => ut.Task)
				.Include(ut => ut.User)
				.FirstOrDefaultAsync(ut => ut.UserTaskId == request.UserTaskId);

			if (userTask == null)
				return Json(new { success = false, message = "Không tìm thấy task" });

			// ✅ CHỈ CHO PHÉP REVIEW TASK Ở TRẠNG THÁI "Testing"
			if (userTask.Status != "Testing")
				return Json(new { success = false, message = "Chỉ có thể review task đang ở trạng thái Testing" });

			// ✅ VALIDATE ACTION
			if (request.Action != "Done" && request.Action != "Reopen")
				return Json(new { success = false, message = "Action không hợp lệ. Chỉ chấp nhận: Done hoặc Reopen" });

			// ✅ REQUIRE NOTE FOR REOPEN
			if (request.Action == "Reopen" && string.IsNullOrWhiteSpace(request.Note))
				return Json(new { success = false, message = "Vui lòng nhập lý do reopen" });

			try
			{
				var oldStatus = userTask.Status;
				userTask.Status = request.Action;
				userTask.UpdatedAt = DateTime.Now;

				await _context.SaveChangesAsync();

				// ✅ LOG AUDIT
				await _auditHelper.LogDetailedAsync(
					testerId,
					"REVIEW",
					"UserTask",
					userTask.UserTaskId,
					new { Status = oldStatus },
					new { Status = request.Action },
					$"Tester {(request.Action == "Done" ? "approve" : "reopen")} task: {userTask.Task.TaskName}",
					new Dictionary<string, object>
					{
						{ "TaskName", userTask.Task.TaskName },
						{ "AssignedTo", userTask.User.FullName },
						{ "Note", request.Note ?? "" },
						{ "TesterId", testerId }
					}
				);

				// ✅ GỬI THÔNG BÁO
				if (request.Action == "Done")
				{
					// Gửi cho Dev
					await _notificationService.SendToUserAsync(
						userTask.UserId,
						"Task đã hoàn thành",
						$"Task '{userTask.Task.TaskName}' đã được Tester approve ",
						"success",
						"/Staff/MyTasks"
					);

					// Gửi cho Admin
					await _notificationService.SendToAdminsAsync(
						"Task hoàn thành",
						$"Task '{userTask.Task.TaskName}' đã được test và hoàn thành bởi {userTask.User.FullName}",
						"success",
						"/Admin/TaskList"
					);
				}
				else if (request.Action == "Reopen")
				{
					// Gửi cho Dev với lý do
					await _notificationService.SendToUserAsync(
						userTask.UserId,
						"Task bị reopen",
						$"Task '{userTask.Task.TaskName}' cần sửa lại.\n\n Lý do: {request.Note}",
						"warning",
						"/Staff/MyTasks"
					);

					// Gửi cho Admin
					await _notificationService.SendToAdminsAsync(
						"Task bị reopen",
						$"Task '{userTask.Task.TaskName}' của {userTask.User.FullName} bị reopen.\nLý do: {request.Note}",
						"warning",
						"/Admin/TaskList"
					);
				}

				return Json(new
				{
					success = true,
					message = request.Action == "Done"
						? " Task đã được approve và hoàn thành"
						: " Task đã được reopen. Dev sẽ nhận được thông báo."
				});
			}
			catch (Exception ex)
			{
				await _auditHelper.LogFailedAttemptAsync(
					testerId,
					"REVIEW",
					"UserTask",
					$"Exception: {ex.Message}",
					new { UserTaskId = request.UserTaskId, Error = ex.ToString() }
				);

				return Json(new { success = false, message = $"Có lỗi xảy ra: {ex.Message}" });
			}
		}

		// ============================================
		// 📊 GET STATISTICS
		// ============================================
		[HttpGet]
		public async Task<IActionResult> GetStatistics()
		{
			if (!IsAuthenticated())
				return Json(new { success = false, message = "Vui lòng đăng nhập" });

			if (!IsTester())
				return Json(new { success = false, message = "Không có quyền truy cập" });

			try
			{
				var testerId = HttpContext.Session.GetInt32("UserId").Value;
				var today = DateOnly.FromDateTime(DateTime.Now);
				var thisWeekStart = today.AddDays(-(int)DateTime.Now.DayOfWeek);

				var stats = new
				{
					// ✅ CHỈ THỐNG KÊ TASK CỦA TESTER NÀY
					totalTesting = await _context.UserTasks
						.CountAsync(ut => ut.Status == "Testing" && ut.TesterId == testerId),
					reviewedToday = await _context.UserTasks
						.CountAsync(ut => (ut.Status == "Done" || ut.Status == "Reopen")
							&& ut.TesterId == testerId
							&& ut.UpdatedAt.HasValue
							&& DateOnly.FromDateTime(ut.UpdatedAt.Value) == today),
					reviewedThisWeek = await _context.UserTasks
						.CountAsync(ut => (ut.Status == "Done" || ut.Status == "Reopen")
							&& ut.TesterId == testerId
							&& ut.UpdatedAt.HasValue
							&& DateOnly.FromDateTime(ut.UpdatedAt.Value) >= thisWeekStart),
					overdueCount = await _context.UserTasks
						.CountAsync(ut => ut.Status == "Testing"
							&& ut.TesterId == testerId
							&& ut.Task.Deadline.HasValue
							&& DateTime.Now > ut.Task.Deadline.Value)
				};

				return Json(new { success = true, stats });
			}
			catch (Exception ex)
			{
				return Json(new { success = false, message = ex.Message });
			}
		}

		// ============================================
		// 📝 REQUEST MODEL
		// ============================================
		public class ReviewTaskRequest
		{
			public int UserTaskId { get; set; }
			public string Action { get; set; } = ""; // "Done" or "Reopen"
			public string? Note { get; set; }
		}
	}
}