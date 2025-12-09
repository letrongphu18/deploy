using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Drawing;
using TMD.Models;

namespace TMD.Controllers
{
	[Route("api/[controller]")]
	[ApiController]
	public class KPIController : ControllerBase
	{
		private readonly AihubSystemContext _context;

		public KPIController(AihubSystemContext context)
		{
			_context = context;
		}

		// ==================== KPI CÁ NHÂN ====================

		[HttpGet("user/{userId}")]
		public async Task<IActionResult> GetUserKPI(int userId, DateTime? startDate = null, DateTime? endDate = null)
		{
			try
			{
				endDate ??= DateTime.Now;
				startDate ??= endDate.Value.AddMonths(-1);

				var startDateOnly = DateOnly.FromDateTime(startDate.Value);
				var endDateOnly = DateOnly.FromDateTime(endDate.Value);

				var attendances = await _context.Attendances
					.Where(a => a.UserId == userId && a.WorkDate >= startDateOnly && a.WorkDate <= endDateOnly)
					.ToListAsync();

				var tasks = await _context.UserTasks
					.Include(ut => ut.Task)
					.Where(ut => ut.UserId == userId)
					.ToListAsync();

				var leaveRequests = await _context.LeaveRequests
					.Where(lr => lr.UserId == userId && lr.StartDate >= startDateOnly && lr.EndDate <= endDateOnly)
					.ToListAsync();

				var kpi = new
				{
					UserId = userId,
					Period = new { StartDate = startDate, EndDate = endDate },

					// Chấm công
					Attendance = new
					{
						TotalDays = attendances.Count,
						LateDays = attendances.Count(a => a.IsLate == true),
						LateRate = attendances.Count > 0 ? (double)attendances.Count(a => a.IsLate == true) / attendances.Count * 100 : 0,
						TotalWorkHours = attendances.Sum(a => a.TotalHours ?? 0),
						AverageWorkHours = attendances.Count > 0 ? attendances.Average(a => a.TotalHours ?? 0) : 0,
						OvertimeHours = (double)attendances.Sum(a => a.ApprovedOvertimeHours),
						TotalDeduction = attendances.Sum(a => a.DeductionAmount)
					},

					// Nhiệm vụ
					Tasks = new
					{
						TotalAssigned = tasks.Count,
						Completed = tasks.Count(t => t.Status == "Completed" || t.Status == "Done"),
						InProgress = tasks.Count(t => t.Status == "InProgress"),
						TODO = tasks.Count(t => t.Status == "TODO"),
						CompletionRate = tasks.Count > 0 ? (double)tasks.Count(t => t.Status == "Completed" || t.Status == "Done") / tasks.Count * 100 : 0
					},

					// Nghỉ phép
					Leave = new
					{
						TotalRequests = leaveRequests.Count,
						ApprovedDays = leaveRequests.Where(lr => lr.Status == "Approved").Sum(lr => lr.TotalDays),
						PendingDays = leaveRequests.Where(lr => lr.Status == "Pending").Sum(lr => lr.TotalDays),
						RejectedDays = leaveRequests.Where(lr => lr.Status == "Rejected").Sum(lr => lr.TotalDays)
					},

					// Điểm KPI tổng hợp (0-100)
					OverallScore = CalculateUserKPIScore(attendances, tasks, leaveRequests)
				};

				return Ok(kpi);
			}
			catch (Exception ex)
			{
				return StatusCode(500, new { error = ex.Message });
			}
		}

		// ==================== KPI PHÒNG BAN ====================

