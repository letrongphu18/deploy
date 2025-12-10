using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AIHUBOS.Helpers;
using TMD.Models;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.SignalR;
using AIHUBOS.Hubs;
using AIHUBOS.Services;
using TMD.ViewModels;
using System.Text.Json;

namespace TMD.Controllers
{
	public class AdminController : Controller
	{
		private readonly AihubSystemContext _context;
		private readonly AuditHelper _auditHelper;
		private readonly IHubContext<NotificationHub> _hubContext;
		private readonly INotificationService _notificationService;
		private readonly IAuditCleanupService _auditCleanupService;
		private readonly ILogger<AdminController> _logger;

		public AdminController(AihubSystemContext context, AuditHelper auditHelper, IHubContext<NotificationHub> hubContext, ILogger<AdminController> logger, INotificationService notificationService, IAuditCleanupService auditCleanupService)
		{
			_context = context;
			_auditHelper = auditHelper;
			_hubContext = hubContext;
			_notificationService = notificationService;
			_auditCleanupService = auditCleanupService;
			_logger = logger;                           // ✅ NEW

		}
		private bool IsSuperAdmin()
		{
			return HttpContext.Session.GetString("RoleName") == "SupAdmin";
		}
		private bool IsAdminOrManager()
		{
			var role = HttpContext.Session.GetString("RoleName");
			// ✅ Đã bao gồm SupAdmin, Admin, và Manager
			return role == "Admin" || role == "Manager" || role == "SupAdmin";
		}
		private bool IsAuthenticated()
		{
			return HttpContext.Session.GetInt32("UserId") != null;
		}

		// Hàm helper để lấy setting và convert sang số (decimal)
		private decimal GetSettingValue(List<SystemSetting> settings, string key, decimal defaultValue = 0)
		{
			var setting = settings.FirstOrDefault(s => s.SettingKey == key);
			if (setting != null && decimal.TryParse(setting.SettingValue, out decimal value))
			{
				return value;
			}
			return defaultValue; // Trả về giá trị mặc định nếu không tìm thấy hoặc lỗi
		}
		private bool IsAdmin()
		{
			return HttpContext.Session.GetString("RoleName") == "Admin";
		}

		// ============================================
		// ADMIN DASHBOARD
		// ============================================
		// ============================================
		// ADMIN DASHBOARD - ĐÃ ĐƯỢC TỐI ƯU HÓA
		// ============================================
		// ============================================
		// ADMIN DASHBOARD - HOÀN CHỈNH SAU KHI XÓA 3 CỘT
		// TargetPerWeek, CompletedThisWeek, WeekStartDate
		// ============================================
		// ============================================
		// ADMIN DASHBOARD - ĐÃ FIX HOÀN CHỈNH
		// ============================================
		public async Task<IActionResult> Dashboard()
		{
			if (!IsAdminOrManager())
				return RedirectToAction("Login", "Account");

			// ========== USER STATISTICS ==========
			ViewBag.TotalUsers = await _context.Users.CountAsync();
			ViewBag.ActiveUsers = await _context.Users.CountAsync(u => u.IsActive == true);
			ViewBag.TotalDepartments = await _context.Departments.CountAsync();

			// ========== TASK STATISTICS (DỰA VÀO STATUS MỚI) ==========
			var allTasks = await _context.Tasks
				.Include(t => t.UserTasks)
				.Where(t => t.IsActive == true)
				.ToListAsync();

			var allUserTasks = allTasks.SelectMany(t => t.UserTasks).ToList();

			ViewBag.TotalTasks = allTasks.Count;
			ViewBag.TotalAssignments = allUserTasks.Count;

			// Đếm theo Status MỚI (TODO, InProgress, Testing, Done, Reopen)
			ViewBag.TodoTasks = allUserTasks.Count(ut => ut.Status == "TODO");
			ViewBag.InProgressTasks = allUserTasks.Count(ut => ut.Status == "InProgress");
			ViewBag.TestingTasks = allUserTasks.Count(ut => ut.Status == "Testing");
			ViewBag.CompletedTasks = allUserTasks.Count(ut => ut.Status == "Done");
			ViewBag.ReopenTasks = allUserTasks.Count(ut => ut.Status == "Reopen");

			// Task quá hạn (task chưa hoàn thành và deadline < now)
			ViewBag.OverdueTasks = allTasks.Count(t =>
				t.Deadline.HasValue &&
				t.Deadline.Value < DateTime.Now &&
				t.UserTasks.Any(ut => ut.Status != "Done")
			);

			// Tỷ lệ hoàn thành (theo assignments)
			ViewBag.TaskCompletionRate = allUserTasks.Count > 0
				? Math.Round((double)ViewBag.CompletedTasks / allUserTasks.Count * 100, 1)
				: 0;

			// ========== ATTENDANCE STATISTICS (THÁNG HIỆN TẠI) ==========
			var startOfMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
			var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);

			var monthlyAttendances = await _context.Attendances
				.Include(a => a.User)
				.Where(a => a.WorkDate >= DateOnly.FromDateTime(startOfMonth) &&
							a.WorkDate <= DateOnly.FromDateTime(endOfMonth))
				.ToListAsync();

			ViewBag.TotalAttendances = monthlyAttendances.Count;
			ViewBag.OnTimeCount = monthlyAttendances.Count(a => a.IsLate == false);
			ViewBag.LateCount = monthlyAttendances.Count(a => a.IsLate == true);
			ViewBag.OnTimeRate = monthlyAttendances.Count > 0
				? Math.Round((double)ViewBag.OnTimeCount / monthlyAttendances.Count * 100, 1)
				: 0;

			// ✅ FIXED: Dùng query trực tiếp _context.UserTasks (KHÔNG cần sửa)
			var topPerformers = await _context.Users
				.Include(u => u.Department)
				.Where(u => u.IsActive == true)
				.Select(u => new
				{
					User = u,
					TotalCompleted = _context.UserTasks
						.Count(ut => ut.UserId == u.UserId && ut.Status == "Done" && ut.Task.IsActive == true),
					TotalTasks = _context.UserTasks
						.Count(ut => ut.UserId == u.UserId && ut.Task.IsActive == true),
					Avatar = string.IsNullOrEmpty(u.Avatar) || u.Avatar == "/images/default-avatar.png" ? null : u.Avatar,
					Initials = u.FullName != null ? u.FullName.Substring(0, 1).ToUpper() : "U",
					HasAvatar = !string.IsNullOrEmpty(u.Avatar) && u.Avatar != "/images/default-avatar.png"
				})
				.OrderByDescending(x => x.TotalCompleted)
				.Take(5)
				.ToListAsync();

			ViewBag.TopPerformers = topPerformers;

			// ========== LATE COMERS ==========
			var lateComers = await _context.Attendances
				.Include(a => a.User)
					.ThenInclude(u => u.Department)
				.Where(a => a.IsLate == true &&
							a.WorkDate >= DateOnly.FromDateTime(startOfMonth) &&
							a.WorkDate <= DateOnly.FromDateTime(endOfMonth))
				.GroupBy(a => a.UserId)
				.Select(g => new
				{
					UserId = g.Key,
					User = g.First().User,
					LateCount = g.Count(),
					Avatar = string.IsNullOrEmpty(g.First().User.Avatar) || g.First().User.Avatar == "/images/default-avatar.png"
						? null
						: g.First().User.Avatar,
					Initials = g.First().User.FullName != null ? g.First().User.FullName.Substring(0, 1).ToUpper() : "U",
					HasAvatar = !string.IsNullOrEmpty(g.First().User.Avatar) && g.First().User.Avatar != "/images/default-avatar.png"
				})
				.OrderByDescending(x => x.LateCount)
				.Take(5)
				.ToListAsync();

			ViewBag.LateComers = lateComers;

			// ========== PUNCTUAL STAFF ==========
			var punctualStaff = await _context.Attendances
				.Include(a => a.User)
					.ThenInclude(u => u.Department)
				.Where(a => a.IsLate == false &&
							a.WorkDate >= DateOnly.FromDateTime(startOfMonth) &&
							a.WorkDate <= DateOnly.FromDateTime(endOfMonth))
				.GroupBy(a => a.UserId)
				.Select(g => new
				{
					UserId = g.Key,
					User = g.First().User,
					OnTimeCount = g.Count(),
					Avatar = string.IsNullOrEmpty(g.First().User.Avatar) || g.First().User.Avatar == "/images/default-avatar.png"
						? null
						: g.First().User.Avatar,
					Initials = g.First().User.FullName != null ? g.First().User.FullName.Substring(0, 1).ToUpper() : "U",
					HasAvatar = !string.IsNullOrEmpty(g.First().User.Avatar) && g.First().User.Avatar != "/images/default-avatar.png"
				})
				.OrderByDescending(x => x.OnTimeCount)
				.Take(5)
				.ToListAsync();

			ViewBag.PunctualStaff = punctualStaff;

			// ========== TASKS BY PRIORITY (THEO STATUS MỚI) ==========
			var tasksByPriority = allTasks
				.GroupBy(t => t.Priority ?? "Medium")
				.Select(g => new
				{
					Priority = g.Key,
					Total = g.Sum(t => t.UserTasks.Count),
					Completed = g.Sum(t => t.UserTasks.Count(ut => ut.Status == "Done")),
					InProgress = g.Sum(t => t.UserTasks.Count(ut => ut.Status == "InProgress")),
					Testing = g.Sum(t => t.UserTasks.Count(ut => ut.Status == "Testing")),
					Todo = g.Sum(t => t.UserTasks.Count(ut => ut.Status == "TODO")),
					Reopen = g.Sum(t => t.UserTasks.Count(ut => ut.Status == "Reopen"))
				})
				.OrderBy(x => x.Priority == "High" ? 1 : x.Priority == "Medium" ? 2 : 3)
				.ToList();

			ViewBag.TasksByPriority = tasksByPriority;

			// ========== UPCOMING TASKS (THEO STATUS MỚI) ==========
			var upcomingTasksData = await _context.Tasks
				.Include(t => t.UserTasks)
					.ThenInclude(ut => ut.User)
				.Where(t => t.IsActive == true && t.Deadline.HasValue)
				.OrderBy(t => t.Deadline)
				.Take(10)
				.ToListAsync();

			var upcomingTasks = upcomingTasksData.Select(t => new
			{
				Task = t,
				AssignedCount = t.UserTasks.Count,
				CompletedCount = t.UserTasks.Count(ut => ut.Status == "Done"),
				InProgressCount = t.UserTasks.Count(ut => ut.Status == "InProgress"),
				TestingCount = t.UserTasks.Count(ut => ut.Status == "Testing"),
				TodoCount = t.UserTasks.Count(ut => ut.Status == "TODO"),
				ReopenCount = t.UserTasks.Count(ut => ut.Status == "Reopen"),
				ProgressPercent = t.UserTasks.Count > 0
					? Math.Round((double)t.UserTasks.Count(ut => ut.Status == "Done") / t.UserTasks.Count * 100, 1)
					: 0
			}).ToList();

			ViewBag.UpcomingTasks = upcomingTasks;

			// ========== TASK STATUS DISTRIBUTION (CHO CHART) ==========
			ViewBag.TaskStatusData = new
			{
				Labels = new[] { "TODO", "Đang làm", "Chờ test", "Hoàn thành", "Reopen" },
				Data = new[] {
			ViewBag.TodoTasks,
			ViewBag.InProgressTasks,
			ViewBag.TestingTasks,
			ViewBag.CompletedTasks,
			ViewBag.ReopenTasks
		}
			};

			// ========== DEPARTMENT PERFORMANCE (CHO CHART) - ✅ FIXED ==========
			var deptPerformance = await _context.Departments
				.Where(d => d.IsActive == true)
				.Select(d => new
				{
					DepartmentName = d.DepartmentName,
					TotalTasks = d.Users
						.SelectMany(u => u.UserTaskUsers)  // ✅ FIXED: UserTasks -> UserTaskUsers
						.Count(ut => ut.Task.IsActive == true),
					CompletedTasks = d.Users
						.SelectMany(u => u.UserTaskUsers)  // ✅ FIXED: UserTasks -> UserTaskUsers
						.Count(ut => ut.Status == "Done" && ut.Task.IsActive == true)
				})
				.Where(x => x.TotalTasks > 0)
				.OrderByDescending(x => x.CompletedTasks)
				.ToListAsync();

			ViewBag.DepartmentPerformance = deptPerformance;

			// ========== RECENT ACTIVITIES ==========
			var recentAudits = await _context.AuditLogs
				.Include(a => a.User)
				.OrderByDescending(a => a.Timestamp)
				.Take(5)
				.ToListAsync();

			ViewBag.RecentAudits = recentAudits;

			return View();
		}


		// ============================================
		// THÊM VÀO AdminController.cs - USER TRASH SYSTEM
		// ============================================

		/// <summary>
		/// Cập nhật UserList để CHỈ hiển thị user ACTIVE
		/// </summary>
		public async Task<IActionResult> UserList(
	int page = 1,
	int pageSize = 10,
	string? search = null,
	string? roleName = null,
	string? status = null,
	int? departmentId = null)
		{
			if (!IsAdminOrManager())
				return RedirectToAction("Login", "Account");

			// Statistics
			ViewBag.TotalUsers = await _context.Users.CountAsync(u => u.IsActive == true);
			ViewBag.ActiveUsers = await _context.Users.CountAsync(u => u.IsActive == true);
			ViewBag.TotalDepartments = await _context.Departments.CountAsync();

			var roles = await _context.Roles.OrderBy(r => r.RoleName).ToListAsync();
			var departments = await _context.Departments.OrderBy(d => d.DepartmentName).ToListAsync();

			var query = _context.Users
				.Include(u => u.Role)
				.Include(u => u.Department)
				.Where(u => u.IsActive == true)
				.AsQueryable();

			// ✅ NẾU LÀ MANAGER → KHÔNG CHO XEM ADMIN THẬT
			if (!IsSuperAdmin())
			{
				query = query.Where(u => u.Role.RoleName != "SupAdmin");
			}

			// Search filter
			if (!string.IsNullOrWhiteSpace(search))
			{
				var s = search.Trim().ToLower();
				query = query.Where(u =>
					(u.FullName != null && u.FullName.ToLower().Contains(s)) ||
					(u.Username != null && u.Username.ToLower().Contains(s)) ||
					(u.Email != null && u.Email.ToLower().Contains(s)) ||
					(u.PhoneNumber != null && u.PhoneNumber.ToLower().Contains(s))
				);
			}

			// Role filter
			if (!string.IsNullOrWhiteSpace(roleName))
			{
				query = query.Where(u => u.Role != null && u.Role.RoleName == roleName);
			}

			// Department filter
			if (departmentId.HasValue && departmentId.Value > 0)
			{
				query = query.Where(u => u.DepartmentId == departmentId.Value);
			}

			var totalCount = await query.CountAsync();

			var users = await query
				.OrderBy(u => u.FullName)
				.Skip((page - 1) * pageSize)
				.Take(pageSize)
				.ToListAsync();

			var vm = new UserListViewModel
			{
				Users = users,
				Roles = roles,
				Departments = departments,
				Page = page,
				PageSize = pageSize,
				TotalCount = totalCount,
				Search = search,
				RoleName = roleName,
				Status = status,
				DepartmentId = departmentId
			};

			// ✅ TRUYỀN QUYỀN XUỐNG VIEW
			ViewBag.IsSuperAdmin = IsSuperAdmin();

			return View(vm);
		}


		/// <summary>
		/// Hiển thị trang Thùng rác User
		/// </summary>
		[HttpGet]
		public async Task<IActionResult> UserTrash(
			int page = 1,
			int pageSize = 10,
			string? search = null,
			string? roleName = null,
			int? departmentId = null)
		{
			if (!IsAdmin())
				return RedirectToAction("Login", "Account");

			await _auditHelper.LogViewAsync(
				HttpContext.Session.GetInt32("UserId").Value,
				"User",
				0,
				"Xem thùng rác người dùng"
			);

			var roles = await _context.Roles.OrderBy(r => r.RoleName).ToListAsync();
			var departments = await _context.Departments.OrderBy(d => d.DepartmentName).ToListAsync();

			// ✅ CHỈ LẤY USER ĐÃ BỊ VÔ HIỆU HÓA
			var query = _context.Users
				.Include(u => u.Role)
				.Include(u => u.Department)
				.Where(u => u.IsActive == false)  // ← QUAN TRỌNG
				.AsQueryable();

			// Search filter
			if (!string.IsNullOrWhiteSpace(search))
			{
				var s = search.Trim().ToLower();
				query = query.Where(u =>
					(u.FullName != null && u.FullName.ToLower().Contains(s)) ||
					(u.Username != null && u.Username.ToLower().Contains(s)) ||
					(u.Email != null && u.Email.ToLower().Contains(s)) ||
					(u.PhoneNumber != null && u.PhoneNumber.ToLower().Contains(s))
				);
			}

			// Role filter
			if (!string.IsNullOrWhiteSpace(roleName))
			{
				query = query.Where(u => u.Role != null && u.Role.RoleName == roleName);
			}

			// Department filter
			if (departmentId.HasValue && departmentId.Value > 0)
			{
				query = query.Where(u => u.DepartmentId == departmentId.Value);
			}

			var totalCount = await query.CountAsync();

			var users = await query
				.OrderByDescending(u => u.UpdatedAt)
				.Skip((page - 1) * pageSize)
				.Take(pageSize)
				.ToListAsync();

			var vm = new UserListViewModel
			{
				Users = users,
				Roles = roles,
				Departments = departments,
				Page = page,
				PageSize = pageSize,
				TotalCount = totalCount,
				Search = search,
				RoleName = roleName,
				DepartmentId = departmentId
			};

			// Statistics cho thùng rác
			ViewBag.TotalTrash = totalCount;
			ViewBag.TotalUsers = await _context.Users.CountAsync();
			ViewBag.ActiveUsers = await _context.Users.CountAsync(u => u.IsActive == true);
			ViewBag.TotalDepartments = await _context.Departments.CountAsync();

			return View(vm);
		}

		/// <summary>
		/// Chuyển user vào thùng rác (soft delete - set IsActive = false)
		/// </summary>
		[HttpPost]
		public async Task<IActionResult> MoveUserToTrash([FromBody] MoveUserToTrashRequest request)
		{
			if (!IsSuperAdmin())
			{
				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"SOFT_DELETE",
					"User",
					"Bạn không có quyền xóa user",
					new { UserId = request.UserId }
				);

				return Json(new { success = false, message = "Bạn không có quyền xóa người dùng!" });
			}

			var user = await _context.Users
				.Include(u => u.Role)
				.FirstOrDefaultAsync(u => u.UserId == request.UserId);

			if (user == null)
			{
				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"SOFT_DELETE",
					"User",
					"User không tồn tại",
					new { UserId = request.UserId }
				);

