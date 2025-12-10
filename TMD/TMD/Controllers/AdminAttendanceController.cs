using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AIHUBOS.Helpers;
using TMD.Models;
using Microsoft.AspNetCore.SignalR;
using AIHUBOS.Hubs;
using AIHUBOS.Services;
using System.Text.Json;
using System.Net.Http;
using TaskAsync = System.Threading.Tasks.Task;

namespace TMD.Controllers
{
	public class AdminAttendanceController : Controller
	{
		private readonly AihubSystemContext _context;
		private readonly AuditHelper _auditHelper;
		private readonly IWebHostEnvironment _env;
		private readonly HttpClient _httpClient;
		private readonly INotificationService _notificationService;
		private readonly ITelegramService _telegramService;

		public AdminAttendanceController(
			AihubSystemContext context,
			AuditHelper auditHelper,
			IWebHostEnvironment env,
			IHttpClientFactory httpClientFactory,
			INotificationService notificationService,
			ITelegramService telegramService)
		{
			_context = context;
			_auditHelper = auditHelper;
			_env = env;
			_httpClient = httpClientFactory.CreateClient();
			_notificationService = notificationService;
			_telegramService = telegramService;
		}
		private DateTime GetVietnamTime()
		{
			// CÁCH FIX: Lấy giờ UTC gốc và cộng cứng 7 tiếng để ra giờ VN
			// Cách này chạy đúng trên mọi server (Windows, Linux, Docker, Azure)
			return DateTime.UtcNow.AddHours(7);
		}

		private async Task<(TimeOnly startTime, TimeOnly endTime)> GetStandardTimesAsync()
		{
			try
			{
				var configs = await _context.SystemSettings
					.Where(c => c.IsActive == true && c.IsEnabled == true)
					.Where(c => c.SettingKey == "CHECK_IN_STANDARD_TIME" ||
								c.SettingKey == "CHECK_OUT_STANDARD_TIME")
					.ToDictionaryAsync(c => c.SettingKey, c => c.SettingValue);

				// ✅ GET WITH FALLBACK
				var startStr = configs.GetValueOrDefault("CHECK_IN_STANDARD_TIME") ?? "08:00";
				var endStr = configs.GetValueOrDefault("CHECK_OUT_STANDARD_TIME") ?? "17:00";

				// ✅ VALIDATE & PARSE
				TimeOnly startTime = TimeOnly.Parse("08:00"); // Default
				TimeOnly endTime = TimeOnly.Parse("17:00");   // Default

				if (!string.IsNullOrWhiteSpace(startStr) && TimeOnly.TryParse(startStr, out var parsedStart))
					startTime = parsedStart;

				if (!string.IsNullOrWhiteSpace(endStr) && TimeOnly.TryParse(endStr, out var parsedEnd))
					endTime = parsedEnd;

				return (startTime, endTime);
			}
			catch (Exception ex)
			{
				// ✅ LOG ERROR & FALLBACK
				Console.WriteLine($"[GetStandardTimes] Error: {ex.Message}");
				return (TimeOnly.Parse("08:00"), TimeOnly.Parse("17:00"));
			}
		}
		private bool IsAdmin()
		{
			return HttpContext.Session.GetString("RoleName") == "Admin";
		}

		private bool IsAuthenticated()
		{
			return HttpContext.Session.GetInt32("UserId") != null;
		}