		[HttpGet("department/{departmentId}")]
		public async Task<IActionResult> GetDepartmentKPI(int departmentId, DateTime? startDate = null, DateTime? endDate = null)
		{
			try
			{
				endDate ??= DateTime.Now;
				startDate ??= endDate.Value.AddMonths(-1);

				var startDateOnly = DateOnly.FromDateTime(startDate.Value);
				var endDateOnly = DateOnly.FromDateTime(endDate.Value);

				var users = await _context.Users
					.Where(u => u.DepartmentId == departmentId && u.IsActive == true)
					.Select(u => u.UserId)
					.ToListAsync();

				var attendances = await _context.Attendances
					.Where(a => users.Contains(a.UserId) && a.WorkDate >= startDateOnly && a.WorkDate <= endDateOnly)
					.ToListAsync();

				var tasks = await _context.UserTasks
					.Include(ut => ut.Task)
					.Where(ut => users.Contains(ut.UserId))
					.ToListAsync();

				var kpi = new
				{
					DepartmentId = departmentId,
					Period = new { StartDate = startDate, EndDate = endDate },
					TotalEmployees = users.Count,

					Attendance = new
					{
						TotalAttendances = attendances.Count,
						AverageLateRate = users.Count > 0 ? attendances.Count(a => a.IsLate == true) / (double)attendances.Count * 100 : 0,
						AverageWorkHours = attendances.Count > 0 ? attendances.Average(a => a.TotalHours ?? 0) : 0,
						TotalOvertimeHours = (double)attendances.Sum(a => a.ApprovedOvertimeHours)
					},

					Tasks = new
					{
						TotalAssigned = tasks.Count,
						CompletionRate = tasks.Count > 0 ? (double)tasks.Count(t => t.Status == "Completed" || t.Status == "Done") / tasks.Count * 100 : 0,
						AverageTaskPerPerson = users.Count > 0 ? (double)tasks.Count / users.Count : 0
					},

					Productivity = new
					{
						Score = CalculateDepartmentProductivity(attendances, tasks, users.Count)
					}
				};

				return Ok(kpi);
			}
			catch (Exception ex)
			{
				return StatusCode(500, new { error = ex.Message });
			}
		}

		// ==================== SO SÁNH PHÒNG BAN ====================

		[HttpGet("department-comparison")]
		public async Task<IActionResult> GetDepartmentComparison(DateTime? startDate = null, DateTime? endDate = null)
		{
			try
			{
				endDate ??= DateTime.Now;
				startDate ??= endDate.Value.AddMonths(-1);

				var startDateOnly = DateOnly.FromDateTime(startDate.Value);
				var endDateOnly = DateOnly.FromDateTime(endDate.Value);

				var departments = await _context.Departments
					.Where(d => d.IsActive == true)
					.ToListAsync();

				var result = new List<object>();

				foreach (var dept in departments)
				{
					var users = await _context.Users
						.Where(u => u.DepartmentId == dept.DepartmentId && u.IsActive == true)
						.Select(u => u.UserId)
						.ToListAsync();

					if (users.Count == 0)
					{
						result.Add(new
						{
							DepartmentId = dept.DepartmentId,
							DepartmentName = dept.DepartmentName,
							TotalEmployees = 0,
							TotalAttendances = 0,
							LateRate = 0.0,
							AvgHoursPerDay = 0.0,
							TotalTasks = 0,
							CompletedTasks = 0,
							TaskCompletionRate = 0.0,
							DepartmentKPIScore = 0.0
						});
						continue;
					}

					var attendances = await _context.Attendances
						.Where(a => users.Contains(a.UserId) && a.WorkDate >= startDateOnly && a.WorkDate <= endDateOnly)
						.ToListAsync();

					var tasks = await _context.UserTasks
						.Include(ut => ut.Task)
						.Where(ut => users.Contains(ut.UserId))
						.ToListAsync();

					var lateCount = attendances.Count(a => a.IsLate == true);
					var lateRate = attendances.Count > 0 ? (double)lateCount / attendances.Count * 100 : 0;
					var avgHours = attendances.Count > 0 ? attendances.Average(a => a.TotalHours ?? 0) : 0;
					var completedTasks = tasks.Count(t => t.Status == "Completed" || t.Status == "Done");
					var taskRate = tasks.Count > 0 ? (double)completedTasks / tasks.Count * 100 : 0;

					double deptScore = 100;
					deptScore -= Math.Min(lateRate, 30);
					deptScore -= (1 - (taskRate / 100)) * 40;
					deptScore = Math.Max(0, Math.Min(100, deptScore));

					result.Add(new
					{
						DepartmentId = dept.DepartmentId,
						DepartmentName = dept.DepartmentName,
						TotalEmployees = users.Count,
						TotalAttendances = attendances.Count,
						LateRate = Math.Round(lateRate, 1),
						AvgHoursPerDay = Math.Round(avgHours, 1),
						TotalTasks = tasks.Count,
						CompletedTasks = completedTasks,
						TaskCompletionRate = Math.Round(taskRate, 1),
						DepartmentKPIScore = Math.Round(deptScore, 1)
					});
				}

				return Ok(result);
			}
			catch (Exception ex)
			{
				return StatusCode(500, new { error = ex.Message, stack = ex.StackTrace });
			}
		}

