using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TMD.Models.ViewModels;
using AIHUBOS.Helpers;
using BCrypt.Net;
using TMD.Models;
using System.Text.Json;
using System.Linq;
using Microsoft.AspNetCore.SignalR;
using AIHUBOS.Hubs;
using AIHUBOS.Services;

namespace TMD.Controllers
{
	public class StaffController : Controller
	{
		private readonly AihubSystemContext _context;
		private readonly AuditHelper _auditHelper;
		private readonly IWebHostEnvironment _env;
		private readonly HttpClient _httpClient;
		private readonly IHubContext<NotificationHub> _hubContext;
		private readonly INotificationService _notificationService;
		private readonly ITelegramService _telegramService;


		public StaffController(AihubSystemContext context, AuditHelper auditHelper, IWebHostEnvironment env, IHttpClientFactory httpClientFactory, IHubContext<NotificationHub> hubContext, INotificationService notificationService, ITelegramService telegramService)
		{
			_context = context;
			_auditHelper = auditHelper;
			_env = env;
			_httpClient = httpClientFactory.CreateClient();
			_hubContext = hubContext;
			_notificationService = notificationService;
			_telegramService = telegramService;

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
		private bool IsAuthenticated()
		{
			return HttpContext.Session.GetInt32("UserId") != null;
		}

		private bool IsStaffOrAdmin()
		{
			var roleName = HttpContext.Session.GetString("RoleName");

			// Chỉ chặn nếu role NULL hoặc rỗng (chưa đăng nhập đúng cách)
			if (string.IsNullOrEmpty(roleName))
				return false;

			// ✅ Chấp nhận MỌI role hợp lệ: Admin, Staff, Manager, Guest, Tester, v.v.
			return true;
		}
		// ✅ THÊM METHOD NÀY VÀO StaffController.cs
		[HttpGet]
		public async Task<IActionResult> GetUserPermissions()
		{
			if (!IsAuthenticated())
				return Json(new { success = false, message = "Phiên đăng nhập hết hạn" });

			var userId = HttpContext.Session.GetInt32("UserId").Value;

			try
			{
				var user = await _context.Users
					.Include(u => u.Role)
					.Include(u => u.Department)
					.FirstOrDefaultAsync(u => u.UserId == userId);

				if (user == null)
					return Json(new { success = false, message = "Không tìm thấy người dùng" });

				var roleName = user.Role?.RoleName ?? "";
				var deptName = user.Department?.DepartmentName ?? "";

				// ✅ KIỂM TRA CHÍNH XÁC
				bool isDevRole = roleName.Contains("Dev", StringComparison.OrdinalIgnoreCase) ||
								roleName.Equals("Developer", StringComparison.OrdinalIgnoreCase);

				bool isDevDepartment = deptName.Contains("Dev Backend", StringComparison.OrdinalIgnoreCase) ||
									  deptName.Contains("Dev Frontend", StringComparison.OrdinalIgnoreCase) ||
									  deptName.Contains("Developer Backend", StringComparison.OrdinalIgnoreCase) ||
									  deptName.Contains("Developer Frontend", StringComparison.OrdinalIgnoreCase) ||
									  deptName.Contains("Backend", StringComparison.OrdinalIgnoreCase) ||
									  deptName.Contains("Frontend", StringComparison.OrdinalIgnoreCase);

				bool canSendToTesting = isDevRole || isDevDepartment;

				return Json(new
				{
					success = true,
					permissions = new
					{
						canSendToTesting = canSendToTesting,
						roleName = roleName,
						departmentName = deptName,
						fullName = user.FullName,
						isDev = canSendToTesting,
						allowedStatuses = canSendToTesting
							? new[] { "TODO", "InProgress", "Testing", "Done", "Reopen" }
							: new[] { "TODO", "InProgress", "Done" }
					}
				});
			}
			catch (Exception ex)
			{
				return Json(new { success = false, message = ex.Message });
			}
		}
		// ============================================
		// REVERSE GEOCODING - LẤY ĐỊA CHỈ TỪ TỌA ĐỘ (IMPROVED WITH RETRY)
		// ============================================
		private async System.Threading.Tasks.Task<string> GetAddressFromCoordinates(decimal latitude, decimal longitude)
		{
			// Retry logic - 3 attempts with exponential backoff
			for (int attempt = 0; attempt < 3; attempt++)
			{
				try
				{
					var url = $"https://nominatim.openstreetmap.org/reverse?format=json&lat={latitude}&lon={longitude}&addressdetails=1&accept-language=vi";

					_httpClient.DefaultRequestHeaders.Clear();
					_httpClient.DefaultRequestHeaders.Add("User-Agent", "TMDSystem/1.0 (Contact: admin@tmdystem.com)");
					_httpClient.Timeout = TimeSpan.FromSeconds(10);

					var response = await _httpClient.GetStringAsync(url);
					var jsonDoc = JsonDocument.Parse(response);

					if (jsonDoc.RootElement.TryGetProperty("display_name", out var displayName))
					{
						var address = displayName.GetString();
						if (!string.IsNullOrEmpty(address))
						{
							return address;
						}
					}

					// Fallback to coordinates
					return $"Lat: {latitude:F6}, Long: {longitude:F6}";
				}
				catch (TaskCanceledException)
				{
					if (attempt == 2) // Last attempt
					{
						return $"Lat: {latitude:F6}, Long: {longitude:F6}";
					}
					await System.Threading.Tasks.Task.Delay(1000 * (attempt + 1)); // Exponential backoff
				}
				catch
				{
					if (attempt == 2)
					{
						return $"Lat: {latitude:F6}, Long: {longitude:F6}";
					}
					await System.Threading.Tasks.Task.Delay(500);
				}
			}

			return $"Lat: {latitude:F6}, Long: {longitude:F6}";
		}

		// ============================================
		// STAFF DASHBOARD
		// ============================================
		// Trong StaffController.cs
		public async System.Threading.Tasks.Task<IActionResult> Dashboard()
		{
			if (!IsAuthenticated())
				return RedirectToAction("Login", "Account");

			if (!IsStaffOrAdmin())
				return RedirectToAction("Login", "Account");

			var userId = HttpContext.Session.GetInt32("UserId");

			var user = await _context.Users
				.Include(u => u.Department)
				.Include(u => u.Role)
				.FirstOrDefaultAsync(u => u.UserId == userId);

			if (user == null)
				return RedirectToAction("Login", "Account");

			ViewBag.User = user;

			// ============================================
			// THÊM: Lấy cấu hình giờ làm việc từ SystemSettings
			// Dùng để tính toán trạng thái (Đi trễ/Về sớm) trên Dashboard.cshtml
			// ============================================
			var configs = await _context.SystemSettings
				.Where(c => c.IsActive == true && c.IsEnabled == true)
				// Chỉ lấy các setting cần thiết để tối ưu truy vấn
				.Where(c => c.SettingKey == "CHECK_IN_STANDARD_TIME" || c.SettingKey == "CHECK_OUT_STANDARD_TIME")
				.ToDictionaryAsync(c => c.SettingKey, c => c.SettingValue);

			// Gán vào ViewBag. Nếu không tìm thấy, dùng giá trị mặc định "08:00" và "17:00"
			ViewBag.StandardStartTime = configs.GetValueOrDefault("CHECK_IN_STANDARD_TIME", "08:00");
			ViewBag.StandardEndTime = configs.GetValueOrDefault("CHECK_OUT_STANDARD_TIME", "17:00");

			// ============================================
			// GIỮ NGUYÊN: Các logic thống kê khác
			// ============================================
			var myLoginHistory = await _context.LoginHistories
				.Where(l => l.UserId == userId && l.IsSuccess == true)
				.OrderByDescending(l => l.LoginTime)
				.Take(5)
				.ToListAsync();

			ViewBag.MyLoginHistory = myLoginHistory;

			var thisMonthLogins = await _context.LoginHistories
				.CountAsync(l => l.UserId == userId
					&& l.IsSuccess == true
					&& l.LoginTime.HasValue
					&& l.LoginTime.Value.Month == DateTime.Now.Month
					&& l.LoginTime.Value.Year == DateTime.Now.Year);

			ViewBag.ThisMonthLogins = thisMonthLogins;

			var lastLogin = await _context.LoginHistories
				.Where(l => l.UserId == userId && l.IsSuccess == true)
				.OrderByDescending(l => l.LoginTime)
				.Skip(1)
				.FirstOrDefaultAsync();

			ViewBag.LastLogin = lastLogin;

			if (user.DepartmentId.HasValue)
			{
				ViewBag.DepartmentUserCount = await _context.Users
					.CountAsync(u => u.DepartmentId == user.DepartmentId && u.IsActive == true);
			}

			var firstDayOfMonth = new DateOnly(DateTime.Now.Year, DateTime.Now.Month, 1);
			var attendanceCount = await _context.Attendances
				.CountAsync(a => a.UserId == userId && a.WorkDate >= firstDayOfMonth);

			ViewBag.AttendanceThisMonth = attendanceCount;

			var totalHours = await _context.Attendances
				.Where(a => a.UserId == userId && a.WorkDate >= firstDayOfMonth)
				.SumAsync(a => a.TotalHours ?? 0);

			ViewBag.TotalHoursThisMonth = totalHours;

			return View();
		}

		// ============================================
		// PROFILE MANAGEMENT
		// ============================================

		[HttpGet]
		public async System.Threading.Tasks.Task<IActionResult> Profile()
		{
			if (!IsAuthenticated())
				return RedirectToAction("Login", "Account");

			var userId = HttpContext.Session.GetInt32("UserId");

			var user = await _context.Users
				.Include(u => u.Role)
				.Include(u => u.Department)
				.FirstOrDefaultAsync(u => u.UserId == userId);

			if (user == null)
				return NotFound();

			ViewBag.User = user;
			return View();
		}

		[HttpPost]
		public async System.Threading.Tasks.Task<IActionResult> UpdateProfileJson([FromBody] UpdateProfileViewModel model)
		{
			if (!IsAuthenticated())
			{
				return Json(new { success = false, message = "Phiên đăng nhập hết hạn. Vui lòng đăng nhập lại." });
			}

			var userId = HttpContext.Session.GetInt32("UserId");

			var user = await _context.Users.FindAsync(userId);
			if (user == null)
			{
				await _auditHelper.LogFailedAttemptAsync(
					userId,
					"UPDATE",
					"User",
					"Không tìm thấy người dùng",
					new { UserId = userId }
				);

				return Json(new { success = false, message = "Không tìm thấy người dùng" });
			}

			if (!string.IsNullOrEmpty(model.Email))
			{
				var emailExists = await _context.Users
					.AnyAsync(u => u.Email == model.Email && u.UserId != userId);

				if (emailExists)
				{
					await _auditHelper.LogFailedAttemptAsync(
						userId,
						"UPDATE",
						"User",
						"Email đã được sử dụng",
						new { Email = model.Email }
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
					user.PhoneNumber
				};

				user.FullName = model.FullName;
				user.Email = model.Email;
				user.PhoneNumber = model.PhoneNumber;
				user.UpdatedAt = DateTime.Now;

				await _context.SaveChangesAsync();

				HttpContext.Session.SetString("FullName", user.FullName);

				var newData = new
				{
					user.FullName,
					user.Email,
					user.PhoneNumber
				};

				await _auditHelper.LogDetailedAsync(
					userId,
					"UPDATE",
					"User",
					user.UserId,
					oldData,
					newData,
					"Cập nhật thông tin cá nhân",
					new Dictionary<string, object>
					{
						{ "ChangedFields", GetChangedFields(oldData, newData) },
						{ "UpdatedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") }
					}
				);

				return Json(new
				{
					success = true,
					message = "Cập nhật thông tin thành công!"
				});
			}
			catch (Exception ex)
			{
				await _auditHelper.LogFailedAttemptAsync(
					userId,
					"UPDATE",
					"User",
					$"Exception: {ex.Message}",
					new { Error = ex.ToString() }
				);

				return Json(new
				{
					success = false,
					message = $"Có lỗi xảy ra: {ex.Message}"
				});
			}
		}

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

		// ============================================
		// CHANGE PASSWORD
		// ============================================

		[HttpPost]
		public async System.Threading.Tasks.Task<IActionResult> ChangePasswordJson([FromBody] ChangePasswordViewModel model)
		{
			if (!IsAuthenticated())
			{
				return Json(new { success = false, message = "Phiên đăng nhập hết hạn. Vui lòng đăng nhập lại." });
			}

			var userId = HttpContext.Session.GetInt32("UserId");

			if (!ModelState.IsValid)
			{
				var errors = ModelState.Values
					.SelectMany(v => v.Errors)
					.Select(e => e.ErrorMessage)
					.FirstOrDefault();

				await _auditHelper.LogFailedAttemptAsync(
					userId,
					"PASSWORD_CHANGE",
					"User",
					$"Dữ liệu không hợp lệ: {errors}",
					null
				);

				return Json(new { success = false, message = errors ?? "Dữ liệu không hợp lệ" });
			}

			var user = await _context.Users
				.FirstOrDefaultAsync(u => u.UserId == userId);

			if (user == null)
			{
				await _auditHelper.LogFailedAttemptAsync(
					userId,
					"PASSWORD_CHANGE",
					"User",
					"Không tìm thấy người dùng",
					null
				);

				return Json(new { success = false, message = "Không tìm thấy người dùng" });
			}

			if (!BCrypt.Net.BCrypt.Verify(model.CurrentPassword, user.PasswordHash))
			{
				await _auditHelper.LogFailedAttemptAsync(
					userId,
					"PASSWORD_CHANGE",
					"User",
					"Mật khẩu hiện tại không đúng",
					new
					{
						Username = user.Username,
						IP = HttpContext.Connection.RemoteIpAddress?.ToString()
					}
				);

				return Json(new { success = false, message = "Mật khẩu hiện tại không đúng" });
			}

			if (BCrypt.Net.BCrypt.Verify(model.NewPassword, user.PasswordHash))
			{
				await _auditHelper.LogFailedAttemptAsync(
					userId,
					"PASSWORD_CHANGE",
					"User",
					"Mật khẩu mới trùng với mật khẩu cũ",
					new { Username = user.Username }
				);

				return Json(new { success = false, message = "Mật khẩu mới phải khác mật khẩu hiện tại" });
			}

			try
			{
				var oldPasswordHash = user.PasswordHash;

				user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);
				user.UpdatedAt = DateTime.Now;
				await _context.SaveChangesAsync();

				var resetHistory = new PasswordResetHistory
				{
					UserId = user.UserId,
					ResetByUserId = userId,
					OldPasswordHash = oldPasswordHash,
					ResetTime = DateTime.Now,
					ResetReason = "Đổi mật khẩu thông qua trang Profile",
					Ipaddress = HttpContext.Connection.RemoteIpAddress?.ToString()
				};

				_context.PasswordResetHistories.Add(resetHistory);
				await _context.SaveChangesAsync();

				await _auditHelper.LogDetailedAsync(
					userId,
					"PASSWORD_CHANGE",
					"User",
					user.UserId,
					new { Action = "Change Password", OldPasswordHash = "***HIDDEN***" },
					new { Action = "Password Changed Successfully" },
					"Đổi mật khẩu thành công qua Profile",
					new Dictionary<string, object>
					{
						{ "Method", "Self-Service" },
						{ "IP", HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown" },
						{ "ChangedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") }
					}
				);

				HttpContext.Session.Clear();

				return Json(new
				{
					success = true,
					message = "Đổi mật khẩu thành công! Vui lòng đăng nhập lại với mật khẩu mới."
				});
			}
			catch (Exception ex)
			{
				await _auditHelper.LogFailedAttemptAsync(
					userId,
					"PASSWORD_CHANGE",
					"User",
					$"Exception: {ex.Message}",
					new { Error = ex.ToString() }
				);

				return Json(new
				{
					success = false,
					message = $"Có lỗi xảy ra: {ex.Message}"
				});
			}
		}

