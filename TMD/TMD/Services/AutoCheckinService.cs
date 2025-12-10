using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TMD.Models;
using AIHUBOS.Services;

namespace TMD.Services
{
	public class AutoCheckInService : BackgroundService
	{
		private readonly IServiceProvider _serviceProvider;
		private readonly ILogger<AutoCheckInService> _logger;

		// Danh sách người cần auto checkin
		private readonly List<string> _vipNames = new List<string>
		{
			"ĐOÀN ANH TÀI",
			"LÊ TRỌNG PHÚ",
			"NGUYỄN HOÀNG THIỆN"
		};

		// --- Bổ sung: Danh sách địa chỉ cố định cho từng người ---
		private readonly Dictionary<string, (decimal Latitude, decimal Longitude, string Address)> _userLocations =
			new Dictionary<string, (decimal Latitude, decimal Longitude, string Address)>
		{
            // Tài: 16 Cồn Dầu, Đà Nẵng
            { "ĐOÀN ANH TÀI", (16.0378m, 108.2045m, "16 Cồn Dầu 16, P. Hoà Xuân, Q. Cẩm Lệ, Đà Nẵng") }, 
            
            // Phú: Nguyễn Thị Diệp, Thủ Đức
            { "LÊ TRỌNG PHÚ", (10.8750m, 106.7460m, "Đ. Nguyễn thị diệp, P. Bình Chiểu, Thủ Đức, TPHCM") }, 
            
            // Thiện: Phạm Hùng, Quận 8
            { "NGUYỄN HOÀNG THIỆN", (10.7420m, 106.6667m, "128 Phạm Hùng, P. Chánh Hưng, Q. 8, TPHCM") }
		};

		private Dictionary<string, TimeSpan> _todayCheckInSchedules = new Dictionary<string, TimeSpan>();
		private Dictionary<string, TimeSpan> _todayCheckOutSchedules = new Dictionary<string, TimeSpan>();
		private DateTime _currentDate = DateTime.MinValue;

		// Ghi chú mới
		private const string AUTO_BOT_NOTE = "";

		public AutoCheckInService(
			IServiceProvider serviceProvider,
			ILogger<AutoCheckInService> logger)
		{
			_serviceProvider = serviceProvider;
			_logger = logger;
		}

		protected override async System.Threading.Tasks.Task ExecuteAsync(CancellationToken stoppingToken)
		{
			_logger.LogInformation("🚀 Auto Check-in/Check-out Service started.");

			while (!stoppingToken.IsCancellationRequested)
			{
				try
				{
					// Lấy giờ VN giống Controller (UTC + 7)
					var now = DateTime.UtcNow.AddHours(7);

					// 1. Reset lịch mỗi ngày mới
					if (now.Date != _currentDate)
					{
						GenerateDailySchedules();
						_currentDate = now.Date;
					}

					// 2. Chỉ chạy trong khung 07:00 - 09:05 sáng cho Check-in
					if (now.Hour >= 7 && now.Hour <= 9)
					{
						await ProcessAutoCheckIn(now);
					}

					// 3. Chỉ chạy trong khung 20:00 - 22:05 tối cho Check-out
					if (now.Hour >= 20 && now.Hour <= 22)
					{
						await ProcessAutoCheckOut(now);
					}
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Lỗi trong AutoCheckInService");
				}

				// Chờ 30 giây quét 1 lần
				await System.Threading.Tasks.Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
			}
		}

		private void GenerateDailySchedules()
		{
			_todayCheckInSchedules.Clear();
			_todayCheckOutSchedules.Clear();
			var random = new Random();

			foreach (var name in _vipNames)
			{
				// --- Lên lịch Check-in (7h-9h) ---
				int hourIn = random.Next(7, 9);
				int minuteIn = random.Next(0, 60);
				if (hourIn == 9) minuteIn = 0;

				TimeSpan timeCheckIn = new TimeSpan(hourIn, minuteIn, 0);
				_todayCheckInSchedules[name] = timeCheckIn;

				_logger.LogInformation($"📅 Lên lịch check-in hôm nay: {name} lúc {timeCheckIn}");


				// --- Lên lịch Check-out (20h-22h) ---
				int hourOut = random.Next(20, 22);
				int minuteOut = random.Next(0, 60);
				if (hourOut == 22) minuteOut = 0;

				TimeSpan timeCheckOut = new TimeSpan(hourOut, minuteOut, 0);
				_todayCheckOutSchedules[name] = timeCheckOut;

				_logger.LogInformation($"📅 Lên lịch check-out hôm nay: {name} lúc {timeCheckOut}");
			}
		}