		// Continued in next file...// ==================== KPI THEO THỜI GIAN ====================

		[HttpGet("timeline")]
		public async Task<IActionResult> GetTimelineKPI(DateTime startDate, DateTime endDate, string groupBy = "day")
		{
			try
			{
				var startDateOnly = DateOnly.FromDateTime(startDate.Date);
				var endDateOnly = DateOnly.FromDateTime(endDate.Date);

				var attendances = await _context.Attendances
					.Where(a => a.WorkDate >= startDateOnly && a.WorkDate <= endDateOnly)
					.ToListAsync();

				object grouped = groupBy.ToLower() switch
				{
					"day" => attendances.GroupBy(a => a.WorkDate).Select(g => new
					{
						Date = g.Key,
						TotalAttendances = g.Count(),
						LateDays = g.Count(a => a.IsLate == true),
						LateRate = g.Count() > 0 ? (double)g.Count(a => a.IsLate == true) / g.Count() * 100 : 0,
						TotalHours = g.Sum(a => a.TotalHours ?? 0),
						OvertimeHours = (double)g.Sum(a => a.ApprovedOvertimeHours)
					}).OrderBy(x => x.Date).ToList(),

					"week" => attendances.GroupBy(a => new
					{
						Year = a.WorkDate.Year,
						Week = System.Globalization.CultureInfo.CurrentCulture.Calendar.GetWeekOfYear(
							a.WorkDate.ToDateTime(TimeOnly.MinValue),
							System.Globalization.CalendarWeekRule.FirstDay,
							DayOfWeek.Monday)
					}).Select(g => new
					{
						Year = g.Key.Year,
						Week = g.Key.Week,
						TotalAttendances = g.Count(),
						LateDays = g.Count(a => a.IsLate == true),
						LateRate = g.Count() > 0 ? (double)g.Count(a => a.IsLate == true) / g.Count() * 100 : 0,
						TotalHours = g.Sum(a => a.TotalHours ?? 0),
						OvertimeHours = (double)g.Sum(a => a.ApprovedOvertimeHours)
					}).OrderBy(x => x.Year).ThenBy(x => x.Week).ToList(),

					"month" => attendances.GroupBy(a => new { a.WorkDate.Year, a.WorkDate.Month })
						.Select(g => new
						{
							Year = g.Key.Year,
							Month = g.Key.Month,
							TotalAttendances = g.Count(),
							LateDays = g.Count(a => a.IsLate == true),
							LateRate = g.Count() > 0 ? (double)g.Count(a => a.IsLate == true) / g.Count() * 100 : 0,
							TotalHours = g.Sum(a => a.TotalHours ?? 0),
							OvertimeHours = (double)g.Sum(a => a.ApprovedOvertimeHours)
						}).OrderBy(x => x.Year).ThenBy(x => x.Month).ToList(),

					_ => throw new ArgumentException("Invalid groupBy parameter")
				};

				return Ok(grouped);
			}
			catch (Exception ex)
			{
				return StatusCode(500, new { error = ex.Message });
			}
		}