		// ============================================
		// MY LOGIN HISTORY
		// ============================================

		[HttpGet]
		public async System.Threading.Tasks.Task<IActionResult> MyLoginHistory()
		{
			if (!IsAuthenticated())
				return RedirectToAction("Login", "Account");

			var userId = HttpContext.Session.GetInt32("UserId");

			await _auditHelper.LogViewAsync(
				userId.Value,
				"LoginHistory",
				userId.Value,
				"Xem lịch sử đăng nhập cá nhân"
			);

			var history = await _context.LoginHistories
				.Where(l => l.UserId == userId)
				.OrderByDescending(l => l.LoginTime)
				.Take(50)
				.ToListAsync();

			return View(history);
		}

		// ============================================
		// MY DEPARTMENT INFO
		// ============================================

		[HttpGet]
		public async System.Threading.Tasks.Task<IActionResult> MyDepartment()
		{
			if (!IsAuthenticated())
				return RedirectToAction("Login", "Account");

			var userId = HttpContext.Session.GetInt32("UserId");

			var user = await _context.Users
				.Include(u => u.Department)
				.FirstOrDefaultAsync(u => u.UserId == userId);

			if (user == null || !user.DepartmentId.HasValue)
			{
				TempData["Error"] = "Bạn chưa được phân công vào phòng ban nào";
				return RedirectToAction("Dashboard");
			}

			var department = await _context.Departments
				.Include(d => d.Users)
					.ThenInclude(u => u.Role)
				.FirstOrDefaultAsync(d => d.DepartmentId == user.DepartmentId);

			ViewBag.MyDepartment = department;
			ViewBag.CurrentUser = user;

			return View();
		}

		// ============================================
		// MY TASKS
		// ============================================

		[HttpGet]
		public async Task<IActionResult> MyTasks()
		{
			if (!IsAuthenticated())
				return RedirectToAction("Login", "Account");

			// ✅ ĐÚNG - Lấy int
			var userIdNullable = HttpContext.Session.GetInt32("UserId");
			if (!userIdNullable.HasValue)
				return RedirectToAction("Login", "Account");

			var userId = userIdNullable.Value;

			// ✅ So sánh int == int (EF sinh SQL đúng)
			var myTasks = await _context.UserTasks
				.Include(ut => ut.Task)  // ✅ CHỈ cần Task
				.Where(ut => ut.UserId == userId)
				.OrderBy(ut => ut.Status == "TODO" ? 1 : ut.Status == "InProgress" ? 2 : 3)
				.ThenByDescending(ut => ut.Task.Priority == "High" ? 1 : ut.Task.Priority == "Medium" ? 2 : 3)
				.ToListAsync();

			return View(myTasks);
		}



		[HttpGet]
		public async Task<IActionResult> GetTesters()
		{
			if (!IsAuthenticated())
				return Json(new { success = false, message = "Phiên đăng nhập hết hạn" });

			try
			{
				var testers = await _context.Users
					.Include(u => u.Role)
					.Include(u => u.Department)
					.Where(u => u.IsActive == true && (u.Role.RoleName == "Tester" || u.IsTester))
					.OrderBy(u => u.Department.DepartmentName)
					.ThenBy(u => u.FullName)
					.Select(u => new
					{
						userId = u.UserId,
						fullName = u.FullName,
						email = u.Email,
						departmentName = u.Department != null ? u.Department.DepartmentName : "N/A",
						roleName = u.Role.RoleName
					})
					.ToListAsync();

				return Json(new { success = true, testers });
			}
			catch (Exception ex)
			{
				return Json(new { success = false, message = ex.Message });
			}
		}
		// ============================================
		// CHECK-IN / CHECK-OUT với UPLOAD ẢNH
		// ============================================

		[HttpGet]
		public async System.Threading.Tasks.Task<IActionResult> GetTodayAttendance()
		{
			if (!IsAuthenticated())
			{
				return Json(new { hasCheckedIn = false });
			}

			var userId = HttpContext.Session.GetInt32("UserId");
			var today = DateOnly.FromDateTime(DateTime.Now);

			var attendance = await _context.Attendances
				.FirstOrDefaultAsync(a => a.UserId == userId && a.WorkDate == today);

			if (attendance == null || !attendance.CheckInTime.HasValue)
			{
				return Json(new { hasCheckedIn = false });
			}

			return Json(new
			{
				hasCheckedIn = true,
				checkInTime = attendance.CheckInTime.Value.ToString("HH:mm:ss"),
				hasCheckedOut = attendance.CheckOutTime.HasValue,
				checkOutTime = attendance.CheckOutTime?.ToString("HH:mm:ss"),
				checkInPhotos = attendance.CheckInPhotos,
				checkOutPhotos = attendance.CheckOutPhotos,
				checkInLatitude = attendance.CheckInLatitude,
				checkInLongitude = attendance.CheckInLongitude,
				checkOutLatitude = attendance.CheckOutLatitude,
				checkOutLongitude = attendance.CheckOutLongitude,
				checkInAddress = attendance.CheckInAddress,
				checkOutAddress = attendance.CheckOutAddress
			});
		}

		// ============================================
		// IMPROVED CHECK-IN - Prevent Multiple Check-ins
		// ============================================
		[HttpPost]
		[RequestSizeLimit(10_485_760)]
		public async System.Threading.Tasks.Task<IActionResult> CheckIn([FromForm] CheckInRequest request)
		{
			if (!IsAuthenticated())
				return Json(new { success = false, message = "Phiên đăng nhập hết hạn" });

			var userId = HttpContext.Session.GetInt32("UserId").Value;
			var serverNow = DateTime.Now;
			var today = DateOnly.FromDateTime(serverNow);

			var existingAttendance = await _context.Attendances
				.FirstOrDefaultAsync(a => a.UserId == userId && a.WorkDate == today);

			if (existingAttendance != null && existingAttendance.CheckInTime.HasValue)
			{
				var checkInTimeStr = existingAttendance.CheckInTime.Value.ToString("HH:mm:ss");
				return Json(new
				{
					success = false,
					message = $"Bạn đã check-in hôm nay rồi!\nThời gian check-in: {checkInTimeStr}",
					alreadyCheckedIn = true,
					checkInTime = checkInTimeStr
				});
			}

			if (request.Photo == null || request.Photo.Length == 0)
				return Json(new { success = false, message = "Vui lòng chụp ảnh hoặc tải lên ảnh để check-in" });

			if (request.Photo.Length > 10 * 1024 * 1024)
				return Json(new { success = false, message = "Kích thước ảnh không được vượt quá 10MB" });

			var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
			var extension = Path.GetExtension(request.Photo.FileName).ToLower();
			if (!allowedExtensions.Contains(extension))
				return Json(new { success = false, message = "Chỉ chấp nhận file ảnh định dạng JPG, JPEG, PNG" });

			if (request.Latitude == 0 || request.Longitude == 0)
				return Json(new { success = false, message = "Không thể lấy vị trí GPS. Vui lòng bật GPS và thử lại" });

			try
			{
				// ✅ ĐỌC TỪ SystemSettings THAY VÌ SalaryConfigurations
				var configs = await _context.SystemSettings
					.Where(c => c.IsActive == true && c.IsEnabled == true)
					.ToDictionaryAsync(c => c.SettingKey, c => c.SettingValue);

				var standardStartTime = TimeOnly.Parse(configs.GetValueOrDefault("CHECK_IN_STANDARD_TIME", "08:00"));

				var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "attendance");
				if (!Directory.Exists(uploadsFolder))
					Directory.CreateDirectory(uploadsFolder);

				var uniqueFileName = $"{userId}_{serverNow:yyyyMMdd_HHmmss}_checkin{extension}";
				var filePath = Path.Combine(uploadsFolder, uniqueFileName);

				using (var fileStream = new FileStream(filePath, FileMode.Create))
					await request.Photo.CopyToAsync(fileStream);

				var photoPath = $"/uploads/attendance/{uniqueFileName}";
				var checkInTime = new TimeOnly(serverNow.Hour, serverNow.Minute, serverNow.Second);
				var isLate = checkInTime > standardStartTime;
				var address = await GetAddressFromCoordinates(request.Latitude, request.Longitude);

				var attendance = existingAttendance ?? new Attendance
				{
					UserId = userId,
					WorkDate = today,
					CreatedAt = serverNow
				};

				attendance.CheckInTime = serverNow;
				attendance.CheckInLatitude = request.Latitude;
				attendance.CheckInLongitude = request.Longitude;
				attendance.CheckInAddress = address;
				attendance.CheckInPhotos = photoPath;
				attendance.CheckInNotes = request.Notes;
				attendance.CheckInIpaddress = HttpContext.Connection.RemoteIpAddress?.ToString();
				attendance.IsLate = isLate;
				attendance.IsWithinGeofence = true;
				attendance.TotalHours = 0;

				if (existingAttendance == null)
					_context.Attendances.Add(attendance);

				await _context.SaveChangesAsync();

				// ✅ LẤY THÔNG TIN USER
				var user = await _context.Users.FindAsync(userId);

				// ✅ GỬI TELEGRAM NOTIFICATION
				try
				{
					await _telegramService.SendCheckInNotificationAsync(
						user?.FullName ?? "N/A",
						user?.Username ?? "N/A",
						serverNow,
						address,
						 
						isLate,
						request.Notes
					);
				}
				catch (Exception ex)
				{
					// Log error nhưng không block flow chính
					Console.WriteLine($"Telegram notification failed: {ex.Message}");
				}

				// GỬI THÔNG BÁO CHO ADMIN NẾU ĐI TRỄ
				if (isLate)
				{
					await _notificationService.SendToAdminsAsync(
						"Nhân viên đi trễ",
						$"{user?.FullName ?? "Nhân viên"} vừa check-in muộn lúc {serverNow:HH:mm:ss}",
						"warning",
						$"/Admin/AttendanceHistory?userId={userId}&fromDate={serverNow:yyyy-MM-dd}&toDate={serverNow:yyyy-MM-dd}"
					);
				}

				return Json(new
				{
					success = true,
					message = $"Check-in thành công!\nThời gian: {serverNow:HH:mm:ss}\nVị trí: {address}" +
							  (isLate ? $"\nGhi nhận: Đến sau {standardStartTime:HH:mm}" : "\nĐúng giờ!"),
					serverTime = serverNow.ToString("yyyy-MM-dd HH:mm:ss"),
					checkInTime = serverNow.ToString("HH:mm:ss"),
					address = address,
					isLate = isLate
				});
			}
			catch (Exception ex)
			{
				return Json(new { success = false, message = $"Có lỗi xảy ra: {ex.Message}" });
			}
		}