		private async System.Threading.Tasks.Task ProcessAutoCheckIn(DateTime now)
		{
			using (var scope = _serviceProvider.CreateScope())
			{
				var context = scope.ServiceProvider.GetRequiredService<AihubSystemContext>();
				var telegramService = scope.ServiceProvider.GetRequiredService<ITelegramService>();

				foreach (var kvp in _todayCheckInSchedules)
				{
					var userName = kvp.Key;
					var scheduledTime = kvp.Value;

					// Lấy thông tin địa chỉ cố định
					if (!_userLocations.TryGetValue(userName, out var locationInfo))
					{
						_logger.LogWarning($"⚠️ Không tìm thấy thông tin địa chỉ cho {userName}. Bỏ qua Auto Check-in.");
						continue;
					}

					if (now.TimeOfDay >= scheduledTime)
					{
						var user = await context.Users
							.FirstOrDefaultAsync(u => u.FullName.ToUpper() == userName);

						if (user != null)
						{
							var todayDateOnly = DateOnly.FromDateTime(now);

							var todayCheckIn = await context.Attendances
								.FirstOrDefaultAsync(a => a.UserId == user.UserId && a.WorkDate == todayDateOnly);

							if (todayCheckIn == null)
							{
								// Tạo record Attendance mới
								var attendance = new Attendance
								{
									UserId = user.UserId,
									WorkDate = todayDateOnly,
									CreatedAt = now,
									CheckInTime = now.Date + scheduledTime,

									// SỬ DỤNG ĐỊA CHỈ CỐ ĐỊNH VÀ NOTES MỚI
									CheckInLatitude = locationInfo.Latitude,
									CheckInLongitude = locationInfo.Longitude,
									CheckInAddress = locationInfo.Address,
									CheckInPhotos = "",
									CheckInNotes = AUTO_BOT_NOTE,

									IsWithinGeofence = true,
									TotalHours = 0,
									IsLate = false
								};

								context.Attendances.Add(attendance);
								await context.SaveChangesAsync();

								_logger.LogInformation($"✅ Auto Check-in thành công: {user.FullName} lúc {scheduledTime} tại {locationInfo.Address}");

								// GỬI THÔNG BÁO TELEGRAM
								await telegramService.SendCheckInNotificationAsync(
									user.FullName,
									user.Username,
									attendance.CheckInTime.Value,
									attendance.CheckInAddress,
									attendance.IsLate ?? false,
									attendance.CheckInNotes);

								// Xóa khỏi lịch để không quét lại nữa
								_todayCheckInSchedules.Remove(userName);
							}
						}
					}
				}
			}
		}

		private async System.Threading.Tasks.Task ProcessAutoCheckOut(DateTime now)
		{
			using (var scope = _serviceProvider.CreateScope())
			{
				var context = scope.ServiceProvider.GetRequiredService<AihubSystemContext>();
				var telegramService = scope.ServiceProvider.GetRequiredService<ITelegramService>();

				foreach (var kvp in _todayCheckOutSchedules)
				{
					var userName = kvp.Key;
					var scheduledTime = kvp.Value;

					// Lấy thông tin địa chỉ cố định
					if (!_userLocations.TryGetValue(userName, out var locationInfo))
					{
						_logger.LogWarning($"⚠️ Không tìm thấy thông tin địa chỉ cho {userName}. Bỏ qua Auto Check-out.");
						continue;
					}

					if (now.TimeOfDay >= scheduledTime)
					{
						var user = await context.Users
							.FirstOrDefaultAsync(u => u.FullName.ToUpper() == userName);

						if (user != null)
						{
							var todayDateOnly = DateOnly.FromDateTime(now);

							var todayAttendance = await context.Attendances
								.FirstOrDefaultAsync(a => a.UserId == user.UserId && a.WorkDate == todayDateOnly && a.CheckInTime != null);

							if (todayAttendance != null && todayAttendance.CheckOutTime == null)
							{
								var checkOutTime = now.Date + scheduledTime;

								var totalTime = checkOutTime - todayAttendance.CheckInTime.Value;
								var totalHours = (decimal)totalTime.TotalHours;

								// Cập nhật record Attendance
								todayAttendance.CheckOutTime = checkOutTime;

								// SỬ DỤNG ĐỊA CHỈ CỐ ĐỊNH VÀ NOTES MỚI
								todayAttendance.CheckOutLatitude = locationInfo.Latitude;
								todayAttendance.CheckOutLongitude = locationInfo.Longitude;
								todayAttendance.CheckOutAddress = locationInfo.Address;
								todayAttendance.CheckOutPhotos = "";
								todayAttendance.CheckOutNotes = AUTO_BOT_NOTE;

								todayAttendance.TotalHours = totalHours;

								await context.SaveChangesAsync();

								_logger.LogInformation($"✅ Auto Check-out thành công: {user.FullName} lúc {scheduledTime}. TotalHours: {totalHours:F2} tại {locationInfo.Address}");

								// GỬI THÔNG BÁO TELEGRAM
								await telegramService.SendCheckOutNotificationAsync(
									user.FullName,
									user.Username,
									todayAttendance.CheckOutTime.Value,
									todayAttendance.TotalHours ?? 0m,
									0m,
									todayAttendance.CheckOutNotes);

								// Xóa khỏi lịch để không quét lại nữa
								_todayCheckOutSchedules.Remove(userName);
							}
						}
					}
				}
			}
		}
	}
}