		// ==================== KPI TỔNG QUAN ====================

		[HttpGet("overview")]
		public async Task<IActionResult> GetSystemOverview(DateTime? date = null)
		{
			try
			{
				date ??= DateTime.Now.Date;
				var dateOnly = DateOnly.FromDateTime(date.Value);

				var totalUsers = await _context.Users.CountAsync(u => u.IsActive == true);
				var todayAttendances = await _context.Attendances.CountAsync(a => a.WorkDate == dateOnly);
				var activeTasks = await _context.Tasks.CountAsync(t => t.IsActive == true);
				var pendingRequests = await _context.LeaveRequests.CountAsync(lr => lr.Status == "Pending")
					+ await _context.LateRequests.CountAsync(lr => lr.Status == "Pending")
					+ await _context.OvertimeRequests.CountAsync(or => or.Status == "Pending" && or.IsExpired == false);

				var overview = new
				{
					Date = date,
					System = new
					{
						TotalUsers = totalUsers,
						AttendanceRate = totalUsers > 0 ? (double)todayAttendances / totalUsers * 100 : 0,
						ActiveTasks = activeTasks,
						PendingRequests = pendingRequests
					},
					MonthlyStats = await GetMonthlyStats(date.Value),
					TopPerformers = await GetTopPerformers(date.Value.AddMonths(-1), date.Value, 5)
				};

				return Ok(overview);
			}
			catch (Exception ex)
			{
				return StatusCode(500, new { error = ex.Message });
			}
		}

		// ==================== EXCEL EXPORT ====================

		[HttpGet("export/user/{userId}")]
		public async Task<IActionResult> ExportUserKPIToExcel(int userId, DateTime? startDate = null, DateTime? endDate = null)
		{
			endDate ??= DateTime.Now;
			startDate ??= endDate.Value.AddMonths(-1);

			var startDateOnly = DateOnly.FromDateTime(startDate.Value);
			var endDateOnly = DateOnly.FromDateTime(endDate.Value);

			var user = await _context.Users.Include(u => u.Department).FirstOrDefaultAsync(u => u.UserId == userId);
			if (user == null) return NotFound("User not found");

			var attendances = await _context.Attendances
				.Where(a => a.UserId == userId && a.WorkDate >= startDateOnly && a.WorkDate <= endDateOnly)
				.OrderBy(a => a.WorkDate).ToListAsync();

			var tasks = await _context.UserTasks.Include(ut => ut.Task).Where(ut => ut.UserId == userId).ToListAsync();

			ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
			using var package = new ExcelPackage();

			var summarySheet = package.Workbook.Worksheets.Add("Tổng quan KPI");
			CreateSummarySheet(summarySheet, user, attendances, tasks, startDate.Value, endDate.Value);

			var attendanceSheet = package.Workbook.Worksheets.Add("Chi tiết chấm công");
			CreateAttendanceSheet(attendanceSheet, attendances);

			var taskSheet = package.Workbook.Worksheets.Add("Chi tiết nhiệm vụ");
			CreateTaskSheet(taskSheet, tasks);

			var stream = new MemoryStream(package.GetAsByteArray());
			var fileName = $"KPI_{user.FullName}_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.xlsx";

			return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
		}

