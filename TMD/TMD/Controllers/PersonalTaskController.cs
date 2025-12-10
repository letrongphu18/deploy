using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AIHUBOS.Helpers;
using TMD.Models;
using System.Text.Json;

namespace TMD.Controllers
{
	public class PersonalTaskController : Controller
	{
		private readonly AihubSystemContext _context;
		private readonly AuditHelper _auditHelper;
		private readonly ILogger<PersonalTaskController> _logger;

		public PersonalTaskController(
			AihubSystemContext context,
			AuditHelper auditHelper,
			ILogger<PersonalTaskController> logger)
		{
			_context = context;
			_auditHelper = auditHelper;
			_logger = logger;
		}

		// ============================================
		// HELPER METHODS
		// ============================================
		private bool IsAdminOrManager()
		{
			var role = HttpContext.Session.GetString("RoleName");
			return role == "Admin" || role == "Manager" || role == "SupAdmin";
		}

		private bool IsAuthenticated()
		{
			return HttpContext.Session.GetInt32("UserId") != null;
		}

		private static string GetStatusColor(string? status)
		{
			return status switch
			{
				"TODO" => "#6b7280",
				"InProgress" => "#f59e0b",
				"Testing" => "#3b82f6",
				"Done" => "#10b981",
				"Reopen" => "#ef4444",
				_ => "#6b7280"
			};
		}

		// ============================================
		// MY TASKS - KANBAN BOARD VIEW (DEFAULT)
		// ============================================
		[HttpGet]
		public async Task<IActionResult> Index(string? view)
		{
			if (!IsAdminOrManager())
				return RedirectToAction("Login", "Account");

			var userId = HttpContext.Session.GetInt32("UserId").Value;

			await _auditHelper.LogViewAsync(
				userId,
				"PersonalTask",
				0,
				"Xem danh sách công việc cá nhân"
			);

			// Lấy tất cả personal tasks của user
			var myTasks = await _context.UserTasks
				.Include(ut => ut.Task)
				.Where(ut => ut.UserId == userId &&
							ut.Task.IsActive == true)
				.OrderByDescending(ut => ut.CreatedAt)
				.ToListAsync();

			// Statistics
			ViewBag.TotalTasks = myTasks.Count;
			ViewBag.TodoCount = myTasks.Count(ut => ut.Status == "TODO");
			ViewBag.InProgressCount = myTasks.Count(ut => ut.Status == "InProgress");
			ViewBag.TestingCount = myTasks.Count(ut => ut.Status == "Testing");
			ViewBag.DoneCount = myTasks.Count(ut => ut.Status == "Done");
			ViewBag.ReopenCount = myTasks.Count(ut => ut.Status == "Reopen");

			// Overdue tasks
			ViewBag.OverdueCount = myTasks.Count(ut =>
				ut.Task.Deadline.HasValue &&
				ut.Task.Deadline.Value < DateTime.Now &&
				ut.Status != "Done");

			// Completion rate
			ViewBag.CompletionRate = myTasks.Count > 0
				? Math.Round((double)ViewBag.DoneCount / myTasks.Count * 100, 1)
				: 0;

			// Priority statistics
			ViewBag.HighPriorityCount = myTasks.Count(ut => ut.Task.Priority == "High" && ut.Status != "Done");
			ViewBag.MediumPriorityCount = myTasks.Count(ut => ut.Task.Priority == "Medium" && ut.Status != "Done");
			ViewBag.LowPriorityCount = myTasks.Count(ut => ut.Task.Priority == "Low" && ut.Status != "Done");

			ViewBag.CurrentView = view ?? "kanban";

			return View(myTasks);
		}