				return Json(new { success = false, message = "Không tìm thấy người dùng!" });
			}

			// Không cho xóa Admin
			if (user.Role?.RoleName == "Admin")
			{
				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"SOFT_DELETE",
					"User",
					"Không thể xóa Admin",
					new { UserId = request.UserId }
				);

				return Json(new { success = false, message = "Không thể xóa tài khoản Admin!" });
			}

			// Không cho tự xóa chính mình
			var currentUserId = HttpContext.Session.GetInt32("UserId");
			if (user.UserId == currentUserId)
			{
				await _auditHelper.LogFailedAttemptAsync(
					currentUserId,
					"SOFT_DELETE",
					"User",
					"Không thể tự xóa chính mình",
					new { UserId = request.UserId }
				);

				return Json(new { success = false, message = "Không thể xóa chính mình!" });
			}

			try
			{
				var adminId = HttpContext.Session.GetInt32("UserId");
				var userName = user.FullName;

				// Soft Delete: Đánh dấu IsActive = false
				user.IsActive = false;
				user.UpdatedAt = DateTime.Now;

				await _context.SaveChangesAsync();

				await _auditHelper.LogAsync(
					adminId,
					"SOFT_DELETE",
					"User",
					user.UserId,
					new { IsActive = true },
					new { IsActive = false },
					$"Chuyển user '{userName}' ({user.Username}) vào thùng rác"
				);

				return Json(new
				{
					success = true,
					message = $"Đã chuyển '{userName}' vào thùng rác"
				});
			}
			catch (Exception ex)
			{
				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"SOFT_DELETE",
					"User",
					$"Exception: {ex.Message}",
					new { UserId = request.UserId, Error = ex.ToString() }
				);

				return Json(new { success = false, message = $"Có lỗi xảy ra: {ex.Message}" });
			}
		}

		/// <summary>
		/// Khôi phục user từ thùng rác
		/// </summary>
		[HttpPost]
		public async Task<IActionResult> RestoreUserFromTrash([FromBody] RestoreUserRequest request)
		{
			if (!IsSuperAdmin())
			{
				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"RESTORE",
					"User",
					"Không có quyền khôi phục",
					new { UserId = request.UserId }
				);

				return Json(new { success = false, message = "Không có quyền thực hiện!" });
			}

			var user = await _context.Users
				.FirstOrDefaultAsync(u => u.UserId == request.UserId);

			if (user == null)
			{
				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"RESTORE",
					"User",
					"User không tồn tại",
					new { UserId = request.UserId }
				);

				return Json(new { success = false, message = "Không tìm thấy người dùng!" });
			}

			try
			{
				var adminId = HttpContext.Session.GetInt32("UserId");
				var userName = user.FullName;

				// Restore: Đánh dấu IsActive = true
				user.IsActive = true;
				user.UpdatedAt = DateTime.Now;

				await _context.SaveChangesAsync();

				await _auditHelper.LogAsync(
					adminId,
					"RESTORE",
					"User",
					user.UserId,
					new { IsActive = false },
					new { IsActive = true },
					$"Khôi phục user '{userName}' ({user.Username}) từ thùng rác"
				);

				return Json(new
				{
					success = true,
					message = $"Đã khôi phục '{userName}'"
				});
			}
			catch (Exception ex)
			{
				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"RESTORE",
					"User",
					$"Exception: {ex.Message}",
					new { UserId = request.UserId, Error = ex.ToString() }
				);

				return Json(new { success = false, message = $"Có lỗi xảy ra: {ex.Message}" });
			}
		}

		/// <summary>
		/// Xóa vĩnh viễn user khỏi database (hard delete)
		/// CẨN THẬN: Chỉ dùng khi thực sự cần thiết
		/// </summary>
		[HttpPost]
		public async Task<IActionResult> PermanentlyDeleteUser([FromBody] DeleteUserRequest request)
		{
			if (!IsSuperAdmin())
			{
				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"HARD_DELETE",
					"User",
					"Không có quyền xóa vĩnh viễn",
					new { UserId = request.UserId }
				);

				return Json(new { success = false, message = "Không có quyền thực hiện!" });
			}

			var user = await _context.Users
				.Include(u => u.Role)
				.Include(u => u.UserTaskUsers)  // Check tasks
				.Include(u => u.Attendances)    // Check attendance
				.FirstOrDefaultAsync(u => u.UserId == request.UserId && u.IsActive == false);

			if (user == null)
			{
				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"HARD_DELETE",
					"User",
					"User không tồn tại hoặc chưa ở trong thùng rác",
					new { UserId = request.UserId }
				);

				return Json(new { success = false, message = "Không tìm thấy người dùng trong thùng rác!" });
			}

			// Không cho xóa vĩnh viễn user có dữ liệu liên quan
			if (user.UserTaskUsers != null && user.UserTaskUsers.Any())
			{
				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"HARD_DELETE",
					"User",
					"User có tasks liên quan",
					new { UserId = request.UserId, TaskCount = user.UserTaskUsers.Count }
				);

				return Json(new
				{
					success = false,
					message = $"Không thể xóa vĩnh viễn! User có {user.UserTaskUsers.Count} task liên quan."
				});
			}

			if (user.Attendances != null && user.Attendances.Any())
			{
				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"HARD_DELETE",
					"User",
					"User có dữ liệu chấm công",
					new { UserId = request.UserId, AttendanceCount = user.Attendances.Count }
				);

				return Json(new
				{
					success = false,
					message = $"Không thể xóa vĩnh viễn! User có {user.Attendances.Count} bản ghi chấm công."
				});
			}

			try
			{
				var adminId = HttpContext.Session.GetInt32("UserId");
				var userName = user.FullName;
				var username = user.Username;

				// Hard Delete: Xóa khỏi database
				_context.Users.Remove(user);
				await _context.SaveChangesAsync();

				await _auditHelper.LogAsync(
					adminId,
					"HARD_DELETE",
					"User",
					request.UserId,
					new { FullName = userName, Username = username },
					null,
					$"Xóa vĩnh viễn user '{userName}' ({username}) khỏi hệ thống"
				);

				return Json(new
				{
					success = true,
					message = $"Đã xóa vĩnh viễn '{userName}'"
				});
			}
			catch (Exception ex)
			{
				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"HARD_DELETE",
					"User",
					$"Exception: {ex.Message}",
					new { UserId = request.UserId, Error = ex.ToString() }
				);

				return Json(new { success = false, message = $"Có lỗi xảy ra: {ex.Message}" });
			}
		}

		/// <summary>
		/// Làm rỗng thùng rác user (xóa tất cả user không có dữ liệu liên quan)
		/// </summary>
		[HttpPost]
		public async Task<IActionResult> EmptyUserTrash()
		{
			if (!IsSuperAdmin())
			{
				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"EMPTY_TRASH",
					"User",
					"Không có quyền làm rỗng thùng rác",
					null
				);

				return Json(new { success = false, message = "Không có quyền thực hiện!" });
			}

			try
			{
				var adminId = HttpContext.Session.GetInt32("UserId");

				// Lấy tất cả user trong thùng rác
				var trashedUsers = await _context.Users
					.Include(u => u.UserTaskUsers)
					.Include(u => u.Attendances)
					.Where(u => u.IsActive == false)
					.ToListAsync();

				if (!trashedUsers.Any())
				{
					return Json(new
					{
						success = false,
						message = "Thùng rác đang trống!"
					});
				}

				// User có thể xóa (không có tasks và attendance)
				var deletableUsers = trashedUsers
					.Where(u => (u.UserTaskUsers == null || !u.UserTaskUsers.Any()) &&
							   (u.Attendances == null || !u.Attendances.Any()))
					.ToList();

				// User không thể xóa (còn dữ liệu liên quan)
				var nonDeletableUsers = trashedUsers.Except(deletableUsers).ToList();

				if (!deletableUsers.Any())
				{
					return Json(new
					{
						success = false,
						message = $"Không thể làm rỗng thùng rác! Có {nonDeletableUsers.Count} user vẫn có dữ liệu liên quan."
					});
				}

				// Xóa vĩnh viễn các user không có dữ liệu liên quan
				_context.Users.RemoveRange(deletableUsers);
				await _context.SaveChangesAsync();

				await _auditHelper.LogDetailedAsync(
					adminId,
					"EMPTY_TRASH",
					"User",
					null,
					null,
					null,
					$"Làm rỗng thùng rác user: Xóa {deletableUsers.Count} user",
					new Dictionary<string, object>
					{
				{ "DeletedCount", deletableUsers.Count },
				{ "SkippedCount", nonDeletableUsers.Count },
				{ "DeletedUsers", string.Join(", ", deletableUsers.Select(u => u.Username)) }
					}
				);

				var message = deletableUsers.Count == trashedUsers.Count
					? $"Đã xóa vĩnh viễn {deletableUsers.Count} user khỏi thùng rác"
					: $"Đã xóa {deletableUsers.Count} user. Bỏ qua {nonDeletableUsers.Count} user vẫn có dữ liệu liên quan.";

				return Json(new
				{
					success = true,
					message = message,
					deletedCount = deletableUsers.Count,
					skippedCount = nonDeletableUsers.Count
				});
			}
			catch (Exception ex)
			{
				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"EMPTY_TRASH",
					"User",
					$"Exception: {ex.Message}",
					new { Error = ex.ToString() }
				);

				return Json(new { success = false, message = $"Có lỗi xảy ra: {ex.Message}" });
			}
		}

		// ============================================
		// REQUEST MODELS
		// ============================================
		public class MoveUserToTrashRequest
		{
			public int UserId { get; set; }
		}

		public class RestoreUserRequest
		{
			public int UserId { get; set; }
		}

		public class DeleteUserRequest
		{
			public int UserId { get; set; }
		}



		[HttpPost]
		public async Task<IActionResult> ToggleUserStatus([FromBody] ToggleUserRequest request)
		{
			if (!IsSuperAdmin())
			{
				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"UPDATE",
					"User",
					"Bạn  không có quyền khóa user",
					new { UserId = request.UserId }
				);

				return Json(new { success = false, message = "Bạn không có quyền khóa người dùng!" });
			}

			var user = await _context.Users.FindAsync(request.UserId);
			if (user == null)
			{
				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"UPDATE",
					"User",
					"Không tìm thấy người dùng",
					new { UserId = request.UserId }
				);

				return Json(new { success = false, message = "Không tìm thấy người dùng" });
			}

			try
			{
				var adminId = HttpContext.Session.GetInt32("UserId");
				user.IsActive = !user.IsActive;
				user.UpdatedAt = DateTime.Now;
				await _context.SaveChangesAsync();

				await _auditHelper.LogAsync(
					adminId,
					"UPDATE",
					"User",
					user.UserId,
					new { IsActive = !user.IsActive },
					new { IsActive = user.IsActive },
					$"Admin {(user.IsActive == true ? "kích hoạt" : "vô hiệu hóa")} tài khoản: {user.Username}"
				);

				return Json(new
				{
					success = true,
					message = $"Đã {(user.IsActive == true ? "kích hoạt" : "vô hiệu hóa")} tài khoản {user.FullName}",
					isActive = user.IsActive
				});
			}
			catch (Exception ex)
			{
				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"UPDATE",
					"User",
					$"Exception: {ex.Message}",
					new { UserId = request.UserId, Error = ex.ToString() }
				);

				return Json(new { success = false, message = $"Có lỗi xảy ra: {ex.Message}" });
			}
		}



		// ============================================
		// GET USER DETAIL - Full info (FIXED)
		// ============================================
		[HttpGet]
		public async Task<IActionResult> GetUserDetail(int userId)
		{
			if (!IsAdminOrManager())
				return Json(new { success = false, message = "Không có quyền truy cập" });

			try
			{
				var user = await _context.Users
					.Include(u => u.Role)
					.Include(u => u.Department)
					.Include(u => u.UserTaskUsers)  // ✅ FIXED: UserTasks -> UserTaskUsers
						.ThenInclude(ut => ut.Task)
					.FirstOrDefaultAsync(u => u.UserId == userId);

				if (user == null)
					return Json(new { success = false, message = "Không tìm thấy nhân viên" });

				// 1. TASK STATISTICS - ✅ FIXED
				var tasks = user.UserTaskUsers.Where(ut => ut.Task.IsActive == true).ToList();
				var totalTasks = tasks.Count;
				var completedTasks = tasks.Count(ut => ut.Status == "Completed");
				var inProgressTasks = tasks.Count(ut => ut.Status == "InProgress");
				var todoTasks = tasks.Count(ut => string.IsNullOrEmpty(ut.Status) || ut.Status == "TODO");
				var overdueTasks = tasks.Count(ut =>
					ut.Task.Deadline.HasValue &&
					ut.Task.Deadline.Value < DateTime.Now &&
					ut.Status != "Completed"
				);

				// 2. ATTENDANCE STATISTICS (Current Month)
				var startOfMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
				var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);

				var monthlyAttendances = await _context.Attendances
					.Where(a => a.UserId == userId &&
							   a.WorkDate >= DateOnly.FromDateTime(startOfMonth) &&
							   a.WorkDate <= DateOnly.FromDateTime(endOfMonth))
					.OrderByDescending(a => a.WorkDate)
					.ToListAsync();

				var totalWorkDays = monthlyAttendances.Count;
				var onTimeDays = monthlyAttendances.Count(a => a.IsLate == false);
				var lateDays = monthlyAttendances.Count(a => a.IsLate == true);

				var totalWorkHours = monthlyAttendances.Sum(a => a.TotalHours.GetValueOrDefault());
				var approvedOvertimeHours = monthlyAttendances.Sum(a => a.ApprovedOvertimeHours);
				var totalDeduction = monthlyAttendances.Sum(a => a.DeductionAmount);

				// 3. SALARY & BENEFITS (Current Month)
				var settings = await _context.SystemSettings
					.Where(s => s.IsActive == true)
					.ToListAsync();

				var baseSalary = GetSettingValue(settings, "BASE_SALARY", 5000000m);
				var hourlyRate = GetSettingValue(settings, "HOURLY_RATE", 50000m);
				var overtimeMultiplier = GetSettingValue(settings, "OVERTIME_MULTIPLIER", 1.5m);

				var workHoursSalary = totalWorkHours * hourlyRate;
				var overtimeSalary = approvedOvertimeHours * hourlyRate * overtimeMultiplier;
				var estimatedSalary = baseSalary + workHoursSalary + overtimeSalary - totalDeduction;

				// 4. LEAVE & REQUESTS
				var totalLeaveRequests = await _context.LeaveRequests
					.CountAsync(lr => lr.UserId == userId);
				var approvedLeaves = await _context.LeaveRequests
					.CountAsync(lr => lr.UserId == userId && lr.Status == "Approved");
				var pendingLeaves = await _context.LeaveRequests
					.CountAsync(lr => lr.UserId == userId && lr.Status == "Pending");

				var totalOvertimeRequests = await _context.OvertimeRequests
					.CountAsync(or => or.UserId == userId);
				var approvedOvertimes = await _context.OvertimeRequests
					.CountAsync(or => or.UserId == userId && or.Status == "Approved");

				// 5. RECENT ATTENDANCE (Last 7 days)
				var recentAttendances = monthlyAttendances
					.Take(7)
					.Select(a => new
					{
						workDate = a.WorkDate.ToString("dd/MM/yyyy"),
						checkInTime = a.CheckInTime.HasValue ? a.CheckInTime.Value.ToString("HH:mm:ss") : "N/A",
						checkOutTime = a.CheckOutTime.HasValue ? a.CheckOutTime.Value.ToString("HH:mm:ss") : "N/A",
						totalHours = Math.Round(a.TotalHours.GetValueOrDefault(), 2),
						isLate = a.IsLate,
						overtimeHours = Math.Round(a.ApprovedOvertimeHours, 2),
						deductionAmount = Math.Round(a.DeductionAmount, 0)
					})
					.ToList();

				await _auditHelper.LogViewAsync(
					HttpContext.Session.GetInt32("UserId").Value,
					"User",
					userId,
					$"Xem chi tiết nhân viên: {user.FullName}"
				);

				return Json(new
				{
					success = true,
					user = new
					{
						userId = user.UserId,
						username = user.Username,
						fullName = user.FullName,
						email = user.Email,
						phoneNumber = user.PhoneNumber,
						avatar = user.Avatar,
						roleName = user.Role?.RoleName ?? "N/A",
						departmentName = user.Department?.DepartmentName ?? "Chưa phân công",
						isActive = user.IsActive,
						createdAt = user.CreatedAt?.ToString("dd/MM/yyyy HH:mm"),
						lastLoginAt = user.LastLoginAt?.ToString("dd/MM/yyyy HH:mm") ?? "Chưa đăng nhập"
					},
					tasks = new
					{
						total = totalTasks,
						completed = completedTasks,
						inProgress = inProgressTasks,
						todo = todoTasks,
						overdue = overdueTasks,
						completionRate = totalTasks > 0 ? Math.Round((double)completedTasks / totalTasks * 100, 1) : 0
					},
					attendance = new
					{
						totalWorkDays = totalWorkDays,
						onTimeDays = onTimeDays,
						lateDays = lateDays,
						onTimeRate = totalWorkDays > 0 ? Math.Round((double)onTimeDays / totalWorkDays * 100, 1) : 0,
						totalWorkHours = Math.Round(totalWorkHours, 2),
						approvedOvertimeHours = Math.Round(approvedOvertimeHours, 2),
						recentAttendances = recentAttendances
					},
					salary = new
					{
						baseSalary = Math.Round(baseSalary, 0),
						workHoursSalary = Math.Round(workHoursSalary, 0),
						overtimeSalary = Math.Round(overtimeSalary, 0),
						totalDeduction = Math.Round(totalDeduction, 0),
						estimatedSalary = Math.Round(estimatedSalary, 0)
					},
					requests = new
					{
						totalLeaveRequests = totalLeaveRequests,
						approvedLeaves = approvedLeaves,
						pendingLeaves = pendingLeaves,
						totalOvertimeRequests = totalOvertimeRequests,
						approvedOvertimes = approvedOvertimes
					}
				});
			}
			catch (Exception ex)
			{
				return Json(new { success = false, message = $"Lỗi: {ex.Message}" });
			}
		}
		// ============================================
		// RESET PASSWORD
		// ============================================
		[HttpGet]
		public async Task<IActionResult> ResetUserPassword(int id)
		{
			if (!IsAdmin())
				return RedirectToAction("Login", "Account");

			var user = await _context.Users.FindAsync(id);
			if (user == null)
				return NotFound();

			ViewBag.User = user;
			return View();
		}

		[HttpPost]
		public async Task<IActionResult> ResetUserPasswordJson([FromBody] ResetPasswordRequest request)
		{
			if (!IsSuperAdmin())
			{
				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"PASSWORD_RESET",
					"User",
					"Không có quyền reset mật khẩu",
					new { TargetUserId = request.UserId }
				);

				return Json(new { success = false, message = "Chỉ Admin mới có quyền reset mật khẩu!" });
			}

			if (string.IsNullOrEmpty(request.NewPassword) || request.NewPassword.Length < 6)
			{
				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"PASSWORD_RESET",
					"User",
					"Mật khẩu không hợp lệ",
					new { UserId = request.UserId }
				);

				return Json(new { success = false, message = "Mật khẩu mới phải có ít nhất 6 ký tự" });
			}

			if (string.IsNullOrEmpty(request.Reason))
			{
				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"PASSWORD_RESET",
					"User",
					"Thiếu lý do reset",
					new { UserId = request.UserId }
				);

				return Json(new { success = false, message = "Vui lòng nhập lý do reset mật khẩu" });
			}

			var user = await _context.Users.FindAsync(request.UserId);
			if (user == null)
			{
				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"PASSWORD_RESET",
					"User",
					"User không tồn tại",
					new { UserId = request.UserId }
				);

				return Json(new { success = false, message = "Không tìm thấy người dùng" });
			}

			try
			{
				var adminId = HttpContext.Session.GetInt32("UserId");
				var oldHash = user.PasswordHash;

				user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
				user.UpdatedAt = DateTime.Now;

				var resetHistory = new PasswordResetHistory
				{
					UserId = user.UserId,
					ResetByUserId = adminId,
					OldPasswordHash = oldHash,
					ResetTime = DateTime.Now,
					ResetReason = request.Reason,
					Ipaddress = HttpContext.Connection.RemoteIpAddress?.ToString()
				};

				_context.PasswordResetHistories.Add(resetHistory);
				await _context.SaveChangesAsync();

				await _auditHelper.LogAsync(
					adminId,
					"PASSWORD_RESET",
					"User",
					user.UserId,
					null,
					null,
					$"Admin reset mật khẩu cho user: {user.Username}. Lý do: {request.Reason}"
				);

				return Json(new
				{
					success = true,
					message = $"Reset mật khẩu thành công cho {user.FullName}!"
				});
			}
			catch (Exception ex)
			{
				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"PASSWORD_RESET",
					"User",
					$"Exception: {ex.Message}",
					new { UserId = request.UserId, Error = ex.ToString() }
				);

				return Json(new { success = false, message = $"Có lỗi xảy ra: {ex.Message}" });
			}
		}

		[HttpGet]
		public async Task<IActionResult> GetAllUsers()
		{
			try
			{
				var users = await _context.Users
					.Include(u => u.Department)
					.Where(u => u.IsActive == true)
					.OrderBy(u => u.FullName)
					.Select(u => new
					{
						userId = u.UserId,
						fullName = u.FullName,
						email = u.Email,
						departmentName = u.Department != null ? u.Department.DepartmentName : "N/A"
					})
					.ToListAsync();

				return Json(new { success = true, users = users });
			}
			catch (Exception ex)
			{
				return Json(new { success = false, message = ex.Message });
			}
		}

		// ============================================
		// PASSWORD RESET HISTORY
		// ============================================
		public async Task<IActionResult> PasswordResetHistory()
		{
			if (!IsAdmin())
				return RedirectToAction("Login", "Account");

			await _auditHelper.LogViewAsync(
				HttpContext.Session.GetInt32("UserId").Value,
				"PasswordResetHistory",
				0,
				"Xem lịch sử reset mật khẩu"
			);

			var history = await _context.PasswordResetHistories
				.Include(p => p.User)
				.Include(p => p.ResetByUser)
				.OrderByDescending(p => p.ResetTime)
				.ToListAsync();

			return View(history);
		}

		// ============================================
		// ATTENDANCE MANAGEMENT
		// ============================================
		public async Task<IActionResult> AttendanceList(DateTime? date)
		{
			if (!IsAdminOrManager())
				return RedirectToAction("Login", "Account");

			var selectedDate = date ?? DateTime.Today;

			await _auditHelper.LogViewAsync(
				HttpContext.Session.GetInt32("UserId").Value,
				"Attendance",
				0,
				$"Xem danh sách chấm công ngày {selectedDate:dd/MM/yyyy}"
			);

			var attendances = await _context.Attendances
				.Include(a => a.User)
					.ThenInclude(u => u.Department)
				.Where(a => a.WorkDate == DateOnly.FromDateTime(selectedDate))
				.OrderByDescending(a => a.CheckInTime)
				.ToListAsync();

			ViewBag.SelectedDate = selectedDate;
			return View(attendances);
		}

		public async Task<IActionResult> AttendanceHistory(int? userId, DateTime? fromDate, DateTime? toDate, int? departmentId)
		{
			if (!IsAdminOrManager())
				return RedirectToAction("Login", "Account");

			var from = fromDate ?? DateTime.Today.AddDays(-30);
			var to = toDate ?? DateTime.Today;

			await _auditHelper.LogDetailedAsync(
				HttpContext.Session.GetInt32("UserId").Value,
				"VIEW",
				"Attendance",
				null,
				null,
				null,
				"Xem lịch sử chấm công tổng hợp",
				new Dictionary<string, object>
				{
					{ "FilterUserId", userId ?? 0 },
					{ "FilterDepartment", departmentId ?? 0 },
					{ "FromDate", from.ToString("yyyy-MM-dd") },
					{ "ToDate", to.ToString("yyyy-MM-dd") }
				}
			);

			var query = _context.Attendances
				.Include(a => a.User)
					.ThenInclude(u => u.Department)
				.AsQueryable();

			if (userId.HasValue && userId.Value > 0)
			{
				query = query.Where(a => a.UserId == userId.Value);
			}

			if (departmentId.HasValue && departmentId.Value > 0)
			{
				query = query.Where(a => a.User.DepartmentId == departmentId.Value);
			}

			query = query.Where(a =>
				a.WorkDate >= DateOnly.FromDateTime(from) &&
				a.WorkDate <= DateOnly.FromDateTime(to)
			);

			var attendances = await query
				.OrderByDescending(a => a.WorkDate)
				.ThenByDescending(a => a.CheckInTime)
				.ToListAsync();

			ViewBag.TotalRecords = attendances.Count;
			ViewBag.TotalCheckIns = attendances.Count(a => a.CheckInTime != null);
			ViewBag.TotalCheckOuts = attendances.Count(a => a.CheckOutTime != null);
			ViewBag.CompletedDays = attendances.Count(a => a.CheckInTime != null && a.CheckOutTime != null);
			ViewBag.OnTimeCount = attendances.Count(a => a.IsLate == false);
			ViewBag.LateCount = attendances.Count(a => a.IsLate == true);
			ViewBag.TotalWorkHours = attendances.Sum(a => a.TotalHours ?? 0);
			ViewBag.WithinGeofence = attendances.Count(a => a.IsWithinGeofence == true);
			ViewBag.OutsideGeofence = attendances.Count(a => a.IsWithinGeofence == false);

			ViewBag.Users = await _context.Users
				.Where(u => u.IsActive == true)
				.OrderBy(u => u.FullName)
				.ToListAsync();

			ViewBag.Departments = await _context.Departments
				.OrderBy(d => d.DepartmentName)
				.ToListAsync();

			ViewBag.SelectedUserId = userId;
			ViewBag.SelectedDepartmentId = departmentId;
			ViewBag.FromDate = from;
			ViewBag.ToDate = to;

			return View(attendances);
		}

		// ============================================
		// AUDIT LOGS (FIXED: no StringComparison)
		// ============================================
		public async Task<IActionResult> AuditLogs(string? action, DateTime? fromDate, DateTime? toDate)
		{
			if (!IsSuperAdmin())
				return RedirectToAction("Login", "Account");

			await _auditHelper.LogViewAsync(
				HttpContext.Session.GetInt32("UserId").Value,
				"AuditLog",
				0,
				$"Xem nhật ký hoạt động - Filter: {action ?? "All"}"
			);

			var query = _context.AuditLogs
				.Include(a => a.User)
				.AsQueryable();

			// Case-insensitive filter without StringComparison (EF-friendly)
			if (!string.IsNullOrWhiteSpace(action))
			{
				var act = action.Trim().ToLower();
				query = query.Where(a => a.Action != null && a.Action.ToLower() == act);
			}

			// Normalize date range (swap if inverted)
			if (fromDate.HasValue && toDate.HasValue && fromDate > toDate)
			{
				var t = fromDate; fromDate = toDate; toDate = t;
			}

			// Inclusive end date (to midnight next day)
			if (fromDate.HasValue)
				query = query.Where(a => a.Timestamp.HasValue && a.Timestamp.Value >= fromDate.Value);

			if (toDate.HasValue)
				query = query.Where(a => a.Timestamp.HasValue && a.Timestamp.Value < toDate.Value.Date.AddDays(1));

			var logs = await query
				.OrderByDescending(a => a.Timestamp)
				.Take(1000)
				.ToListAsync();

			// Fallback to recent when filters yield none
			if (!logs.Any())
			{
				logs = await _context.AuditLogs
					.Include(a => a.User)
					.OrderByDescending(a => a.Timestamp)
					.Take(20)
					.ToListAsync();

				TempData["Info"] = "Không có log theo bộ lọc. Đang hiển thị gần đây.";
			}

			ViewBag.Actions = await _context.AuditLogs
				.Where(a => a.Action != null)
				.Select(a => a.Action)
				.Distinct()
				.OrderBy(a => a)
				.ToListAsync();

			ViewBag.SelectedAction = action;
			ViewBag.FromDate = fromDate;
			ViewBag.ToDate = toDate;

			return View(logs);
		}

		// ============================================
		// LOGIN HISTORY
		// ============================================
		public async Task<IActionResult> LoginHistory(DateTime? fromDate, DateTime? toDate, bool? isSuccess)
		{
			if (!IsSuperAdmin())
				return RedirectToAction("Login", "Account");

			await _auditHelper.LogDetailedAsync(
				HttpContext.Session.GetInt32("UserId").Value,
				"VIEW",
				"LoginHistory",
				null,
				null,
				null,
				"Xem lịch sử đăng nhập hệ thống",
				new Dictionary<string, object>
				{
					{ "FromDate", fromDate?.ToString("yyyy-MM-dd") ?? "All" },
					{ "ToDate", toDate?.ToString("yyyy-MM-dd") ?? "All" },
					{ "FilterSuccess", isSuccess?.ToString() ?? "All" }
				}
			);

			var query = _context.LoginHistories
				.Include(l => l.User)
				.AsQueryable();

			if (fromDate.HasValue)
				query = query.Where(l => l.LoginTime.HasValue && l.LoginTime.Value >= fromDate.Value);

			if (toDate.HasValue)
				query = query.Where(l => l.LoginTime.HasValue && l.LoginTime.Value <= toDate.Value.AddDays(1));

			if (isSuccess.HasValue)
				query = query.Where(l => l.IsSuccess == isSuccess.Value);

			var history = await query
				.OrderByDescending(l => l.LoginTime)
				.Take(1000)
				.ToListAsync();

			ViewBag.FromDate = fromDate;
			ViewBag.ToDate = toDate;
			ViewBag.IsSuccess = isSuccess;

			return View(history);
		}

		// ============================================
		// DEPARTMENT MANAGEMENT
		// ============================================
		public async Task<IActionResult> DepartmentList()
		{
			if (!IsAdminOrManager())
				return RedirectToAction("Login", "Account");

			var departments = await _context.Departments
				.Include(d => d.Users)
				.OrderBy(d => d.DepartmentName)
				.ToListAsync();

			return View(departments);
		}

		[HttpGet]
		public async Task<IActionResult> DepartmentDetail(int id)
		{
			if (!IsAdminOrManager())
				return RedirectToAction("Login", "Account");

			await _auditHelper.LogViewAsync(
				HttpContext.Session.GetInt32("UserId").Value,
				"Department",
				id,
				"Xem chi tiết phòng ban"
			);

			var department = await _context.Departments
				.Include(d => d.Users)
					.ThenInclude(u => u.Role)
				.FirstOrDefaultAsync(d => d.DepartmentId == id);

			if (department == null)
				return NotFound();

			return View(department);
		}

		[HttpPost]
		public async Task<IActionResult> CreateDepartment([FromBody] CreateDepartmentRequest request)
		{
			if (!IsSuperAdmin())
			{
				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"CREATE",
					"Department",
					"Không có quyền tạo phòng ban",
					new { DepartmentName = request.DepartmentName }
				);

				return Json(new { success = false, message = "Không có quyền thực hiện!" });
			}

			if (string.IsNullOrWhiteSpace(request.DepartmentName))
			{
				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"CREATE",
					"Department",
					"Tên phòng ban rỗng",
					null
				);

				return Json(new { success = false, message = "Tên phòng ban không được để trống!" });
			}

			var existingDept = await _context.Departments
				.FirstOrDefaultAsync(d => d.DepartmentName.ToLower() == request.DepartmentName.Trim().ToLower());

			if (existingDept != null)
			{
				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"CREATE",
					"Department",
					"Tên phòng ban đã tồn tại",
					new { DepartmentName = request.DepartmentName }
				);

				return Json(new { success = false, message = "Tên phòng ban đã tồn tại!" });
			}

			try
			{
				var adminId = HttpContext.Session.GetInt32("UserId");

				var department = new Department
				{
					DepartmentName = request.DepartmentName.Trim(),
					Description = request.Description?.Trim(),
					IsActive = request.IsActive,
					CreatedAt = DateTime.Now,
					UpdatedAt = DateTime.Now
				};

				_context.Departments.Add(department);
				await _context.SaveChangesAsync();

				await _auditHelper.LogAsync(
					adminId,
					"CREATE",
					"Department",
					department.DepartmentId,
					null,
					new { department.DepartmentName, department.Description, department.IsActive },
					$"Tạo phòng ban mới: {department.DepartmentName}"
				);

				return Json(new
				{
					success = true,
					message = $"Tạo phòng ban '{department.DepartmentName}' thành công!",
					departmentId = department.DepartmentId
				});
			}
			catch (Exception ex)
			{
				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"CREATE",
					"Department",
					$"Exception: {ex.Message}",
					new { DepartmentName = request.DepartmentName, Error = ex.ToString() }
				);

				return Json(new { success = false, message = $"Có lỗi xảy ra: {ex.Message}" });
			}
		}
		[HttpPost]
		public async Task<IActionResult> UpdateDepartment([FromBody] UpdateDepartmentRequest request)
		{
			if (!IsSuperAdmin())
			{
				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"UPDATE",
					"Department",
					"Không có quyền cập nhật",
					new { DepartmentId = request.DepartmentId }
				);

				return Json(new { success = false, message = "Không có quyền thực hiện!" });
			}

			if (string.IsNullOrWhiteSpace(request.DepartmentName))
			{
				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"UPDATE",
					"Department",
					"Tên phòng ban rỗng",
					new { DepartmentId = request.DepartmentId }
				);

				return Json(new { success = false, message = "Tên phòng ban không được để trống!" });
			}

			var department = await _context.Departments.FindAsync(request.DepartmentId);

			if (department == null)
			{
				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"UPDATE",
					"Department",
					"Phòng ban không tồn tại",
					new { DepartmentId = request.DepartmentId }
				);

				return Json(new { success = false, message = "Không tìm thấy phòng ban!" });
			}

			var existingDept = await _context.Departments
				.FirstOrDefaultAsync(d => d.DepartmentId != request.DepartmentId && d.DepartmentName.ToLower() == request.DepartmentName.Trim().ToLower());
			if (existingDept != null)
			{
				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"UPDATE",
					"Department",
					"Tên phòng ban đã tồn tại",
					new { DepartmentId = request.DepartmentId, DepartmentName = request.DepartmentName }
				);

				return Json(new { success = false, message = "Tên phòng ban đã tồn tại!" });
			}

			try
			{
				var adminId = HttpContext.Session.GetInt32("UserId");

				var oldValues = new
				{
					department.DepartmentName,
					department.Description,
					department.IsActive
				};

				department.DepartmentName = request.DepartmentName.Trim();
				department.Description = request.Description?.Trim();
				department.IsActive = request.IsActive;
				department.UpdatedAt = DateTime.Now;

				await _context.SaveChangesAsync();

				var newValues = new
				{
					department.DepartmentName,
					department.Description,
					department.IsActive
				};

				await _auditHelper.LogAsync(
					adminId,
					"UPDATE",
					"Department",
					department.DepartmentId,
					oldValues,
					newValues,
					$"Cập nhật phòng ban: {department.DepartmentName}"
				);

				// ✅ GỬI THÔNG BÁO CHO TẤT CẢ THÀNH VIÊN PHÒNG BAN
				await _notificationService.SendToDepartmentAsync(
	department.DepartmentId,
	"Cập nhật phòng ban",
	$"Phòng ban {department.DepartmentName} vừa được cập nhật thông tin",
	"info",
	$"/Admin/DepartmentDetail/{department.DepartmentId}"
);

				return Json(new
				{
					success = true,
					message = $"Cập nhật phòng ban '{department.DepartmentName}' thành công!"
				});
			}
			catch (Exception ex)
			{
				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"UPDATE",
					"Department",
					$"Exception: {ex.Message}",
					new { DepartmentId = request.DepartmentId, Error = ex.ToString() }
				);

				return Json(new { success = false, message = $"Có lỗi xảy ra: {ex.Message}" });
			}
		}




		[HttpPost]
		public async Task<IActionResult> ToggleDepartmentStatus([FromBody] ToggleDepartmentRequest request)
		{
			if (!IsAdmin())
			{
				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"UPDATE",
					"Department",
					"Không có quyền thực hiện",
					new { DepartmentId = request.DepartmentId }
				);

				return Json(new { success = false, message = "Không có quyền thực hiện!" });
			}

			var department = await _context.Departments
				.Include(d => d.Users)
				.FirstOrDefaultAsync(d => d.DepartmentId == request.DepartmentId);

			if (department == null)
			{
				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"UPDATE",
					"Department",
					"Phòng ban không tồn tại",
					new { DepartmentId = request.DepartmentId }
				);

				return Json(new { success = false, message = "Không tìm thấy phòng ban!" });
			}

			if (department.IsActive == true && department.Users != null && department.Users.Any(u => u.IsActive == true))
			{
				var activeUserCount = department.Users.Count(u => u.IsActive == true);
				if (activeUserCount > 0)
				{
					await _auditHelper.LogFailedAttemptAsync(
						HttpContext.Session.GetInt32("UserId"),
						"UPDATE",
						"Department",
						"Phòng ban có nhân viên đang hoạt động",
						new { DepartmentId = request.DepartmentId, ActiveUsers = activeUserCount }
					);

					return Json(new
					{
						success = false,
						message = $"Không thể vô hiệu hóa phòng ban có {activeUserCount} nhân viên đang hoạt động!"
					});
				}
			}

			try
			{
				var adminId = HttpContext.Session.GetInt32("UserId");

				department.IsActive = !department.IsActive;
				department.UpdatedAt = DateTime.Now;

				await _context.SaveChangesAsync();

				await _auditHelper.LogAsync(
					adminId,
					"UPDATE",
					"Department",
					department.DepartmentId,
					new { IsActive = !department.IsActive },
					new { IsActive = department.IsActive },
					$"Thay đổi trạng thái phòng ban: {department.DepartmentName} - {(department.IsActive == true ? "Kích hoạt" : "Vô hiệu hóa")}"
				);

				return Json(new
				{
					success = true,
					message = $"Đã {(department.IsActive == true ? "kích hoạt" : "vô hiệu hóa")} phòng ban: {department.DepartmentName}",
					isActive = department.IsActive
				});
			}
			catch (Exception ex)
			{
				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"UPDATE",
					"Department",
					$"Exception: {ex.Message}",
					new { DepartmentId = request.DepartmentId, Error = ex.ToString() }
				);

				return Json(new { success = false, message = $"Có lỗi xảy ra: {ex.Message}" });
			}
		}

		[HttpPost]
		public async Task<IActionResult> DeleteDepartment([FromBody] DeleteDepartmentRequest request)
		{
			if (!IsSuperAdmin())
			{
				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"DELETE",
					"Department",
					"Không có quyền xóa",
					new { DepartmentId = request.DepartmentId }
				);

				return Json(new { success = false, message = "Không có quyền thực hiện!" });
			}

			var department = await _context.Departments
				.Include(d => d.Users)
				.FirstOrDefaultAsync(d => d.DepartmentId == request.DepartmentId);

			if (department == null)
			{
				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"DELETE",
					"Department",
					"Phòng ban không tồn tại",
					new { DepartmentId = request.DepartmentId }
				);

				return Json(new { success = false, message = "Không tìm thấy phòng ban!" });
			}

			if (department.Users != null && department.Users.Any())
			{
				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"DELETE",
					"Department",
					"Phòng ban có nhân viên",
					new { DepartmentId = request.DepartmentId, UserCount = department.Users.Count }
				);

				return Json(new
				{
					success = false,
					message = $"Không thể xóa phòng ban có {department.Users.Count} nhân viên! Vui lòng chuyển họ sang phòng ban khác trước."
				});
			}

			try
			{
				var adminId = HttpContext.Session.GetInt32("UserId");

				department.IsActive = false;
				department.UpdatedAt = DateTime.Now;

				await _context.SaveChangesAsync();

				await _auditHelper.LogAsync(
					adminId,
					"DELETE",
					"Department",
					department.DepartmentId,
					new { IsActive = true },
					new { IsActive = false },
					$"Xóa phòng ban: {department.DepartmentName}"
				);

				return Json(new
				{
					success = true,
					message = $"Đã xóa phòng ban: {department.DepartmentName}"
				});
			}
			catch (Exception ex)
			{
				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"DELETE",
					"Department",
					$"Exception: {ex.Message}",
					new { DepartmentId = request.DepartmentId, Error = ex.ToString() }
				);

				return Json(new { success = false, message = $"Có lỗi xảy ra: {ex.Message}" });
			}
		}

		[HttpGet]
		public async Task<IActionResult> GetDepartmentDetails(int id)
		{
			if (!IsAdminOrManager())
				return Json(new { success = false, message = "Không có quyền truy cập!" });

			await _auditHelper.LogViewAsync(
				HttpContext.Session.GetInt32("UserId").Value,
				"Department",
				id,
				"Xem chi tiết phòng ban (AJAX)"
			);

			var department = await _context.Departments
				.Include(d => d.Users)
					.ThenInclude(u => u.Role)
				.FirstOrDefaultAsync(d => d.DepartmentId == id);

			if (department == null)
				return Json(new { success = false, message = "Không tìm thấy phòng ban!" });

			var result = new
			{
				success = true,
				department = new
				{
					department.DepartmentId,
					department.DepartmentName,
					department.Description,
					department.IsActive,
					department.CreatedAt,
					department.UpdatedAt,
					TotalUsers = department.Users?.Count ?? 0,
					ActiveUsers = department.Users?.Count(u => u.IsActive == true) ?? 0,
					InactiveUsers = department.Users?.Count(u => u.IsActive == false) ?? 0,
					Users = department.Users?.Select(u => new
					{
						u.UserId,
						u.Username,
						u.FullName,
						u.Email,
						u.Avatar,
						RoleName = u.Role?.RoleName,
						u.IsActive
					}).OrderBy(u => u.FullName).ToList()
				}
			};

			return Json(result);
		}

		// ============================================
		// TASK MANAGEMENT - CRUD
		// ============================================
		// ============================================
		// TASK MANAGEMENT - CRUD
		// ============================================
		// ============================================
		// TASK MANAGEMENT - CRUD (FIXED)
		// ============================================
		public async Task<IActionResult> TaskList()
		{
			if (!IsAdminOrManager())
				return RedirectToAction("Login", "Account");

			var tasks = await _context.Tasks
				.Include(t => t.UserTasks)
					.ThenInclude(ut => ut.User)
				.OrderByDescending(t => t.CreatedAt)
				.ToListAsync();

			// ✅ TÍNH TOÁN STATISTICS ĐƠN GIẢN DỰA TRÊN STATUS
			ViewBag.TotalTasks = tasks.Count;
			ViewBag.ActiveTasks = tasks.Count(t => t.IsActive == true);
			ViewBag.InactiveTasks = tasks.Count(t => t.IsActive == false);

			// ✅ THỐNG KÊ THEO STATUS MỚI
			var allUserTasks = tasks.SelectMany(t => t.UserTasks).ToList();

			ViewBag.TodoTasks = allUserTasks.Count(ut => ut.Status == "TODO");
			ViewBag.InProgressTasks = allUserTasks.Count(ut => ut.Status == "InProgress");
			ViewBag.TestingTasks = allUserTasks.Count(ut => ut.Status == "Testing");
			ViewBag.CompletedTasks = allUserTasks.Count(ut => ut.Status == "Done");
			ViewBag.ReopenTasks = allUserTasks.Count(ut => ut.Status == "Reopen");

			// ✅ TASKS QUÁ HẠN
			ViewBag.OverdueTasks = tasks.Count(t =>
				t.IsActive == true &&
				t.Deadline.HasValue &&
				t.Deadline.Value < DateTime.Now &&
				t.UserTasks.Any(ut => ut.Status != "Done")
			);

			// ✅ COMPLETION RATE (theo Done status)
			var totalAssignments = allUserTasks.Count;
			var completedAssignments = allUserTasks.Count(ut => ut.Status == "Done");

			ViewBag.TaskCompletionRate = totalAssignments > 0
				? Math.Round((double)completedAssignments / totalAssignments * 100, 1)
				: 0;

			return View(tasks);
		}

		[HttpGet]
		public async Task<IActionResult> CreateTask()
		{
			if (!IsAdminOrManager())
				return RedirectToAction("Login", "Account");

			ViewBag.Users = await _context.Users
				.Include(u => u.Department)
				.Where(u => u.IsActive == true)
				.OrderBy(u => u.FullName)
				.ToListAsync();

			return View();
		}
		[HttpGet]
		public async Task<IActionResult> MyProjectDetail(int id)
		{
			if (!IsAuthenticated())
				return RedirectToAction("Login", "Account");

			var userId = HttpContext.Session.GetInt32("UserId").Value;

			// ✅ Kiểm tra membership
			var membership = await _context.ProjectMembers
				.Include(pm => pm.Project)
					.ThenInclude(p => p.Leader)
				.Include(pm => pm.Project)
					.ThenInclude(p => p.Department)
				.Include(pm => pm.Project)
					.ThenInclude(p => p.ProjectMembers)
						.ThenInclude(pm => pm.User)
							.ThenInclude(u => u.Department)
				.FirstOrDefaultAsync(pm => pm.ProjectId == id && pm.UserId == userId && pm.IsActive == true);

			if (membership == null)
			{
				TempData["Error"] = "Bạn không phải thành viên của dự án này";
				return RedirectToAction("MyProjects");
			}

			var project = membership.Project;

			// ✅ Lấy tasks và tính toán TRONG CONTROLLER
			var myTasks = await _context.UserTasks
				.Include(ut => ut.Task)
				.Include(ut => ut.Tester)
				.Where(ut => ut.UserId == userId &&
							ut.Task.ProjectId == id &&
							ut.Task.IsActive == true)
				.OrderBy(ut => ut.Status == "TODO" ? 1 : ut.Status == "InProgress" ? 2 : 3)
				.ThenByDescending(ut => ut.Task.Priority == "High" ? 1 : ut.Task.Priority == "Medium" ? 2 : 3)
				.ToListAsync();

			// ✅ TÍNH TOÁN TRƯỚC - Tránh lambda trong View
			var myTasksWithMetadata = myTasks.Select(ut => new
			{
				UserTask = ut,
				Task = ut.Task,
				IsOverdue = ut.Task.Deadline.HasValue &&
						   (ut.Status != "Done"
							? DateTime.Now > ut.Task.Deadline.Value
							: (ut.UpdatedAt.HasValue && ut.UpdatedAt.Value > ut.Task.Deadline.Value)),
				StatusBadgeClass = ut.Status switch
				{
					"TODO" => "style='background: rgba(107, 114, 128, 0.1); color: #6b7280;'",
					"InProgress" => "pm-badge-medium",
					"Testing" => "style='background: rgba(59, 130, 246, 0.1); color: var(--info);'",
					"Done" => "style='background: rgba(16, 185, 129, 0.1); color: var(--success);'",
					"Reopen" => "pm-badge-high",
					_ => ""
				},
				StatusText = ut.Status switch
				{
					"TODO" => "Chưa bắt đầu",
					"InProgress" => "Đang làm",
					"Testing" => "Chờ test",
					"Done" => "Hoàn thành",
					"Reopen" => "Cần sửa lại",
					_ => "TODO"
				},
				PriorityClass = ut.Task.Priority switch
				{
					"High" => "pm-badge-high",
					"Medium" => "pm-badge-medium",
					"Low" => "pm-badge-low",
					_ => "pm-badge-medium"
				}
			}).ToList();

			// ✅ Thống kê
			ViewBag.MyTotalTasks = myTasks.Count;
			ViewBag.MyTodoTasks = myTasks.Count(ut => ut.Status == "TODO");
			ViewBag.MyInProgressTasks = myTasks.Count(ut => ut.Status == "InProgress");
			ViewBag.MyTestingTasks = myTasks.Count(ut => ut.Status == "Testing");
			ViewBag.MyCompletedTasks = myTasks.Count(ut => ut.Status == "Done");
			ViewBag.MyReopenTasks = myTasks.Count(ut => ut.Status == "Reopen");

			ViewBag.MyCompletionRate = myTasks.Count > 0
				? Math.Round((double)ViewBag.MyCompletedTasks / myTasks.Count * 100, 1)
				: 0;

			ViewBag.IsLeader = project.LeaderId == userId;
			ViewBag.MyRole = membership.Role ?? "Member";

			ViewBag.Project = project;
			ViewBag.MyTasksWithMetadata = myTasksWithMetadata; // ✅ Gửi data đã xử lý

			// ✅ TÍNH SẴN MEMBERS
			var activeMembers = project.ProjectMembers
				.Where(pm => pm.IsActive)
				.OrderBy(pm => pm.Role == "Leader" ? 0 : 1)
				.Select(pm => new
				{
					Member = pm,
					HasAvatar = !string.IsNullOrEmpty(pm.User?.Avatar) &&
							   pm.User.Avatar != "/images/default-avatar.png",
					Initials = pm.User?.FullName?.Substring(0, 1).ToUpper() ?? "?"
				})
				.ToList();

			ViewBag.ActiveMembers = activeMembers;

			await _auditHelper.LogViewAsync(
				userId,
				"Project",
				id,
				$"Xem chi tiết dự án: {project.ProjectName}"
			);

			return View();
		}

		[HttpPost]
		public async Task<IActionResult> CreateTaskPost([FromBody] CreateTaskRequest request)
		{
			if (!IsAdmin())
			{
				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"CREATE",
					"Task",
					"Không có quyền tạo task",
					new { TaskName = request.TaskName }
				);

				return Json(new { success = false, message = "Không có quyền thực hiện!" });
			}

			if (string.IsNullOrWhiteSpace(request.TaskName))
			{
				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"CREATE",
					"Task",
					"Tên task rỗng",
					null
				);

				return Json(new { success = false, message = "Tên task không được để trống!" });
			}

			if (request.Deadline.HasValue && request.Deadline.Value < DateTime.Now)
			{
				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"CREATE",
					"Task",
					"Deadline không hợp lệ",
					new { TaskName = request.TaskName, Deadline = request.Deadline }
				);

				return Json(new { success = false, message = "Deadline không được là thời điểm trong quá khứ!" });
			}

			try
			{
				var adminId = HttpContext.Session.GetInt32("UserId");

				var task = new TMD.Models.Task
				{
					TaskName = request.TaskName.Trim(),
					Description = request.Description?.Trim(),
					Platform = request.Platform?.Trim(),
					Deadline = request.Deadline,
					Priority = request.Priority ?? "Medium",
					// ❌ TargetPerWeek = request.TargetPerWeek, // REMOVED
					IsActive = true,
					CreatedAt = DateTime.Now,
					UpdatedAt = DateTime.Now
				};

				_context.Tasks.Add(task);
				await _context.SaveChangesAsync();

				// ✅ TẠO USERTASK VỚI STATUS MẶC ĐỊNH = "TODO"
				if (request.AssignedUserIds != null && request.AssignedUserIds.Count > 0)
				{
					foreach (var userId in request.AssignedUserIds)
					{
						var userTask = new UserTask
						{
							UserId = userId,
							TaskId = task.TaskId,
							Status = "TODO",
							ReportLink = null,
							CreatedAt = DateTime.Now,
							UpdatedAt = DateTime.Now
						};
						_context.UserTasks.Add(userTask);

						// GỬI THÔNG BÁO
						await _notificationService.SendToUserAsync(
							userId,
							"Nhiệm vụ mới",
							$"Bạn vừa được giao task: {request.TaskName}",
							"info",
							"/Staff/MyTasks"
						);
					}
					await _context.SaveChangesAsync();
				}

				await _auditHelper.LogDetailedAsync(
					adminId,
					"CREATE",
					"Task",
					task.TaskId,
					null,
					new { task.TaskName, task.Platform, task.Priority, task.Deadline },
					$"Tạo task mới: {task.TaskName}",
					new Dictionary<string, object>
					{
				{ "AssignedUsers", request.AssignedUserIds?.Count ?? 0 },
				{ "CreatedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") }
					}
				);

				return Json(new
				{
					success = true,
					message = "Tạo task thành công!",
					taskId = task.TaskId
				});
			}
			catch (Exception ex)
			{
				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"CREATE",
					"Task",
					$"Exception: {ex.Message}",
					new { TaskName = request.TaskName, Error = ex.ToString() }
				);

				return Json(new { success = false, message = $"Có lỗi xảy ra: {ex.Message}" });
			}
		}
		[HttpGet]
		public async Task<IActionResult> EditProject(int id)
		{
			if (!IsAdminOrManager())
				return RedirectToAction("Login", "Account");

			var project = await _context.Projects
				.Include(p => p.Leader)
				.Include(p => p.Department)
				.Include(p => p.ProjectMembers)
					.ThenInclude(pm => pm.User)
				.FirstOrDefaultAsync(p => p.ProjectId == id);

			if (project == null)
				return NotFound();

			ViewBag.Departments = await _context.Departments
				.Where(d => d.IsActive == true)
				.OrderBy(d => d.DepartmentName)
				.ToListAsync();

			ViewBag.Users = await _context.Users
				.Include(u => u.Department)
				.Where(u => u.IsActive == true)
				.OrderBy(u => u.FullName)
				.ToListAsync();

			ViewBag.ProjectMembers = project.ProjectMembers
				.Where(pm => pm.IsActive)
				.Select(pm => pm.UserId)
				.ToList();

			await _auditHelper.LogViewAsync(
				HttpContext.Session.GetInt32("UserId").Value,
				"Project",
				id,
				$"Vào trang chỉnh sửa dự án: {project.ProjectName}"
			);

			return View(project);
		}


		[HttpGet]
		public async Task<IActionResult> EditTask(int id)
		{
			if (!IsAdminOrManager())
				return RedirectToAction("Login", "Account");

			var task = await _context.Tasks
				.Include(t => t.UserTasks)
					.ThenInclude(ut => ut.User)
						.ThenInclude(u => u.Department)
				.FirstOrDefaultAsync(t => t.TaskId == id);

			if (task == null)
				return NotFound();

			ViewBag.Users = await _context.Users
				.Include(u => u.Department)
				.Where(u => u.IsActive == true)
				.OrderBy(u => u.FullName)
				.ToListAsync();

			ViewBag.AssignedUserIds = task.UserTasks.Select(ut => ut.UserId).ToList();

			return View(task);
		}

		[HttpPost]
		public async Task<IActionResult> UpdateTask([FromBody] UpdateTaskRequest request)
		{
			if (!IsAdminOrManager())
			{
				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"UPDATE",
					"Task",
					"Không có quyền cập nhật",
					new { TaskId = request.TaskId }
				);
				return Json(new { success = false, message = "Không có quyền thực hiện!" });
			}

			if (string.IsNullOrWhiteSpace(request.TaskName))
			{
				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"UPDATE",
					"Task",
					"Tên task rỗng",
					new { TaskId = request.TaskId }
				);
				return Json(new { success = false, message = "Tên task không được để trống!" });
			}

			var task = await _context.Tasks
				.Include(t => t.UserTasks)
					.ThenInclude(ut => ut.User)
				.FirstOrDefaultAsync(t => t.TaskId == request.TaskId);

			if (task == null)
			{
				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"UPDATE",
					"Task",
					"Task không tồn tại",
					new { TaskId = request.TaskId }
				);
				return Json(new { success = false, message = "Không tìm thấy task!" });
			}

			try
			{
				var adminId = HttpContext.Session.GetInt32("UserId");

				var oldValues = new
				{
					task.TaskName,
					task.Description,
					task.Platform,
					task.Deadline,
					task.Priority
				};

				var oldAssignments = task.UserTasks.ToList();
				var oldUserIds = oldAssignments.Select(ut => ut.UserId).ToList();
				var newUserIds = request.AssignedUserIds ?? new List<int>();

				// CẬP NHẬT THÔNG TIN TASK
				task.TaskName = request.TaskName.Trim();
				task.Description = request.Description?.Trim();
				task.Platform = request.Platform?.Trim();
				task.Deadline = request.Deadline;
				task.Priority = request.Priority ?? "Medium";
				// ❌ task.TargetPerWeek = request.TargetPerWeek; // REMOVED
				task.UpdatedAt = DateTime.Now;

				// XỬ LÝ XÓA USER
				var toRemove = oldAssignments.Where(ut => !newUserIds.Contains(ut.UserId)).ToList();
				foreach (var ut in toRemove)
				{
					await _notificationService.SendToUserAsync(
						ut.UserId,
						"Task đã bị gỡ",
						$"Bạn không còn được giao task: {task.TaskName}",
						"warning",
						"/Staff/MyTasks"
					);
				}
				_context.UserTasks.RemoveRange(toRemove);

				// XỬ LÝ THÊM USER MỚI
				var toAdd = newUserIds.Where(uid => !oldUserIds.Contains(uid)).ToList();
				foreach (var userId in toAdd)
				{
					var userTask = new UserTask
					{
						UserId = userId,
						TaskId = task.TaskId,
						Status = "TODO",
						CreatedAt = DateTime.Now,
						UpdatedAt = DateTime.Now
					};
					_context.UserTasks.Add(userTask);

					await _notificationService.SendToUserAsync(
						userId,
						"Task mới được giao",
						$"Bạn vừa được giao task: {request.TaskName}",
						"info",
						"/Staff/MyTasks"
					);
				}

				// THÔNG BÁO CHO USER ĐÃ TỒN TẠI (nếu task info thay đổi)
				bool taskInfoChanged =
					oldValues.TaskName != task.TaskName ||
					oldValues.Description != task.Description ||
					oldValues.Platform != task.Platform ||
					oldValues.Deadline != task.Deadline ||
					oldValues.Priority != task.Priority;

				if (taskInfoChanged)
				{
					var remainingUserIds = oldUserIds.Intersect(newUserIds).ToList();
					foreach (var userId in remainingUserIds)
					{
						await _notificationService.SendToUserAsync(
							userId,
							"Task đã được cập nhật",
							$"Task '{task.TaskName}' vừa được cập nhật thông tin",
							"info",
							"/Staff/MyTasks"
						);
					}
				}

				await _context.SaveChangesAsync();

				var newValues = new
				{
					task.TaskName,
					task.Description,
					task.Platform,
					task.Deadline,
					task.Priority
				};

				await _auditHelper.LogAsync(
					adminId,
					"UPDATE",
					"Task",
					task.TaskId,
					oldValues,
					newValues,
					$"Cập nhật task: {task.TaskName}"
				);

				return Json(new
				{
					success = true,
					message = "Cập nhật task thành công!"
				});
			}
			catch (Exception ex)
			{
				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"UPDATE",
					"Task",
					$"Exception: {ex.Message}",
					new { TaskId = request.TaskId, Error = ex.ToString() }
				);

				return Json(new { success = false, message = $"Có lỗi xảy ra: {ex.Message}" });
			}
		}

		[HttpPost]
		public async Task<IActionResult> DeleteTask([FromBody] DeleteTaskRequest request)
		{
			if (!IsAdminOrManager())
			{
				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"DELETE",
					"Task",
					"Không có quyền xóa",
					new { TaskId = request.TaskId }
				);

				return Json(new { success = false, message = "Không có quyền thực hiện!" });
			}

			var task = await _context.Tasks.FindAsync(request.TaskId);

			if (task == null)
			{
				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"DELETE",
					"Task",
					"Task không tồn tại",
					new { TaskId = request.TaskId }
				);

				return Json(new { success = false, message = "Không tìm thấy task!" });
			}

			try
			{
				var adminId = HttpContext.Session.GetInt32("UserId");

				task.IsActive = false;
				task.UpdatedAt = DateTime.Now;

				await _context.SaveChangesAsync();

				await _auditHelper.LogAsync(
					adminId,
					"DELETE",
					"Task",
					task.TaskId,
					new { IsActive = true },
					new { IsActive = false },
					$"Xóa task: {task.TaskName}"
				);

				return Json(new
				{
					success = true,
					message = $"Đã xóa task: {task.TaskName}"
				});
			}
			catch (Exception ex)
			{
				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"DELETE",
					"Task",
					$"Exception: {ex.Message}",
					new { TaskId = request.TaskId, Error = ex.ToString() }
				);

				return Json(new { success = false, message = $"Có lỗi xảy ra: {ex.Message}" });
			}
		}

		[HttpPost]
		public async Task<IActionResult> ToggleTaskStatus([FromBody] ToggleTaskStatusRequest request)
		{
			if (!IsAdminOrManager())
			{
				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"UPDATE",
					"Task",
					"Không có quyền thực hiện",
					new { TaskId = request.TaskId }
				);

				return Json(new { success = false, message = "Không có quyền thực hiện!" });
			}

			var task = await _context.Tasks.FindAsync(request.TaskId);

			if (task == null)
			{
				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"UPDATE",
					"Task",
					"Task không tồn tại",
					new { TaskId = request.TaskId }
				);

				return Json(new { success = false, message = "Không tìm thấy task!" });
			}

			try
			{
				var adminId = HttpContext.Session.GetInt32("UserId");

				task.IsActive = !task.IsActive;
				task.UpdatedAt = DateTime.Now;

				await _context.SaveChangesAsync();

				await _auditHelper.LogAsync(
					adminId,
					"UPDATE",
					"Task",
					task.TaskId,
					new { IsActive = !task.IsActive },
					new { IsActive = task.IsActive },
					$"Thay đổi trạng thái task: {task.TaskName} - {(task.IsActive == true ? "Kích hoạt" : "Vô hiệu hóa")}"
				);

				return Json(new
				{
					success = true,
					message = $"Đã {(task.IsActive == true ? "kích hoạt" : "vô hiệu hóa")} task: {task.TaskName}",
					isActive = task.IsActive
				});
			}
			catch (Exception ex)
			{
				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"UPDATE",
					"Task",
					$"Exception: {ex.Message}",
					new { TaskId = request.TaskId, Error = ex.ToString() }
				);

				return Json(new { success = false, message = $"Có lỗi xảy ra: {ex.Message}" });
			}
		}

		// ============================================
		// ✅ FIX HOÀN CHỈNH - GET TASK DETAILS
		// ============================================
		// ============================================
		// ✅ GET TASK DETAILS - ĐƠNGIẢN & ĐẦY ĐỦ
		// ============================================
		// Thêm action này vào AdminController.cs

		// ============================================
		// GET TASK DETAILS - UPDATED VERSION
		// Thêm vào AdminController.cs
		// ============================================

		[HttpGet]
		public async Task<IActionResult> GetTaskDetails(int id)
		{
			if (!IsAdmin())
				return Json(new { success = false, message = "Không có quyền truy cập" });

			try
			{
				var task = await _context.Tasks
					.Include(t => t.UserTasks)
						.ThenInclude(ut => ut.User)
							.ThenInclude(u => u.Department)
					.FirstOrDefaultAsync(t => t.TaskId == id);

				if (task == null)
					return Json(new { success = false, message = "Không tìm thấy task" });

				if (task.UserTasks == null)
					task.UserTasks = new List<UserTask>();

				await _auditHelper.LogViewAsync(
					HttpContext.Session.GetInt32("UserId").Value,
					"Task",
					id,
					$"Xem chi tiết task: {task.TaskName}"
				);

				// ✅ STATISTICS
				var todoCount = task.UserTasks.Count(ut => ut.Status == "TODO");
				var inProgressCount = task.UserTasks.Count(ut => ut.Status == "InProgress");
				var testingCount = task.UserTasks.Count(ut => ut.Status == "Testing");
				var doneCount = task.UserTasks.Count(ut => ut.Status == "Done");
				var reopenCount = task.UserTasks.Count(ut => ut.Status == "Reopen");

				// ✅ ASSIGNED USERS WITH STATUS
				var assignedUsers = task.UserTasks.Select(ut => {
					// Map status to badge class
					string statusClass = ut.Status switch
					{
						"TODO" => "secondary",
						"InProgress" => "warning",
						"Testing" => "info",
						"Done" => "success",
						"Reopen" => "danger",
						_ => "secondary"
					};

					// Map status to text
					string statusText = ut.Status switch
					{
						"TODO" => "Chưa bắt đầu",
						"InProgress" => "Đang làm",
						"Testing" => "Chờ test",
						"Done" => "Hoàn thành",
						"Reopen" => "Reopen",
						_ => "Chưa bắt đầu"
					};

					bool hasAvatar = !string.IsNullOrEmpty(ut.User?.Avatar) &&
								   ut.User.Avatar != "/images/default-avatar.png";

					return new
					{
						UserId = ut.UserId,
						FullName = ut.User?.FullName ?? "N/A",
						DepartmentName = ut.User?.Department?.DepartmentName ?? "N/A",
						Avatar = hasAvatar ? ut.User.Avatar : (string)null,
						Status = ut.Status ?? "TODO",
						StatusClass = statusClass,
						StatusText = statusText,
						ReportLink = ut.ReportLink ?? "",
						UpdatedAtStr = ut.UpdatedAt?.ToString("dd/MM/yyyy HH:mm") ?? "Chưa cập nhật"
					};
				}).OrderBy(u => u.FullName).ToList();

				var result = new
				{
					success = true,
					task = new
					{
						TaskId = task.TaskId,
						TaskName = task.TaskName ?? "Không có tên",
						Description = task.Description ?? "Không có mô tả",
						Platform = task.Platform ?? "N/A",
						Priority = task.Priority ?? "Medium",
						DeadlineStr = task.Deadline?.ToString("dd/MM/yyyy HH:mm") ?? "",
						IsActive = task.IsActive ?? false,
						CreatedAtStr = task.CreatedAt?.ToString("dd/MM/yyyy HH:mm") ?? "N/A",
						UpdatedAtStr = task.UpdatedAt?.ToString("dd/MM/yyyy HH:mm") ?? "N/A",

						// Status counts
						TodoCount = todoCount,
						InProgressCount = inProgressCount,
						TestingCount = testingCount,
						DoneCount = doneCount,
						ReopenCount = reopenCount,

						// Assigned users with status
						AssignedUsers = assignedUsers
					}
				};

				return Json(result);
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"[GetTaskDetails] ERROR: {ex.Message}\n{ex.StackTrace}");

				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"VIEW",
					"Task",
					$"Exception: {ex.Message}",
					new { TaskId = id, Error = ex.ToString() }
				);

				return Json(new { success = false, message = $"Lỗi: {ex.Message}" });
			}
		}
		// ============================================
		// GET PENDING REQUEST COUNT (FOR NOTIFICATION)
		// ============================================
		[HttpGet]
		public async Task<IActionResult> GetPendingCount()
		{
			if (!IsAdmin())
				return Json(new { success = false, count = 0 });

			try
			{
				var overtimePending = await _context.OvertimeRequests
					.CountAsync(r => r.Status == "Pending");

				var leavePending = await _context.LeaveRequests
					.CountAsync(r => r.Status == "Pending");

				var latePending = await _context.LateRequests
					.CountAsync(r => r.Status == "Pending");

				var totalPending = overtimePending + leavePending + latePending;

				return Json(new
				{
					success = true,
					count = totalPending,
					overtime = overtimePending,
					leave = leavePending,
					late = latePending
				});
			}
			catch (Exception ex)
			{
				return Json(new { success = false, count = 0, error = ex.Message });
			}
		}
		// Thêm method này vào AdminController.cs
		// Thay thế cả 2 method này

		[HttpGet]
		public async Task<JsonResult> GetPendingRequests(string? type, string? status, string? from, string? to, string? keyword)
		{
			if (!IsAdminOrManager())
				return Json(new { success = false, message = "Không có quyền truy cập" });

			try
			{
				// Parse dates
				DateTime? fromDate = string.IsNullOrEmpty(from) ? null : DateTime.Parse(from);
				DateTime? toDate = string.IsNullOrEmpty(to) ? null : DateTime.Parse(to);

				var overtime = new List<object>();
				var leave = new List<object>();
				var late = new List<object>();

				// Overtime Requests
				if (string.IsNullOrEmpty(type) || type == "Overtime")
				{
					var otQuery = _context.OvertimeRequests
						.Include(x => x.User)
						.AsQueryable();

					if (!string.IsNullOrEmpty(status))
						otQuery = otQuery.Where(x => x.Status == status);

					if (fromDate.HasValue)
						otQuery = otQuery.Where(x => x.CreatedAt >= fromDate.Value);

					if (toDate.HasValue)
						otQuery = otQuery.Where(x => x.CreatedAt <= toDate.Value.AddDays(1));

					if (!string.IsNullOrEmpty(keyword))
					{
						var kw = keyword.Trim().ToLower();
						otQuery = otQuery.Where(x =>
							(x.Reason ?? "").ToLower().Contains(kw) ||
							(x.TaskDescription ?? "").ToLower().Contains(kw) ||
							(x.User.FullName ?? "").ToLower().Contains(kw)
						);
					}

					overtime = await otQuery
						.OrderByDescending(x => x.CreatedAt)
						.Select(x => new
						{
							x.OvertimeRequestId,
							x.UserId,
							employeeId = x.User.Username,
							employeeName = x.User.FullName,
							workDate = x.WorkDate.ToString("yyyy-MM-dd"),
							actualCheckOutTime = x.ActualCheckOutTime != null ? ((DateTime)x.ActualCheckOutTime).ToString("HH:mm:ss") : "N/A",
							overtimeHours = x.OvertimeHours,
							reason = x.Reason ?? "",
							taskDescription = x.TaskDescription ?? "",
							status = x.Status ?? "",
							createdAt = x.CreatedAt.HasValue ? x.CreatedAt.Value.ToString("yyyy-MM-dd HH:mm:ss") : ""
						})
						.ToListAsync<object>();
				}

				// Leave Requests
				if (string.IsNullOrEmpty(type) || type == "Leave")
				{
					var leaveQuery = _context.LeaveRequests
						.Include(x => x.User)
						.AsQueryable();

					if (!string.IsNullOrEmpty(status))
						leaveQuery = leaveQuery.Where(x => x.Status == status);

					if (fromDate.HasValue)
						leaveQuery = leaveQuery.Where(x => x.CreatedAt >= fromDate.Value);

					if (toDate.HasValue)
						leaveQuery = leaveQuery.Where(x => x.CreatedAt <= toDate.Value.AddDays(1));

					if (!string.IsNullOrEmpty(keyword))
					{
						var kw = keyword.Trim().ToLower();
						leaveQuery = leaveQuery.Where(x =>
							(x.Reason ?? "").ToLower().Contains(kw) ||
							(x.ProofDocument ?? "").ToLower().Contains(kw) ||
							(x.User.FullName ?? "").ToLower().Contains(kw)
						);
					}

					leave = await leaveQuery
						.OrderByDescending(x => x.CreatedAt)
						.Select(x => new
						{
							x.LeaveRequestId,
							x.UserId,
							employeeId = x.User.Username,
							employeeName = x.User.FullName,
							leaveType = x.LeaveType ?? "",
							startDate = x.StartDate.ToString("yyyy-MM-dd"),
							endDate = x.EndDate.ToString("yyyy-MM-dd"),
							totalDays = x.TotalDays,
							reason = x.Reason ?? "",
							proofDocument = x.ProofDocument ?? "",
							status = x.Status ?? "",
							createdAt = x.CreatedAt.HasValue ? x.CreatedAt.Value.ToString("yyyy-MM-dd HH:mm:ss") : ""
						})
						.ToListAsync<object>();
				}

				// Late Requests
				if (string.IsNullOrEmpty(type) || type == "Late")
				{
					var lateQuery = _context.LateRequests
						.Include(x => x.User)
						.AsQueryable();

					if (!string.IsNullOrEmpty(status))
						lateQuery = lateQuery.Where(x => x.Status == status);

					if (fromDate.HasValue)
						lateQuery = lateQuery.Where(x => x.CreatedAt >= fromDate.Value);

					if (toDate.HasValue)
						lateQuery = lateQuery.Where(x => x.CreatedAt <= toDate.Value.AddDays(1));

					if (!string.IsNullOrEmpty(keyword))
					{
						var kw = keyword.Trim().ToLower();
						lateQuery = lateQuery.Where(x =>
							(x.Reason ?? "").ToLower().Contains(kw) ||
							(x.ProofDocument ?? "").ToLower().Contains(kw) ||
							(x.User.FullName ?? "").ToLower().Contains(kw)
						);
					}

					late = await lateQuery
						.OrderByDescending(x => x.CreatedAt)
						.Select(x => new
						{
							x.LateRequestId,
							x.UserId,
							employeeId = x.User.Username,
							employeeName = x.User.FullName,
							requestDate = x.RequestDate.ToString("yyyy-MM-dd"),
							expectedArrivalTime = x.ExpectedArrivalTime.ToString("HH:mm"),
							reason = x.Reason ?? "",
							proofDocument = x.ProofDocument ?? "",
							status = x.Status ?? "",
							createdAt = x.CreatedAt.HasValue ? x.CreatedAt.Value.ToString("yyyy-MM-dd HH:mm:ss") : ""
						})
						.ToListAsync<object>();
				}

				return Json(new { success = true, overtime, leave, late });
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"GetPendingRequests Error: {ex.Message}\n{ex.StackTrace}");
				return Json(new { success = false, message = $"Lỗi: {ex.Message}" });
			}
		}

		[HttpGet]
		public async Task<JsonResult> GetRequestDetail(string type, int id)
		{
			if (!IsAdmin())
				return Json(new { success = false, message = "Không có quyền" });

			try
			{
				if (type == "Overtime")
				{
					var r = await _context.OvertimeRequests
						.Include(x => x.User)
						.FirstOrDefaultAsync(x => x.OvertimeRequestId == id);

					if (r == null)
						return Json(new { success = false, message = "Không tìm thấy request" });

					// Xử lý ActualCheckOutTime an toàn
					string checkOutTimeStr = "N/A";
					if (r.ActualCheckOutTime != null && r.ActualCheckOutTime != default(DateTime))
					{
						checkOutTimeStr = ((DateTime)r.ActualCheckOutTime).ToString("HH:mm:ss");
					}

					return Json(new
					{
						success = true,
						request = new
						{
							overtimeRequestId = r.OvertimeRequestId,
							userId = r.UserId,
							employeeName = r.User?.FullName ?? "N/A",
							workDate = r.WorkDate.ToString("dd/MM/yyyy"),
							actualCheckOutTime = checkOutTimeStr,
							overtimeHours = r.OvertimeHours,
							reason = r.Reason ?? "",
							taskDescription = r.TaskDescription ?? "",
							status = r.Status ?? "Pending",
							reviewedBy = r.ReviewedBy ?? 0,
							reviewedAt = r.ReviewedAt?.ToString("dd/MM/yyyy HH:mm") ?? "",
							reviewNote = r.ReviewNote ?? "",
							createdAt = r.CreatedAt?.ToString("dd/MM/yyyy HH:mm") ?? "",
							updatedAt = r.UpdatedAt?.ToString("dd/MM/yyyy HH:mm") ?? ""
						}
					});
				}

				if (type == "Leave")
				{
					var r = await _context.LeaveRequests
						.Include(x => x.User)
						.FirstOrDefaultAsync(x => x.LeaveRequestId == id);

					if (r == null)
						return Json(new { success = false, message = "Không tìm thấy request" });

					return Json(new
					{
						success = true,
						request = new
						{
							leaveRequestId = r.LeaveRequestId,
							userId = r.UserId,
							employeeName = r.User?.FullName ?? "N/A",
							leaveType = r.LeaveType ?? "",
							startDate = r.StartDate.ToString("dd/MM/yyyy"),
							endDate = r.EndDate.ToString("dd/MM/yyyy"),
							totalDays = r.TotalDays,
							reason = r.Reason ?? "",
							proofDocument = r.ProofDocument ?? "",
							status = r.Status ?? "Pending",
							reviewedBy = r.ReviewedBy ?? 0,
							reviewedAt = r.ReviewedAt?.ToString("dd/MM/yyyy HH:mm") ?? "",
							reviewNote = r.ReviewNote ?? "",
							createdAt = r.CreatedAt?.ToString("dd/MM/yyyy HH:mm") ?? "",
							updatedAt = r.UpdatedAt?.ToString("dd/MM/yyyy HH:mm") ?? ""
						}
					});
				}

				if (type == "Late")
				{
					var r = await _context.LateRequests
						.Include(x => x.User)
						.FirstOrDefaultAsync(x => x.LateRequestId == id);

					if (r == null)
						return Json(new { success = false, message = "Không tìm thấy request" });

					return Json(new
					{
						success = true,
						request = new
						{
							lateRequestId = r.LateRequestId,
							userId = r.UserId,
							employeeName = r.User?.FullName ?? "N/A",
							requestDate = r.RequestDate.ToString("dd/MM/yyyy"),
							expectedArrivalTime = r.ExpectedArrivalTime.ToString("HH:mm"),
							reason = r.Reason ?? "",
							proofDocument = r.ProofDocument ?? "",
							status = r.Status ?? "Pending",
							reviewedBy = r.ReviewedBy ?? 0,
							reviewedAt = r.ReviewedAt?.ToString("dd/MM/yyyy HH:mm") ?? "",
							reviewNote = r.ReviewNote ?? "",
							createdAt = r.CreatedAt?.ToString("dd/MM/yyyy HH:mm") ?? "",
							updatedAt = r.UpdatedAt?.ToString("dd/MM/yyyy HH:mm") ?? ""
						}
					});
				}

				return Json(new { success = false, message = "Loại request không hợp lệ" });
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"GetRequestDetail Error: {ex.Message}\n{ex.StackTrace}");
				return Json(new { success = false, message = $"Lỗi server: {ex.Message}" });
			}
		}

		[HttpGet]
		public async Task<IActionResult> PendingRequests(string? type, string? status, DateTime? fromDate, DateTime? toDate, string? keyword)
		{
			if (!IsAdmin()) return RedirectToAction("Login", "Account");

			var from = fromDate ?? DateTime.Today.AddMonths(-1);
			var to = toDate ?? DateTime.Today.AddDays(1);

			var vm = new TMD.Models.ViewModels.PendingRequestsViewModel
			{
				SelectedType = type
			};

			// Prepare base queries
			IQueryable<OvertimeRequest> otQuery = _context.OvertimeRequests.Include(r => r.User).AsQueryable();
			IQueryable<LeaveRequest> leaveQuery = _context.LeaveRequests.Include(r => r.User).AsQueryable();
			IQueryable<LateRequest> lateQuery = _context.LateRequests.Include(r => r.User).AsQueryable();

			// Date range filter (CreatedAt)
			otQuery = otQuery.Where(r => r.CreatedAt >= from && r.CreatedAt <= to);
			leaveQuery = leaveQuery.Where(r => r.CreatedAt >= from && r.CreatedAt <= to);
			lateQuery = lateQuery.Where(r => r.CreatedAt >= from && r.CreatedAt <= to);

			// Status filter (if provided)
			if (!string.IsNullOrWhiteSpace(status))
			{
				otQuery = otQuery.Where(r => r.Status == status);
				leaveQuery = leaveQuery.Where(r => r.Status == status);
				lateQuery = lateQuery.Where(r => r.Status == status);
			}

			// Keyword filter (if provided) - search in reason, task description, proof document
			if (!string.IsNullOrWhiteSpace(keyword))
			{
				var kw = keyword.Trim().ToLower();
				otQuery = otQuery.Where(r => (r.Reason ?? "").ToLower().Contains(kw) || (r.TaskDescription ?? "").ToLower().Contains(kw));
				leaveQuery = leaveQuery.Where(r => (r.Reason ?? "").ToLower().Contains(kw) || (r.ProofDocument ?? "").ToLower().Contains(kw));
				lateQuery = lateQuery.Where(r => (r.Reason ?? "").ToLower().Contains(kw) || (r.ProofDocument ?? "").ToLower().Contains(kw));
			}

			// Only fetch types requested (to save queries)
			if (string.IsNullOrEmpty(type) || type == "Overtime")
			{
				vm.Overtime = await otQuery
					.OrderByDescending(r => r.CreatedAt)
					.ToListAsync();
			}

			if (string.IsNullOrEmpty(type) || type == "Leave")
			{
				vm.Leave = await leaveQuery
					.OrderByDescending(r => r.CreatedAt)
					.ToListAsync();
			}

			if (string.IsNullOrEmpty(type) || type == "Late")
			{
				vm.Late = await lateQuery
					.OrderByDescending(r => r.CreatedAt)
					.ToListAsync();
			}

			// Preserve view state for UI
			ViewBag.Type = type;
			ViewBag.FilterStatus = status;
			ViewBag.FromDate = fromDate;
			ViewBag.ToDate = toDate;
			ViewBag.Keyword = keyword;

			// Calculate statistics for display
			var allOt = vm.Overtime ?? new List<OvertimeRequest>();
			var allLeave = vm.Leave ?? new List<LeaveRequest>();
			var allLate = vm.Late ?? new List<LateRequest>();

			ViewBag.TotalPending = allOt.Count(r => r.Status == "Pending") +
								   allLeave.Count(r => r.Status == "Pending") +
								   allLate.Count(r => r.Status == "Pending");

			ViewBag.TotalApproved = allOt.Count(r => r.Status == "Approved") +
									allLeave.Count(r => r.Status == "Approved") +
									allLate.Count(r => r.Status == "Approved");

			ViewBag.TotalRejected = allOt.Count(r => r.Status == "Rejected") +
									allLeave.Count(r => r.Status == "Rejected") +
									allLate.Count(r => r.Status == "Rejected");

			return View(vm);
		}


		// ✅ THÊM/THAY THẾ METHOD NÀY VÀO AdminController.cs

		[HttpPost]
		public async Task<IActionResult> ReviewRequest([FromBody] ReviewRequestViewModel model)
		{
			if (!IsAdminOrManager())
				return Json(new { success = false, message = "Không có quyền" });

			var adminId = HttpContext.Session.GetInt32("UserId");

			try
			{
				if (model.RequestType == "Overtime")
				{
					var r = await _context.OvertimeRequests
						.Include(x => x.User)
						.FirstOrDefaultAsync(x => x.OvertimeRequestId == model.RequestId);

					if (r == null) return Json(new { success = false, message = "Không tìm thấy" });

					var old = new { r.Status, r.ReviewedBy, r.ReviewedAt, r.ReviewNote };

					if (model.Action == "Approve")
					{
						r.Status = "Approved";
						r.ReviewedBy = adminId;
						r.ReviewedAt = DateTime.Now;
						r.ReviewNote = model.Note;

						var att = await _context.Attendances
							.FirstOrDefaultAsync(a => a.UserId == r.UserId && a.WorkDate == r.WorkDate);

						if (att != null)
						{
							att.IsOvertimeApproved = true;
							att.ApprovedOvertimeHours = r.OvertimeHours;
							att.HasOvertimeRequest = true;
							att.OvertimeRequestId = r.OvertimeRequestId;
							att.UpdatedAt = DateTime.Now;
						}

						// GỬI THÔNG BÁO CHO USER
						// ✅ ĐÚNG
						await _notificationService.SendToUserAsync(
							r.UserId,
							"Tăng ca được duyệt",
							$"Yêu cầu tăng ca {r.OvertimeHours:F2}h ngày {r.WorkDate:dd/MM/yyyy} đã được phê duyệt",
							"success",
							"/Staff/AttendanceHistory"
						);
					}
					else if (model.Action == "Reject")
					{
						r.Status = "Rejected";
						r.ReviewedBy = adminId;
						r.ReviewedAt = DateTime.Now;
						r.ReviewNote = model.Note;

						var att = await _context.Attendances
							.FirstOrDefaultAsync(a => a.UserId == r.UserId && a.WorkDate == r.WorkDate);

						if (att != null)
						{
							att.IsOvertimeApproved = false;
							att.ApprovedOvertimeHours = 0;
							att.HasOvertimeRequest = true;
							att.OvertimeRequestId = r.OvertimeRequestId;
							att.UpdatedAt = DateTime.Now;
						}

						// GỬI THÔNG BÁO CHO USER
						// ✅ ĐÚNG
						await _notificationService.SendToUserAsync(
							r.UserId,
							"Tăng ca bị từ chối",
							$"Yêu cầu tăng ca bị từ chối. Lý do: {model.Note}",
							"error",
							"/Staff/MyRequests"
						);
					}

					r.UpdatedAt = DateTime.Now;
					await _context.SaveChangesAsync();

					await _auditHelper.LogAsync(
						adminId,
						"REVIEW",
						"OvertimeRequest",
						r.OvertimeRequestId,
						old,
						new { r.Status, r.ReviewedBy, r.ReviewedAt, r.ReviewNote },
						$"Admin {(model.Action == "Approve" ? "DUYỆT" : "TỪ CHỐI")} overtime request #{r.OvertimeRequestId}"
					);

					return Json(new
					{
						success = true,
						message = model.Action == "Approve"
							? $"Đã duyệt tăng ca {r.OvertimeHours:F2}h"
							: "Đã từ chối yêu cầu tăng ca"
					});
				}

				if (model.RequestType == "Leave")
				{
					var r = await _context.LeaveRequests
						.Include(x => x.User)
						.FirstOrDefaultAsync(x => x.LeaveRequestId == model.RequestId);

					if (r == null) return Json(new { success = false, message = "Không tìm thấy" });

					var old = new { r.Status, r.ReviewedBy, r.ReviewedAt, r.ReviewNote };

					if (model.Action == "Approve")
					{
						r.Status = "Approved";
						r.ReviewedBy = adminId;
						r.ReviewedAt = DateTime.Now;
						r.ReviewNote = model.Note;

						var leaveMultiplierConfig = await _context.SystemSettings
							.FirstOrDefaultAsync(c => c.SettingKey == "LEAVE_ANNUAL_MULTIPLIER" && c.IsActive == true);

						var leaveMultiplier = leaveMultiplierConfig != null
							? decimal.Parse(leaveMultiplierConfig.SettingValue) / 100m
							: 1.0m;

						for (var date = r.StartDate; date <= r.EndDate; date = date.AddDays(1))
						{
							var att = await _context.Attendances
								.FirstOrDefaultAsync(a => a.UserId == r.UserId && a.WorkDate == date);

							if (att == null)
							{
								att = new Attendance
								{
									UserId = r.UserId,
									WorkDate = date,
									CheckInTime = null,
									CheckOutTime = null,
									IsLate = false,
									TotalHours = 0,
									SalaryMultiplier = leaveMultiplier,
									StandardWorkHours = 8,
									ActualWorkHours = 0,
									CreatedAt = DateTime.Now
								};
								_context.Attendances.Add(att);
							}
							else
							{
								att.SalaryMultiplier = leaveMultiplier;
								att.UpdatedAt = DateTime.Now;
							}
						}

						// GỬI THÔNG BÁO CHO USER
						// ✅ ĐÚNG
						await _notificationService.SendToUserAsync(
							r.UserId,
							"Nghỉ phép được duyệt",
							$"Yêu cầu nghỉ phép {r.TotalDays} ngày từ {r.StartDate:dd/MM/yyyy} đến {r.EndDate:dd/MM/yyyy} đã được duyệt",
							"success",
							"/Staff/AttendanceHistory"
						);
					}
					else if (model.Action == "Reject")
					{
						r.Status = "Rejected";
						r.ReviewedBy = adminId;
						r.ReviewedAt = DateTime.Now;
						r.ReviewNote = model.Note;

						var unpaidMultiplierConfig = await _context.SystemSettings
							.FirstOrDefaultAsync(c => c.SettingKey == "LEAVE_UNPAID_MULTIPLIER" && c.IsActive == true);

						var unpaidMultiplier = unpaidMultiplierConfig != null
							? decimal.Parse(unpaidMultiplierConfig.SettingValue) / 100m
							: 0m;

						for (var date = r.StartDate; date <= r.EndDate; date = date.AddDays(1))
						{
							var att = await _context.Attendances
								.FirstOrDefaultAsync(a => a.UserId == r.UserId && a.WorkDate == date);

							if (att != null)
							{
								att.SalaryMultiplier = unpaidMultiplier;
								att.UpdatedAt = DateTime.Now;
							}
						}

						// GỬI THÔNG BÁO CHO USER
						// ✅ ĐÚNG
						await _notificationService.SendToUserAsync(
							r.UserId,
							"Nghỉ phép bị từ chối",
							$"Yêu cầu nghỉ phép bị từ chối. Lý do: {model.Note}",
							"error",
							"/Staff/MyRequests"
						);
					}

					r.UpdatedAt = DateTime.Now;
					await _context.SaveChangesAsync();

					await _auditHelper.LogAsync(
						adminId,
						"REVIEW",
						"LeaveRequest",
						r.LeaveRequestId,
						old,
						new { r.Status, r.ReviewedBy, r.ReviewedAt, r.ReviewNote },
						$"Admin {(model.Action == "Approve" ? "DUYỆT" : "TỪ CHỐI")} leave request #{r.LeaveRequestId}"
					);

					return Json(new
					{
						success = true,
						message = model.Action == "Approve"
							? $"Đã duyệt nghỉ phép {r.TotalDays} ngày"
							: "Đã từ chối yêu cầu nghỉ phép"
					});
				}

				if (model.RequestType == "Late")
				{
					var r = await _context.LateRequests
						.Include(x => x.User)
						.FirstOrDefaultAsync(x => x.LateRequestId == model.RequestId);

					if (r == null) return Json(new { success = false, message = "Không tìm thấy" });

					var old = new { r.Status, r.ReviewedBy, r.ReviewedAt, r.ReviewNote };

					if (model.Action == "Approve")
					{
						r.Status = "Approved";
						r.ReviewedBy = adminId;
						r.ReviewedAt = DateTime.Now;
						r.ReviewNote = model.Note;

						var att = await _context.Attendances
							.FirstOrDefaultAsync(a => a.UserId == r.UserId && a.WorkDate == r.RequestDate);

						if (att != null)
						{
							att.HasLateRequest = true;
							att.LateRequestId = r.LateRequestId;

							var lateApprovedDeductionConfig = await _context.SystemSettings
								.FirstOrDefaultAsync(c => c.SettingKey.Contains("LATE_APPROVED") && c.IsActive == true);

							var approvedDeductionPercent = lateApprovedDeductionConfig != null
								? decimal.Parse(lateApprovedDeductionConfig.SettingValue) / 100m
								: 0m;

							if (approvedDeductionPercent == 0)
							{
								att.DeductionHours = 0;
								att.DeductionAmount = 0;
							}
							else
							{
								att.DeductionHours = att.DeductionHours * approvedDeductionPercent;
								att.DeductionAmount = att.DeductionAmount * approvedDeductionPercent;
							}

							att.UpdatedAt = DateTime.Now;
						}

						// GỬI THÔNG BÁO CHO USER
						// ✅ ĐÚNG
						await _notificationService.SendToUserAsync(
							r.UserId,
							"Đi trễ được duyệt",
							$"Yêu cầu đi trễ ngày {r.RequestDate:dd/MM/yyyy} đã được phê duyệt",
							"success",
							"/Staff/AttendanceHistory"
						);
					}
					else if (model.Action == "Reject")
					{
						r.Status = "Rejected";
						r.ReviewedBy = adminId;
						r.ReviewedAt = DateTime.Now;
						r.ReviewNote = model.Note;

						var att = await _context.Attendances
							.FirstOrDefaultAsync(a => a.UserId == r.UserId && a.WorkDate == r.RequestDate);

						if (att != null)
						{
							att.HasLateRequest = true;
							att.LateRequestId = r.LateRequestId;
							att.UpdatedAt = DateTime.Now;
						}

						// GỬI THÔNG BÁO CHO USER
						// ✅ ĐÚNG
						await _notificationService.SendToUserAsync(
							r.UserId,
							"Đi trễ bị từ chối",
							$"Yêu cầu đi trễ bị từ chối. Lý do: {model.Note}",
							"error",
							"/Staff/MyRequests"
						);
					}

					r.UpdatedAt = DateTime.Now;
					await _context.SaveChangesAsync();

					await _auditHelper.LogAsync(
						adminId,
						"REVIEW",
						"LateRequest",
						r.LateRequestId,
						old,
						new { r.Status, r.ReviewedBy, r.ReviewedAt, r.ReviewNote },
						$"Admin {(model.Action == "Approve" ? "DUYỆT" : "TỪ CHỐI")} late request #{r.LateRequestId}"
					);

					return Json(new
					{
						success = true,
						message = model.Action == "Approve"
							? "Đã duyệt yêu cầu đi trễ"
							: "Đã từ chối yêu cầu đi trễ"
					});
				}

				return Json(new { success = false, message = "Loại request không hợp lệ" });
			}
			catch (Exception ex)
			{
				await _auditHelper.LogFailedAttemptAsync(adminId, "REVIEW", "Request",
					$"Exception: {ex.Message}", new { Error = ex.ToString(), model });
				return Json(new { success = false, message = $"Có lỗi: {ex.Message}" });
			}
		}

		[HttpGet]
		public async Task<IActionResult> GetMyNotifications(int skip = 0, int take = 20)
		{
			var userId = HttpContext.Session.GetInt32("UserId");
			if (!userId.HasValue)
				return Json(new { success = false, message = "Not logged in" });

			var notifications = await _notificationService.GetUserNotificationsAsync(userId.Value, skip, take);
			var unreadCount = await _notificationService.GetUnreadCountAsync(userId.Value);

			return Json(new
			{
				success = true,
				notifications = notifications.Select(n => new {
					id = n.UserNotificationId,
					title = n.Notification.Title,
					message = n.Notification.Message,
					type = n.Notification.Type,
					link = n.Notification.Link,
					time = n.Notification.CreatedAt,
					read = n.IsRead
				}),
				unreadCount
			});
		}
		[HttpGet]
		public async Task<IActionResult> TestNotification(int userId)
		{
			await _notificationService.SendToUserAsync(
				userId,
				"🔔 Test Notification",
				"Đây là thông báo test từ hệ thống",
				"info"
			);

			return Json(new { success = true, message = "Sent!" });
		}
		// ============================================
		// THÊM VÀO AdminController.cs
		// ============================================

		/// <summary>
		/// Tự động từ chối các đề xuất quá 3 ngày chưa được duyệt
		/// </summary>
		[HttpPost]
		public async Task<IActionResult> AutoRejectExpiredRequests()
		{
			if (!IsAdmin())
				return Json(new { success = false, message = "Không có quyền thực hiện!" });

			const int MAX_PENDING_DAYS = 3;
			var cutoffDate = DateTime.Now.AddDays(-MAX_PENDING_DAYS);
			int totalRejected = 0;

			try
			{
				var adminId = HttpContext.Session.GetInt32("UserId");

				// Helper: safe parse config percent -> decimal (0..1)
				async Task<decimal> GetPercentSettingAsync(string key, decimal defaultPercent)
				{
					var cfg = await _context.SystemSettings
						.FirstOrDefaultAsync(c => c.SettingKey == key && c.IsActive == true);
					if (cfg == null) return defaultPercent;
					if (decimal.TryParse(cfg.SettingValue, out var v))
						return v / 100m;
					return defaultPercent;
				}

				// ---------------------------
				// OVERTIME: auto-reject
				// ---------------------------
				var expiredOvertime = await _context.OvertimeRequests
					.Where(r => r.Status == "Pending" &&
								r.CreatedAt.HasValue &&
								r.CreatedAt.Value <= cutoffDate)
					.ToListAsync();

				foreach (var r in expiredOvertime)
				{
					var oldStatus = r.Status;
					r.Status = "Rejected";
					r.ReviewedBy = adminId;
					r.ReviewedAt = DateTime.Now;
					r.ReviewNote = $"Tự động từ chối do quá {MAX_PENDING_DAYS} ngày không được xử lý";
					r.UpdatedAt = DateTime.Now;

					var att = await _context.Attendances
						.FirstOrDefaultAsync(a => a.UserId == r.UserId && a.WorkDate == r.WorkDate);

					if (att != null)
					{
						att.IsOvertimeApproved = false;
						att.ApprovedOvertimeHours = 0;
						att.HasOvertimeRequest = true;
						att.OvertimeRequestId = r.OvertimeRequestId;
						att.UpdatedAt = DateTime.Now;
					}

					await _auditHelper.LogDetailedAsync(
						adminId,
						"AUTO_REJECT",
						"OvertimeRequest",
						r.OvertimeRequestId,
						new { Status = oldStatus },
						new { Status = "Rejected" },
						$"Tự động từ chối overtime request #{r.OvertimeRequestId} - Quá {MAX_PENDING_DAYS} ngày",
						new Dictionary<string, object>
						{
					{ "UserId", r.UserId },
					{ "WorkDate", r.WorkDate.ToString("dd/MM/yyyy") },
					{ "OvertimeHours", r.OvertimeHours },
					{ "DaysExpired", (DateTime.Now - r.CreatedAt.Value).Days }
						}
					);

					totalRejected++;
				}

				// ---------------------------
				// LEAVE: auto-reject and apply unpaid multiplier
				// ---------------------------
				var expiredLeaves = await _context.LeaveRequests
					.Where(r => r.Status == "Pending" &&
								r.CreatedAt.HasValue &&
								r.CreatedAt.Value <= cutoffDate)
					.ToListAsync();

				var unpaidMultiplier = await GetPercentSettingAsync("LEAVE_UNPAID_MULTIPLIER", 0m);

				foreach (var r in expiredLeaves)
				{
					var oldStatus = r.Status;
					r.Status = "Rejected";
					r.ReviewedBy = adminId;
					r.ReviewedAt = DateTime.Now;
					r.ReviewNote = $"Tự động từ chối do quá {MAX_PENDING_DAYS} ngày không được xử lý";
					r.UpdatedAt = DateTime.Now;

					for (var date = r.StartDate; date <= r.EndDate; date = date.AddDays(1))
					{
						var att = await _context.Attendances
							.FirstOrDefaultAsync(a => a.UserId == r.UserId && a.WorkDate == date);

						if (att != null)
						{
							att.SalaryMultiplier = unpaidMultiplier;
							att.UpdatedAt = DateTime.Now;
						}
					}

					await _auditHelper.LogDetailedAsync(
						adminId,
						"AUTO_REJECT",
						"LeaveRequest",
						r.LeaveRequestId,
						new { Status = oldStatus },
						new { Status = "Rejected" },
						$"Tự động từ chối leave request #{r.LeaveRequestId} - Quá {MAX_PENDING_DAYS} ngày",
						new Dictionary<string, object>
						{
					{ "UserId", r.UserId },
					{ "LeaveType", r.LeaveType ?? "N/A" },
					{ "TotalDays", r.TotalDays },
					{ "DaysExpired", (DateTime.Now - r.CreatedAt.Value).Days }
						}
					);

					totalRejected++;
				}

				// ---------------------------
				// LATE: auto-reject and mark attendance
				// ---------------------------
				var expiredLate = await _context.LateRequests
					.Where(r => r.Status == "Pending" &&
								r.CreatedAt.HasValue &&
								r.CreatedAt.Value <= cutoffDate)
					.ToListAsync();

				foreach (var r in expiredLate)
				{
					var oldStatus = r.Status;
					r.Status = "Rejected";
					r.ReviewedBy = adminId;
					r.ReviewedAt = DateTime.Now;
					r.ReviewNote = $"Tự động từ chối do quá {MAX_PENDING_DAYS} ngày không được xử lý";
					r.UpdatedAt = DateTime.Now;

					var att = await _context.Attendances
						.FirstOrDefaultAsync(a => a.UserId == r.UserId && a.WorkDate == r.RequestDate);

					if (att != null)
					{
						att.HasLateRequest = true;
						att.LateRequestId = r.LateRequestId;
						att.UpdatedAt = DateTime.Now;
					}

					await _auditHelper.LogDetailedAsync(
						adminId,
						"AUTO_REJECT",
						"LateRequest",
						r.LateRequestId,
						new { Status = oldStatus },
						new { Status = "Rejected" },
						$"Tự động từ chối late request #{r.LateRequestId} - Quá {MAX_PENDING_DAYS} ngày",
						new Dictionary<string, object>
						{
					{ "UserId", r.UserId },
					{ "RequestDate", r.RequestDate.ToString("dd/MM/yyyy") },
					{ "ExpectedArrivalTime", r.ExpectedArrivalTime.ToString("HH:mm") },
					{ "DaysExpired", (DateTime.Now - r.CreatedAt.Value).Days }
						}
					);

					totalRejected++;
				}

				await _context.SaveChangesAsync();

				return Json(new
				{
					success = true,
					message = $"Đã tự động từ chối {totalRejected} đề xuất quá hạn",
					totalRejected
				});
			}
			catch (Exception ex)
			{
				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"AUTO_REJECT",
					"Request",
					$"Exception: {ex.Message}",
					new { Error = ex.ToString() }
				);

				return Json(new { success = false, message = $"Có lỗi xảy ra: {ex.Message}" });
			}
		}
		[HttpPost]
		public async Task<IActionResult> MarkNotificationAsRead([FromBody] int userNotificationId)
		{
			var userId = HttpContext.Session.GetInt32("UserId");
			if (!userId.HasValue)
				return Json(new { success = false, message = "Not logged in" });

			try
			{
				var userNotif = await _context.UserNotifications
					.FirstOrDefaultAsync(un => un.UserNotificationId == userNotificationId && un.UserId == userId);

				if (userNotif != null && !userNotif.IsRead)
				{
					userNotif.IsRead = true;
					userNotif.ReadAt = DateTime.UtcNow;
					await _context.SaveChangesAsync();
				}

				return Json(new { success = true });
			}
			catch (Exception ex)
			{
				return Json(new { success = false, message = ex.Message });
			}
		}

		[HttpPost]
		public async Task<IActionResult> MarkAllNotificationsAsRead()
		{
			var userId = HttpContext.Session.GetInt32("UserId");
			if (!userId.HasValue)
				return Json(new { success = false, message = "Not logged in" });

			try
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
				return Json(new { success = true });
			}
			catch (Exception ex)
			{
				return Json(new { success = false, message = ex.Message });
			}
		}

		[HttpGet]
		public IActionResult KPIDashboard()
		{
			// Kiểm tra quyền admin
			if (!IsAdminOrManager())
				return RedirectToAction("Login", "Account");

			return View();
		}

		[HttpGet]
		public async Task<IActionResult> GetDepartmentComparison(DateTime startDate, DateTime endDate)
		{
			try
			{
				// Gọi stored procedure sp_CompareDepartmentKPI
				var result = await _context.Database
					.SqlQueryRaw<DepartmentKPIDto>(
						"EXEC sp_CompareDepartmentKPI @StartDate, @EndDate",
						new Microsoft.Data.SqlClient.SqlParameter("@StartDate", startDate),
						new Microsoft.Data.SqlClient.SqlParameter("@EndDate", endDate)
					)
					.ToListAsync();

				return Json(result);
			}
			catch (Exception ex)
			{
				return Json(new { error = ex.Message });
			}
		}


		[HttpGet]
		public async Task<IActionResult> EditUser(int id)
		{
			if (!IsSuperAdmin())
				return RedirectToAction("Login", "Account");

			var user = await _context.Users
				.Include(u => u.Role)
				.Include(u => u.Department)
				.FirstOrDefaultAsync(u => u.UserId == id);

			if (user == null)
				return NotFound();

			ViewBag.Departments = await _context.Departments
				.Where(d => d.IsActive == true)
				.OrderBy(d => d.DepartmentName)
				.ToListAsync();

			ViewBag.Roles = await _context.Roles
				.OrderBy(r => r.RoleName)
				.ToListAsync();

			return View(user);
		}

		[HttpPost]
		public async Task<IActionResult> UpdateUserJson([FromBody] UpdateUserRequest request)
		{
			if (!IsSuperAdmin())
			{
				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"UPDATE",
					"User",
					"Không có quyền cập nhật",
					new { UserId = request.UserId }
				);

				return Json(new { success = false, message = "Chỉ Admin mới có quyền cập nhật tài khoản!" });
			}

			var adminId = HttpContext.Session.GetInt32("UserId");

			if (string.IsNullOrWhiteSpace(request.FullName))
			{
				return Json(new { success = false, message = "Họ tên không được để trống!" });
			}

			var user = await _context.Users
				.Include(u => u.Role)
				.FirstOrDefaultAsync(u => u.UserId == request.UserId);

			if (user == null)
			{
				await _auditHelper.LogFailedAttemptAsync(
					adminId,
					"UPDATE",
					"User",
					"User không tồn tại",
					new { UserId = request.UserId }
				);

				return Json(new { success = false, message = "Không tìm thấy người dùng" });
			}

			// Check email exists (excluding current user)
			if (!string.IsNullOrEmpty(request.Email))
			{
				var emailExists = await _context.Users
					.AnyAsync(u => u.Email == request.Email && u.UserId != request.UserId);

				if (emailExists)
				{
					await _auditHelper.LogFailedAttemptAsync(
						adminId,
						"UPDATE",
						"User",
						"Email đã được sử dụng",
						new { Email = request.Email }
					);

					return Json(new { success = false, message = "Email đã được sử dụng bởi người dùng khác" });
				}
			}

			try
			{
				var oldData = new
				{
					user.FullName,
					user.Email,
					user.PhoneNumber,
					user.DepartmentId,
					user.RoleId,
					RoleName = user.Role?.RoleName,
					user.IsTester,
					user.IsActive
				};

				// ✅ CẬP NHẬT THÔNG TIN USER
				user.FullName = request.FullName.Trim();
				user.Email = request.Email?.Trim();
				user.PhoneNumber = request.PhoneNumber?.Trim();
				user.DepartmentId = request.DepartmentId;
				user.RoleId = request.RoleId;

				// ✅ CẬP NHẬT IsTester - LOGIC MỚI
				// Admin: KHÔNG BAO GIỜ là Tester
				// Tester role: TỰ ĐỘNG là Tester
				// Các role khác (Staff, Leader, Manager, v.v.): LẤY TỪ CHECKBOX
				var newRole = await _context.Roles.FindAsync(request.RoleId);
				if (newRole != null)
				{
					if (newRole.RoleName == "Admin")
					{
						// Admin không bao giờ là Tester
						user.IsTester = false;
					}
					else if (newRole.RoleName == "Tester")
					{
						// Role Tester tự động có quyền Tester
						user.IsTester = true;
					}
					else
					{
						// TẤT CẢ ROLE KHÁC: lấy từ checkbox
						user.IsTester = request.IsTester ?? false;
					}
				}

				user.IsActive = request.IsActive;
				user.UpdatedAt = DateTime.Now;

				await _context.SaveChangesAsync();

				var newData = new
				{
					user.FullName,
					user.Email,
					user.PhoneNumber,
					user.DepartmentId,
					user.RoleId,
					RoleName = newRole?.RoleName,
					user.IsTester,
					user.IsActive
				};

				await _auditHelper.LogDetailedAsync(
					adminId,
					"UPDATE",
					"User",
					user.UserId,
					oldData,
					newData,
					$"Cập nhật thông tin user: {user.Username} ({user.FullName})",
					new Dictionary<string, object>
					{
				{ "ChangedFields", GetChangedFields(oldData, newData) },
				{ "UpdatedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") },
				{ "IsTester", user.IsTester }
					}
				);

				return Json(new
				{
					success = true,
					message = $"Cập nhật thông tin {user.FullName} thành công!" +
							  (user.IsTester ? " (Quyền Tester đã được cấp)" : "")
				});
			}
			catch (Exception ex)
			{
				await _auditHelper.LogFailedAttemptAsync(
					adminId,
					"UPDATE",
					"User",
					$"Exception: {ex.Message}",
					new { UserId = request.UserId, Error = ex.ToString() }
				);

				return Json(new
				{
					success = false,
					message = $"Có lỗi xảy ra: {ex.Message}"
				});
			}
		}

		// ============================================
		// 📋 HELPER METHOD
		// ============================================
		private string GetChangedFields(object oldData, object newData)
		{
			var changes = new List<string>();
			var oldProps = oldData.GetType().GetProperties();
			var newProps = newData.GetType().GetProperties();

			foreach (var oldProp in oldProps)
			{
				var newProp = newProps.FirstOrDefault(p => p.Name == oldProp.Name);
				if (newProp != null)
				{
					var oldVal = oldProp.GetValue(oldData)?.ToString() ?? "";
					var newVal = newProp.GetValue(newData)?.ToString() ?? "";

					if (oldVal != newVal)
					{
						changes.Add($"{oldProp.Name}: '{oldVal}' → '{newVal}'");
					}
				}
			}

			return changes.Count > 0 ? string.Join(", ", changes) : "No changes";
		}

		/// <summary>
		/// Hiển thị danh sách tất cả các Role
		/// </summary>
		[HttpGet]
		public async Task<IActionResult> RoleList()
		{
			if (!IsSuperAdmin())
				return RedirectToAction("Login", "Account");

			await _auditHelper.LogViewAsync(
				HttpContext.Session.GetInt32("UserId").Value,
				"Role",
				0,
				"Xem danh sách Role"
			);

			var roles = await _context.Roles
				.Include(r => r.Users)
				.OrderBy(r => r.RoleName)
				.ToListAsync();

			// Thống kê
			ViewBag.TotalRoles = roles.Count;
			ViewBag.TotalUsers = roles.Sum(r => r.Users.Count);
			ViewBag.ActiveUsers = roles.Sum(r => r.Users.Count(u => u.IsActive == true));

			return View(roles);
		}

		/// <summary>
		/// Lấy thông tin chi tiết Role (AJAX)
		/// </summary>
		[HttpGet]
		public async Task<IActionResult> GetRoleDetail(int roleId)
		{
			if (!IsSuperAdmin())
				return Json(new { success = false, message = "Không có quyền truy cập" });

			try
			{
				var role = await _context.Roles
					.Include(r => r.Users)
						.ThenInclude(u => u.Department)
					.FirstOrDefaultAsync(r => r.RoleId == roleId);

				if (role == null)
					return Json(new { success = false, message = "Không tìm thấy Role" });

				await _auditHelper.LogViewAsync(
					HttpContext.Session.GetInt32("UserId").Value,
					"Role",
					roleId,
					$"Xem chi tiết Role: {role.RoleName}"
				);

				var result = new
				{
					success = true,
					role = new
					{
						roleId = role.RoleId,
						roleName = role.RoleName,
						createdAt = role.CreatedAt?.ToString("dd/MM/yyyy HH:mm") ?? "N/A",
						totalUsers = role.Users.Count,
						activeUsers = role.Users.Count(u => u.IsActive == true),
						inactiveUsers = role.Users.Count(u => u.IsActive == false),
						users = role.Users
							.OrderBy(u => u.FullName)
							.Select(u => new
							{
								userId = u.UserId,
								username = u.Username,
								fullName = u.FullName,
								email = u.Email,
								departmentName = u.Department?.DepartmentName ?? "Chưa phân công",
								isActive = u.IsActive,
								avatar = u.Avatar,
								lastLoginAt = u.LastLoginAt?.ToString("dd/MM/yyyy HH:mm") ?? "Chưa đăng nhập"
							})
							.ToList()
					}
				};

				return Json(result);
			}
			catch (Exception ex)
			{
				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"VIEW",
					"Role",
					$"Exception: {ex.Message}",
					new { RoleId = roleId, Error = ex.ToString() }
				);

				return Json(new { success = false, message = $"Có lỗi xảy ra: {ex.Message}" });
			}
		}

		/// <summary>
		/// Tạo Role mới (AJAX)
		/// </summary>
		[HttpPost]
		public async Task<IActionResult> CreateRole([FromBody] CreateRoleRequest request)
		{
			if (!IsSuperAdmin())
			{
				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"CREATE",
					"Role",
					"Không có quyền tạo Role",
					new { RoleName = request.RoleName }
				);

				return Json(new { success = false, message = "Chỉ Admin mới có quyền tạo Role!" });
			}

			if (string.IsNullOrWhiteSpace(request.RoleName))
			{
				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"CREATE",
					"Role",
					"Tên Role rỗng",
					null
				);

				return Json(new { success = false, message = "Tên Role không được để trống!" });
			}

			// Kiểm tra tên Role đã tồn tại chưa
			var existingRole = await _context.Roles
				.FirstOrDefaultAsync(r => r.RoleName.ToLower() == request.RoleName.Trim().ToLower());

			if (existingRole != null)
			{
				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"CREATE",
					"Role",
					"Tên Role đã tồn tại",
					new { RoleName = request.RoleName }
				);

				return Json(new { success = false, message = $"Role '{request.RoleName}' đã tồn tại!" });
			}

			try
			{
				var adminId = HttpContext.Session.GetInt32("UserId");

				var role = new Role
				{
					RoleName = request.RoleName.Trim(),
					CreatedAt = DateTime.Now
				};

				_context.Roles.Add(role);
				await _context.SaveChangesAsync();

				await _auditHelper.LogAsync(
					adminId,
					"CREATE",
					"Role",
					role.RoleId,
					null,
					new { role.RoleName, role.CreatedAt },
					$"Tạo Role mới: {role.RoleName}"
				);

				return Json(new
				{
					success = true,
					message = $"Tạo Role '{role.RoleName}' thành công!",
					roleId = role.RoleId
				});
			}
			catch (Exception ex)
			{
				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"CREATE",
					"Role",
					$"Exception: {ex.Message}",
					new { RoleName = request.RoleName, Error = ex.ToString() }
				);

				return Json(new { success = false, message = $"Có lỗi xảy ra: {ex.Message}" });
			}
		}

		/// <summary>
		/// Cập nhật Role (AJAX)
		/// </summary>
		[HttpPost]
		public async Task<IActionResult> UpdateRole([FromBody] UpdateRoleRequest request)
		{
			if (!IsSuperAdmin())
			{
				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"UPDATE",
					"Role",
					"Không có quyền cập nhật",
					new { RoleId = request.RoleId }
				);

				return Json(new { success = false, message = "Chỉ Admin mới có quyền cập nhật Role!" });
			}

			if (string.IsNullOrWhiteSpace(request.RoleName))
			{
				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"UPDATE",
					"Role",
					"Tên Role rỗng",
					new { RoleId = request.RoleId }
				);

				return Json(new { success = false, message = "Tên Role không được để trống!" });
			}

			var role = await _context.Roles
				.Include(r => r.Users)
				.FirstOrDefaultAsync(r => r.RoleId == request.RoleId);

			if (role == null)
			{
				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"UPDATE",
					"Role",
					"Role không tồn tại",
					new { RoleId = request.RoleId }
				);

				return Json(new { success = false, message = "Không tìm thấy Role!" });
			}

			// Kiểm tra tên mới có trùng với Role khác không
			var duplicateRole = await _context.Roles
				.FirstOrDefaultAsync(r => r.RoleId != request.RoleId &&
										  r.RoleName.ToLower() == request.RoleName.Trim().ToLower());

			if (duplicateRole != null)
			{
				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"UPDATE",
					"Role",
					"Tên Role đã tồn tại",
					new { RoleId = request.RoleId, RoleName = request.RoleName }
				);

				return Json(new { success = false, message = $"Role '{request.RoleName}' đã tồn tại!" });
			}

			try
			{
				var adminId = HttpContext.Session.GetInt32("UserId");

				var oldValues = new
				{
					RoleName = role.RoleName
				};

				role.RoleName = request.RoleName.Trim();

				await _context.SaveChangesAsync();

				var newValues = new
				{
					RoleName = role.RoleName
				};

				await _auditHelper.LogAsync(
					adminId,
					"UPDATE",
					"Role",
					role.RoleId,
					oldValues,
					newValues,
					$"Cập nhật Role: {oldValues.RoleName} → {role.RoleName}"
				);

				// Gửi thông báo cho tất cả user có role này
				var affectedUserIds = role.Users.Select(u => u.UserId).ToList();
				foreach (var userId in affectedUserIds)
				{
					await _notificationService.SendToUserAsync(
						userId,
						"Vai trò được cập nhật",
						$"Vai trò của bạn đã được đổi tên thành: {role.RoleName}",
						"info",
						"/Staff/Profile"
					);
				}

				return Json(new
				{
					success = true,
					message = $"Cập nhật Role thành công! ({role.Users.Count} người dùng bị ảnh hưởng)"
				});
			}
			catch (Exception ex)
			{
				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"UPDATE",
					"Role",
					$"Exception: {ex.Message}",
					new { RoleId = request.RoleId, Error = ex.ToString() }
				);

				return Json(new { success = false, message = $"Có lỗi xảy ra: {ex.Message}" });
			}
		}

		/// <summary>
		/// Xóa Role (AJAX) - Chỉ xóa được khi không có user nào sử dụng
		/// </summary>
		[HttpPost]
		public async Task<IActionResult> DeleteRole([FromBody] DeleteRoleRequest request)
		{
			if (!IsSuperAdmin())
			{
				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"DELETE",
					"Role",
					"Không có quyền xóa",
					new { RoleId = request.RoleId }
				);

				return Json(new { success = false, message = "Chỉ Admin mới có quyền xóa Role!" });
			}

			var role = await _context.Roles
				.Include(r => r.Users)
				.FirstOrDefaultAsync(r => r.RoleId == request.RoleId);

			if (role == null)
			{
				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"DELETE",
					"Role",
					"Role không tồn tại",
					new { RoleId = request.RoleId }
				);

				return Json(new { success = false, message = "Không tìm thấy Role!" });
			}

			// Không cho xóa role Admin, Staff, Tester (hệ thống mặc định)
			var protectedRoles = new[] { "Admin", "Staff", "Tester" };
			if (protectedRoles.Contains(role.RoleName))
			{
				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"DELETE",
					"Role",
					"Không thể xóa Role hệ thống",
					new { RoleId = request.RoleId, RoleName = role.RoleName }
				);

				return Json(new
				{
					success = false,
					message = $"Không thể xóa Role '{role.RoleName}' - Đây là role hệ thống!"
				});
			}

			// Kiểm tra có user nào đang sử dụng role này không
			if (role.Users != null && role.Users.Any())
			{
				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"DELETE",
					"Role",
					"Role đang được sử dụng",
					new { RoleId = request.RoleId, UserCount = role.Users.Count }
				);

				return Json(new
				{
					success = false,
					message = $"Không thể xóa Role '{role.RoleName}' vì có {role.Users.Count} người dùng đang sử dụng!"
				});
			}

			try
			{
				var adminId = HttpContext.Session.GetInt32("UserId");
				var roleName = role.RoleName;

				_context.Roles.Remove(role);
				await _context.SaveChangesAsync();

				await _auditHelper.LogAsync(
					adminId,
					"DELETE",
					"Role",
					request.RoleId,
					new { RoleName = roleName },
					null,
					$"Xóa Role: {roleName}"
				);

				return Json(new
				{
					success = true,
					message = $"Đã xóa Role '{roleName}' thành công!"
				});
			}
			catch (Exception ex)
			{
				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"DELETE",
					"Role",
					$"Exception: {ex.Message}",
					new { RoleId = request.RoleId, Error = ex.ToString() }
				);

				return Json(new { success = false, message = $"Có lỗi xảy ra: {ex.Message}" });
			}
		}




		[HttpGet]
		public async Task<IActionResult> ProjectList()
		{
			if (!IsAdminOrManager())
				return RedirectToAction("Login", "Account");

			var projects = await _context.Projects
				.Include(p => p.Leader)
				.Include(p => p.Department)
				.Include(p => p.ProjectMembers)
				.Include(p => p.Tasks)
				.OrderByDescending(p => p.CreatedAt)
				.ToListAsync();

			// Statistics
			ViewBag.TotalProjects = projects.Count;
			ViewBag.ActiveProjects = projects.Count(p => p.Status == "InProgress");
			ViewBag.CompletedProjects = projects.Count(p => p.Status == "Completed");
			ViewBag.PlanningProjects = projects.Count(p => p.Status == "Planning");

			return View(projects);
		}

		// ============================================
		// CREATE PROJECT (GET)
		// ============================================
		[HttpGet]
		public async Task<IActionResult> CreateProject()
		{
			if (!IsAdminOrManager())
				return RedirectToAction("Login", "Account");

			ViewBag.Departments = await _context.Departments
				.Where(d => d.IsActive == true)
				.OrderBy(d => d.DepartmentName)
				.ToListAsync();

			ViewBag.Users = await _context.Users
				.Include(u => u.Department)
				.Where(u => u.IsActive == true)
				.OrderBy(u => u.FullName)
				.ToListAsync();

			return View();
		}

		// ============================================
		// CREATE PROJECT (POST)
		// ============================================
		[HttpPost]
		public async Task<IActionResult> CreateProjectPost([FromBody] CreateProjectRequest request)
		{
			if (!IsAdmin())
				return Json(new { success = false, message = "Không có quyền tạo dự án" });

			if (string.IsNullOrWhiteSpace(request.ProjectName))
				return Json(new { success = false, message = "Tên dự án không được để trống" });

			try
			{
				var adminId = HttpContext.Session.GetInt32("UserId").Value;

				// Generate unique ProjectCode
				var year = DateTime.Now.Year;
				var lastProject = await _context.Projects
					.Where(p => p.ProjectCode.StartsWith($"PRJ-{year}-"))
					.OrderByDescending(p => p.ProjectCode)
					.FirstOrDefaultAsync();

				int nextNumber = 1;
				if (lastProject != null)
				{
					var lastNumber = lastProject.ProjectCode.Split('-').Last();
					if (int.TryParse(lastNumber, out int num))
						nextNumber = num + 1;
				}

				var projectCode = $"PRJ-{year}-{nextNumber:D3}";

				var project = new Project
				{
					ProjectCode = projectCode,
					ProjectName = request.ProjectName.Trim(),
					Description = request.Description?.Trim(),
					StartDate = request.StartDate.HasValue ? DateOnly.FromDateTime(request.StartDate.Value) : null,
					EndDate = request.EndDate.HasValue ? DateOnly.FromDateTime(request.EndDate.Value) : null,
					Status = "Planning",
					Priority = request.Priority ?? "Medium",
					Budget = request.Budget,
					LeaderId = request.LeaderId,
					DepartmentId = request.DepartmentId,
					Progress = 0,
					IsActive = true,
					CreatedBy = adminId,
					CreatedAt = DateTime.Now
				};

				_context.Projects.Add(project);
				await _context.SaveChangesAsync();

				// Add Leader as first member
				if (request.LeaderId.HasValue)
				{
					var leaderMember = new ProjectMember
					{
						ProjectId = project.ProjectId,
						UserId = request.LeaderId.Value,
						Role = "Leader",
						JoinedAt = DateTime.Now,
						IsActive = true
					};
					_context.ProjectMembers.Add(leaderMember);
				}

				// Add team members
				if (request.MemberIds != null && request.MemberIds.Count > 0)
				{
					foreach (var memberId in request.MemberIds)
					{
						if (memberId == request.LeaderId) continue; // Skip leader (already added)

						var member = new ProjectMember
						{
							ProjectId = project.ProjectId,
							UserId = memberId,
							Role = "Member",
							JoinedAt = DateTime.Now,
							IsActive = true
						};
						_context.ProjectMembers.Add(member);
					}
				}

				await _context.SaveChangesAsync();

				await _auditHelper.LogAsync(
					adminId,
					"CREATE",
					"Project",
					project.ProjectId,
					null,
					new { project.ProjectCode, project.ProjectName },
					$"Tạo dự án mới: {project.ProjectName}"
				);

				// Send notification to Leader
				if (request.LeaderId.HasValue)
				{
					await _notificationService.SendToUserAsync(
						request.LeaderId.Value,
						"🎯 Dự án mới",
						$"Bạn được chỉ định làm Leader cho dự án '{project.ProjectName}'",
						"info",
						$"/Admin/ProjectDetail/{project.ProjectId}"
					);
				}

				return Json(new
				{
					success = true,
					message = "Tạo dự án thành công!",
					projectId = project.ProjectId,
					projectCode = project.ProjectCode
				});
			}
			catch (Exception ex)
			{
				return Json(new { success = false, message = $"Có lỗi xảy ra: {ex.Message}" });
			}
		}

		// ============================================
		// PROJECT DETAIL
		// ============================================
		[HttpGet]
		public async Task<IActionResult> ProjectDetail(int id)
		{
			if (!IsAdmin())
				return RedirectToAction("Login", "Account");

			var project = await _context.Projects
				.Include(p => p.Leader)
				.Include(p => p.Department)
				.Include(p => p.ProjectMembers)
					.ThenInclude(pm => pm.User)
						.ThenInclude(u => u.Department)
				.Include(p => p.Tasks)
					.ThenInclude(t => t.UserTasks)
						.ThenInclude(ut => ut.User)
				.FirstOrDefaultAsync(p => p.ProjectId == id);

			if (project == null)
				return NotFound();

			// Calculate progress
			var totalTasks = project.Tasks.Count;
			var completedTasks = project.Tasks.Count(t =>
				t.UserTasks.Any(ut => ut.Status == "Done"));

			project.Progress = totalTasks > 0
				? Math.Round((decimal)completedTasks / totalTasks * 100, 2)
				: 0;

			await _context.SaveChangesAsync();

			return View(project);
		}

		// ============================================
		// CREATE TASK IN PROJECT
		// ============================================
		[HttpPost]
		public async Task<IActionResult> CreateProjectTask([FromBody] CreateProjectTaskRequest request)
		{
			if (!IsAdmin()) return Json(new { success = false, message = "Không có quyền thực hiện" });
			if (string.IsNullOrWhiteSpace(request.TaskName)) return Json(new { success = false, message = "Tên task không được để trống" });

			try
			{
				var project = await _context.Projects
					.Include(p => p.ProjectMembers)
					.FirstOrDefaultAsync(p => p.ProjectId == request.ProjectId);

				if (project == null) return Json(new { success = false, message = "Không tìm thấy dự án" });

				var adminId = HttpContext.Session.GetInt32("UserId").Value;

				var task = new TMD.Models.Task
				{
					TaskName = request.TaskName.Trim(),
					Description = request.Description?.Trim(),
					Platform = request.Platform?.Trim(),
					Deadline = request.Deadline,
					Priority = request.Priority ?? "Medium",
					ProjectId = request.ProjectId,
					TaskType = "Project",
					OrderIndex = request.OrderIndex,
					IsActive = true,
					CreatedAt = DateTime.Now
				};

				_context.Tasks.Add(task);
				await _context.SaveChangesAsync();

				int assignedCount = 0;
				if (request.AssignedUserIds != null && request.AssignedUserIds.Count > 0)
				{
					foreach (var userId in request.AssignedUserIds)
					{
						if (!project.ProjectMembers.Any(pm => pm.UserId == userId && pm.IsActive)) continue;

						var userTask = new UserTask
						{
							UserId = userId,
							TaskId = task.TaskId,
							Status = "TODO",
							CreatedAt = DateTime.Now
						};
						_context.UserTasks.Add(userTask);
						assignedCount++;

						await _notificationService.SendToUserAsync(userId, "📋 Task mới", $"Bạn được giao task '{request.TaskName}'", "info", "/Staff/MyTasks");
					}
					await _context.SaveChangesAsync();
				}

				await _auditHelper.LogAsync(adminId, "CREATE", "Task", task.TaskId, null, new { task.TaskName }, $"Tạo task: {task.TaskName}");

				// TRẢ VỀ JSON ĐẦY ĐỦ ĐỂ VẼ GIAO DIỆN NGAY LẬP TỨC
				return Json(new
				{
					success = true,
					message = "Tạo task thành công!",
					task = new
					{
						taskId = task.TaskId,
						taskName = task.TaskName,
						description = task.Description,
						platform = task.Platform,
						deadline = task.Deadline.HasValue ? task.Deadline.Value.ToString("dd/MM/yyyy") : null,
						priority = task.Priority,
						assignedCount = assignedCount,
						status = "TODO" // Mặc định là TODO
					}
				});
			}
			catch (Exception ex)
			{
				return Json(new { success = false, message = $"Có lỗi: {ex.Message}" });
			}
		}

		// ============================================
		// UPDATE PROJECT
		// ============================================
		[HttpPost]
		public async Task<IActionResult> UpdateProject([FromBody] UpdateProjectRequest request)
		{
			if (!IsAdminOrManager())
				return Json(new { success = false, message = "Không có quyền" });

			try
			{
				var project = await _context.Projects
					.Include(p => p.ProjectMembers)
					.FirstOrDefaultAsync(p => p.ProjectId == request.ProjectId);

				if (project == null)
					return Json(new { success = false, message = "Không tìm thấy dự án" });

				var adminId = HttpContext.Session.GetInt32("UserId").Value;

				var oldValues = new
				{
					project.ProjectName,
					project.Status,
					project.LeaderId
				};

				project.ProjectName = request.ProjectName.Trim();
				project.Description = request.Description?.Trim();
				project.StartDate = request.StartDate.HasValue ? DateOnly.FromDateTime(request.StartDate.Value) : null;
				project.EndDate = request.EndDate.HasValue ? DateOnly.FromDateTime(request.EndDate.Value) : null;
				project.Status = request.Status ?? project.Status;
				project.Priority = request.Priority ?? project.Priority;
				project.Budget = request.Budget;
				project.DepartmentId = request.DepartmentId;
				project.UpdatedAt = DateTime.Now;

				// Update leader
				if (request.LeaderId.HasValue && request.LeaderId != project.LeaderId)
				{
					project.LeaderId = request.LeaderId;

					// Update member role
					var leaderMember = project.ProjectMembers
						.FirstOrDefault(pm => pm.UserId == request.LeaderId.Value);

					if (leaderMember != null)
					{
						leaderMember.Role = "Leader";
					}
					else
					{
						_context.ProjectMembers.Add(new ProjectMember
						{
							ProjectId = project.ProjectId,
							UserId = request.LeaderId.Value,
							Role = "Leader",
							JoinedAt = DateTime.Now,
							IsActive = true
						});
					}
				}

				if (request.Status == "Completed" && !project.CompletedAt.HasValue)
				{
					project.CompletedAt = DateTime.Now;
				}

				await _context.SaveChangesAsync();

				await _auditHelper.LogAsync(
					adminId,
					"UPDATE",
					"Project",
					project.ProjectId,
					oldValues,
					new { project.ProjectName, project.Status, project.LeaderId },
					$"Cập nhật dự án: {project.ProjectName}"
				);

				return Json(new
				{
					success = true,
					message = "Cập nhật dự án thành công!"
				});
			}
			catch (Exception ex)
			{
				return Json(new { success = false, message = $"Có lỗi: {ex.Message}" });
			}
		}

		// ============================================
		// ADD/REMOVE PROJECT MEMBER
		// ============================================
		[HttpPost]
		public async Task<IActionResult> ManageProjectMember([FromBody] ManageProjectMemberRequest request)
		{
			if (!IsAdmin())
				return Json(new { success = false, message = "Không có quyền" });

			try
			{
				var project = await _context.Projects
					.Include(p => p.ProjectMembers)
					.FirstOrDefaultAsync(p => p.ProjectId == request.ProjectId);

				if (project == null)
					return Json(new { success = false, message = "Không tìm thấy dự án" });

				if (request.Action == "Add")
				{
					var existingMember = project.ProjectMembers
						.FirstOrDefault(pm => pm.UserId == request.UserId);

					if (existingMember != null)
						return Json(new { success = false, message = "Người dùng đã là thành viên dự án" });

					var member = new ProjectMember
					{
						ProjectId = request.ProjectId,
						UserId = request.UserId,
						Role = request.Role ?? "Member",
						JoinedAt = DateTime.Now,
						IsActive = true
					};

					_context.ProjectMembers.Add(member);
					await _context.SaveChangesAsync();

					await _notificationService.SendToUserAsync(
						request.UserId,
						"🎯 Thêm vào dự án",
						$"Bạn được thêm vào dự án '{project.ProjectName}'",
						"info",
						$"/Admin/ProjectDetail/{project.ProjectId}"
					);

					return Json(new { success = true, message = "Thêm thành viên thành công!" });
				}
				else if (request.Action == "Remove")
				{
					var member = project.ProjectMembers
						.FirstOrDefault(pm => pm.UserId == request.UserId);

					if (member == null)
						return Json(new { success = false, message = "Không tìm thấy thành viên" });

					member.IsActive = false;
					member.LeftAt = DateTime.Now;
					await _context.SaveChangesAsync();

					return Json(new { success = true, message = "Xóa thành viên thành công!" });
				}

				return Json(new { success = false, message = "Action không hợp lệ" });
			}
			catch (Exception ex)
			{
				return Json(new { success = false, message = $"Có lỗi: {ex.Message}" });
			}
		}


		// ============================================
		// THÊM VÀO AdminController.cs
		// ============================================

		/// <summary>
		/// Hiển thị trang Import Users
		/// </summary>
		[HttpGet]
		public async Task<IActionResult> ImportUsers()
		{
			if (!IsSuperAdmin())
				return RedirectToAction("Login", "Account");

			await _auditHelper.LogViewAsync(
				HttpContext.Session.GetInt32("UserId").Value,
				"ImportUsers",
				0,
				"Vào trang Import Users"
			);

			return View();
		}

		/// <summary>
		/// Import từng user một (được gọi từ JavaScript)
		/// </summary>
		[HttpPost]
		public async Task<IActionResult> ImportSingleUser([FromBody] ImportUserRequest request)
		{
			if (!IsAdmin())
				return Json(new { success = false, message = "Không có quyền" });

			var adminId = HttpContext.Session.GetInt32("UserId");

			try
			{
				// ✅ Validate required fields
				if (string.IsNullOrWhiteSpace(request.FullName))
					return Json(new { success = false, message = "Tên không được để trống" });

				if (string.IsNullOrWhiteSpace(request.Email))
					return Json(new { success = false, message = "Email không được để trống" });

				if (string.IsNullOrWhiteSpace(request.PhoneNumber))
					return Json(new { success = false, message = "Số điện thoại không được để trống" });

				// ✅ Validate email format
				if (!IsValidEmail(request.Email))
					return Json(new { success = false, message = "Email không hợp lệ" });

				// ✅ Check email already exists
				var emailExists = await _context.Users
					.AnyAsync(u => u.Email == request.Email.ToLower());

				if (emailExists)
					return Json(new { success = false, message = $"Email {request.Email} đã tồn tại trong hệ thống" });

				// ============================================
				// PHÒNG BAN: TỰ ĐỘNG TẠO NẾU CHƯA TỒN TẠI
				// ============================================
				int? departmentId = null;
				string departmentStatus = "Không có";
				bool departmentCreated = false;

				if (!string.IsNullOrWhiteSpace(request.Department))
				{
					var dept = await _context.Departments
						.FirstOrDefaultAsync(d =>
							d.DepartmentName.ToLower() == request.Department.ToLower());

					if (dept != null)
					{
						// Phòng ban đã tồn tại
						departmentId = dept.DepartmentId;
						departmentStatus = dept.DepartmentName;
					}
					else
					{
						// ✅ TỰ ĐỘNG TẠO PHÒNG BAN MỚI
						var newDept = new Department
						{
							DepartmentName = request.Department.Trim(),
							IsActive = true,
							CreatedAt = DateTime.Now,
							UpdatedAt = DateTime.Now
						};
						_context.Departments.Add(newDept);
						await _context.SaveChangesAsync();

						departmentId = newDept.DepartmentId;
						departmentStatus = newDept.DepartmentName;
						departmentCreated = true;

						// ✅ LOG TỰ ĐỘNG TẠO PHÒNG BAN
						await _auditHelper.LogAsync(
							adminId,
							"CREATE",
							"Department",
							newDept.DepartmentId,
							null,
							new { newDept.DepartmentName, newDept.IsActive },
							$"Tự động tạo phòng ban '{newDept.DepartmentName}' khi import user"
						);
					}
				}

				// ============================================
				// VAI TRÒ: TỰ ĐỘNG TẠO NẾU CHƯA TỒN TẠI
				// ============================================
				var roleName = string.IsNullOrWhiteSpace(request.Role) ? "Staff" : request.Role.Trim();
				var role = await _context.Roles
					.FirstOrDefaultAsync(r => r.RoleName.ToLower() == roleName.ToLower());

				bool roleCreated = false;

				// ✅ Nếu role không tồn tại → tạo mới
				if (role == null)
				{
					role = new Role
					{
						RoleName = roleName,
						CreatedAt = DateTime.Now
					};
					_context.Roles.Add(role);
					await _context.SaveChangesAsync();
					roleCreated = true;

					await _auditHelper.LogAsync(
						adminId,
						"CREATE",
						"Role",
						role.RoleId,
						null,
						new { role.RoleName, role.CreatedAt },
						$"Tự động tạo role '{roleName}' khi import user"
					);
				}

				// ============================================
				// GENERATE USERNAME từ email
				// ============================================
				var username = GenerateUsernameFromEmail(request.Email);

				// ✅ Check username already exists
				var usernameExists = await _context.Users
					.AnyAsync(u => u.Username == username);

				if (usernameExists)
				{
					var counter = 1;
					var baseUsername = username;
					while (await _context.Users.AnyAsync(u => u.Username == username))
					{
						username = $"{baseUsername}{counter}";
						counter++;
					}
				}

				// ============================================
				// TÍNH ISTESTER - Đơn giản
				// ============================================
				bool isTester = role.RoleName.ToLower() == "tester";

				// ============================================
				// TẠO USER MỚI
				// ============================================
				var user = new User
				{
					Username = username,
					PasswordHash = BCrypt.Net.BCrypt.HashPassword("123456789A@a"), // Mật khẩu mặc định
					FullName = request.FullName.Trim(),
					Email = request.Email.Trim().ToLower(),
					PhoneNumber = request.PhoneNumber.Trim(),
					DepartmentId = departmentId,
					RoleId = role.RoleId,
					IsTester = isTester,
					IsActive = true,
					CreatedAt = DateTime.Now,
					CreatedBy = adminId
				};

				_context.Users.Add(user);
				await _context.SaveChangesAsync();

				// ============================================
				// LOG AUDIT
				// ============================================
				await _auditHelper.LogDetailedAsync(
					adminId,
					"IMPORT",
					"User",
					user.UserId,
					null,
					new
					{
						Username = user.Username,
						FullName = user.FullName,
						Email = user.Email,
						PhoneNumber = user.PhoneNumber,
						RoleName = role.RoleName,
						DepartmentId = departmentId ?? 0,
						IsTester = user.IsTester
					},
					$"Import user: {user.FullName} ({username})",
					new Dictionary<string, object>
					{
				{ "ImportMethod", "Excel" },
				{ "DefaultPassword", "123456789A@a" },
				{ "CreatedBy", HttpContext.Session.GetString("FullName") ?? "Admin" },
				{ "RoleCreated", roleCreated },
				{ "DepartmentCreated", departmentCreated }
					}
				);

				// ============================================
				// RESPONSE VỚI WARNINGS NẾU CẦN
				// ============================================
				var warnings = new List<string>();

				if (roleCreated)
					warnings.Add($"✓ Tự động tạo role '{role.RoleName}'");

				if (departmentCreated)
					warnings.Add($"✓ Tự động tạo phòng ban '{request.Department}'");

				if (!roleCreated && !departmentCreated)
					warnings.Add("✓ Import thành công");

				return Json(new
				{
					success = true,
					message = "Import thành công",
					username = username,
					userId = user.UserId,
					department = departmentStatus,
					role = role.RoleName,
					warnings = warnings
				});
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"[ImportSingleUser] Error: {ex.Message}\n{ex.StackTrace}");

				await _auditHelper.LogFailedAttemptAsync(
					adminId,
					"IMPORT",
					"User",
					$"Exception: {ex.Message}",
					new
					{
						Email = request.Email,
						FullName = request.FullName,
						PhoneNumber = request.PhoneNumber,
						Department = request.Department,
						Error = ex.ToString()
					}
				);

				return Json(new
				{
					success = false,
					message = $"Có lỗi xảy ra: {ex.Message}"
				});
			}
		}

		// ============================================
		// HELPER METHODS CHO IMPORT
		// ============================================

		/// <summary>
		/// Validate email format
		/// </summary>
		private bool IsValidEmail(string email)
		{
			try
			{
				var addr = new System.Net.Mail.MailAddress(email);
				return addr.Address == email;
			}
			catch
			{
				return false;
			}
		}

		private string GenerateUsernameFromEmail(string email)
		{
			if (string.IsNullOrEmpty(email))
				return "user" + DateTime.Now.Ticks;

			var username = email.Split('@')[0];
			username = new string(username.Where(c => char.IsLetterOrDigit(c) || c == '_' || c == '.').ToArray());
			username = username.ToLower();

			if (username.Length > 50)
				username = username.Substring(0, 50);

			if (string.IsNullOrEmpty(username))
				username = "user" + new Random().Next(1000, 9999);

			return username;
		}



		/// <summary>
		/// Export danh sách users ra file Excel
		/// </summary>
		[HttpGet]
		public async Task<IActionResult> ExportUsersToExcel(
			string? search = null,
			string? roleName = null,
			string? status = null,
			int? departmentId = null)
		{
			if (!IsSuperAdmin())
				return Json(new { success = false, message = "Không có quyền" });

			try
			{
				var adminId = HttpContext.Session.GetInt32("UserId");

				// Query với filter giống UserList
				var query = _context.Users
					.Include(u => u.Role)
					.Include(u => u.Department)
					.AsQueryable();

				if (!string.IsNullOrWhiteSpace(search))
				{
					var s = search.Trim().ToLower();
					query = query.Where(u =>
						(u.FullName != null && u.FullName.ToLower().Contains(s)) ||
						(u.Username != null && u.Username.ToLower().Contains(s)) ||
						(u.Email != null && u.Email.ToLower().Contains(s)) ||
						(u.PhoneNumber != null && u.PhoneNumber.ToLower().Contains(s))
					);
				}

				if (!string.IsNullOrWhiteSpace(roleName))
					query = query.Where(u => u.Role != null && u.Role.RoleName == roleName);

				if (!string.IsNullOrWhiteSpace(status))
				{
					if (status == "active")
						query = query.Where(u => u.IsActive == true);
					else if (status == "inactive")
						query = query.Where(u => u.IsActive == false);
				}

				if (departmentId.HasValue && departmentId.Value > 0)
					query = query.Where(u => u.DepartmentId == departmentId.Value);

				var users = await query
					.OrderBy(u => u.FullName)
					.ToListAsync();

				// Tạo danh sách data để export
				var exportData = users.Select((u, index) => new
				{
					STT = index + 1,
					HoTen = u.FullName ?? "",
					Username = u.Username ?? "",
					Email = u.Email ?? "",
					SoDienThoai = u.PhoneNumber ?? "",
					PhongBan = u.Department?.DepartmentName ?? "Chưa phân công",
					VaiTro = u.Role?.RoleName ?? "N/A",
					TrangThai = u.IsActive == true ? "Hoạt động" : "Vô hiệu hóa",
					NgayTao = u.CreatedAt?.ToString("dd/MM/yyyy HH:mm") ?? "",
					LanDangNhapCuoi = u.LastLoginAt?.ToString("dd/MM/yyyy HH:mm") ?? "Chưa đăng nhập"
				}).ToList();

				// Log audit
				await _auditHelper.LogAsync(
					adminId,
					"EXPORT",
					"User",
					null,
					null,
					null,
					$"Xuất danh sách {users.Count} users ra Excel"
				);

				// Trả về JSON data để xử lý ở client
				return Json(new
				{
					success = true,
					data = exportData,
					totalRecords = users.Count,
					exportDate = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss")
				});
			}
			catch (Exception ex)
			{
				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"EXPORT",
					"User",
					$"Exception: {ex.Message}",
					new { Error = ex.ToString() }
				);

				return Json(new { success = false, message = $"Có lỗi xảy ra: {ex.Message}" });
			}
		}

		/// <summary>
		/// Hiển thị thông tin audit logs và cleanup status
		/// </summary>
		[HttpGet]
		public async Task<IActionResult> AuditLogsStatus()
		{
			if (!IsSuperAdmin())
				return RedirectToAction("Login", "Account");

			try
			{
				var cleanupStatus = await _auditCleanupService.GetCleanupStatusAsync();

				ViewBag.CleanupStatus = cleanupStatus;
				ViewBag.DatabaseSizeEstimate = cleanupStatus.GetDatabaseSizeInfo();
				ViewBag.PercentageOld = cleanupStatus.TotalRecords > 0
					? Math.Round((double)cleanupStatus.RecordsOlderThan2Months / cleanupStatus.TotalRecords * 100, 1)
					: 0;

				await _auditHelper.LogViewAsync(
					HttpContext.Session.GetInt32("UserId").Value,
					"AuditLog",
					0,
					"Xem thông tin Audit Logs Status"
				);

				return View();
			}
			catch (Exception ex)
			{
				TempData["Error"] = $"Lỗi: {ex.Message}";
				return RedirectToAction("Dashboard");
			}
		}

		/// <summary>
		/// TRIGGER CLEANUP NGAY LẬP TỨC (thường xuyên không cần dùng, Hangfire sẽ tự chạy)
		/// </summary>
		[HttpPost]
		public async Task<IActionResult> TriggerAuditCleanup([FromBody] CleanupRequest request)
		{
			if (!IsSuperAdmin())
				return Json(new { success = false, message = "Không có quyền thực hiện" });

			try
			{
				var adminId = HttpContext.Session.GetInt32("UserId");
				var daysToKeep = request.DaysToKeep ?? 60; // Default 60 days = 2 months

				_logger.LogInformation(
					"[AuditCleanup] Admin {AdminId} triggered cleanup (keep {DaysToKeep} days)",
					adminId, daysToKeep);

				// ✅ CHẠY CLEANUP (background task)
				await _auditCleanupService.CleanupOldAuditLogsAsync(daysToKeep);

				// ✅ LOG AUDIT ACTION
				await _auditHelper.LogAsync(
					adminId,
					"CLEANUP",
					"AuditLog",
					null,
					null,
					new { DaysToKeep = daysToKeep, ExecutedAt = DateTime.Now },
					$"Admin trigger cleanup audit logs (giữ lại {daysToKeep} ngày)"
				);

				return Json(new
				{
					success = true,
					message = $"✅ Cleanup hoàn tất! Giữ lại {daysToKeep} ngày dữ liệu"
				});
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "[AuditCleanup] Cleanup failed");

				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"CLEANUP",
					"AuditLog",
					$"Exception: {ex.Message}",
					new { Error = ex.ToString() }
				);

				return Json(new
				{
					success = false,
					message = $"❌ Cleanup thất bại: {ex.Message}"
				});
			}
		}

		/// <summary>
		/// Lấy thông tin cleanup status (JSON)
		/// </summary>
		[HttpGet]
		public async Task<IActionResult> GetCleanupStatus()
		{
			if (!IsAdmin())
				return Json(new { success = false, message = "Không có quyền" });

			try
			{
				var status = await _auditCleanupService.GetCleanupStatusAsync();

				return Json(new
				{
					success = true,
					data = new
					{
						totalRecords = status.TotalRecords,
						recordsLast2Months = status.RecordsLast2Months,
						recordsLast6Months = status.RecordsLast6Months,
						recordsOlderThan2Months = status.RecordsOlderThan2Months,
						percentageOld = status.TotalRecords > 0
							? Math.Round((double)status.RecordsOlderThan2Months / status.TotalRecords * 100, 1)
							: 0,
						databaseSize = status.GetDatabaseSizeInfo(),
						oldestLogDate = status.OldestLogDate?.ToString("dd/MM/yyyy HH:mm"),
						newestLogDate = status.NewestLogDate?.ToString("dd/MM/yyyy HH:mm"),
						status = status.Status,
						lastCleanupDate = status.LastCleanupDate.ToString("dd/MM/yyyy HH:mm")
					}
				});
			}
			catch (Exception ex)
			{
				return Json(new { success = false, message = ex.Message });
			}
		}

		/// <summary>
		/// Export audit logs ra file (với filter optional)
		/// </summary>
		[HttpGet]
		public async Task<IActionResult> ExportAuditLogs(string? action, DateTime? fromDate, DateTime? toDate)
		{
			if (!IsAdmin())
				return Json(new { success = false, message = "Không có quyền" });

			try
			{
				var from = fromDate ?? DateTime.Today.AddMonths(-1);
				var to = toDate ?? DateTime.Today;

				var query = _context.AuditLogs
					.Include(a => a.User)
					.AsQueryable();

				if (!string.IsNullOrWhiteSpace(action))
					query = query.Where(a => a.Action.ToLower() == action.ToLower());

				query = query.Where(a => a.Timestamp >= from && a.Timestamp <= to);

				var logs = await query
					.OrderByDescending(a => a.Timestamp)
					.ToListAsync();

				var exportData = logs.Select((log, index) => new
				{
					STT = index + 1,
					Timestamp = log.Timestamp?.ToString("dd/MM/yyyy HH:mm:ss"),
					User = log.User?.FullName ?? "System",
					Action = log.Action,
					EntityName = log.EntityName,
					EntityId = log.EntityId,
					Description = log.Description,
					IpAddress = log.Ipaddress,
					Location = log.Location
				}).ToList();

				return Json(new
				{
					success = true,
					data = exportData,
					totalRecords = logs.Count,
					exportDate = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss")
				});
			}
			catch (Exception ex)
			{
				return Json(new { success = false, message = ex.Message });
			}
		}


		/// <summary>
		/// Hiển thị trang Thùng rác Phòng ban
		/// </summary>
		[HttpGet]
		public async Task<IActionResult> DepartmentTrash()
		{
			if (!IsAdmin())
				return RedirectToAction("Login", "Account");

			await _auditHelper.LogViewAsync(
				HttpContext.Session.GetInt32("UserId").Value,
				"Department",
				0,
				"Xem thùng rác phòng ban"
			);

			// Lấy danh sách phòng ban đã bị xóa (IsActive = false)
			var trashedDepartments = await _context.Departments
				.Include(d => d.Users)
				.Where(d => d.IsActive == false)
				.OrderByDescending(d => d.UpdatedAt)
				.ToListAsync();

			ViewBag.TotalTrash = trashedDepartments.Count;
			ViewBag.TotalUsersInTrash = trashedDepartments.Sum(d => d.Users?.Count ?? 0);

			return View(trashedDepartments);
		}

		/// <summary>
		/// Di chuyển phòng ban vào thùng rác (soft delete)
		/// </summary>
		[HttpPost]
		public async Task<IActionResult> MoveDepartmentToTrash([FromBody] DeleteDepartmentRequest request)
		{
			if (!IsSuperAdmin())
			{
				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"SOFT_DELETE",
					"Department",
					"Không có quyền xóa",
					new { DepartmentId = request.DepartmentId }
				);

				return Json(new { success = false, message = "Không có quyền thực hiện!" });
			}

			var department = await _context.Departments
				.Include(d => d.Users)
				.FirstOrDefaultAsync(d => d.DepartmentId == request.DepartmentId);

			if (department == null)
			{
				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"SOFT_DELETE",
					"Department",
					"Phòng ban không tồn tại",
					new { DepartmentId = request.DepartmentId }
				);

				return Json(new { success = false, message = "Không tìm thấy phòng ban!" });
			}

			// Không cho xóa nếu có nhân viên đang hoạt động
			if (department.Users != null && department.Users.Any(u => u.IsActive == true))
			{
				var activeUserCount = department.Users.Count(u => u.IsActive == true);

				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"SOFT_DELETE",
					"Department",
					"Phòng ban có nhân viên đang hoạt động",
					new { DepartmentId = request.DepartmentId, ActiveUsers = activeUserCount }
				);

				return Json(new
				{
					success = false,
					message = $"Không thể xóa phòng ban có {activeUserCount} nhân viên đang hoạt động! Vui lòng chuyển họ sang phòng ban khác trước."
				});
			}

			try
			{
				var adminId = HttpContext.Session.GetInt32("UserId");
				var departmentName = department.DepartmentName;

				// Soft Delete: Đánh dấu IsActive = false
				department.IsActive = false;
				department.UpdatedAt = DateTime.Now;

				await _context.SaveChangesAsync();

				await _auditHelper.LogAsync(
					adminId,
					"SOFT_DELETE",
					"Department",
					department.DepartmentId,
					new { IsActive = true },
					new { IsActive = false },
					$"Chuyển phòng ban '{departmentName}' vào thùng rác"
				);

				return Json(new
				{
					success = true,
					message = $"Đã chuyển phòng ban '{departmentName}' vào thùng rác"
				});
			}
			catch (Exception ex)
			{
				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"SOFT_DELETE",
					"Department",
					$"Exception: {ex.Message}",
					new { DepartmentId = request.DepartmentId, Error = ex.ToString() }
				);

				return Json(new { success = false, message = $"Có lỗi xảy ra: {ex.Message}" });
			}
		}

		/// <summary>
		/// Khôi phục phòng ban từ thùng rác
		/// </summary>
		[HttpPost]
		public async Task<IActionResult> RestoreDepartmentFromTrash([FromBody] RestoreDepartmentRequest request)
		{
			if (!IsAdmin())
			{
				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"RESTORE",
					"Department",
					"Không có quyền khôi phục",
					new { DepartmentId = request.DepartmentId }
				);

				return Json(new { success = false, message = "Không có quyền thực hiện!" });
			}

			var department = await _context.Departments
				.FirstOrDefaultAsync(d => d.DepartmentId == request.DepartmentId);

			if (department == null)
			{
				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"RESTORE",
					"Department",
					"Phòng ban không tồn tại",
					new { DepartmentId = request.DepartmentId }
				);

				return Json(new { success = false, message = "Không tìm thấy phòng ban!" });
			}

			try
			{
				var adminId = HttpContext.Session.GetInt32("UserId");
				var departmentName = department.DepartmentName;

				// Restore: Đánh dấu IsActive = true
				department.IsActive = true;
				department.UpdatedAt = DateTime.Now;

				await _context.SaveChangesAsync();

				await _auditHelper.LogAsync(
					adminId,
					"RESTORE",
					"Department",
					department.DepartmentId,
					new { IsActive = false },
					new { IsActive = true },
					$"Khôi phục phòng ban '{departmentName}' từ thùng rác"
				);

				return Json(new
				{
					success = true,
					message = $"Đã khôi phục phòng ban '{departmentName}'"
				});
			}
			catch (Exception ex)
			{
				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"RESTORE",
					"Department",
					$"Exception: {ex.Message}",
					new { DepartmentId = request.DepartmentId, Error = ex.ToString() }
				);

				return Json(new { success = false, message = $"Có lỗi xảy ra: {ex.Message}" });
			}
		}

		/// <summary>
		/// Xóa vĩnh viễn phòng ban khỏi database (hard delete)
		/// </summary>
		[HttpPost]
		public async Task<IActionResult> PermanentlyDeleteDepartment([FromBody] DeleteDepartmentRequest request)
		{
			if (!IsAdmin())
			{
				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"HARD_DELETE",
					"Department",
					"Không có quyền xóa vĩnh viễn",
					new { DepartmentId = request.DepartmentId }
				);

				return Json(new { success = false, message = "Không có quyền thực hiện!" });
			}

			var department = await _context.Departments
				.Include(d => d.Users)
				.FirstOrDefaultAsync(d => d.DepartmentId == request.DepartmentId && d.IsActive == false);

			if (department == null)
			{
				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"HARD_DELETE",
					"Department",
					"Phòng ban không tồn tại hoặc chưa ở trong thùng rác",
					new { DepartmentId = request.DepartmentId }
				);

				return Json(new { success = false, message = "Không tìm thấy phòng ban trong thùng rác!" });
			}

			// Không cho xóa vĩnh viễn nếu vẫn còn user liên kết
			if (department.Users != null && department.Users.Any())
			{
				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"HARD_DELETE",
					"Department",
					"Phòng ban vẫn còn nhân viên",
					new { DepartmentId = request.DepartmentId, UserCount = department.Users.Count }
				);

				return Json(new
				{
					success = false,
					message = $"Không thể xóa vĩnh viễn phòng ban có {department.Users.Count} nhân viên! Vui lòng chuyển họ sang phòng ban khác trước."
				});
			}

			try
			{
				var adminId = HttpContext.Session.GetInt32("UserId");
				var departmentName = department.DepartmentName;

				// Hard Delete: Xóa khỏi database
				_context.Departments.Remove(department);
				await _context.SaveChangesAsync();

				await _auditHelper.LogAsync(
					adminId,
					"HARD_DELETE",
					"Department",
					request.DepartmentId,
					new { DepartmentName = departmentName },
					null,
					$"Xóa vĩnh viễn phòng ban '{departmentName}' khỏi hệ thống"
				);

				return Json(new
				{
					success = true,
					message = $"Đã xóa vĩnh viễn phòng ban '{departmentName}'"
				});
			}
			catch (Exception ex)
			{
				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"HARD_DELETE",
					"Department",
					$"Exception: {ex.Message}",
					new { DepartmentId = request.DepartmentId, Error = ex.ToString() }
				);

				return Json(new { success = false, message = $"Có lỗi xảy ra: {ex.Message}" });
			}
		}

		/// <summary>
		/// Làm rỗng toàn bộ thùng rác (xóa vĩnh viễn tất cả)
		/// </summary>
		[HttpPost]
		public async Task<IActionResult> EmptyDepartmentTrash()
		{
			if (!IsAdmin())
			{
				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"EMPTY_TRASH",
					"Department",
					"Không có quyền làm rỗng thùng rác",
					null
				);

				return Json(new { success = false, message = "Không có quyền thực hiện!" });
			}

			try
			{
				var adminId = HttpContext.Session.GetInt32("UserId");

				// Lấy tất cả phòng ban trong thùng rác KHÔNG CÓ USER
				var trashedDepartments = await _context.Departments
					.Include(d => d.Users)
					.Where(d => d.IsActive == false)
					.ToListAsync();

				if (!trashedDepartments.Any())
				{
					return Json(new
					{
						success = false,
						message = "Thùng rác đang trống!"
					});
				}

				// Phòng ban có thể xóa (không có user)
				var deletableDepartments = trashedDepartments
					.Where(d => d.Users == null || !d.Users.Any())
					.ToList();

				// Phòng ban không thể xóa (còn user)
				var nonDeletableDepartments = trashedDepartments
					.Where(d => d.Users != null && d.Users.Any())
					.ToList();

				if (!deletableDepartments.Any())
				{
					return Json(new
					{
						success = false,
						message = $"Không thể làm rỗng thùng rác! Có {nonDeletableDepartments.Count} phòng ban vẫn còn nhân viên."
					});
				}

				// Xóa vĩnh viễn các phòng ban không có user
				_context.Departments.RemoveRange(deletableDepartments);
				await _context.SaveChangesAsync();

				await _auditHelper.LogDetailedAsync(
					adminId,
					"EMPTY_TRASH",
					"Department",
					null,
					null,
					null,
					$"Làm rỗng thùng rác phòng ban: Xóa {deletableDepartments.Count} phòng ban",
					new Dictionary<string, object>
					{
				{ "DeletedCount", deletableDepartments.Count },
				{ "SkippedCount", nonDeletableDepartments.Count },
				{ "DeletedDepartments", string.Join(", ", deletableDepartments.Select(d => d.DepartmentName)) }
					}
				);

				var message = deletableDepartments.Count == trashedDepartments.Count
					? $"Đã xóa vĩnh viễn {deletableDepartments.Count} phòng ban khỏi thùng rác"
					: $"Đã xóa {deletableDepartments.Count} phòng ban. Bỏ qua {nonDeletableDepartments.Count} phòng ban vẫn còn nhân viên.";

				return Json(new
				{
					success = true,
					message = message,
					deletedCount = deletableDepartments.Count,
					skippedCount = nonDeletableDepartments.Count
				});
			}
			catch (Exception ex)
			{
				await _auditHelper.LogFailedAttemptAsync(
					HttpContext.Session.GetInt32("UserId"),
					"EMPTY_TRASH",
					"Department",
					$"Exception: {ex.Message}",
					new { Error = ex.ToString() }
				);

				return Json(new { success = false, message = $"Có lỗi xảy ra: {ex.Message}" });
			}
		}
		public class RestoreDepartmentRequest
		{
			public int DepartmentId { get; set; }
		}
		// ============================================
		// REQUEST MODEL
		// ============================================
		public class CleanupRequest
		{
			public int? DaysToKeep { get; set; } = 60; // Default 60 days = 2 months
		}

		// ============================================
		// REQUEST MODEL
		// ============================================
		public class ImportUserRequest
		{
			public int RowNumber { get; set; }
			public string FullName { get; set; } = string.Empty;
			public string Email { get; set; } = string.Empty;
			public string? PhoneNumber { get; set; }
			public string? Department { get; set; } // Có thể null
			public string? Role { get; set; }       // Có thể null, mặc định Staff
		}
		// ============================================
		// REQUEST MODELS
		// ============================================
		public class CreateProjectRequest
		{
			public string ProjectName { get; set; } = string.Empty;
			public string? Description { get; set; }
			public DateTime? StartDate { get; set; }
			public DateTime? EndDate { get; set; }
			public string? Priority { get; set; }
			public decimal? Budget { get; set; }
			public int? LeaderId { get; set; }
			public int? DepartmentId { get; set; }
			public List<int>? MemberIds { get; set; }
		}

		public class UpdateProjectRequest
		{
			public int ProjectId { get; set; }
			public string ProjectName { get; set; } = string.Empty;
			public string? Description { get; set; }
			public DateTime? StartDate { get; set; }
			public DateTime? EndDate { get; set; }
			public string? Status { get; set; }
			public string? Priority { get; set; }
			public decimal? Budget { get; set; }
			public int? LeaderId { get; set; }
			public int? DepartmentId { get; set; }
		}

		public class CreateProjectTaskRequest
		{
			public int ProjectId { get; set; }
			public string TaskName { get; set; } = string.Empty;
			public string? Description { get; set; }
			public string? Platform { get; set; }
			public DateTime? Deadline { get; set; }
			public string? Priority { get; set; }
			public int? OrderIndex { get; set; }
			public List<int>? AssignedUserIds { get; set; }
		}
		public class ManageProjectMemberRequest
		{
			public int ProjectId { get; set; }
			public int UserId { get; set; }
			public string Action { get; set; } = "Add"; // Add or Remove
			public string? Role { get; set; }
		}

		public class CreateRoleRequest
		{
			public string RoleName { get; set; } = string.Empty;
		}

		public class UpdateRoleRequest
		{
			public int RoleId { get; set; }
			public string RoleName { get; set; } = string.Empty;
		}

		public class DeleteRoleRequest
		{
			public int RoleId { get; set; }
		}
		public class UpdateUserRequest
		{
			public int UserId { get; set; }
			public string FullName { get; set; } = string.Empty;
			public string? Email { get; set; }
			public string? PhoneNumber { get; set; }
			public int? DepartmentId { get; set; }
			public int RoleId { get; set; }
			public bool? IsTester { get; set; } // ✅ THÊM FIELD NÀY
			public bool IsActive { get; set; }
		}

		// DTO class để map kết quả từ stored procedure
		public class DepartmentKPIDto
		{
			public int DepartmentId { get; set; }
			public string DepartmentName { get; set; } = string.Empty;
			public int TotalEmployees { get; set; }
			public int TotalAttendances { get; set; }
			public double AvgAttendancePerEmployee { get; set; }
			public int TotalLateDays { get; set; }
			public double LateRate { get; set; }
			public decimal TotalWorkHours { get; set; }
			public decimal AvgHoursPerDay { get; set; }
			public decimal TotalOvertimeHours { get; set; }
			public int TotalTasks { get; set; }
			public int CompletedTasks { get; set; }
			public double TaskCompletionRate { get; set; }
			public double DepartmentKPIScore { get; set; }
		}

		public class ReviewRequestViewModel
		{
			public string RequestType { get; set; } = string.Empty;
			public int RequestId { get; set; }
			public string Action { get; set; } = string.Empty;
			public string? Note { get; set; }
		}
		// ============================================
		// REQUEST MODELS
		// ============================================
		public class CreateDepartmentRequest
		{
			public string DepartmentName { get; set; } = string.Empty;
			public string? Description { get; set; }
			public bool IsActive { get; set; } = true;
		}

		public class UpdateDepartmentRequest
		{
			public int DepartmentId { get; set; }
			public string DepartmentName { get; set; } = string.Empty;
			public string? Description { get; set; }
			public bool IsActive { get; set; }
		}

		public class DeleteDepartmentRequest
		{
			public int DepartmentId { get; set; }
		}

		public class ToggleDepartmentRequest
		{
			public int DepartmentId { get; set; }
		}

		public class ResetPasswordRequest
		{
			public int UserId { get; set; }
			public string NewPassword { get; set; } = string.Empty;
			public string Reason { get; set; } = string.Empty;
		}

		public class ToggleUserRequest
		{
			public int UserId { get; set; }
		}

		public class CreateTaskRequest
		{
			public string TaskName { get; set; } = string.Empty;
			public string? Description { get; set; }
			public string? Platform { get; set; }
			public DateTime? Deadline { get; set; }
			public string? Priority { get; set; }
			public List<int>? AssignedUserIds { get; set; }
		}

		public class UpdateTaskRequest
		{
			public int TaskId { get; set; }
			public string TaskName { get; set; } = string.Empty;
			public string? Description { get; set; }
			public string? Platform { get; set; }
			public DateTime? Deadline { get; set; }
			public string? Priority { get; set; }
			public List<int>? AssignedUserIds { get; set; }
		}

		public class DeleteTaskRequest
		{
			public int TaskId { get; set; }
		}

		public class ToggleTaskStatusRequest
		{
			public int TaskId { get; set; }
		}
	}
}