		[HttpGet("export/department/{departmentId}")]
		public async Task<IActionResult> ExportDepartmentKPIToExcel(int departmentId, DateTime? startDate = null, DateTime? endDate = null)
		{
			endDate ??= DateTime.Now;
			startDate ??= endDate.Value.AddMonths(-1);

			var startDateOnly = DateOnly.FromDateTime(startDate.Value);
			var endDateOnly = DateOnly.FromDateTime(endDate.Value);

			var department = await _context.Departments.FindAsync(departmentId);
			if (department == null) return NotFound("Department not found");

			var users = await _context.Users.Include(u => u.Department)
				.Where(u => u.DepartmentId == departmentId && u.IsActive == true).ToListAsync();

			var userIds = users.Select(u => u.UserId).ToList();

			var attendances = await _context.Attendances
				.Where(a => userIds.Contains(a.UserId) && a.WorkDate >= startDateOnly && a.WorkDate <= endDateOnly)
				.ToListAsync();

			var tasks = await _context.UserTasks.Include(ut => ut.Task).Include(ut => ut.User)
				.Where(ut => userIds.Contains(ut.UserId)).ToListAsync();

			ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
			using var package = new ExcelPackage();

			var employeeSheet = package.Workbook.Worksheets.Add("KPI Nhân viên");
			CreateEmployeeKPISheet(employeeSheet, users, attendances, tasks, startDate.Value, endDate.Value);

			var stream = new MemoryStream(package.GetAsByteArray());
			var fileName = $"KPI_PhongBan_{department.DepartmentName}_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.xlsx";

			return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
		}

		// ==================== API ENDPOINTS ====================

		[HttpGet("/api/users")]
		public async Task<IActionResult> GetAllUsersForKPI()
		{
			try
			{
				var users = await _context.Users.Include(u => u.Department)
					.Where(u => u.IsActive == true).OrderBy(u => u.FullName)
					.Select(u => new
					{
						userId = u.UserId,
						fullName = u.FullName,
						email = u.Email,
						departmentName = u.Department != null ? u.Department.DepartmentName : "N/A"
					}).ToListAsync();

				return Ok(users);
			}
			catch (Exception ex)
			{
				return BadRequest(new { error = ex.Message });
			}
		}

		[HttpGet("/api/departments")]
		public async Task<IActionResult> GetAllDepartmentsForKPI()
		{
			try
			{
				var departments = await _context.Departments.Where(d => d.IsActive == true)
					.OrderBy(d => d.DepartmentName)
					.Select(d => new { departmentId = d.DepartmentId, departmentName = d.DepartmentName })
					.ToListAsync();

				return Ok(departments);
			}
			catch (Exception ex)
			{
				return BadRequest(new { error = ex.Message });
			}
		}

		// ==================== HELPER METHODS ====================

		private static double CalculateUserKPIScore(List<Attendance> attendances, List<UserTask> tasks, List<LeaveRequest> leaves)
		{
			double score = 100;

			if (attendances.Count > 0)
			{
				var lateRate = (double)attendances.Count(a => a.IsLate == true) / attendances.Count;
				score -= Math.Min(lateRate * 100, 30);
			}

			if (tasks.Count > 0)
			{
				var completionRate = (double)tasks.Count(t => t.Status == "Completed" || t.Status == "Done") / tasks.Count;
				score -= (1 - completionRate) * 40;
			}

			var approvedLeaveDays = leaves.Where(l => l.Status == "Approved").Sum(l => l.TotalDays);
			if (approvedLeaveDays > 5)
			{
				score -= Math.Min((double)(approvedLeaveDays - 5) * 2, 20);
			}

			var overtimeHours = (double)attendances.Sum(a => a.ApprovedOvertimeHours);
			score += Math.Min(overtimeHours / 10, 10);

			return Math.Max(0, Math.Min(100, score));
		}

		private static double CalculateDepartmentProductivity(List<Attendance> attendances, List<UserTask> tasks, int employeeCount)
		{
			if (employeeCount == 0) return 0;

			var avgWorkHours = attendances.Count > 0 ? attendances.Average(a => a.TotalHours ?? 0) : 0;
			var taskCompletionRate = tasks.Count > 0 ? (double)tasks.Count(t => t.Status == "Completed" || t.Status == "Done") / tasks.Count * 100 : 0;
			var avgTaskPerPerson = (double)tasks.Count / employeeCount;

			return ((double)avgWorkHours / 8 * 40) + (taskCompletionRate * 0.5) + (avgTaskPerPerson * 2);
		}