		// ============================================
		// LIST VIEW
		// ============================================
		// ============================================
		// LIST VIEW
		// ============================================
		[HttpGet]
		public async Task<IActionResult> ListView()
		{
			if (!IsAdminOrManager())
				return RedirectToAction("Login", "Account");

			var userId = HttpContext.Session.GetInt32("UserId").Value;

			var myTasks = await _context.UserTasks
				.Include(ut => ut.Task)
				.Where(ut => ut.UserId == userId &&
							ut.Task.IsActive == true)
				.ToListAsync();

			// Custom sorting: Done last, then by deadline, then by priority
			var sortedTasks = myTasks
				.OrderBy(ut => ut.Status == "Done" ? 1 : 0) // Done xuống cuối
				.ThenBy(ut => ut.Task.Deadline ?? DateTime.MaxValue) // Deadline gần nhất lên đầu
				.ThenByDescending(ut => ut.Task.Priority == "High" ? 3 : ut.Task.Priority == "Medium" ? 2 : 1) // Priority cao lên đầu
				.ToList();

			ViewBag.TotalTasks = myTasks.Count;
			ViewBag.TodoCount = myTasks.Count(ut => ut.Status == "TODO");
			ViewBag.InProgressCount = myTasks.Count(ut => ut.Status == "InProgress");
			ViewBag.TestingCount = myTasks.Count(ut => ut.Status == "Testing");
			ViewBag.DoneCount = myTasks.Count(ut => ut.Status == "Done");
			ViewBag.ReopenCount = myTasks.Count(ut => ut.Status == "Reopen");
			ViewBag.OverdueCount = myTasks.Count(ut =>
				ut.Task.Deadline.HasValue &&
				ut.Task.Deadline.Value < DateTime.Now &&
				ut.Status != "Done");
			ViewBag.CompletionRate = myTasks.Count > 0
				? Math.Round((double)ViewBag.DoneCount / myTasks.Count * 100, 1)
				: 0;
			ViewBag.CurrentView = "list";

			return View("Index", sortedTasks);
		}

		// ============================================
		// CALENDAR VIEW
		// ============================================
		[HttpGet]
		public async Task<IActionResult> CalendarView()
		{
			if (!IsAdminOrManager())
				return RedirectToAction("Login", "Account");

			var userId = HttpContext.Session.GetInt32("UserId").Value;

			var myTasks = await _context.UserTasks
				.Include(ut => ut.Task)
				.Where(ut => ut.UserId == userId &&
							ut.Task.IsActive == true)
				.ToListAsync();

			ViewBag.TotalTasks = myTasks.Count;
			ViewBag.TodoCount = myTasks.Count(ut => ut.Status == "TODO");
			ViewBag.InProgressCount = myTasks.Count(ut => ut.Status == "InProgress");
			ViewBag.TestingCount = myTasks.Count(ut => ut.Status == "Testing");
			ViewBag.DoneCount = myTasks.Count(ut => ut.Status == "Done");
			ViewBag.ReopenCount = myTasks.Count(ut => ut.Status == "Reopen");
			ViewBag.OverdueCount = myTasks.Count(ut =>
				ut.Task.Deadline.HasValue &&
				ut.Task.Deadline.Value < DateTime.Now &&
				ut.Status != "Done");
			ViewBag.CompletionRate = myTasks.Count > 0
				? Math.Round((double)ViewBag.DoneCount / myTasks.Count * 100, 1)
				: 0;
			ViewBag.CurrentView = "calendar";

			return View("Index", myTasks);
		}

		// ============================================
		// GET TASKS FOR CALENDAR (JSON)
		// ============================================
		[HttpGet]
		public async Task<IActionResult> GetCalendarTasks()
		{
			if (!IsAdminOrManager())
				return Json(new { success = false, message = "Không có quyền" });

			var userId = HttpContext.Session.GetInt32("UserId").Value;

			var tasks = await _context.UserTasks
				.Include(ut => ut.Task)
				.Where(ut => ut.UserId == userId &&
							ut.Task.IsActive == true &&
							ut.Task.Deadline.HasValue)
				.Select(ut => new
				{
					id = ut.UserTaskId,
					title = ut.Task.TaskName,
					start = ut.Task.Deadline.Value.ToString("yyyy-MM-dd"),
					backgroundColor = GetStatusColor(ut.Status),
					borderColor = GetStatusColor(ut.Status),
					extendedProps = new
					{
						status = ut.Status,
						priority = ut.Task.Priority,
						description = ut.Task.Description
					}
				})
				.ToListAsync();

			return Json(tasks);
		}

