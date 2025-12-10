using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TMD.Models;

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

		private Dictionary<string, TimeSpan> _todaySchedules = new Dictionary<string, TimeSpan>();
		private DateTime _currentDate = DateTime.MinValue;

		public AutoCheckInService(IServiceProvider serviceProvider, ILogger<AutoCheckInService> logger)
		{
			_serviceProvider = serviceProvider;
			_logger = logger;
		}

		protected override async System.Threading.Tasks.Task ExecuteAsync(CancellationToken stoppingToken)
		{
			_logger.LogInformation("🚀 Auto Check-in Service started.");

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

					// 2. Chỉ chạy trong khung 07:00 - 09:05 sáng
					if (now.Hour >= 7 && now.Hour <= 9)
					{
						await ProcessAutoCheckIn(now);
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
			_todaySchedules.Clear();
			var random = new Random();

			foreach (var name in _vipNames)
			{
				// Mặc định random từ 7h-9h
				int hour = random.Next(7, 9);
				int minute = random.Next(0, 60);
				if (hour == 9) minute = 0;

				// Nếu muốn fix cứng giờ cho từng người thì mở đoạn này ra:
				/*
                if (name.ToUpper().Contains("TÀI")) { hour = 8; minute = 29; }
                else if (name.ToUpper().Contains("PHÚ")) { hour = 8; minute = 51; }
                else if (name.ToUpper().Contains("THIỆN")) { hour = 8; minute = 54; }
                */

				TimeSpan timeToCheckIn = new TimeSpan(hour, minute, 0);
				_todaySchedules[name] = timeToCheckIn;

				_logger.LogInformation($"📅 Lên lịch check-in hôm nay: {name} lúc {timeToCheckIn}");
			}
		}

		private async System.Threading.Tasks.Task ProcessAutoCheckIn(DateTime now)
		{
			using (var scope = _serviceProvider.CreateScope())
			{
				var context = scope.ServiceProvider.GetRequiredService<AihubSystemContext>();

				foreach (var kvp in _todaySchedules)
				{
					var userName = kvp.Key;
					var scheduledTime = kvp.Value;

					// So sánh giờ hiện tại với giờ hẹn (tính theo TimeSpan trong ngày)
					if (now.TimeOfDay >= scheduledTime)
					{
						var user = await context.Users
							.FirstOrDefaultAsync(u => u.FullName.ToUpper() == userName);

						if (user != null)
						{
							// SỬA LỖI 1: Dùng DateOnly.FromDateTime cho WorkDate
							var todayDateOnly = DateOnly.FromDateTime(now);

							// SỬA LỖI 2: Property tên là WorkDate, không phải Date
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

									// SỬA LỖI 3: CheckInTime kiểu DateTime, cần cộng ngày + giờ
									CheckInTime = now.Date + scheduledTime,

									// SỬA LỖI 4: Tên property Latitude/Longitude
									CheckInLatitude = 10.7769m,
									CheckInLongitude = 106.7009m,
									CheckInAddress = "Auto Check-in System (HCM)",

									// SỬA LỖI 5: Tên property Photos/Notes
									CheckInPhotos = "auto_bot.jpg",
									CheckInNotes = "Auto Check-in System",

									// Các trường mặc định khác
									IsWithinGeofence = true,
									TotalHours = 0,
									IsLate = false // Giả sử auto là đúng giờ
								};

								context.Attendances.Add(attendance);
								await context.SaveChangesAsync();

								_logger.LogInformation($"✅ Auto Check-in thành công: {user.FullName} lúc {scheduledTime}");
							}
						}
					}
				}
			}
		}
	}
}