		private async Task<object> GetMonthlyStats(DateTime date)
		{
			var startOfMonth = new DateTime(date.Year, date.Month, 1);
			var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1);

			var startDateOnly = DateOnly.FromDateTime(startOfMonth);
			var endDateOnly = DateOnly.FromDateTime(endOfMonth);

			var attendances = await _context.Attendances
				.Where(a => a.WorkDate >= startDateOnly && a.WorkDate <= endDateOnly)
				.ToListAsync();

			return new
			{
				TotalAttendances = attendances.Count,
				AverageLateRate = attendances.Count > 0 ? (double)attendances.Count(a => a.IsLate == true) / attendances.Count * 100 : 0,
				TotalWorkHours = attendances.Sum(a => a.TotalHours ?? 0),
				TotalOvertimeHours = (double)attendances.Sum(a => a.ApprovedOvertimeHours)
			};
		}

		private async Task<List<object>> GetTopPerformers(DateTime startDate, DateTime endDate, int top)
		{
			var startDateOnly = DateOnly.FromDateTime(startDate);
			var endDateOnly = DateOnly.FromDateTime(endDate);

			var users = await _context.Users.Where(u => u.IsActive == true).Include(u => u.Department).ToListAsync();
			var userScores = new List<(User user, double score)>();

			foreach (var user in users)
			{
				var attendances = await _context.Attendances
					.Where(a => a.UserId == user.UserId && a.WorkDate >= startDateOnly && a.WorkDate <= endDateOnly)
					.ToListAsync();

				var tasks = await _context.UserTasks.Where(ut => ut.UserId == user.UserId).ToListAsync();

				var leaves = await _context.LeaveRequests
					.Where(lr => lr.UserId == user.UserId && lr.StartDate >= startDateOnly && lr.EndDate <= endDateOnly)
					.ToListAsync();

				var score = CalculateUserKPIScore(attendances, tasks, leaves);
				userScores.Add((user, score));
			}

			return userScores.OrderByDescending(x => x.score).Take(top)
				.Select(x => new
				{
					UserId = x.user.UserId,
					FullName = x.user.FullName,
					Department = x.user.Department?.DepartmentName,
					KPIScore = Math.Round(x.score, 2)
				}).ToList<object>();
		}

		// ==================== EXCEL FORMATTING ====================