		// ============================================
		// CREATE PERSONAL TASK
		// ============================================
		[HttpPost]
		public async Task<IActionResult> CreateTask([FromBody] CreatePersonalTaskRequest request)
		{
			if (!IsAdminOrManager())
				return Json(new { success = false, message = "Không có quyền" });

			if (string.IsNullOrWhiteSpace(request.TaskName))
				return Json(new { success = false, message = "Tên công việc không được để trống" });

			try
			{
				var userId = HttpContext.Session.GetInt32("UserId").Value;

				// Create Task
				var task = new TMD.Models.Task
				{
					TaskName = request.TaskName.Trim(),
					Description = request.Description?.Trim(),
					Platform = request.Platform?.Trim(),
					Priority = request.Priority ?? "Medium",
					Deadline = request.Deadline,
					IsActive = true,
					CreatedAt = DateTime.Now,
					UpdatedAt = DateTime.Now
				};

				_context.Tasks.Add(task);
				await _context.SaveChangesAsync();

				// Create UserTask
				var userTask = new UserTask
				{
					UserId = userId,
					TaskId = task.TaskId,
					Status = "TODO",
					CreatedAt = DateTime.Now,
					UpdatedAt = DateTime.Now
				};

				_context.UserTasks.Add(userTask);
				await _context.SaveChangesAsync();

				await _auditHelper.LogAsync(
					userId,
					"CREATE",
					"PersonalTask",
					task.TaskId,
					null,
					new { task.TaskName, task.Priority, task.Deadline },
					$"Tạo công việc cá nhân: {task.TaskName}"
				);

				return Json(new
				{
					success = true,
					message = "Tạo công việc thành công!",
					task = new
					{
						userTaskId = userTask.UserTaskId,
						taskId = task.TaskId,
						taskName = task.TaskName,
						description = task.Description,
						platform = task.Platform,
						priority = task.Priority,
						deadline = task.Deadline?.ToString("yyyy-MM-dd"),
						status = userTask.Status,
						createdAt = task.CreatedAt?.ToString("dd/MM/yyyy HH:mm")
					}
				});
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "[CreateTask] Error creating personal task");
				var innerException = ex.InnerException?.Message ?? "No inner exception";
				_logger.LogError($"Inner Exception: {innerException}");

				return Json(new
				{
					success = false,
					message = $"Có lỗi: {ex.Message}",
					detail = innerException
				});
			}
		}