		// ============================================
		// ADMIN ATTENDANCE DASHBOARD
		// ============================================
		[HttpGet]
		public async System.Threading.Tasks.Task<IActionResult> Dashboard()
		{
			if (!IsAdmin())
				return RedirectToAction("Login", "Account");

			var adminId = HttpContext.Session.GetInt32("UserId");
			if (!adminId.HasValue)
				return RedirectToAction("Login", "Account");

			var admin = await _context.Users
				.Include(u => u.Role)
				.Include(u => u.Department)
				.FirstOrDefaultAsync(u => u.UserId == adminId.Value);

			if (admin == null)
				return RedirectToAction("Login", "Account");

			ViewBag.Admin = admin;

			// Get today's attendance
			var today = DateOnly.FromDateTime(DateTime.Now);
			var todayAttendance = await _context.Attendances
				.FirstOrDefaultAsync(a => a.UserId == adminId && a.WorkDate == today);

			ViewBag.TodayAttendance = todayAttendance;

			// Get standard times from settings
			var configs = await _context.SystemSettings
				.Where(c => c.IsActive == true && c.IsEnabled == true)
				.ToDictionaryAsync(c => c.SettingKey, c => c.SettingValue);

			ViewBag.StandardStartTime = configs.GetValueOrDefault("CHECK_IN_STANDARD_TIME", "08:00");
			ViewBag.StandardEndTime = configs.GetValueOrDefault("CHECK_OUT_STANDARD_TIME", "17:00");

			// Statistics for this month
			var firstDayOfMonth = new DateOnly(DateTime.Now.Year, DateTime.Now.Month, 1);
			var monthAttendances = await _context.Attendances
				.Where(a => a.UserId == adminId && a.WorkDate >= firstDayOfMonth)
				.ToListAsync();

			ViewBag.ThisMonthAttendances = monthAttendances.Count;
			ViewBag.ThisMonthWorkHours = monthAttendances.Sum(a => a.TotalHours ?? 0);
			ViewBag.OnTimeCount = monthAttendances.Count(a => a.IsLate == false);
			ViewBag.LateCount = monthAttendances.Count(a => a.IsLate == true);

			await _auditHelper.LogViewAsync(
				adminId.Value,
				"Attendance",
				0,
				"Xem dashboard check-in/out Admin"
			);

			return View();
		}