		// ============================================
		// IMPROVED CHECK-OUT - Calculate Exact Working Hours
		// ============================================
		// ============================================
		// CHECK-OUT - ĐÃ SỬA ĐỌC TỪ SystemSettings
		// ============================================
		[HttpPost]
		[RequestSizeLimit(5_242_880)]
		public async System.Threading.Tasks.Task<IActionResult> CheckOut([FromForm] CheckOutRequest request)
		{
			if (!IsAuthenticated())
				return Json(new { success = false, message = "Phiên đăng nhập hết hạn" });

			var userId = HttpContext.Session.GetInt32("UserId").Value;
			var serverNow = DateTime.Now;
			var today = DateOnly.FromDateTime(serverNow);

			var attendance = await _context.Attendances
				.FirstOrDefaultAsync(a => a.UserId == userId && a.WorkDate == today);

			if (attendance == null || !attendance.CheckInTime.HasValue)
				return Json(new { success = false, message = "Bạn chưa check-in hôm nay" });

			if (attendance.CheckOutTime.HasValue)
				return Json(new { success = false, message = "Bạn đã check-out hôm nay rồi! Chúc bạn một ngày vui vẻ! ", isCompleted = true });

			if (request.Photo == null || request.Photo.Length == 0)
				return Json(new { success = false, message = "Vui lòng chụp ảnh hoặc tải lên ảnh để check-out" });

			if (request.Photo.Length > 10 * 1024 * 1024)
				return Json(new { success = false, message = "Kích thước ảnh không được vượt quá 10MB" });

			var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
			var extension = Path.GetExtension(request.Photo.FileName).ToLower();
			if (!allowedExtensions.Contains(extension))
				return Json(new { success = false, message = "Chỉ chấp nhận file ảnh định dạng JPG, JPEG, PNG" });

			// ✅ Validate GPS
			if (request.Latitude == 0 || request.Longitude == 0)
			{
				return Json(new
				{
					success = false,
					message = " Không thể lấy vị trí GPS. Vui lòng đợi GPS ổn định và thử lại."
				});
			}

			if (Math.Abs(request.Latitude) > 90 || Math.Abs(request.Longitude) > 180)
			{
				return Json(new
				{
					success = false,
					message = " Tọa độ GPS không hợp lệ. Vui lòng thử lại."
				});
			}

			try
			{
				// ✅ ĐỌC TỪ SystemSettings THAY VÌ HARD-CODE
				var configs = await _context.SystemSettings
					.Where(c => c.IsActive == true && c.IsEnabled == true)
					.ToDictionaryAsync(c => c.SettingKey, c => c.SettingValue);

				var standardEndTime = TimeOnly.Parse(configs.GetValueOrDefault("CHECK_OUT_STANDARD_TIME", "17:00"));
				var standardStartTime = TimeOnly.Parse(configs.GetValueOrDefault("CHECK_IN_STANDARD_TIME", "08:00"));
				var standardHoursPerDay = decimal.Parse(configs.GetValueOrDefault("STANDARD_HOURS_PER_DAY", "8"));

				var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "attendance");
				if (!Directory.Exists(uploadsFolder))
					Directory.CreateDirectory(uploadsFolder);

				var uniqueFileName = $"{userId}_{serverNow:yyyyMMdd_HHmmss}_checkout{extension}";
				var filePath = Path.Combine(uploadsFolder, uniqueFileName);

				using (var fileStream = new FileStream(filePath, FileMode.Create))
					await request.Photo.CopyToAsync(fileStream);

				var photoPath = $"/uploads/attendance/{uniqueFileName}";
				var address = await GetAddressFromCoordinates(request.Latitude, request.Longitude);

				// ✅ CALCULATE EXACT WORKING HOURS
				var duration = serverNow - attendance.CheckInTime.Value;
				var totalHours = (decimal)duration.TotalHours;
				var hours = duration.Hours;
				var minutes = duration.Minutes;
				var seconds = duration.Seconds;

				// ✅ KIỂM TRA CHECKOUT SỚM (TRƯỚC GIỜ CHUẨN)
				var checkOutTime = new TimeOnly(serverNow.Hour, serverNow.Minute, serverNow.Second);
				bool isEarlyCheckout = checkOutTime < standardEndTime;

				// ✅ TÍNH GIỜ THIẾU NẾU CHECKOUT SỚM
				decimal penaltyHours = 0;
				if (isEarlyCheckout)
				{
					var missedTime = standardEndTime - checkOutTime;
					penaltyHours = (decimal)missedTime.TotalHours;
				}

				attendance.CheckOutTime = serverNow;
				attendance.CheckOutLatitude = request.Latitude;
				attendance.CheckOutLongitude = request.Longitude;
				attendance.CheckOutAddress = address;
				attendance.CheckOutPhotos = photoPath;
				attendance.CheckOutNotes = request.Notes;
				attendance.CheckOutIpaddress = HttpContext.Connection.RemoteIpAddress?.ToString();
				attendance.TotalHours = totalHours;
				attendance.ActualWorkHours = totalHours;

				// ✅ GHI CHÚ NẾU CHECKOUT SỚM
				if (isEarlyCheckout)
				{
					attendance.CheckOutNotes = $"{request.Notes ?? ""} [Checkout sớm {penaltyHours:F2}h - Thiếu {penaltyHours:F2}h so với chuẩn]".Trim();
				}

				await _context.SaveChangesAsync();

				var user = await _context.Users.FindAsync(userId);

				// ✅ GỬI TELEGRAM NOTIFICATION
				try
				{
					await _telegramService.SendCheckOutNotificationAsync(
						user?.FullName ?? "N/A",
						user?.Username ?? "N/A",
						serverNow,
						totalHours,
						0 ,// Overtime chưa được approve nên để 0
						  attendance.CheckOutNotes
					);
				}
				catch (Exception ex)
				{
					Console.WriteLine($"Telegram notification failed: {ex.Message}");
				}

				await _auditHelper.LogDetailedAsync(
					userId, "CHECK_OUT", "Attendance", attendance.AttendanceId,
					null, new
					{
						CheckOutTime = serverNow.ToString("HH:mm:ss"),
						TotalHours = $"{hours:D2}:{minutes:D2}:{seconds:D2}",
						Address = address,
						IsEarlyCheckout = isEarlyCheckout,
						PenaltyHours = penaltyHours
					},
					$"Check-out tại {address} - Tổng giờ: {hours:D2}:{minutes:D2}:{seconds:D2}",
					new Dictionary<string, object> {
				{ "CheckOutTime", serverNow.ToString("HH:mm:ss") },
				{ "TotalHours", $"{hours:D2}:{minutes:D2}:{seconds:D2}" },
				{ "StandardEndTime", standardEndTime.ToString("HH:mm") },
				{ "IsEarlyCheckout", isEarlyCheckout }
					}
				);

				// ✅ THÔNG BÁO CHO ADMIN NẾU CHECKOUT SỚM
				if (isEarlyCheckout)
				{
					await _notificationService.SendToAdminsAsync(
						"Nhân viên checkout sớm",
						$"{user?.FullName ?? "Nhân viên"} vừa checkout sớm lúc {serverNow:HH:mm:ss} (Chuẩn: {standardEndTime:HH:mm})",
						"warning",
						$"/Admin/AttendanceHistory?userId={userId}&fromDate={serverNow:yyyy-MM-dd}&toDate={serverNow:yyyy-MM-dd}"
					);
				}

				// ✅ TẠO MESSAGE ĐỘNG
				string message = $" Check-out thành công!\n Thời gian: {serverNow:HH:mm:ss}\n Tổng giờ làm: {hours:D2}:{minutes:D2}:{seconds:D2}\n📍 Vị trí: {address}";

				if (isEarlyCheckout)
				{
					message += $"\n\n Lưu ý: Bạn checkout sớm hơn {penaltyHours:F2}h so với giờ chuẩn ({standardEndTime:HH:mm})";
				}
				else
				{
					message += "\n\n Chúc bạn một buổi tối vui vẻ!";
				}

				return Json(new
				{
					success = true,
					message = message,
					totalHours = totalHours,
					totalHoursFormatted = $"{hours:D2}:{minutes:D2}:{seconds:D2}",
					serverTime = serverNow.ToString("yyyy-MM-dd HH:mm:ss"),
					checkOutTime = serverNow.ToString("HH:mm:ss"),
					address = address,
					isEarlyCheckout = isEarlyCheckout,
					penaltyHours = penaltyHours,
					standardEndTime = standardEndTime.ToString("HH:mm")
				});
			}
			catch (Exception ex)
			{
				await _auditHelper.LogFailedAttemptAsync(userId, "CHECK_OUT", "Attendance", $"Exception: {ex.Message}", new { Error = ex.ToString() });
				return Json(new { success = false, message = $"Có lỗi xảy ra: {ex.Message}" });
			}
		}


		[HttpGet]
		public async Task<IActionResult> TestTelegram()
		{
			if (!IsAuthenticated())
				return Json(new { success = false, message = "Phiên đăng nhập hết hạn" });

			try
			{
				await _telegramService.SendCheckInNotificationAsync(
					"Nguyễn Văn A (Test)",
					"testuser",
					DateTime.Now,
					"Văn phòng AIHUB - TP.HCM",
					false
				);

				return Json(new { success = true, message = "Telegram test sent! Check your group." });
			}
			catch (Exception ex)
			{
				return Json(new { success = false, message = ex.Message });
			}
		}
		[HttpGet]
		public async Task<IActionResult> GetMyTasks()
		{
			if (!IsAuthenticated())
				return Json(new { success = false, message = "Phiên đăng nhập hết hạn" });

			var userIdNullable = HttpContext.Session.GetInt32("UserId");
			if (!userIdNullable.HasValue)
				return Json(new { success = false, message = "Phiên đăng nhập hết hạn" });

			var userId = userIdNullable.Value;

			try
			{
				var myTasks = await _context.UserTasks
					.Include(ut => ut.Task)
					.Where(ut => ut.UserId == userId && ut.Task.IsActive == true)
					.OrderBy(ut => ut.Status == "TODO" ? 1 : ut.Status == "InProgress" ? 2 : 3)
					.ThenByDescending(ut => ut.Task.Priority == "High" ? 1 : ut.Task.Priority == "Medium" ? 2 : 3)
					.ToListAsync();

				var tasksData = myTasks.Select(ut =>
				{
					var task = ut.Task;
					bool isOverdue = false;

					if (task.Deadline.HasValue)
					{
						if (ut.Status == "Done")
						{
							if (ut.UpdatedAt.HasValue && ut.UpdatedAt.Value > task.Deadline.Value)
								isOverdue = true;
						}
						else
						{
							if (DateTime.Now > task.Deadline.Value)
								isOverdue = true;
						}
					}

					return new
					{
						taskId = task.TaskId,
						userTaskId = ut.UserTaskId,
						taskName = task.TaskName,
						description = task.Description ?? "",
						platform = task.Platform ?? "",
						reportLink = ut.ReportLink ?? "",
						deadline = task.Deadline,
						deadlineStr = task.Deadline.HasValue ? task.Deadline.Value.ToString("dd/MM/yyyy") : "Không có",
						priority = task.Priority ?? "Medium",
						status = ut.Status ?? "TODO",
						isOverdue = isOverdue
					};
				}).ToList();

				return Json(new
				{
					success = true,
					tasks = tasksData
				});
			}
			catch (Exception ex)
			{
				await _auditHelper.LogFailedAttemptAsync(
					userId,
					"VIEW",
					"UserTask",
					$"Exception: {ex.Message}",
					new { Error = ex.ToString() }
				);

				return Json(new { success = false, message = $"Có lỗi xảy ra: {ex.Message}" });
			}
		}

		[HttpPost]
		public async Task<IActionResult> UpdateMyTask([FromBody] UpdateMyTaskRequest request)
		{
			if (!IsAuthenticated())
				return Json(new { success = false, message = "Phiên đăng nhập hết hạn" });

			var userId = HttpContext.Session.GetInt32("UserId").Value;

			try
			{
				var userTask = await _context.UserTasks
					.Include(ut => ut.Task)
					.Include(ut => ut.User)
						.ThenInclude(u => u.Department)
					.FirstOrDefaultAsync(ut => ut.UserTaskId == request.UserTaskId && ut.UserId == userId);

				if (userTask == null)
					return Json(new { success = false, message = "Không tìm thấy công việc" });

				var oldStatus = userTask.Status ?? "TODO";

				// Không cho phép cập nhật task đã hoàn thành
				if (oldStatus == "Done")
					return Json(new { success = false, message = " Công việc đã hoàn thành, không thể cập nhật" });

				// Validate status transition
				var validTransitions = new Dictionary<string, List<string>>
		{
			{ "TODO", new List<string> { "InProgress" } },
			{ "InProgress", new List<string> { "Testing", "Done", "TODO" } },
			{ "Reopen", new List<string> { "InProgress" } },
			{ "Testing", new List<string> { } },
			{ "Done", new List<string> { } }
		};

				if (!validTransitions.ContainsKey(oldStatus) || !validTransitions[oldStatus].Contains(request.Status))
				{
					var allowedStatuses = validTransitions.ContainsKey(oldStatus) && validTransitions[oldStatus].Count > 0
						? string.Join(", ", validTransitions[oldStatus])
						: "không có trạng thái nào";

					return Json(new
					{
						success = false,
						message = $" Không thể chuyển từ '{GetStatusText(oldStatus)}' sang '{GetStatusText(request.Status)}'\n\nCác trạng thái hợp lệ: {allowedStatuses}"
					});
				}

				// Cập nhật
				userTask.Status = request.Status;
				userTask.ReportLink = request.ReportLink ?? userTask.ReportLink;
				userTask.UpdatedAt = DateTime.Now;

				await _context.SaveChangesAsync();

				// Log audit
				await _auditHelper.LogDetailedAsync(
					userId,
					"UPDATE",
					"UserTask",
					userTask.UserTaskId,
					new { Status = oldStatus, ReportLink = userTask.ReportLink },
					new { Status = request.Status, ReportLink = request.ReportLink },
					$"Cập nhật tiến độ task: {userTask.Task.TaskName}",
					new Dictionary<string, object>
					{
				{ "OldStatus", oldStatus },
				{ "NewStatus", request.Status }
					}
				);

				// Gửi thông báo
				if (request.Status == "Done")
				{
					await _notificationService.SendToAdminsAsync(
						"Task hoàn thành",
						$"{userTask.User.FullName} vừa hoàn thành task '{userTask.Task.TaskName}'",
						"success",
						"/Admin/TaskList"
					);
				}

				return Json(new
				{
					success = true,
					message = $" Cập nhật thành công!\nTrạng thái: {GetStatusText(request.Status)}"
				});
			}
			catch (Exception ex)
			{
				await _auditHelper.LogFailedAttemptAsync(
					userId,
					"UPDATE",
					"UserTask",
					$"Exception: {ex.Message}",
					new { UserTaskId = request.UserTaskId, Error = ex.ToString() }
				);

				return Json(new { success = false, message = $"Có lỗi xảy ra: {ex.Message}" });
			}
		}

		// Model class cho request
		public class UpdateMyTaskRequest
		{
			public int UserTaskId { get; set; }
			public string Status { get; set; } = "TODO";
			public string? ReportLink { get; set; }
		}



		// ============================================
		// TASKS SUMMARY FOR DASHBOARD
		// ============================================
		[HttpGet]
		public async Task<IActionResult> GetMyTasksSummary()
		{
			if (!IsAuthenticated())
				return Json(new { success = false, message = "Phiên đăng nhập hết hạn" });

			var userIdNullable = HttpContext.Session.GetInt32("UserId");
			if (!userIdNullable.HasValue)
				return Json(new { success = false, message = "Phiên đăng nhập hết hạn" });

			var userId = userIdNullable.Value;

			try
			{
				var myTasks = await _context.UserTasks
					.Include(ut => ut.Task)
					.Where(ut => ut.UserId == userId && ut.Task.IsActive == true)
					.ToListAsync();

				var tasksSummary = myTasks
					.OrderByDescending(ut => ut.Task.Priority == "High" ? 1 : ut.Task.Priority == "Medium" ? 2 : 3)
					.ThenBy(ut => ut.Task.Deadline)
					.Select(ut =>
					{
						var task = ut.Task;
						string status = ut.Status ?? "TODO";

						// ✅ LOGIC KIỂM TRA QUÁ HẠN
						bool isOverdue = false;
						bool isCompletedLate = false;

						if (task.Deadline.HasValue)
						{
							if (status == "Done")
							{
								if (ut.UpdatedAt.HasValue && ut.UpdatedAt.Value > task.Deadline.Value)
								{
									isCompletedLate = true;
									isOverdue = true;
								}
							}
							else
							{
								if (DateTime.Now > task.Deadline.Value)
								{
									isOverdue = true;
								}
							}
						}

						return new
						{
							taskId = task.TaskId,
							taskName = task.TaskName,
							description = task.Description ?? "",
							platform = task.Platform ?? "",
							reportLink = ut.ReportLink ?? "",
							deadline = task.Deadline.HasValue ? task.Deadline.Value.ToString("dd/MM/yyyy") : "",
							priority = task.Priority ?? "Medium",
							status = status,
							isOverdue = isOverdue,
							isCompletedLate = isCompletedLate,
							updatedAt = ut.UpdatedAt.HasValue ? ut.UpdatedAt.Value.ToString("yyyy-MM-dd HH:mm:ss") : ""
						};
					})
					.ToList();

				return Json(new
				{
					success = true,
					tasks = tasksSummary,
					totalTasks = tasksSummary.Count,
					completedTasks = tasksSummary.Count(t => t.status == "Done"),
					inProgressTasks = tasksSummary.Count(t => t.status == "InProgress"),
					overdueTasks = tasksSummary.Count(t => t.isOverdue)
				});
			}
			catch (Exception ex)
			{
				await _auditHelper.LogFailedAttemptAsync(
					userId,
					"VIEW",
					"UserTask",
					$"Exception: {ex.Message}",
					new { Error = ex.ToString() }
				);

				return Json(new { success = false, message = $"Có lỗi xảy ra: {ex.Message}" });
			}
		}

		[HttpGet]
		public async Task<IActionResult> GetTaskDetail(int taskId)
		{
			if (!IsAuthenticated())
				return Json(new { success = false, message = "Phiên đăng nhập hết hạn" });

			var userId = HttpContext.Session.GetInt32("UserId");

			try
			{
				var userTask = await _context.UserTasks
					.Include(ut => ut.Task)
					.Include(ut => ut.Tester)
					.FirstOrDefaultAsync(ut => ut.Task.TaskId == taskId && ut.UserId == userId);

				if (userTask == null)
					return Json(new { success = false, message = "Không tìm thấy công việc" });

				var task = userTask.Task;

				bool isOverdue = false;
				if (task.Deadline.HasValue)
				{
					if (userTask.Status == "Done")
					{
						if (userTask.UpdatedAt.HasValue && userTask.UpdatedAt.Value > task.Deadline.Value)
							isOverdue = true;
					}
					else if (DateTime.Now > task.Deadline.Value)
					{
						isOverdue = true;
					}
				}

				return Json(new
				{
					success = true,
					task = new
					{
						taskId = task.TaskId,
						userTaskId = userTask.UserTaskId,
						taskName = task.TaskName,
						description = task.Description ?? "Không có mô tả",
						platform = task.Platform ?? "N/A",
						reportLink = userTask.ReportLink ?? "",
						deadlineStr = task.Deadline.HasValue ? task.Deadline.Value.ToString("dd/MM/yyyy HH:mm") : "Không có deadline",
						priority = task.Priority ?? "Medium",
						status = userTask.Status ?? "TODO",
						isOverdue = isOverdue,
						createdAt = userTask.CreatedAt.HasValue ? userTask.CreatedAt.Value.ToString("dd/MM/yyyy HH:mm") : "",
						updatedAt = userTask.UpdatedAt.HasValue ? userTask.UpdatedAt.Value.ToString("dd/MM/yyyy HH:mm") : "Chưa cập nhật",
						reopenReason = userTask.ReopenReason ?? "",
						testerName = userTask.Tester != null ? userTask.Tester.FullName : null,
						testerId = userTask.TesterId
					}
				});
			}
			catch (Exception ex)
			{
				return Json(new { success = false, message = $"Có lỗi xảy ra: {ex.Message}" });
			}
		}


		// ============================================
		// GET ADDRESS FROM COORDINATES API
		// ============================================
		[HttpGet]
		public async System.Threading.Tasks.Task<IActionResult> GetAddressFromCoordinatesApi(decimal latitude, decimal longitude)
		{
			if (!IsAuthenticated())
			{
				return Json(new { success = false, message = "Phiên đăng nhập hết hạn" });
			}

			try
			{
				var address = await GetAddressFromCoordinates(latitude, longitude);
				return Json(new
				{
					success = true,
					address = address
				});
			}
			catch (Exception ex)
			{
				return Json(new
				{
					success = false,
					address = $"Lat: {latitude:F6}, Long: {longitude:F6}",
					error = ex.Message
				});
			}
		}

		// ============================================
		// ✅ IMPROVED OVERTIME REQUEST - CHỈ CHO PHÉP XIN TRONG 3 NGÀY GẦN NHẤT
		// ============================================
		[HttpPost]
		public async System.Threading.Tasks.Task<IActionResult> CreateOvertimeRequest([FromBody] JsonElement payload)
		{
			if (!IsAuthenticated())
				return Json(new { success = false, message = "Phiên đăng nhập hết hạn" });

			var userId = HttpContext.Session.GetInt32("UserId").Value;

			string workDateStr = payload.TryGetProperty("WorkDate", out var wdProp) ? wdProp.GetString() ?? "" : "";
			string reason = payload.TryGetProperty("Reason", out var rProp) ? rProp.GetString() ?? "" : "";
			string taskDesc = payload.TryGetProperty("TaskDescription", out var tdProp) ? tdProp.GetString() ?? "" : "";

			if (string.IsNullOrWhiteSpace(workDateStr) || string.IsNullOrWhiteSpace(reason))
				return Json(new { success = false, message = "Vui lòng chọn ngày và nhập lý do" });

			try
			{
				if (!DateOnly.TryParse(workDateStr, out var workDateDo))
				{
					if (!DateTime.TryParse(workDateStr, out var workDt))
						return Json(new { success = false, message = "Ngày tăng ca không hợp lệ" });
					workDateDo = DateOnly.FromDateTime(workDt);
				}

				var today = DateOnly.FromDateTime(DateTime.Now);
				var threeDaysAgo = today.AddDays(-3);

				if (workDateDo < threeDaysAgo)
				{
					return Json(new
					{
						success = false,
						message = $"Chỉ được xin tăng ca trong vòng 3 ngày gần nhất\nTừ {threeDaysAgo:dd/MM/yyyy} đến {today:dd/MM/yyyy}"
					});
				}

				if (workDateDo > today)
					return Json(new { success = false, message = "Không thể xin tăng ca cho ngày trong tương lai" });

				var attendance = await _context.Attendances
					.FirstOrDefaultAsync(a => a.UserId == userId && a.WorkDate == workDateDo);

				if (attendance == null || !attendance.CheckInTime.HasValue)
					return Json(new { success = false, message = "Không tìm thấy bản ghi chấm công cho ngày này" });

				if (attendance.HasOvertimeRequest == true)
				{
					var existingOT = await _context.OvertimeRequests
						.FirstOrDefaultAsync(ot => ot.UserId == userId && ot.WorkDate == workDateDo);

					if (existingOT != null)
					{
						return Json(new
						{
							success = false,
							message = $"Đã có yêu cầu tăng ca cho ngày này\nTrạng thái: {existingOT.Status}"
						});
					}
				}

				// ✅ ĐỌC TỪ SystemSettings THAY VÌ SalaryConfigurations
				var configs = await _context.SystemSettings
					.Where(c => c.IsActive == true && c.IsEnabled == true)
					.ToDictionaryAsync(c => c.SettingKey, c => c.SettingValue);

				var standardEndTime = TimeOnly.Parse(configs.GetValueOrDefault("CHECK_OUT_STANDARD_TIME", "17:00"));
				var standardHoursPerDay = decimal.Parse(configs.GetValueOrDefault("STANDARD_HOURS_PER_DAY", "8"));

				DateTime effectiveCheckOutTime;
				decimal overtimeHours = 0;

				if (attendance.CheckOutTime.HasValue)
				{
					effectiveCheckOutTime = attendance.CheckOutTime.Value;
					var checkOutTime = TimeOnly.FromDateTime(effectiveCheckOutTime);

					if (checkOutTime <= standardEndTime)
					{
						return Json(new
						{
							success = false,
							message = $"Không có giờ tăng ca\nCheckout: {checkOutTime:HH:mm}\nGiờ chuẩn: {standardEndTime:HH:mm}"
						});
					}

					var overtimeSpan = checkOutTime - standardEndTime;
					overtimeHours = (decimal)overtimeSpan.TotalHours;
				}
				else
				{
					var serverNow = DateTime.Now;
					var currentTime = new TimeOnly(serverNow.Hour, serverNow.Minute, serverNow.Second);

					if (workDateDo == today && currentTime < standardEndTime)
					{
						return Json(new
						{
							success = false,
							message = $"Chưa đến giờ checkout chuẩn ({standardEndTime:HH:mm})"
						});
					}

					if (workDateDo == today)
					{
						if (currentTime > standardEndTime)
						{
							var overtimeSpan = currentTime - standardEndTime;
							overtimeHours = (decimal)overtimeSpan.TotalHours;
							effectiveCheckOutTime = serverNow;
						}
						else
						{
							return Json(new { success = false, message = "Chưa có giờ tăng ca" });
						}
					}
					else
					{
						return Json(new
						{
							success = false,
							message = $"Ngày {workDateDo:dd/MM/yyyy} chưa checkout\nKhông thể xác định giờ tăng ca"
						});
					}

					if (attendance.TotalHours == null || attendance.TotalHours == 0)
					{
						attendance.TotalHours = standardHoursPerDay;
						attendance.ActualWorkHours = standardHoursPerDay;
						await _context.SaveChangesAsync();
					}
				}

				var ot = new OvertimeRequest
				{
					UserId = userId,
					WorkDate = workDateDo,
					ActualCheckOutTime = effectiveCheckOutTime,
					OvertimeHours = overtimeHours,
					Reason = reason,
					TaskDescription = taskDesc,
					Status = "Pending",
					ExpiryDate = DateTime.Now.AddDays(7),
					CreatedAt = DateTime.Now
				};

				_context.OvertimeRequests.Add(ot);
				await _context.SaveChangesAsync();

				attendance.HasOvertimeRequest = true;
				attendance.OvertimeRequestId = ot.OvertimeRequestId;
				await _context.SaveChangesAsync();

				await _auditHelper.LogDetailedAsync(
					userId, "CREATE", "OvertimeRequest", ot.OvertimeRequestId, null, ot,
					$"Gửi yêu cầu tăng ca {workDateDo:dd/MM/yyyy} - {overtimeHours:F2}h",
					new Dictionary<string, object> {
				{ "OvertimeHours", $"{overtimeHours:F2}h" },
				{ "HasCheckout", attendance.CheckOutTime.HasValue }
					}
				);

				// GỬI THÔNG BÁO CHO ADMIN
				await _notificationService.SendToAdminsAsync(
					"Yêu cầu tăng ca mới",
					$"Nhân viên vừa gửi yêu cầu tăng ca {overtimeHours:F2}h cho ngày {workDateDo:dd/MM/yyyy}",
					"info",
					"/Admin/PendingRequests?type=Overtime&status=Pending"
				);

				return Json(new
				{
					success = true,
					message = $"Gửi yêu cầu tăng ca thành công!\n\nNgày: {workDateDo:dd/MM/yyyy}\nGiờ tăng ca: {overtimeHours:F2}h\nTrạng thái: Chờ duyệt"
				});
			}
			catch (Exception ex)
			{
				return Json(new { success = false, message = $"Lỗi: {ex.Message}" });
			}
		}

		public async System.Threading.Tasks.Task AutoFillMissingCheckouts()
		{
			try
			{
				var yesterday = DateOnly.FromDateTime(DateTime.Now.AddDays(-1));

				// Lấy tất cả attendance có check-in nhưng chưa checkout
				var missingCheckouts = await _context.Attendances
					.Where(a => a.WorkDate < yesterday
						&& a.CheckInTime.HasValue
						&& !a.CheckOutTime.HasValue
						&& (a.TotalHours == null || a.TotalHours == 0))
					.ToListAsync();

				if (!missingCheckouts.Any())
					return;

				// ✅ ĐỌC TỪ SystemSettings THAY VÌ SalaryConfigurations
				var configs = await _context.SystemSettings
					.Where(c => c.IsActive == true && c.IsEnabled == true)
					.ToDictionaryAsync(c => c.SettingKey, c => c.SettingValue);

				var standardHoursPerDay = decimal.Parse(configs.GetValueOrDefault("STANDARD_HOURS_PER_DAY", "8"));

				foreach (var att in missingCheckouts)
				{
					// ✅ GHI NHẬN ĐỦ 8 GIỜ CHUẨN
					att.TotalHours = standardHoursPerDay;
					att.ActualWorkHours = standardHoursPerDay;
					att.UpdatedAt = DateTime.Now;
				}

				await _context.SaveChangesAsync();

				await _auditHelper.LogAsync(
					null,
					"SYSTEM_AUTO",
					"Attendance",
					null,
					null,
					new { Count = missingCheckouts.Count, StandardHours = standardHoursPerDay },
					$"Tự động ghi nhận {standardHoursPerDay}h cho {missingCheckouts.Count} bản ghi chưa checkout"
				);
			}
			catch (Exception ex)
			{
				// Log error
				Console.WriteLine($"AutoFillMissingCheckouts Error: {ex.Message}");
			}
		}
		// ============================================
		// LEAVE REQUEST
		// ============================================
		[HttpPost]
		public async System.Threading.Tasks.Task<IActionResult> CreateLeaveRequest([FromBody] CreateLeaveRequestViewModel model)
		{
			if (!IsAuthenticated())
				return Json(new { success = false, message = "Phiên đăng nhập hết hạn" });

			var userId = HttpContext.Session.GetInt32("UserId").Value;

			if (string.IsNullOrEmpty(model.LeaveType) || model.StartDate == default || model.EndDate == default || string.IsNullOrEmpty(model.Reason))
				return Json(new { success = false, message = "Vui lòng điền đầy đủ thông tin" });

			if (model.EndDate < model.StartDate)
				return Json(new { success = false, message = "Ngày kết thúc phải sau ngày bắt đầu" });

			try
			{
				var totalDays = (model.EndDate - model.StartDate).Days + 1;

				var leave = new LeaveRequest
				{
					UserId = userId,
					LeaveType = model.LeaveType,
					StartDate = DateOnly.FromDateTime(model.StartDate),
					EndDate = DateOnly.FromDateTime(model.EndDate),
					TotalDays = totalDays,
					Reason = model.Reason,
					ProofDocument = model.ProofDocument,
					Status = "Pending",
					CreatedAt = DateTime.Now
				};

				_context.LeaveRequests.Add(leave);
				await _context.SaveChangesAsync();

				await _auditHelper.LogDetailedAsync(
					userId, "CREATE", "LeaveRequest", leave.LeaveRequestId, null, leave,
					$"Gửi yêu cầu nghỉ phép từ {leave.StartDate:dd/MM/yyyy} đến {leave.EndDate:dd/MM/yyyy}",
					new Dictionary<string, object> { { "TotalDays", totalDays }, { "LeaveType", model.LeaveType } }
				);

				// GỬI THÔNG BÁO CHO ADMIN
				// ✅ ĐÚNG
				await _notificationService.SendToAdminsAsync(
					"Yêu cầu nghỉ phép mới",
					$"Nhân viên vừa gửi yêu cầu nghỉ phép {totalDays} ngày ({model.LeaveType})",
					"info",
					"/Admin/PendingRequests?type=Leave&status=Pending"
				);

				return Json(new { success = true, message = $"Gửi yêu cầu nghỉ phép thành công!\nSố ngày: {totalDays}" });
			}
			catch (Exception ex)
			{
				return Json(new { success = false, message = $"Có lỗi xảy ra: {ex.Message}" });
			}
		}

		// ============================================
		// LATE REQUEST
		// ============================================
		[HttpPost]
		public async System.Threading.Tasks.Task<IActionResult> CreateLateRequest([FromBody] CreateLateRequestViewModel model)
		{
			if (!IsAuthenticated())
				return Json(new { success = false, message = "Phiên đăng nhập hết hạn" });

			var userId = HttpContext.Session.GetInt32("UserId").Value;

			if (!ModelState.IsValid)
			{
				var errors = string.Join(", ", ModelState.Values
					.SelectMany(v => v.Errors)
					.Select(e => e.ErrorMessage));
				return Json(new { success = false, message = errors });
			}

			try
			{
				var late = new LateRequest
				{
					UserId = userId,
					RequestDate = DateOnly.FromDateTime(model.RequestDate),
					ExpectedArrivalTime = TimeOnly.FromTimeSpan(model.ExpectedArrivalTime),
					Reason = model.Reason,
					ProofDocument = model.ProofDocument,
					Status = "Pending",
					CreatedAt = DateTime.Now
				};

				_context.LateRequests.Add(late);
				await _context.SaveChangesAsync();

				var attendance = await _context.Attendances
					.FirstOrDefaultAsync(a => a.UserId == userId && a.WorkDate == DateOnly.FromDateTime(model.RequestDate));

				if (attendance != null)
				{
					attendance.HasLateRequest = true;
					attendance.LateRequestId = late.LateRequestId;
					await _context.SaveChangesAsync();
				}

				await _auditHelper.LogDetailedAsync(
					userId, "CREATE", "LateRequest", late.LateRequestId,
					null, late,
					$"Gửi yêu cầu đi trễ ngày {late.RequestDate:dd/MM/yyyy}"
				);

				// GỬI THÔNG BÁO CHO ADMIN
				// ✅ ĐÚNG
				await _notificationService.SendToAdminsAsync(
					"Yêu cầu đi trễ mới",
					$"Nhân viên vừa gửi yêu cầu đi trễ cho ngày {late.RequestDate:dd/MM/yyyy}",
					"info",
					"/Admin/PendingRequests?type=Late&status=Pending"
				);

				return Json(new { success = true, message = "Gửi yêu cầu đi trễ thành công!" });
			}
			catch (Exception ex)
			{
				return Json(new { success = false, message = $"Có lỗi xảy ra: {ex.Message}" });
			}
		}

		// ============================================
		// GET MY REQUESTS
		// ============================================
		[HttpGet]
		public async System.Threading.Tasks.Task<IActionResult> GetMyRequests(string? type, string? status, DateTime? from, DateTime? to, string? keyword)
		{
			if (!IsAuthenticated())
				return Json(new { success = false, message = "Phiên đăng nhập hết hạn" });

			var userId = HttpContext.Session.GetInt32("UserId").Value;

			try
			{
				var otQuery = _context.OvertimeRequests.Where(r => r.UserId == userId).AsQueryable();
				var leaveQuery = _context.LeaveRequests.Where(r => r.UserId == userId).AsQueryable();
				var lateQuery = _context.LateRequests.Where(r => r.UserId == userId).AsQueryable();

				if (!string.IsNullOrEmpty(status))
				{
					otQuery = otQuery.Where(r => r.Status == status);
					leaveQuery = leaveQuery.Where(r => r.Status == status);
					lateQuery = lateQuery.Where(r => r.Status == status);
				}

				if (from.HasValue)
				{
					var fromDate = DateOnly.FromDateTime(from.Value.Date);
					otQuery = otQuery.Where(r => r.WorkDate >= fromDate);
					leaveQuery = leaveQuery.Where(r => r.StartDate >= fromDate || r.EndDate >= fromDate);
					lateQuery = lateQuery.Where(r => r.RequestDate >= fromDate);
				}
				if (to.HasValue)
				{
					var toDate = DateOnly.FromDateTime(to.Value.Date);
					otQuery = otQuery.Where(r => r.WorkDate <= toDate);
					leaveQuery = leaveQuery.Where(r => r.StartDate <= toDate || r.EndDate <= toDate);
					lateQuery = lateQuery.Where(r => r.RequestDate <= toDate);
				}

				if (!string.IsNullOrWhiteSpace(keyword))
				{
					var kw = keyword.Trim().ToLower();
					otQuery = otQuery.Where(r => (r.Reason ?? "").ToLower().Contains(kw) || (r.TaskDescription ?? "").ToLower().Contains(kw));
					leaveQuery = leaveQuery.Where(r => (r.Reason ?? "").ToLower().Contains(kw) || (r.ProofDocument ?? "").ToLower().Contains(kw));
					lateQuery = lateQuery.Where(r => (r.Reason ?? "").ToLower().Contains(kw) || (r.ProofDocument ?? "").ToLower().Contains(kw));
				}

				List<object> ot = new List<object>();
				List<object> leave = new List<object>();
				List<object> late = new List<object>();

				if (string.IsNullOrEmpty(type) || type == "Overtime")
				{
					ot = await otQuery
						.OrderByDescending(r => r.CreatedAt)
						.Select(r => new
						{
							r.OvertimeRequestId,
							r.UserId,
							workDate = r.WorkDate.ToString("yyyy-MM-dd"),
							actualCheckOutTime = r.ActualCheckOutTime == default(DateTime) ? "" : r.ActualCheckOutTime.ToString(),
							overtimeHours = r.OvertimeHours,
							reason = r.Reason ?? "",
							taskDescription = r.TaskDescription ?? "",
							status = r.Status ?? "",
							createdAt = r.CreatedAt.HasValue ? r.CreatedAt.Value.ToString("yyyy-MM-dd HH:mm:ss") : ""
						})
						.ToListAsync<object>();
				}

				if (string.IsNullOrEmpty(type) || type == "Leave")
				{
					leave = await leaveQuery
						.OrderByDescending(r => r.CreatedAt)
						.Select(r => new
						{
							r.LeaveRequestId,
							r.UserId,
							leaveType = r.LeaveType ?? "",
							startDate = r.StartDate.ToString("yyyy-MM-dd"),
							endDate = r.EndDate.ToString("yyyy-MM-dd"),
							totalDays = r.TotalDays,
							reason = r.Reason ?? "",
							proofDocument = r.ProofDocument ?? "",
							status = r.Status ?? "",
							createdAt = r.CreatedAt.HasValue ? r.CreatedAt.Value.ToString("yyyy-MM-dd HH:mm:ss") : ""
						})
						.ToListAsync<object>();
				}

				if (string.IsNullOrEmpty(type) || type == "Late")
				{
					late = await lateQuery
						.OrderByDescending(r => r.CreatedAt)
						.Select(r => new
						{
							r.LateRequestId,
							r.UserId,
							requestDate = r.RequestDate.ToString("yyyy-MM-dd"),
							expectedArrivalTime = r.ExpectedArrivalTime.ToString("HH:mm"),
							reason = r.Reason ?? "",
							proofDocument = r.ProofDocument ?? "",
							status = r.Status ?? "",
							createdAt = r.CreatedAt.HasValue ? r.CreatedAt.Value.ToString("yyyy-MM-dd HH:mm:ss") : ""
						})
						.ToListAsync<object>();
				}

				return Json(new { success = true, overtime = ot, leave = leave, late = late });
			}
			catch (Exception ex)
			{
				await _auditHelper.LogFailedAttemptAsync(userId, "VIEW", "Request", $"Exception: {ex.Message}", new { Error = ex.ToString() });
				return Json(new { success = false, message = $"Có lỗi: {ex.Message}" });
			}
		}

		// ============================================
		// GET REQUEST DETAIL
		// ============================================
		[HttpGet]
		public async System.Threading.Tasks.Task<IActionResult> GetRequestDetail(string type, int id)
		{
			if (!IsAuthenticated())
				return Json(new { success = false, message = "Phiên đăng nhập hết hạn" });

			var userId = HttpContext.Session.GetInt32("UserId").Value;

			try
			{
				if (type == "Overtime")
				{
					var r = await _context.OvertimeRequests.Include(x => x.User)
						.FirstOrDefaultAsync(x => x.OvertimeRequestId == id && x.UserId == userId);

					if (r == null)
						return Json(new { success = false, message = "Không tìm thấy request" });

					string checkOutTimeStr = "N/A";
					if (r.ActualCheckOutTime != default(DateTime))
					{
						checkOutTimeStr = r.ActualCheckOutTime.ToString("HH:mm:ss");
					}

					return Json(new
					{
						success = true,
						request = new
						{
							overtimeRequestId = r.OvertimeRequestId,
							userId = r.UserId,
							workDate = r.WorkDate.ToString("dd/MM/yyyy"),
							actualCheckOutTime = checkOutTimeStr,
							overtimeHours = $"{r.OvertimeHours:F2}h",
							reason = r.Reason ?? "",
							taskDescription = r.TaskDescription ?? "",
							status = r.Status ?? "",
							reviewedBy = r.ReviewedBy,
							reviewedAt = r.ReviewedAt.HasValue ? r.ReviewedAt.Value.ToString("dd/MM/yyyy HH:mm:ss") : "",
							reviewNote = r.ReviewNote ?? "",
							createdAt = r.CreatedAt.HasValue ? r.CreatedAt.Value.ToString("dd/MM/yyyy HH:mm:ss") : "",
							updatedAt = r.UpdatedAt.HasValue ? r.UpdatedAt.Value.ToString("dd/MM/yyyy HH:mm:ss") : "",
							userName = r.User?.FullName ?? "",
							userEmail = r.User?.Email ?? ""
						}
					});
				}

				if (type == "Leave")
				{
					var r = await _context.LeaveRequests.Include(x => x.User)
						.FirstOrDefaultAsync(x => x.LeaveRequestId == id && x.UserId == userId);

					if (r == null)
						return Json(new { success = false, message = "Không tìm thấy request" });

					return Json(new
					{
						success = true,
						request = new
						{
							leaveRequestId = r.LeaveRequestId,
							userId = r.UserId,
							leaveType = r.LeaveType ?? "",
							startDate = r.StartDate.ToString("dd/MM/yyyy"),
							endDate = r.EndDate.ToString("dd/MM/yyyy"),
							totalDays = $"{r.TotalDays} ngày",
							reason = r.Reason ?? "",
							proofDocument = r.ProofDocument ?? "",
							status = r.Status ?? "",
							reviewedBy = r.ReviewedBy,
							reviewedAt = r.ReviewedAt.HasValue ? r.ReviewedAt.Value.ToString("dd/MM/yyyy HH:mm:ss") : "",
							reviewNote = r.ReviewNote ?? "",
							createdAt = r.CreatedAt.HasValue ? r.CreatedAt.Value.ToString("dd/MM/yyyy HH:mm:ss") : "",
							updatedAt = r.UpdatedAt.HasValue ? r.UpdatedAt.Value.ToString("dd/MM/yyyy HH:mm:ss") : "",
							userName = r.User?.FullName ?? "",
							userEmail = r.User?.Email ?? ""
						}
					});
				}

				if (type == "Late")
				{
					var r = await _context.LateRequests.Include(x => x.User)
						.FirstOrDefaultAsync(x => x.LateRequestId == id && x.UserId == userId);

					if (r == null)
						return Json(new { success = false, message = "Không tìm thấy request" });

					return Json(new
					{
						success = true,
						request = new
						{
							lateRequestId = r.LateRequestId,
							userId = r.UserId,
							requestDate = r.RequestDate.ToString("dd/MM/yyyy"),
							expectedArrivalTime = r.ExpectedArrivalTime.ToString("HH:mm:ss"),
							reason = r.Reason ?? "",
							proofDocument = r.ProofDocument ?? "",
							status = r.Status ?? "",
							reviewedBy = r.ReviewedBy,
							reviewedAt = r.ReviewedAt.HasValue ? r.ReviewedAt.Value.ToString("dd/MM/yyyy HH:mm:ss") : "",
							reviewNote = r.ReviewNote ?? "",
							createdAt = r.CreatedAt.HasValue ? r.CreatedAt.Value.ToString("yyyy-MM-dd HH:mm:ss") : "",
							updatedAt = r.UpdatedAt.HasValue ? r.UpdatedAt.Value.ToString("dd/MM/yyyy HH:mm:ss") : "",
							userName = r.User?.FullName ?? "",
							userEmail = r.User?.Email ?? ""
						}
					});
				}

				return Json(new { success = false, message = "Loại request không hợp lệ" });
			}
			catch (Exception ex)
			{
				await _auditHelper.LogFailedAttemptAsync(userId, "VIEW", "Request", $"Exception: {ex.Message}", new { Error = ex.ToString() });
				return Json(new { success = false, message = $"Có lỗi: {ex.Message}" });
			}
		}

		// ============================================
		// CANCEL REQUEST
		// ============================================
		[HttpPost]
		public async System.Threading.Tasks.Task<IActionResult> CancelRequest([FromBody] ReviewRequestViewModel model)
		{
			if (!IsAuthenticated())
				return Json(new { success = false, message = "Phiên đăng nhập hết hạn" });

			var userId = HttpContext.Session.GetInt32("UserId").Value;

			try
			{
				if (model.RequestType == "Overtime")
				{
					var r = await _context.OvertimeRequests.FirstOrDefaultAsync(x => x.OvertimeRequestId == model.RequestId && x.UserId == userId);
					if (r == null) return Json(new { success = false, message = "Không tìm thấy request" });
					if (r.Status != "Pending") return Json(new { success = false, message = "Chỉ có thể hủy request đang ở trạng thái Pending" });

					r.Status = "Cancelled";
					r.UpdatedAt = DateTime.Now;
					await _context.SaveChangesAsync();

					await _auditHelper.LogAsync(userId, "UPDATE", "OvertimeRequest", r.OvertimeRequestId, null, new { Status = "Cancelled" }, "User cancelled overtime request");
					return Json(new { success = true, message = "Đã hủy request" });
				}

				if (model.RequestType == "Leave")
				{
					var r = await _context.LeaveRequests.FirstOrDefaultAsync(x => x.LeaveRequestId == model.RequestId && x.UserId == userId);
					if (r == null) return Json(new { success = false, message = "Không tìm thấy request" });
					if (r.Status != "Pending") return Json(new { success = false, message = "Chỉ có thể hủy request đang ở trạng thái Pending" });

					r.Status = "Cancelled";
					r.UpdatedAt = DateTime.Now;
					await _context.SaveChangesAsync();

					await _auditHelper.LogAsync(userId, "UPDATE", "LeaveRequest", r.LeaveRequestId, null, new { Status = "Cancelled" }, "User cancelled leave request");
					return Json(new { success = true, message = "Đã hủy request" });
				}

				if (model.RequestType == "Late")
				{
					var r = await _context.LateRequests.FirstOrDefaultAsync(x => x.LateRequestId == model.RequestId && x.UserId == userId);
					if (r == null) return Json(new { success = false, message = "Không tìm thấy request" });
					if (r.Status != "Pending") return Json(new { success = false, message = "Chỉ có thể hủy request đang ở trạng thái Pending" });

					r.Status = "Cancelled";
					r.UpdatedAt = DateTime.Now;
					await _context.SaveChangesAsync();

					await _auditHelper.LogAsync(userId, "UPDATE", "LateRequest", r.LateRequestId, null, new { Status = "Cancelled" }, "User cancelled late request");
					return Json(new { success = true, message = "Đã hủy request" });
				}

				return Json(new { success = false, message = "Loại request không hợp lệ" });
			}
			catch (Exception ex)
			{
				await _auditHelper.LogFailedAttemptAsync(userId, "UPDATE", "Request", $"Exception: {ex.Message}", new { Error = ex.ToString() });
				return Json(new { success = false, message = $"Có lỗi: {ex.Message}" });
			}
		}

		// ============================================
		// MY REQUESTS PAGE
		// ============================================
		[HttpGet]
		public async System.Threading.Tasks.Task<IActionResult> MyRequests()
		{
			if (!IsAuthenticated())
				return RedirectToAction("Login", "Account");

			var userId = HttpContext.Session.GetInt32("UserId");
			if (userId == null)
				return RedirectToAction("Login", "Account");

			await _auditHelper.LogViewAsync(userId.Value, "Request", userId.Value, "Xem trang MyRequests");

			var overtime = await _context.OvertimeRequests.Where(r => r.UserId == userId).OrderByDescending(r => r.CreatedAt).ToListAsync();
			var leave = await _context.LeaveRequests.Where(r => r.UserId == userId).OrderByDescending(r => r.CreatedAt).ToListAsync();
			var late = await _context.LateRequests.Where(r => r.UserId == userId).OrderByDescending(r => r.CreatedAt).ToListAsync();

			var model = new
			{
				Overtime = overtime,
				Leave = leave,
				Late = late
			};

			return View(model);
		}
		// ============================================
		// THÊM METHOD NÀY VÀO StaffController.cs
		// ============================================

		[HttpPost]
		[RequestSizeLimit(5_242_880)] // 5MB limit
		public async Task<IActionResult> UploadAvatar(IFormFile avatar)
		{
			if (!IsAuthenticated())
				return Json(new { success = false, message = "Phiên đăng nhập hết hạn" });

			var userId = HttpContext.Session.GetInt32("UserId").Value;

			// Validate file
			if (avatar == null || avatar.Length == 0)
				return Json(new { success = false, message = "Vui lòng chọn ảnh để tải lên" });

			// Validate file type
			var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
			var extension = Path.GetExtension(avatar.FileName).ToLower();
			if (!allowedExtensions.Contains(extension))
				return Json(new { success = false, message = "Chỉ chấp nhận file ảnh định dạng JPG, JPEG, PNG" });

			// Validate file size (max 5MB)
			if (avatar.Length > 5 * 1024 * 1024)
				return Json(new { success = false, message = "Kích thước ảnh không được vượt quá 5MB" });

			try
			{
				var user = await _context.Users.FindAsync(userId);
				if (user == null)
					return Json(new { success = false, message = "Không tìm thấy người dùng" });

				// Create uploads folder if not exists
				var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "avatars");
				if (!Directory.Exists(uploadsFolder))
					Directory.CreateDirectory(uploadsFolder);

				// Delete old avatar if exists
				if (!string.IsNullOrEmpty(user.Avatar) && user.Avatar != "/images/default-avatar.png")
				{
					var oldAvatarPath = Path.Combine(_env.WebRootPath, user.Avatar.TrimStart('/'));
					if (System.IO.File.Exists(oldAvatarPath))
					{
						try
						{
							System.IO.File.Delete(oldAvatarPath);
						}
						catch (Exception ex)
						{
							// Log but continue
							Console.WriteLine($"Failed to delete old avatar: {ex.Message}");
						}
					}
				}

				// Generate unique filename
				var uniqueFileName = $"{userId}_{DateTime.Now:yyyyMMddHHmmss}{extension}";
				var filePath = Path.Combine(uploadsFolder, uniqueFileName);

				// Save file
				using (var fileStream = new FileStream(filePath, FileMode.Create))
				{
					await avatar.CopyToAsync(fileStream);
				}

				// Update user avatar path
				var avatarUrl = $"/uploads/avatars/{uniqueFileName}";
				var oldAvatar = user.Avatar;
				user.Avatar = avatarUrl;
				user.UpdatedAt = DateTime.Now;

				await _context.SaveChangesAsync();

				// Update session
				HttpContext.Session.SetString("Avatar", avatarUrl);

				// Log audit
				await _auditHelper.LogDetailedAsync(
					userId,
					"UPDATE",
					"User",
					userId,
					new { Avatar = oldAvatar },
					new { Avatar = avatarUrl },
					"Cập nhật ảnh đại diện",
					new Dictionary<string, object>
					{
				{ "FileName", uniqueFileName },
				{ "FileSize", $"{avatar.Length / 1024.0:F2} KB" },
				{ "Extension", extension }
					}
				);

				return Json(new
				{
					success = true,
					message = "Cập nhật ảnh đại diện thành công!",
					avatarUrl = avatarUrl
				});
			}
			catch (Exception ex)
			{
				await _auditHelper.LogFailedAttemptAsync(
					userId,
					"UPLOAD_AVATAR",
					"User",
					$"Exception: {ex.Message}",
					new { Error = ex.ToString() }
				);

				return Json(new
				{
					success = false,
					message = $"Có lỗi xảy ra: {ex.Message}"
				});
			}
		}

		[HttpPost]
		public async Task<IActionResult> UpdateTaskProgress([FromBody] UpdateTaskProgressRequest request)
		{
			if (!IsAuthenticated())
				return Json(new { success = false, message = "Phiên đăng nhập hết hạn" });

			var userId = HttpContext.Session.GetInt32("UserId").Value;

			try
			{
				var userTask = await _context.UserTasks
					.Include(ut => ut.Task)
					.Include(ut => ut.User)
						.ThenInclude(u => u.Department)
					.Include(ut => ut.User)
						.ThenInclude(u => u.Role)
					.FirstOrDefaultAsync(ut => ut.UserTaskId == request.UserTaskId && ut.UserId == userId);

				if (userTask == null)
					return Json(new { success = false, message = "Không tìm thấy công việc" });

				var oldStatus = userTask.Status ?? "TODO";

				// ❌ KHÔNG CHO PHÉP CẬP NHẬT TASK ĐÃ HOÀN THÀNH
				if (oldStatus == "Done")
					return Json(new { success = false, message = "⚠️ Công việc đã hoàn thành, không thể cập nhật" });

				// ✅ KIỂM TRA USER CÓ PHẢI DEV KHÔNG
				bool isDev = await IsDevUser(userId);

				// ✅ XÁC ĐỊNH VALID TRANSITIONS DỰA VÀO ROLE/DEPARTMENT
				Dictionary<string, List<string>> validTransitions;

				if (isDev)
				{
					// ✅ LUỒNG DEV: TODO → InProgress → Testing → (Done/Reopen by Tester)
					validTransitions = new Dictionary<string, List<string>>
			{
				{ "TODO", new List<string> { "InProgress" } },
				{ "InProgress", new List<string> { "Testing", "TODO" } },
				{ "Reopen", new List<string> { "InProgress" } },
				{ "Testing", new List<string> { } }, // Chỉ Tester mới chuyển
                { "Done", new List<string> { } }     // Chỉ Tester mới chuyển
            };
				}
				else
				{
					// ✅ LUỒNG NON-DEV: TODO → InProgress → Done
					validTransitions = new Dictionary<string, List<string>>
			{
				{ "TODO", new List<string> { "InProgress" } },
				{ "InProgress", new List<string> { "Done", "TODO" } },
				{ "Reopen", new List<string> { "InProgress" } }, // Phòng khi có edge case
                { "Testing", new List<string> { } },
				{ "Done", new List<string> { } }
			};
				}

				// ✅ VALIDATE TRANSITION
				if (!validTransitions.ContainsKey(oldStatus) || !validTransitions[oldStatus].Contains(request.Status))
				{
					var allowedStatuses = validTransitions.ContainsKey(oldStatus) && validTransitions[oldStatus].Count > 0
						? string.Join(", ", validTransitions[oldStatus].Select(s => GetStatusText(s)))
						: "không có trạng thái nào";

					return Json(new
					{
						success = false,
						message = $"⚠️ Không thể chuyển từ '{GetStatusText(oldStatus)}' sang '{GetStatusText(request.Status)}'\n\n" +
								 $"Trạng thái hợp lệ: {allowedStatuses}"
					});
				}

				// ✅ VALIDATE: CHỈ DEV MỚI ĐƯỢC GỬI TEST
				if (request.Status == "Testing")
				{
					if (!isDev)
					{
						return Json(new
						{
							success = false,
							message = "⚠️ Chỉ Dev mới được gửi công việc đi test"
						});
					}

					// ✅ BẮT BUỘC CHỌN TESTER
					if (!request.TesterId.HasValue || request.TesterId.Value <= 0)
					{
						return Json(new
						{
							success = false,
							message = "⚠️ Vui lòng chọn Tester trước khi gửi test",
							needTester = true
						});
					}

					// ✅ KIỂM TRA TESTER TỒN TẠI
					var tester = await _context.Users
						.Include(u => u.Role)
						.FirstOrDefaultAsync(u => u.UserId == request.TesterId.Value
							&& u.IsActive == true
							&& (u.IsTester == true || u.Role.RoleName == "Tester"));

					if (tester == null)
					{
						return Json(new
						{
							success = false,
							message = "⚠️ Tester được chọn không tồn tại hoặc không còn hoạt động"
						});
					}

					// ✅ GÁN TESTERID
					userTask.TesterId = request.TesterId.Value;
				}

				// ✅ VALIDATE: NON-DEV CHỈ ĐƯỢC CHUYỂN DONE
				if (!isDev && request.Status == "Testing")
				{
					return Json(new
					{
						success = false,
						message = "⚠️ Bạn không có quyền gửi công việc đi test. Chỉ Dev mới được phép."
					});
				}

				// ✅ CẬP NHẬT TRẠNG THÁI
				userTask.Status = request.Status;
				userTask.ReportLink = request.ReportLink ?? userTask.ReportLink;
				userTask.UpdatedAt = DateTime.Now;

				await _context.SaveChangesAsync();

				// ✅ LOG AUDIT
				await _auditHelper.LogDetailedAsync(
					userId,
					"UPDATE",
					"UserTask",
					userTask.UserTaskId,
					new { Status = oldStatus, ReportLink = userTask.ReportLink, TesterId = userTask.TesterId },
					new { Status = request.Status, ReportLink = request.ReportLink, TesterId = request.TesterId },
					$"Cập nhật tiến độ task: {userTask.Task.TaskName}",
					new Dictionary<string, object>
					{
				{ "OldStatus", oldStatus },
				{ "NewStatus", request.Status },
				{ "TesterId", request.TesterId ?? 0 },
				{ "IsDev", isDev }
					}
				);

				// ✅ GỬI THÔNG BÁO
				if (request.Status == "Testing" && request.TesterId.HasValue)
				{
					var tester = await _context.Users.FindAsync(request.TesterId.Value);

					// Gửi cho TESTER cụ thể (Realtime)
					await _notificationService.SendToTesterAsync(
						request.TesterId.Value,
						"🧪 Task mới cần test",
						$"Task '{userTask.Task.TaskName}' từ {userTask.User.FullName} cần bạn test",
						"info",
						"/Tester/Dashboard"
					);

					// Gửi cho Admin
					await _notificationService.SendToAdminsAsync(
						"Task gửi test",
						$"{userTask.User.FullName} vừa gửi task '{userTask.Task.TaskName}' cho Tester: {tester?.FullName ?? "N/A"}",
						"info",
						"/Admin/TaskList"
					);
				}
				else if (request.Status == "Done")
				{
					// GỬI CHO ADMIN KHI HOÀN THÀNH
					await _notificationService.SendToAdminsAsync(
						"Task hoàn thành",
						$"{userTask.User.FullName} vừa hoàn thành task '{userTask.Task.TaskName}'",
						"success",
						"/Admin/TaskList"
					);
				}

				return Json(new
				{
					success = true,
					message = $"✅ Cập nhật thành công!\nTrạng thái: {GetStatusText(request.Status)}"
				});
			}
			catch (Exception ex)
			{
				await _auditHelper.LogFailedAttemptAsync(
					userId,
					"UPDATE",
					"UserTask",
					$"Exception: {ex.Message}",
					new { UserTaskId = request.UserTaskId, Error = ex.ToString() }
				);

				return Json(new { success = false, message = $"Có lỗi xảy ra: {ex.Message}" });
			}
		}

		private string GetStatusText(string status)
		{
			return status switch
			{
				"TODO" => "Chưa bắt đầu",
				"InProgress" => "Đang làm",
				"Testing" => "Chờ test",
				"Done" => "Hoàn thành",
				"Reopen" => "Cần sửa lại",
				_ => status
			};
		}

		

		// Thêm vào StaffController class
		private async Task<bool> IsDevUser(int userId)
		{
			var user = await _context.Users
				.Include(u => u.Role)
				.Include(u => u.Department)
				.FirstOrDefaultAsync(u => u.UserId == userId);

			if (user == null) return false;

			var roleName = user.Role?.RoleName ?? "";
			var deptName = user.Department?.DepartmentName ?? "";

			// ✅ KIỂM TRA ROLE
			bool isDevRole = roleName.Contains("Dev", StringComparison.OrdinalIgnoreCase) ||
							 roleName.Equals("Developer", StringComparison.OrdinalIgnoreCase);

			// ✅ KIỂM TRA DEPARTMENT
			bool isDevDepartment = deptName.Contains("Dev Backend", StringComparison.OrdinalIgnoreCase) ||
								  deptName.Contains("Dev Frontend", StringComparison.OrdinalIgnoreCase) ||
								  deptName.Contains("Developer Backend", StringComparison.OrdinalIgnoreCase) ||
								  deptName.Contains("Developer Frontend", StringComparison.OrdinalIgnoreCase) ||
								  deptName.Contains("Backend", StringComparison.OrdinalIgnoreCase) ||
								  deptName.Contains("Frontend", StringComparison.OrdinalIgnoreCase);

			return isDevRole || isDevDepartment;
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
		public async Task<IActionResult> MyProjects()
		{
			if (!IsAuthenticated())
				return RedirectToAction("Login", "Account");

			var userId = HttpContext.Session.GetInt32("UserId").Value;

			await _auditHelper.LogViewAsync(
				userId,
				"Project",
				0,
				"Xem danh sách dự án đang tham gia"
			);

			// Lấy tất cả dự án mà user là thành viên
			var myProjects = await _context.ProjectMembers
				.Include(pm => pm.Project)
					.ThenInclude(p => p.Leader)
				.Include(pm => pm.Project)
					.ThenInclude(p => p.Department)
				.Include(pm => pm.Project)
					.ThenInclude(p => p.ProjectMembers)
				.Include(pm => pm.Project)
					.ThenInclude(p => p.Tasks)
						.ThenInclude(t => t.UserTasks)
				.Where(pm => pm.UserId == userId && pm.IsActive == true)
				.OrderByDescending(pm => pm.JoinedAt)
				.Select(pm => pm.Project)
				.ToListAsync();

			// Thống kê
			ViewBag.TotalProjects = myProjects.Count;
			ViewBag.ActiveProjects = myProjects.Count(p => p.Status == "InProgress");
			ViewBag.CompletedProjects = myProjects.Count(p => p.Status == "Completed");
			ViewBag.MyLeadProjects = myProjects.Count(p => p.LeaderId == userId);

			// ✅ TÍNH TOÁN TRƯỚC TRONG CONTROLLER - Tránh lambda trong View
			var projectStatsDict = new Dictionary<int, object>();

			foreach (var p in myProjects)
			{
				var myTotalTasks = p.Tasks
					.SelectMany(t => t.UserTasks)
					.Count(ut => ut.UserId == userId);

				var myCompletedTasks = p.Tasks
					.SelectMany(t => t.UserTasks)
					.Count(ut => ut.UserId == userId && ut.Status == "Done");

				var myInProgressTasks = p.Tasks
					.SelectMany(t => t.UserTasks)
					.Count(ut => ut.UserId == userId && ut.Status == "InProgress");

				var myTodoTasks = p.Tasks
					.SelectMany(t => t.UserTasks)
					.Count(ut => ut.UserId == userId && ut.Status == "TODO");

				var isLeader = p.LeaderId == userId;

				projectStatsDict.Add(p.ProjectId, new
				{
					Project = p,
					MyTotalTasks = myTotalTasks,
					MyCompletedTasks = myCompletedTasks,
					MyInProgressTasks = myInProgressTasks,
					MyTodoTasks = myTodoTasks,
					IsLeader = isLeader
				});
			}

			ViewBag.ProjectStats = projectStatsDict;

			return View(myProjects);
		}

		// ============================================
		// MY PROJECT DETAIL - Chi tiết dự án & task của mình
		// ============================================
		// ============================================
		// ✅ MYPROJECTDETAIL - FIXED TYPE CONVERSION
		// ============================================
		[HttpGet]
		public async Task<IActionResult> MyProjectDetail(int id)
		{
			if (!IsAuthenticated())
				return RedirectToAction("Login", "Account");

			var userId = HttpContext.Session.GetInt32("UserId").Value;

			try
			{
				// 1. Kiểm tra membership
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

				// 2. Lấy tasks
				var myTasks = await _context.UserTasks
					.Include(ut => ut.Task)
					.Include(ut => ut.Tester)
					.Include(ut => ut.User)
					.Where(ut => ut.UserId == userId &&
								ut.Task != null &&
								ut.Task.ProjectId == id &&
								ut.Task.IsActive == true)
					.OrderBy(ut => ut.Status == "TODO" ? 1 : ut.Status == "InProgress" ? 2 : 3)
					.ThenByDescending(ut => ut.Task.Priority == "High" ? 1 : ut.Task.Priority == "Medium" ? 2 : 3)
					.ToListAsync();

				// ✅ 3. Tạo metadata - QUAN TRỌNG: Cast sang object ngay
				var myTasksWithMetadata = myTasks.Select(ut => (object)new
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
					},
					PriorityText = ut.Task.Priority switch
					{
						"High" => "Cao",
						"Medium" => "Trung bình",
						"Low" => "Thấp",
						_ => "Trung bình"
					}
				}).ToList();

				// 4. Thống kê tasks
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
				ViewBag.MyTasksWithMetadata = myTasksWithMetadata;

				// ✅ 5. Active members - Cast sang object
				var activeMembers = new List<object>();

				if (project.ProjectMembers != null && project.ProjectMembers.Any())
				{
					activeMembers = project.ProjectMembers
						.Where(pm => pm.IsActive && pm.User != null)
						.OrderBy(pm => pm.Role == "Leader" ? 0 : 1)
						.ThenBy(pm => pm.User.FullName)
						.Select(pm => (object)new
						{
							Member = pm,
							HasAvatar = !string.IsNullOrEmpty(pm.User?.Avatar) &&
									   pm.User.Avatar != "/images/default-avatar.png",
							Initials = pm.User?.FullName?.Substring(0, 1).ToUpper() ?? "?"
						})
						.ToList();
				}

				ViewBag.ActiveMembers = activeMembers;

				// 6. Log audit
				await _auditHelper.LogViewAsync(
					userId,
					"Project",
					id,
					$"Xem chi tiết dự án: {project.ProjectName}"
				);

				return View();
			}
			catch (Exception ex)
			{
				Console.WriteLine($"[ERROR] MyProjectDetail Exception: {ex.Message}");
				Console.WriteLine($"[ERROR] StackTrace: {ex.StackTrace}");

				TempData["Error"] = $"Có lỗi xảy ra: {ex.Message}";
				return RedirectToAction("MyProjects");
			}
		}
		[HttpGet]
		public async System.Threading.Tasks.Task<IActionResult> AttendanceHistory()
		{
			if (!IsAuthenticated())
				return RedirectToAction("Login", "Account");

			if (!IsStaffOrAdmin())
				return RedirectToAction("Login", "Account");

			var userId = HttpContext.Session.GetInt32("UserId");

			await _auditHelper.LogViewAsync(
				userId.Value,
				"Attendance",
				userId.Value,
				"Xem lịch sử chấm công cá nhân (Staff)"
			);

			// ✅ FIX: Include đầy đủ User và Department như Admin version
			var attendances = await _context.Attendances
				.Include(a => a.User)
					.ThenInclude(u => u.Department)
				.Where(a => a.UserId == userId)
				.OrderByDescending(a => a.WorkDate)
				.ThenByDescending(a => a.CheckInTime)
				.ToListAsync();

			// ✅ TÍNH TOÁN STATISTICS GIỐNG ADMIN
			ViewBag.TotalRecords = attendances.Count;
			ViewBag.TotalCheckIns = attendances.Count(a => a.CheckInTime != null);
			ViewBag.TotalCheckOuts = attendances.Count(a => a.CheckOutTime != null);
			ViewBag.CompletedDays = attendances.Count(a => a.CheckInTime != null && a.CheckOutTime != null);
			ViewBag.OnTimeCount = attendances.Count(a => a.IsLate == false);
			ViewBag.LateCount = attendances.Count(a => a.IsLate == true);
			ViewBag.TotalWorkHours = attendances.Sum(a => a.TotalHours ?? 0);
			ViewBag.OutsideGeofence = attendances.Count(a => a.IsWithinGeofence == false);

			return View(attendances);
		}

		// ============================================
		// GET MY PROJECTS (JSON)
		// ============================================
		[HttpGet]
		public async Task<IActionResult> GetMyProjects()
		{
			if (!IsAuthenticated())
				return Json(new { success = false, message = "Phiên đăng nhập hết hạn" });

			var userId = HttpContext.Session.GetInt32("UserId").Value;

			try
			{
				var myProjects = await _context.ProjectMembers
					.Include(pm => pm.Project)
						.ThenInclude(p => p.Leader)
					.Include(pm => pm.Project)
						.ThenInclude(p => p.Department)
					.Include(pm => pm.Project)
						.ThenInclude(p => p.Tasks)
							.ThenInclude(t => t.UserTasks)
					.Where(pm => pm.UserId == userId && pm.IsActive == true)
					.Select(pm => new
					{
						projectId = pm.Project.ProjectId,
						projectCode = pm.Project.ProjectCode,
						projectName = pm.Project.ProjectName,
						description = pm.Project.Description ?? "",
						status = pm.Project.Status,
						priority = pm.Project.Priority ?? "Medium",
						progress = pm.Project.Progress ?? 0,
						startDate = pm.Project.StartDate.HasValue ? pm.Project.StartDate.Value.ToString("dd/MM/yyyy") : "",
						endDate = pm.Project.EndDate.HasValue ? pm.Project.EndDate.Value.ToString("dd/MM/yyyy") : "",
						leaderName = pm.Project.Leader != null ? pm.Project.Leader.FullName : "Chưa chỉ định",
						departmentName = pm.Project.Department != null ? pm.Project.Department.DepartmentName : "N/A",
						myRole = pm.Role ?? "Member",
						isLeader = pm.Project.LeaderId == userId,

						// Task stats của mình
						myTotalTasks = pm.Project.Tasks
							.SelectMany(t => t.UserTasks)
							.Count(ut => ut.UserId == userId),
						myCompletedTasks = pm.Project.Tasks
							.SelectMany(t => t.UserTasks)
							.Count(ut => ut.UserId == userId && ut.Status == "Done"),
						myInProgressTasks = pm.Project.Tasks
							.SelectMany(t => t.UserTasks)
							.Count(ut => ut.UserId == userId && ut.Status == "InProgress"),
						myTodoTasks = pm.Project.Tasks
							.SelectMany(t => t.UserTasks)
							.Count(ut => ut.UserId == userId && ut.Status == "TODO"),

						joinedAt = pm.JoinedAt.ToString("dd/MM/yyyy HH:mm")
					})
					.OrderByDescending(p => p.joinedAt)
					.ToListAsync();

				return Json(new
				{
					success = true,
					projects = myProjects,
					totalProjects = myProjects.Count
				});
			}
			catch (Exception ex)
			{
				return Json(new { success = false, message = $"Có lỗi xảy ra: {ex.Message}" });
			}
		}

		// ============================================
		// GET MY PROJECT TASKS (JSON)
		// ============================================
		[HttpGet]
		public async Task<IActionResult> GetMyProjectTasks(int projectId)
		{
			if (!IsAuthenticated())
				return Json(new { success = false, message = "Phiên đăng nhập hết hạn" });

			var userId = HttpContext.Session.GetInt32("UserId").Value;

			try
			{
				// Kiểm tra user có phải member không
				var isMember = await _context.ProjectMembers
					.AnyAsync(pm => pm.ProjectId == projectId && pm.UserId == userId && pm.IsActive == true);

				if (!isMember)
					return Json(new { success = false, message = "Bạn không phải thành viên của dự án này" });

				var myTasks = await _context.UserTasks
					.Include(ut => ut.Task)
					.Include(ut => ut.Tester)
					.Where(ut => ut.UserId == userId &&
								ut.Task.ProjectId == projectId &&
								ut.Task.IsActive == true)
					.OrderBy(ut => ut.Status == "TODO" ? 1 : ut.Status == "InProgress" ? 2 : 3)
					.ThenByDescending(ut => ut.Task.Priority == "High" ? 1 : ut.Task.Priority == "Medium" ? 2 : 3)
					.Select(ut => new
					{
						userTaskId = ut.UserTaskId,
						taskId = ut.Task.TaskId,
						taskName = ut.Task.TaskName,
						description = ut.Task.Description ?? "",
						platform = ut.Task.Platform ?? "",
						reportLink = ut.ReportLink ?? "",
						deadline = ut.Task.Deadline.HasValue ? ut.Task.Deadline.Value.ToString("dd/MM/yyyy HH:mm") : "",
						priority = ut.Task.Priority ?? "Medium",
						status = ut.Status ?? "TODO",
						testerId = ut.TesterId,
						testerName = ut.Tester != null ? ut.Tester.FullName : null,

						isOverdue = ut.Task.Deadline.HasValue &&
								   (ut.Status != "Done"
									? DateTime.Now > ut.Task.Deadline.Value
									: (ut.UpdatedAt.HasValue && ut.UpdatedAt.Value > ut.Task.Deadline.Value)),

						updatedAt = ut.UpdatedAt.HasValue ? ut.UpdatedAt.Value.ToString("dd/MM/yyyy HH:mm") : ""
					})
					.ToListAsync();

				return Json(new
				{
					success = true,
					tasks = myTasks,
					totalTasks = myTasks.Count,
					todoTasks = myTasks.Count(t => t.status == "TODO"),
					inProgressTasks = myTasks.Count(t => t.status == "InProgress"),
					testingTasks = myTasks.Count(t => t.status == "Testing"),
					completedTasks = myTasks.Count(t => t.status == "Done"),
					overdueTasks = myTasks.Count(t => t.isOverdue)
				});
			}
			catch (Exception ex)
			{
				return Json(new { success = false, message = $"Có lỗi xảy ra: {ex.Message}" });
			}
		}

		// ============================================
		// GET PROJECT MEMBERS (cho chat/collab)
		// ============================================
		[HttpGet]
		public async Task<IActionResult> GetProjectMembers(int projectId)
		{
			if (!IsAuthenticated())
				return Json(new { success = false, message = "Phiên đăng nhập hết hạn" });

			var userId = HttpContext.Session.GetInt32("UserId").Value;

			try
			{
				// Kiểm tra user có phải member không
				var isMember = await _context.ProjectMembers
					.AnyAsync(pm => pm.ProjectId == projectId && pm.UserId == userId && pm.IsActive == true);

				if (!isMember)
					return Json(new { success = false, message = "Bạn không phải thành viên của dự án này" });

				var members = await _context.ProjectMembers
					.Include(pm => pm.User)
						.ThenInclude(u => u.Department)
					.Include(pm => pm.User)
						.ThenInclude(u => u.Role)
					.Where(pm => pm.ProjectId == projectId && pm.IsActive == true)
					.OrderBy(pm => pm.Role == "Leader" ? 0 : 1)
					.ThenBy(pm => pm.User.FullName)
					.Select(pm => new
					{
						userId = pm.UserId,
						fullName = pm.User.FullName,
						email = pm.User.Email,
						avatar = pm.User.Avatar,
						departmentName = pm.User.Department != null ? pm.User.Department.DepartmentName : "N/A",
						roleName = pm.User.Role.RoleName,
						projectRole = pm.Role ?? "Member",
						isLeader = pm.Role == "Leader",
						joinedAt = pm.JoinedAt.ToString("dd/MM/yyyy"),
						isCurrentUser = pm.UserId == userId
					})
					.ToListAsync();

				return Json(new
				{
					success = true,
					members = members,
					totalMembers = members.Count
				});
			}
			catch (Exception ex)
			{
				return Json(new { success = false, message = $"Có lỗi xảy ra: {ex.Message}" });
			}
		}

		// ============================================
		// MY PROJECT DASHBOARD (Statistics)
		// ============================================
		[HttpGet]
		public async Task<IActionResult> GetMyProjectStats()
		{
			if (!IsAuthenticated())
				return Json(new { success = false, message = "Phiên đăng nhập hết hạn" });

			var userId = HttpContext.Session.GetInt32("UserId").Value;

			try
			{
				// Tất cả dự án đang tham gia
				var myProjects = await _context.ProjectMembers
					.Include(pm => pm.Project)
						.ThenInclude(p => p.Tasks)
							.ThenInclude(t => t.UserTasks)
					.Where(pm => pm.UserId == userId && pm.IsActive == true)
					.ToListAsync();

				// Tổng số task của mình trong tất cả dự án
				var allMyTasks = myProjects
					.SelectMany(pm => pm.Project.Tasks)
					.SelectMany(t => t.UserTasks)
					.Where(ut => ut.UserId == userId)
					.ToList();

				return Json(new
				{
					success = true,
					stats = new
					{
						totalProjects = myProjects.Count,
						activeProjects = myProjects.Count(pm => pm.Project.Status == "InProgress"),
						completedProjects = myProjects.Count(pm => pm.Project.Status == "Completed"),
						leadingProjects = myProjects.Count(pm => pm.Project.LeaderId == userId),

						totalTasks = allMyTasks.Count,
						todoTasks = allMyTasks.Count(ut => ut.Status == "TODO"),
						inProgressTasks = allMyTasks.Count(ut => ut.Status == "InProgress"),
						testingTasks = allMyTasks.Count(ut => ut.Status == "Testing"),
						completedTasks = allMyTasks.Count(ut => ut.Status == "Done"),

						completionRate = allMyTasks.Count > 0
							? Math.Round((double)allMyTasks.Count(ut => ut.Status == "Done") / allMyTasks.Count * 100, 1)
							: 0
					}
				});
			}
			catch (Exception ex)
			{
				return Json(new { success = false, message = $"Có lỗi xảy ra: {ex.Message}" });
			}
		}

		// ============================================
		// LEAVE PROJECT (tự rời khỏi dự án)
		// ============================================
		[HttpPost]
		public async Task<IActionResult> LeaveProject([FromBody] LeaveProjectRequest request)
		{
			if (!IsAuthenticated())
				return Json(new { success = false, message = "Phiên đăng nhập hết hạn" });

			var userId = HttpContext.Session.GetInt32("UserId").Value;

			try
			{
				var membership = await _context.ProjectMembers
					.Include(pm => pm.Project)
					.FirstOrDefaultAsync(pm => pm.ProjectId == request.ProjectId &&
											  pm.UserId == userId &&
											  pm.IsActive == true);

				if (membership == null)
					return Json(new { success = false, message = "Bạn không phải thành viên của dự án này" });

				// Không cho phép Leader rời dự án
				if (membership.Project.LeaderId == userId)
					return Json(new { success = false, message = "Leader không thể rời dự án. Vui lòng liên hệ Admin." });

				// Đánh dấu không còn active
				membership.IsActive = false;
				membership.LeftAt = DateTime.Now;

				await _context.SaveChangesAsync();

				await _auditHelper.LogAsync(
					userId,
					"LEAVE",
					"ProjectMember",
					membership.ProjectMemberId,
					new { IsActive = true },
					new { IsActive = false, LeftAt = DateTime.Now },
					$"Rời khỏi dự án: {membership.Project.ProjectName}"
				);

				return Json(new
				{
					success = true,
					message = $"Bạn đã rời khỏi dự án '{membership.Project.ProjectName}'"
				});
			}
			catch (Exception ex)
			{
				return Json(new { success = false, message = $"Có lỗi xảy ra: {ex.Message}" });
			}
		}
		// ============================================
		// REQUEST MODELS
		// ============================================

		// ============================================
		// REQUEST MODELS
		// ============================================
		public class LeaveProjectRequest
		{
			public int ProjectId { get; set; }
		}
		public class CheckInRequest
		{
			public decimal Latitude { get; set; }
			public decimal Longitude { get; set; }
			public string? Address { get; set; }
			public string? Notes { get; set; }
			public IFormFile Photo { get; set; }
		}
		public class UpdateTaskProgressRequest
		{
			public int UserTaskId { get; set; }
			public string Status { get; set; } = "TODO";
			public string? ReportLink { get; set; }
			public int? TesterId { get; set; }
		}
		public class CheckOutRequest
		{
			public decimal Latitude { get; set; }
			public decimal Longitude { get; set; }
			public string? Address { get; set; }
			public string? Notes { get; set; }
			public IFormFile Photo { get; set; }
		}
	}
}