		// ============================================
		// UPDATE TASK STATUS (DRAG & DROP)
		// ============================================
		[HttpPost]
		public async Task<IActionResult> UpdateTaskStatus([FromBody] UpdateTaskStatusRequest request)
		{
			if (!IsAdminOrManager())
				return Json(new { success = false, message = "Không có quyền" });

			try
			{
				var userId = HttpContext.Session.GetInt32("UserId").Value;

				var userTask = await _context.UserTasks
					.Include(ut => ut.Task)
					.FirstOrDefaultAsync(ut => ut.UserTaskId == request.UserTaskId && ut.UserId == userId);

				if (userTask == null)
					return Json(new { success = false, message = "Không tìm thấy công việc" });

				var oldStatus = userTask.Status;
				userTask.Status = request.NewStatus;
				userTask.UpdatedAt = DateTime.Now;

				await _context.SaveChangesAsync();

				await _auditHelper.LogAsync(
					userId,
					"UPDATE",
					"PersonalTask",
					userTask.TaskId,
					new { Status = oldStatus },
					new { Status = request.NewStatus },
					$"Cập nhật trạng thái công việc: {userTask.Task.TaskName}"
				);

				return Json(new
				{
					success = true,
					message = "Cập nhật trạng thái thành công!"
				});
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "[UpdateTaskStatus] Error");
				return Json(new { success = false, message = $"Có lỗi: {ex.Message}" });
			}
		}

		// ============================================
		// UPDATE TASK DETAILS
		// ============================================
		[HttpPost]
		public async Task<IActionResult> UpdateTask([FromBody] UpdatePersonalTaskRequest request)
		{
			if (!IsAdminOrManager())
				return Json(new { success = false, message = "Không có quyền" });

			if (string.IsNullOrWhiteSpace(request.TaskName))
				return Json(new { success = false, message = "Tên công việc không được để trống" });

			try
			{
				var userId = HttpContext.Session.GetInt32("UserId").Value;

				var userTask = await _context.UserTasks
					.Include(ut => ut.Task)
					.FirstOrDefaultAsync(ut => ut.UserTaskId == request.UserTaskId && ut.UserId == userId);

				if (userTask == null)
					return Json(new { success = false, message = "Không tìm thấy công việc" });

				var task = userTask.Task;

				var oldValues = new
				{
					task.TaskName,
					task.Description,
					task.Platform,
					task.Priority,
					task.Deadline
				};

				task.TaskName = request.TaskName.Trim();
				task.Description = request.Description?.Trim();
				task.Platform = request.Platform?.Trim();
				task.Priority = request.Priority ?? "Medium";
				task.Deadline = request.Deadline;
				task.UpdatedAt = DateTime.Now;

				await _context.SaveChangesAsync();

				await _auditHelper.LogAsync(
					userId,
					"UPDATE",
					"PersonalTask",
					task.TaskId,
					oldValues,
					new { task.TaskName, task.Description, task.Platform, task.Priority, task.Deadline },
					$"Cập nhật công việc: {task.TaskName}"
				);

				return Json(new
				{
					success = true,
					message = "Cập nhật công việc thành công!"
				});
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "[UpdateTask] Error");
				return Json(new { success = false, message = $"Có lỗi: {ex.Message}" });
			}
		}

		// ============================================
		// DELETE TASK
		// ============================================
		[HttpPost]
		public async Task<IActionResult> DeleteTask([FromBody] DeleteTaskRequest request)
		{
			if (!IsAdminOrManager())
				return Json(new { success = false, message = "Không có quyền" });

			try
			{
				var userId = HttpContext.Session.GetInt32("UserId").Value;

				var userTask = await _context.UserTasks
					.Include(ut => ut.Task)
					.FirstOrDefaultAsync(ut => ut.UserTaskId == request.UserTaskId && ut.UserId == userId);

				if (userTask == null)
					return Json(new { success = false, message = "Không tìm thấy công việc" });

				var taskName = userTask.Task.TaskName;

				// Soft delete
				userTask.Task.IsActive = false;
				userTask.Task.UpdatedAt = DateTime.Now;

				await _context.SaveChangesAsync();

				await _auditHelper.LogAsync(
					userId,
					"DELETE",
					"PersonalTask",
					userTask.TaskId,
					new { TaskName = taskName },
					null,
					$"Xóa công việc: {taskName}"
				);

				return Json(new
				{
					success = true,
					message = "Xóa công việc thành công!"
				});
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "[DeleteTask] Error");
				return Json(new { success = false, message = $"Có lỗi: {ex.Message}" });
			}
		}

		// ============================================
		// GET TASK DETAIL
		// ============================================
		[HttpGet]
		public async Task<IActionResult> GetTaskDetail(int userTaskId)
		{
			if (!IsAdminOrManager())
				return Json(new { success = false, message = "Không có quyền" });

			try
			{
				var userId = HttpContext.Session.GetInt32("UserId").Value;

				var userTask = await _context.UserTasks
					.Include(ut => ut.Task)
					.FirstOrDefaultAsync(ut => ut.UserTaskId == userTaskId && ut.UserId == userId);

				if (userTask == null)
					return Json(new { success = false, message = "Không tìm thấy công việc" });

				var task = userTask.Task;

				return Json(new
				{
					success = true,
					task = new
					{
						userTaskId = userTask.UserTaskId,
						taskId = task.TaskId,
						taskName = task.TaskName,
						description = task.Description,
						platform = task.Platform,
						priority = task.Priority,
						deadline = task.Deadline?.ToString("yyyy-MM-dd"),
						status = userTask.Status,
						reportLink = userTask.ReportLink,
						createdAt = task.CreatedAt?.ToString("dd/MM/yyyy HH:mm"),
						updatedAt = task.UpdatedAt?.ToString("dd/MM/yyyy HH:mm")
					}
				});
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "[GetTaskDetail] Error");
				return Json(new { success = false, message = $"Có lỗi: {ex.Message}" });
			}
		}

		// ============================================
		// REQUEST MODELS
		// ============================================
		public class CreatePersonalTaskRequest
		{
			public string TaskName { get; set; } = string.Empty;
			public string? Description { get; set; }
			public string? Platform { get; set; }
			public string? Priority { get; set; }
			public DateTime? Deadline { get; set; }
		}

		public class UpdatePersonalTaskRequest
		{
			public int UserTaskId { get; set; }
			public string TaskName { get; set; } = string.Empty;
			public string? Description { get; set; }
			public string? Platform { get; set; }
			public string? Priority { get; set; }
			public DateTime? Deadline { get; set; }
		}

		public class UpdateTaskStatusRequest
		{
			public int UserTaskId { get; set; }
			public string NewStatus { get; set; } = string.Empty;
		}

		public class DeleteTaskRequest
		{
			public int UserTaskId { get; set; }
		}
	}
}