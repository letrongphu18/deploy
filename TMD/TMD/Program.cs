using Microsoft.EntityFrameworkCore;
using TMD.Models;
using AIHUBOS.Helpers;
using AIHUBOS.Services;
using AIHUBOS.Hubs;

var builder = WebApplication.CreateBuilder(args);

// ============================================
// 1. ADD SERVICES TO CONTAINER
// ============================================

// Controllers with Views
builder.Services.AddControllersWithViews();

// ============================================
// 📌 FIX: ĐÚNG CÁCH ĐĂNG KÝ DbContext VÀ Factory
// ============================================

// ✅ OPTION 1: Chỉ dùng DbContextFactory (RECOMMENDED)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContextFactory<AihubSystemContext>(options =>
{
	options.UseSqlServer(connectionString);
}, ServiceLifetime.Scoped); // ✅ QUAN TRỌNG: Scoped thay vì Singleton!

// ✅ OPTION 2: Nếu cần cả DbContext cho controllers
builder.Services.AddScoped<AihubSystemContext>(provider =>
{
	var factory = provider.GetRequiredService<IDbContextFactory<AihubSystemContext>>();
	return factory.CreateDbContext();
});

// ============================================
// 2. SESSION CONFIGURATION
// ============================================
builder.Services.AddSession(options =>
{
	options.IdleTimeout = TimeSpan.FromHours(8);
	options.Cookie.HttpOnly = true;
	options.Cookie.IsEssential = true;
});

// HttpContextAccessor
builder.Services.AddHttpContextAccessor();

// HttpClient
builder.Services.AddHttpClient();

// ============================================
// 3. DEPENDENCY INJECTION
// ============================================

// ✅ Helpers
builder.Services.AddScoped<AuditHelper>();

// ✅ Services
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddHostedService<AutoRejectRequestsService>();

// ✅ SignalR
builder.Services.AddSignalR();

// ✅ Logging
builder.Services.AddLogging(config =>
{
	config.AddConsole();
	config.AddDebug();
	config.SetMinimumLevel(LogLevel.Information);
});

// ============================================
// 4. FILE UPLOAD SIZE LIMITS (10MB)
// ============================================

builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
	options.MultipartBodyLengthLimit = 10_485_760;
	options.ValueLengthLimit = 10_485_760;
});

builder.Services.Configure<Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerOptions>(options =>
{
	options.Limits.MaxRequestBodySize = 10_485_760;
});

// ============================================
// 5. BUILD APP
// ============================================

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
	app.UseExceptionHandler("/Home/Error");
	app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// ✅ IMPORTANT: Session must be before UseAuthorization
app.UseSession();
app.UseAuthorization();

// ============================================
// 6. ROUTE MAPPING
// ============================================

// SignalR Hub
app.MapHub<NotificationHub>("/notificationHub");

// Default Controller Route
app.MapControllerRoute(
	name: "default",
	pattern: "{controller=Account}/{action=Login}/{id?}");

// ============================================
// 7. CREATE UPLOADS DIRECTORY
// ============================================

var uploadsPath = Path.Combine(app.Environment.WebRootPath, "uploads", "attendance");
if (!Directory.Exists(uploadsPath))
{
	Directory.CreateDirectory(uploadsPath);
	Console.WriteLine($"✅ Created uploads directory: {uploadsPath}");
}

// ============================================
// 8. ✅ TEST AUDIT LOG KHI KHỞI ĐỘNG
// ============================================

using (var scope = app.Services.CreateScope())
{
	try
	{
		var auditHelper = scope.ServiceProvider.GetRequiredService<AuditHelper>();

		await auditHelper.LogAsync(
			userId: null,
			action: "SYSTEM_START",
			entityName: "Application",
			entityId: null,
			oldValue: null,
			newValue: new { Version = "1.0", StartTime = DateTime.Now },
			description: "✅ TMD System Started Successfully"
		);

		Console.WriteLine("✅ AuditHelper initialized and tested successfully!");
		Console.WriteLine("✅ Check AuditLogs table for SYSTEM_START record");
	}
	catch (Exception ex)
	{
		Console.WriteLine($"❌ AuditHelper test FAILED: {ex.Message}");
		if (ex.InnerException != null)
		{
			Console.WriteLine($"   Inner: {ex.InnerException.Message}");
		}
		Console.WriteLine($"   Stack: {ex.StackTrace}");
	}
}

// ============================================
// 9. STARTUP LOGGING
// ============================================

Console.WriteLine("\n╔════════════════════════════════════════════╗");
Console.WriteLine("║     🚀 TMD SYSTEM IS STARTING...          ║");
Console.WriteLine("╚════════════════════════════════════════════╝");
Console.WriteLine($"📁 Upload folder: {uploadsPath}");
Console.WriteLine("⏰ Using SERVER TIME for attendance records");
Console.WriteLine("🌍 Reverse Geocoding: OpenStreetMap Nominatim");
Console.WriteLine("📸 Max file size: 10MB (JPG, JPEG, PNG)");
Console.WriteLine("🔔 SignalR Hub: /notificationHub");
Console.WriteLine("📧 Email Service: Gmail SMTP");
Console.WriteLine("🔐 Password Reset: OTP (3 minutes expiry)");
Console.WriteLine("📝 Audit Logging: ENABLED with Scoped DbContextFactory");
Console.WriteLine("══════════════════════════════════════════════\n");

// ============================================
// 10. RUN APP
// ============================================

app.Run();