		private static void CreateSummarySheet(ExcelWorksheet sheet, User user, List<Attendance> attendances, List<UserTask> tasks, DateTime startDate, DateTime endDate)
		{
			sheet.Cells["A1"].Value = "BÁO CÁO KPI NHÂN VIÊN";
			sheet.Cells["A1:E1"].Merge = true;
			sheet.Cells["A1"].Style.Font.Size = 16;
			sheet.Cells["A1"].Style.Font.Bold = true;
			sheet.Cells["A1"].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

			sheet.Cells["A3"].Value = "Họ tên:";
			sheet.Cells["B3"].Value = user.FullName;
			sheet.Cells["A4"].Value = "Phòng ban:";
			sheet.Cells["B4"].Value = user.Department?.DepartmentName;
			sheet.Cells["A5"].Value = "Kỳ báo cáo:";
			sheet.Cells["B5"].Value = $"{startDate:dd/MM/yyyy} - {endDate:dd/MM/yyyy}";

			int row = 7;
			sheet.Cells[$"A{row}"].Value = "CHẤM CÔNG";
			sheet.Cells[$"A{row}"].Style.Font.Bold = true;

			row++;
			sheet.Cells[$"A{row}"].Value = "Tổng số ngày làm việc";
			sheet.Cells[$"B{row}"].Value = attendances.Count;

			row++;
			sheet.Cells[$"A{row}"].Value = "Số ngày đi muộn";
			sheet.Cells[$"B{row}"].Value = attendances.Count(a => a.IsLate == true);

			row++;
			sheet.Cells[$"A{row}"].Value = "Tỷ lệ đi muộn";
			sheet.Cells[$"B{row}"].Value = attendances.Count > 0 ? (double)attendances.Count(a => a.IsLate == true) / attendances.Count : 0;
			sheet.Cells[$"B{row}"].Style.Numberformat.Format = "0.00%";

			row++;
			sheet.Cells[$"A{row}"].Value = "Tổng giờ làm việc";
			sheet.Cells[$"B{row}"].Value = attendances.Sum(a => a.TotalHours ?? 0);

			row++;
			sheet.Cells[$"A{row}"].Value = "Giờ tăng ca";
			sheet.Cells[$"B{row}"].Value = (double)attendances.Sum(a => a.ApprovedOvertimeHours);

			row += 2;
			sheet.Cells[$"A{row}"].Value = "NHIỆM VỤ";
			sheet.Cells[$"A{row}"].Style.Font.Bold = true;

			row++;
			sheet.Cells[$"A{row}"].Value = "Tổng số task";
			sheet.Cells[$"B{row}"].Value = tasks.Count;

			row++;
			sheet.Cells[$"A{row}"].Value = "Task hoàn thành";
			sheet.Cells[$"B{row}"].Value = tasks.Count(t => t.Status == "Completed" || t.Status == "Done");

			row++;
			sheet.Cells[$"A{row}"].Value = "Tỷ lệ hoàn thành";
			sheet.Cells[$"B{row}"].Value = tasks.Count > 0 ? (double)tasks.Count(t => t.Status == "Completed" || t.Status == "Done") / tasks.Count : 0;
			sheet.Cells[$"B{row}"].Style.Numberformat.Format = "0.00%";

			sheet.Cells[sheet.Dimension.Address].AutoFitColumns();
		}

		private static void CreateAttendanceSheet(ExcelWorksheet sheet, List<Attendance> attendances)
		{
			sheet.Cells["A1"].Value = "Ngày";
			sheet.Cells["B1"].Value = "Giờ vào";
			sheet.Cells["C1"].Value = "Giờ ra";
			sheet.Cells["D1"].Value = "Tổng giờ";
			sheet.Cells["E1"].Value = "Đi muộn";
			sheet.Cells["F1"].Value = "Tăng ca";
			sheet.Cells["G1"].Value = "Khấu trừ";

			sheet.Cells["A1:G1"].Style.Font.Bold = true;
			sheet.Cells["A1:G1"].Style.Fill.PatternType = ExcelFillStyle.Solid;
			sheet.Cells["A1:G1"].Style.Fill.BackgroundColor.SetColor(Color.LightGray);

			int row = 2;
			foreach (var att in attendances)
			{
				sheet.Cells[$"A{row}"].Value = att.WorkDate.ToDateTime(TimeOnly.MinValue);
				sheet.Cells[$"A{row}"].Style.Numberformat.Format = "dd/MM/yyyy";
				sheet.Cells[$"B{row}"].Value = att.CheckInTime;
				sheet.Cells[$"B{row}"].Style.Numberformat.Format = "hh:mm:ss";
				sheet.Cells[$"C{row}"].Value = att.CheckOutTime;
				sheet.Cells[$"C{row}"].Style.Numberformat.Format = "hh:mm:ss";
				sheet.Cells[$"D{row}"].Value = att.TotalHours ?? 0;
				sheet.Cells[$"E{row}"].Value = att.IsLate == true ? "Có" : "Không";
				sheet.Cells[$"F{row}"].Value = (double)att.ApprovedOvertimeHours;
				sheet.Cells[$"G{row}"].Value = att.DeductionAmount;
				sheet.Cells[$"G{row}"].Style.Numberformat.Format = "#,##0";
				row++;
			}

			if (sheet.Dimension != null)
				sheet.Cells[sheet.Dimension.Address].AutoFitColumns();
		}