		// ============================================
		// GET TODAY ATTENDANCE
		// ============================================
		[HttpGet]
		public async System.Threading.Tasks.Task<IActionResult> GetTodayAttendance()
		{
			if (!IsAuthenticated())
				return Json(new { hasCheckedIn = false });

			var userIdNullable = HttpContext.Session.GetInt32("UserId");
			if (!userIdNullable.HasValue)
				return Json(new { hasCheckedIn = false });

			var userId = userIdNullable.Value;
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
				checkInAddress = attendance.CheckInAddress,
				checkOutAddress = attendance.CheckOutAddress
			});
		}

		// ============================================
		// REVERSE GEOCODING
		// ============================================
		private async Task<string> GetAddressFromCoordinates(decimal latitude, decimal longitude)
		{
			for (int attempt = 0; attempt < 3; attempt++)
			{
				try
				{
					var url = $"https://nominatim.openstreetmap.org/reverse?format=json&lat={latitude}&lon={longitude}&addressdetails=1&accept-language=vi";

					_httpClient.DefaultRequestHeaders.Clear();
					_httpClient.DefaultRequestHeaders.Add("User-Agent", "TMDSystem/1.0 (Contact: admin@tmdsystem.com)");
					_httpClient.Timeout = TimeSpan.FromSeconds(10);

					var response = await _httpClient.GetStringAsync(url);
					var jsonDoc = JsonDocument.Parse(response);

					if (jsonDoc.RootElement.TryGetProperty("display_name", out var displayName))
					{
						var address = displayName.GetString();
						if (!string.IsNullOrEmpty(address))
							return address;
					}

					return $"Lat: {latitude:F6}, Long: {longitude:F6}";
				}
				catch (TaskCanceledException)
				{
					if (attempt == 2)
						return $"Lat: {latitude:F6}, Long: {longitude:F6}";
					await System.Threading.Tasks.Task.Delay(1000 * (attempt + 1));
				}
				catch
				{
					if (attempt == 2)
						return $"Lat: {latitude:F6}, Long: {longitude:F6}";
					await System.Threading.Tasks.Task.Delay(500);
				}
			}

			return $"Lat: {latitude:F6}, Long: {longitude:F6}";
		}

		// ========== AdminAttendanceController.cs - UPDATED CHECK-IN/OUT METHODS ==========
		// Thay thế 2 method CheckIn và CheckOut trong AdminAttendanceController.cs

		[HttpPost]
		[RequestSizeLimit(10_485_760)]
		public async System.Threading.Tasks.Task<IActionResult> CheckIn([FromForm] CheckInRequest request)
		{
			if (!IsAdmin())
				return Json(new { success = false, message = "Chỉ Admin mới có quyền" });

			var userId = HttpContext.Session.GetInt32("UserId").Value;
			var serverNow = GetVietnamTime();
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

			// ✅ ẢNH TÙY CHỌN (không bắt buộc)
			string photoPath = null;
			if (request.Photo != null && request.Photo.Length > 0)
			{
				if (request.Photo.Length > 10 * 1024 * 1024)
					return Json(new { success = false, message = "Kích thước ảnh không được vượt quá 10MB" });

				var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
				var extension = Path.GetExtension(request.Photo.FileName).ToLower();
				if (!allowedExtensions.Contains(extension))
					return Json(new { success = false, message = "Chỉ chấp nhận file ảnh định dạng JPG, JPEG, PNG" });

				try
				{
					var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "attendance");
					if (!Directory.Exists(uploadsFolder))
						Directory.CreateDirectory(uploadsFolder);

					var uniqueFileName = $"{userId}_{serverNow:yyyyMMdd_HHmmss}_checkin{extension}";
					var filePath = Path.Combine(uploadsFolder, uniqueFileName);

					using (var fileStream = new FileStream(filePath, FileMode.Create))
						await request.Photo.CopyToAsync(fileStream);

					photoPath = $"/uploads/attendance/{uniqueFileName}";
				}
				catch (Exception ex)
				{
					Console.WriteLine($"Photo upload failed: {ex.Message}");
				}
			}

			// ✅ GPS TÙY CHỌN (không bắt buộc, nếu = 0 thì không có GPS)
			bool hasGPS = request.Latitude != 0 && request.Longitude != 0;

			try
			{
				var configs = await _context.SystemSettings
					.Where(c => c.IsActive == true && c.IsEnabled == true)
					.ToDictionaryAsync(c => c.SettingKey, c => c.SettingValue);

				// ✅ Đảm bảo giờ chuẩn Check-in là 09:00
				var standardStartTimeStr = configs.GetValueOrDefault("CHECK_IN_STANDARD_TIME") ?? "09:00";
				if (!TimeOnly.TryParse(standardStartTimeStr, out var standardStartTime))
					standardStartTime = TimeOnly.Parse("09:00");

				// ✅ NGƯỠNG TRỄ: 09:01 (9 giờ 1 phút)
				var lateThreshold = standardStartTime.AddMinutes(1);
				var checkInTime = new TimeOnly(serverNow.Hour, serverNow.Minute, serverNow.Second);

				// ✅ LOGIC TRỄ: CHỈ TRỄ KHI > 09:01
				var isLate = checkInTime > lateThreshold;

				// ✅ LẤY ĐỊA CHỈ NẾU CÓ GPS
				string address = "Không có GPS";
				if (hasGPS)
				{
					address = await GetAddressFromCoordinates(request.Latitude, request.Longitude);
				}

				var attendance = existingAttendance ?? new Attendance
				{
					UserId = userId,
					WorkDate = today,
					CreatedAt = serverNow
				};

				attendance.CheckInTime = serverNow;

				// ✅ CHỈ LƯU GPS NẾU CÓ
				if (hasGPS)
				{
					attendance.CheckInLatitude = request.Latitude;
					attendance.CheckInLongitude = request.Longitude;
					attendance.CheckInAddress = address;
					attendance.IsWithinGeofence = true;
				}
				else
				{
					attendance.CheckInLatitude = null;
					attendance.CheckInLongitude = null;
					attendance.CheckInAddress = "Không có GPS";
					attendance.IsWithinGeofence = null;
				}

				attendance.CheckInPhotos = photoPath;
				attendance.CheckInNotes = request.Notes;
				attendance.CheckInIpaddress = HttpContext.Connection.RemoteIpAddress?.ToString();
				attendance.IsLate = isLate;
				attendance.TotalHours = 0;

				if (existingAttendance == null)
					_context.Attendances.Add(attendance);

				await _context.SaveChangesAsync();

				var user = await _context.Users.FindAsync(userId);

				try
				{
					await _telegramService.SendCheckInNotificationAsync(
						user?.FullName ?? "Admin",
						user?.Username ?? "admin",
						serverNow,
						address,
						isLate,
						request.Notes
					);
				}
				catch (Exception ex)
				{
					Console.WriteLine($"Telegram notification failed: {ex.Message}");
				}

				await _auditHelper.LogDetailedAsync(
					userId,
					"CHECK_IN",
					"Attendance",
					attendance.AttendanceId,
					null,
					new
					{
						CheckInTime = serverNow.ToString("HH:mm:ss"),
						Address = address,
						IsLate = isLate,
						HasPhoto = photoPath != null,
						HasGPS = hasGPS
					},
					$"Admin check-in tại {address}",
					new Dictionary<string, object> {
		{ "CheckInTime", serverNow.ToString("HH:mm:ss") },
		{ "StandardTime", standardStartTime.ToString("HH:mm") },
		{ "IsLate", isLate }
					}
				);

				return Json(new
				{
					success = true,
					message = $"✅ Check-in thành công!\n⏰ Thời gian: {serverNow:HH:mm:ss}\n📍 Vị trí: {address}" +
							  (isLate ? $"\n⚠️ Ghi nhận: Đến sau {lateThreshold:HH:mm}" : "\n✨ Đúng giờ!") +
							  (photoPath == null ? "\n📷 Không có ảnh check-in" : ""),
					serverTime = serverNow.ToString("yyyy-MM-dd HH:mm:ss"),
					checkInTime = serverNow.ToString("HH:mm:ss"),
					address = address,
					isLate = isLate,
					hasPhoto = photoPath != null,
					hasGPS = hasGPS
				});
			}
			catch (Exception ex)
			{
				await _auditHelper.LogFailedAttemptAsync(
					userId,
					"CHECK_IN",
					"Attendance",
					$"Exception: {ex.Message}",
					new { Error = ex.ToString() }
				);
				return Json(new { success = false, message = $"Có lỗi xảy ra: {ex.Message}" });
			}
		}

		[HttpPost]
		[RequestSizeLimit(10_485_760)]
		public async System.Threading.Tasks.Task<IActionResult> CheckOut([FromForm] CheckOutRequest request)
		{
			if (!IsAdmin())
				return Json(new { success = false, message = "Chỉ Admin mới có quyền" });

			var userId = HttpContext.Session.GetInt32("UserId").Value;
			var serverNow = GetVietnamTime();
			var today = DateOnly.FromDateTime(serverNow);

			var attendance = await _context.Attendances
				.FirstOrDefaultAsync(a => a.UserId == userId && a.WorkDate == today);

			if (attendance == null || !attendance.CheckInTime.HasValue)
				return Json(new { success = false, message = "Bạn chưa check-in hôm nay" });

			if (attendance.CheckOutTime.HasValue)
				return Json(new { success = false, message = "Bạn đã check-out hôm nay rồi!", isCompleted = true });

			// ✅ ẢNH TÙY CHỌN
			string photoPath = null;
			if (request.Photo != null && request.Photo.Length > 0)
			{
				if (request.Photo.Length > 10 * 1024 * 1024)
					return Json(new { success = false, message = "Kích thước ảnh không được vượt quá 10MB" });

				var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
				var extension = Path.GetExtension(request.Photo.FileName).ToLower();
				if (!allowedExtensions.Contains(extension))
					return Json(new { success = false, message = "Chỉ chấp nhận file ảnh định dạng JPG, JPEG, PNG" });

				try
				{
					var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "attendance");
					if (!Directory.Exists(uploadsFolder))
						Directory.CreateDirectory(uploadsFolder);

					var uniqueFileName = $"{userId}_{serverNow:yyyyMMdd_HHmmss}_checkout{extension}";
					var filePath = Path.Combine(uploadsFolder, uniqueFileName);

					using (var fileStream = new FileStream(filePath, FileMode.Create))
						await request.Photo.CopyToAsync(fileStream);

					photoPath = $"/uploads/attendance/{uniqueFileName}";
				}
				catch (Exception ex)
				{
					Console.WriteLine($"Photo upload failed: {ex.Message}");
				}
			}

			// ✅ GPS TÙY CHỌN
			bool hasGPS = request.Latitude != 0 && request.Longitude != 0;

			try
			{
				var configs = await _context.SystemSettings
					.Where(c => c.IsActive == true && c.IsEnabled == true)
					.ToDictionaryAsync(c => c.SettingKey, c => c.SettingValue);

				var standardEndTimeStr = configs.GetValueOrDefault("CHECK_OUT_STANDARD_TIME") ?? "22:00";
				if (!TimeOnly.TryParse(standardEndTimeStr, out var standardEndTime))
					standardEndTime = TimeOnly.Parse("22:00");

				// ✅ LẤY ĐỊA CHỈ NẾU CÓ GPS
				string address = "Không có GPS";
				if (hasGPS)
					address = await GetAddressFromCoordinates(request.Latitude, request.Longitude);

				var duration = serverNow - attendance.CheckInTime.Value;
				var totalHours = (decimal)duration.TotalHours;

				var checkOutTime = new TimeOnly(serverNow.Hour, serverNow.Minute, serverNow.Second);
				bool isEarlyCheckout = checkOutTime < standardEndTime;

				decimal penaltyHours = 0;
				if (isEarlyCheckout)
				{
					var missedTime = standardEndTime - checkOutTime;
					penaltyHours = (decimal)missedTime.TotalHours;
				}

				// ============================================
				// ✅ LOGIC CUỐI TUẦN - TỰ ĐỘNG TÍNH OT (KHÔNG CẦN DUYỆT)
				// ============================================
				var dayOfWeek = serverNow.DayOfWeek;
				bool isSaturday = dayOfWeek == DayOfWeek.Saturday;
				bool isSunday = dayOfWeek == DayOfWeek.Sunday;

				decimal overtimeHours = 0;
				decimal regularHours = totalHours;
				string overtimeNote = "";
				bool isWeekendOvertime = false;

				if (isSunday)
				{
					// ✅ CHỦ NHẬT: TOÀN BỘ LÀ OT
					overtimeHours = totalHours;
					regularHours = 0;
					overtimeNote = "[Chủ nhật - Toàn bộ tính OT]";
					isWeekendOvertime = true;
				}
				else if (isSaturday)
				{
					// ✅ THỨ 7: CHIỀU LÀ OT (sau 12:00)
					var noon = new TimeOnly(12, 0);
					var checkInTime = new TimeOnly(
						attendance.CheckInTime.Value.Hour,
						attendance.CheckInTime.Value.Minute,
						attendance.CheckInTime.Value.Second
					);

					if (checkOutTime > noon)
					{
						if (checkInTime < noon)
						{
							// Check-in trước 12:00, check-out sau 12:00
							var morningDuration = noon.ToTimeSpan() - checkInTime.ToTimeSpan();
							var afternoonDuration = checkOutTime.ToTimeSpan() - noon.ToTimeSpan();

							regularHours = (decimal)morningDuration.TotalHours;
							overtimeHours = (decimal)afternoonDuration.TotalHours;
							overtimeNote = $"[Thứ 7 - Sáng: {regularHours:F2}h, Chiều OT: {overtimeHours:F2}h]";
							isWeekendOvertime = true;
						}
						else
						{
							// Check-in sau 12:00 → toàn bộ là OT
							overtimeHours = totalHours;
							regularHours = 0;
							overtimeNote = "[Thứ 7 chiều - Toàn bộ tính OT]";
							isWeekendOvertime = true;
						}
					}
					else
					{
						// Check-out trước 12:00 → không có OT
						regularHours = totalHours;
						overtimeHours = 0;
						overtimeNote = "[Thứ 7 sáng - Không tính OT]";
					}
				}

				// ✅ CẬP NHẬT ATTENDANCE
				attendance.CheckOutTime = serverNow;

				if (hasGPS)
				{
					attendance.CheckOutLatitude = request.Latitude;
					attendance.CheckOutLongitude = request.Longitude;
					attendance.CheckOutAddress = address;
				}
				else
				{
					attendance.CheckOutLatitude = null;
					attendance.CheckOutLongitude = null;
					attendance.CheckOutAddress = "Không có GPS";
				}

				attendance.CheckOutPhotos = photoPath;
				attendance.CheckOutIpaddress = HttpContext.Connection.RemoteIpAddress?.ToString();

				// ✅ LƯU TỔNG GIỜ VÀ GIỜ CHÍNH THỨC
				attendance.TotalHours = totalHours;
				attendance.ActualWorkHours = regularHours;

				// ✅ TỰ ĐỘNG GHI NHẬN OT (KHÔNG CẦN REQUEST)
				if (isWeekendOvertime && overtimeHours > 0)
				{
					attendance.ApprovedOvertimeHours = overtimeHours;  // ✅ TỰ ĐỘNG DUYỆT
					attendance.IsOvertimeApproved = true;
				}

				// ✅ GHI CHÚ
				var notes = new List<string>();
				if (!string.IsNullOrEmpty(request.Notes))
					notes.Add(request.Notes);

				if (isEarlyCheckout && !isWeekendOvertime)
					notes.Add($"Checkout sớm {penaltyHours:F2}h");

				if (!string.IsNullOrEmpty(overtimeNote))
					notes.Add(overtimeNote);

				attendance.CheckOutNotes = string.Join(" | ", notes.Where(n => !string.IsNullOrEmpty(n)));

				await _context.SaveChangesAsync();

				var user = await _context.Users.FindAsync(userId);

				// ✅ GỬI TELEGRAM
				try
				{
					await _telegramService.SendCheckOutNotificationAsync(
						user?.FullName ?? "Admin",
						user?.Username ?? "admin",
						serverNow,
						totalHours,
						overtimeHours,
						attendance.CheckOutNotes
					);
				}
				catch (Exception ex)
				{
					Console.WriteLine($"Telegram notification failed: {ex.Message}");
				}

				// ✅ LOG AUDIT
				await _auditHelper.LogDetailedAsync(
					userId, "CHECK_OUT", "Attendance", attendance.AttendanceId,
					null, new
					{
						CheckOutTime = serverNow.ToString("HH:mm:ss"),
						TotalHours = $"{totalHours:F2}h",
						RegularHours = $"{regularHours:F2}h",
						OvertimeHours = $"{overtimeHours:F2}h",
						Address = address,
						IsEarlyCheckout = isEarlyCheckout,
						HasPhoto = photoPath != null,
						HasGPS = hasGPS,
						IsWeekendOvertime = isWeekendOvertime,
						DayOfWeek = dayOfWeek.ToString()
					},
					$"Admin check-out - Total: {totalHours:F2}h | Regular: {regularHours:F2}h | OT: {overtimeHours:F2}h",
					new Dictionary<string, object> {
		{ "TotalHours", $"{totalHours:F2}h" },
		{ "RegularHours", $"{regularHours:F2}h" },
		{ "OvertimeHours", $"{overtimeHours:F2}h" },
		{ "IsWeekend", isWeekendOvertime }
					}
				);

				// ✅ MESSAGE TRẢ VỀ
				string message = $"✅ Check-out thành công!\n⏰ Thời gian: {serverNow:HH:mm:ss}\n⏱️ Tổng giờ làm: {totalHours:F2}h\n📍 Vị trí: {address}";

				if (isWeekendOvertime)
				{
					message += $"\n\n💰 TĂNG CA CUỐI TUẦN";
					message += $"\n• Giờ chính thức: {regularHours:F2}h";
					message += $"\n• Giờ tăng ca: {overtimeHours:F2}h";
					message += $"\n• Lý do: {(isSunday ? "Làm Chủ nhật" : "Làm Thứ 7 chiều")}";
					message += $"\n• ✅ Tự động ghi nhận (Không cần duyệt)";
				}
				else if (isEarlyCheckout)
				{
					message += $"\n\n⚠️ Lưu ý: Checkout sớm {penaltyHours:F2}h so với chuẩn ({standardEndTime:HH:mm})";
				}
				else
				{
					message += "\n\n🎉 Chúc bạn một buổi tối vui vẻ!";
				}

				if (photoPath == null)
					message += "\n📷 Không có ảnh check-out";

				return Json(new
				{
					success = true,
					message = message,
					totalHours = totalHours,
					regularHours = regularHours,
					overtimeHours = overtimeHours,
					totalHoursFormatted = $"{totalHours:F2}h",
					serverTime = serverNow.ToString("yyyy-MM-dd HH:mm:ss"),
					checkOutTime = serverNow.ToString("HH:mm:ss"),
					address = address,
					isEarlyCheckout = isEarlyCheckout,
					penaltyHours = penaltyHours,
					standardEndTime = standardEndTime.ToString("HH:mm"),
					hasPhoto = photoPath != null,
					hasGPS = hasGPS,
					isWeekendOvertime = isWeekendOvertime,
					overtimeAutoApproved = isWeekendOvertime && overtimeHours > 0
				});
			}
			catch (Exception ex)
			{
				await _auditHelper.LogFailedAttemptAsync(
					userId, "CHECK_OUT", "Attendance",
					$"Exception: {ex.Message}",
					new { Error = ex.ToString() }
				);
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
				return Json(new { success = false, message = "Phiên đăng nhập hết hạn" });

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
		// REQUEST MODELS
		// ============================================
		public class CheckInRequest
		{
			public decimal Latitude { get; set; }
			public decimal Longitude { get; set; }
			public string? Notes { get; set; }
			public IFormFile Photo { get; set; }
		}

		public class CheckOutRequest
		{
			public decimal Latitude { get; set; }
			public decimal Longitude { get; set; }
			public string? Notes { get; set; }
			public IFormFile Photo { get; set; }
		}
	}
}