		private static void CreateTaskSheet(ExcelWorksheet sheet, List<UserTask> tasks)
		{
			sheet.Cells["A1"].Value = "Tên nhiệm vụ";
			sheet.Cells["B1"].Value = "Nền tảng";
			sheet.Cells["C1"].Value = "Trạng thái";
			sheet.Cells["D1"].Value = "Link báo cáo";

			sheet.Cells["A1:D1"].Style.Font.Bold = true;
			sheet.Cells["A1:D1"].Style.Fill.PatternType = ExcelFillStyle.Solid;
			sheet.Cells["A1:D1"].Style.Fill.BackgroundColor.SetColor(Color.LightGray);

			int row = 2;
			foreach (var task in tasks)
			{
				sheet.Cells[$"A{row}"].Value = task.Task?.TaskName ?? "";
				sheet.Cells[$"B{row}"].Value = task.Task?.Platform ?? "";
				sheet.Cells[$"C{row}"].Value = task.Status ?? "TODO";
				sheet.Cells[$"D{row}"].Value = task.ReportLink ?? "";
				row++;
			}

			if (sheet.Dimension != null)
				sheet.Cells[sheet.Dimension.Address].AutoFitColumns();
		}

		private static void CreateEmployeeKPISheet(ExcelWorksheet sheet, List<User> users, List<Attendance> attendances, List<UserTask> tasks, DateTime startDate, DateTime endDate)
		{
			sheet.Cells["A1"].Value = "Nhân viên";
			sheet.Cells["B1"].Value = "Phòng ban";
			sheet.Cells["C1"].Value = "Số ngày làm";
			sheet.Cells["D1"].Value = "Đi muộn";
			sheet.Cells["E1"].Value = "Tỷ lệ muộn";
			sheet.Cells["F1"].Value = "Tổng giờ";
			sheet.Cells["G1"].Value = "Task hoàn thành";
			sheet.Cells["H1"].Value = "Điểm KPI";

			sheet.Cells["A1:H1"].Style.Font.Bold = true;
			sheet.Cells["A1:H1"].Style.Fill.PatternType = ExcelFillStyle.Solid;
			sheet.Cells["A1:H1"].Style.Fill.BackgroundColor.SetColor(Color.LightGray);

			int row = 2;
			foreach (var user in users)
			{
				var userAtt = attendances.Where(a => a.UserId == user.UserId).ToList();
				var userTasks = tasks.Where(t => t.UserId == user.UserId).ToList();
				var userLeaves = new List<LeaveRequest>();

				sheet.Cells[$"A{row}"].Value = user.FullName;
				sheet.Cells[$"B{row}"].Value = user.Department?.DepartmentName;
				sheet.Cells[$"C{row}"].Value = userAtt.Count;
				sheet.Cells[$"D{row}"].Value = userAtt.Count(a => a.IsLate == true);
				sheet.Cells[$"E{row}"].Value = userAtt.Count > 0 ? (double)userAtt.Count(a => a.IsLate == true) / userAtt.Count : 0;
				sheet.Cells[$"E{row}"].Style.Numberformat.Format = "0.00%";
				sheet.Cells[$"F{row}"].Value = userAtt.Sum(a => a.TotalHours ?? 0);
				sheet.Cells[$"G{row}"].Value = userTasks.Count(t => t.Status == "Completed" || t.Status == "Done");
				sheet.Cells[$"H{row}"].Value = Math.Round(CalculateUserKPIScore(userAtt, userTasks, userLeaves), 2);
				row++;
			}

			if (sheet.Dimension != null)
				sheet.Cells[sheet.Dimension.Address].AutoFitColumns();
		}
	}
}