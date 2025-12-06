using System.Text;
using System.Text.Json;

namespace AIHUBOS.Services
{
	public interface ITelegramService
	{
		Task SendCheckInNotificationAsync(string fullName, string username, DateTime checkInTime, string address, bool isLate);
		Task SendCheckOutNotificationAsync(string fullName, string username, DateTime checkOutTime, decimal totalHours, decimal overtimeHours);
		Task SendTestMessageAsync();
	}

	public class TelegramService : ITelegramService
	{
		private readonly HttpClient _httpClient;
		private readonly string _botToken;
		private readonly string _chatId;
		private readonly ILogger<TelegramService> _logger;

		public TelegramService(
			IHttpClientFactory httpClientFactory,
			IConfiguration configuration,
			ILogger<TelegramService> logger)
		{
			_httpClient = httpClientFactory.CreateClient();
			_botToken = configuration["Telegram:BotToken"] ?? throw new ArgumentNullException("Telegram:BotToken");
			_chatId = configuration["Telegram:ChatId"] ?? throw new ArgumentNullException("Telegram:ChatId");
			_logger = logger;

			_logger.LogInformation("🤖 TelegramService initialized - BotToken: {Token}, ChatId: {ChatId}",
				_botToken.Substring(0, 10) + "...", _chatId);
		}

		public async Task SendCheckInNotificationAsync(string fullName, string username, DateTime checkInTime, string address, bool isLate)
		{
			try
			{
				var emoji = isLate ? "⚠️" : "✅";
				var statusText = isLate ? "ĐI TRỄ" : "ĐÚng GIỜ";

				var message = $@"{emoji} <b>CHECK-IN {statusText}</b>

👤 <b>Nhân viên:</b> {fullName} (@{username})
🕐 <b>Thời gian:</b> {checkInTime:dd/MM/yyyy HH:mm:ss}
📍 <b>Vị trí:</b> {address}

{(isLate ? "⚠️ Nhân viên đến muộn!" : "✨ Nhân viên đến đúng giờ")}";

				await SendMessageAsync(message);
				_logger.LogInformation("✅ Check-in notification sent for {FullName}", fullName);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "❌ Failed to send check-in notification for {FullName}", fullName);
			}
		}

		public async Task SendCheckOutNotificationAsync(string fullName, string username, DateTime checkOutTime, decimal totalHours, decimal overtimeHours)
		{
			try
			{
				var message = $@"🏁 <b>CHECK-OUT</b>

👤 <b>Nhân viên:</b> {fullName} (@{username})
🕐 <b>Thời gian:</b> {checkOutTime:dd/MM/yyyy HH:mm:ss}
⏱️ <b>Tổng giờ làm:</b> {totalHours:F2}h
{(overtimeHours > 0 ? $"🔥 <b>Giờ tăng ca:</b> {overtimeHours:F2}h" : "")}

✨ Chúc bạn buổi tối vui vẻ!";

				await SendMessageAsync(message);
				_logger.LogInformation("✅ Check-out notification sent for {FullName}", fullName);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "❌ Failed to send check-out notification for {FullName}", fullName);
			}
		}

		public async Task SendTestMessageAsync()
		{
			var message = $@"🧪 <b>TEST MESSAGE</b>

✅ Bot hoạt động bình thường
🕐 Thời gian: {DateTime.Now:dd/MM/yyyy HH:mm:ss}

Nếu bạn nhận được tin nhắn này, bot đã được cấu hình đúng!";

			await SendMessageAsync(message);
		}

		private async Task SendMessageAsync(string message)
		{
			try
			{
				var url = $"https://api.telegram.org/bot{_botToken}/sendMessage";

				var payload = new
				{
					chat_id = _chatId,
					text = message,
					parse_mode = "HTML"
				};

				var jsonPayload = JsonSerializer.Serialize(payload);
				_logger.LogInformation("📤 Sending to Telegram: {Url}", url);
				_logger.LogInformation("📦 Payload: {Payload}", jsonPayload);

				var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

				var response = await _httpClient.PostAsync(url, content);
				var responseBody = await response.Content.ReadAsStringAsync();

				_logger.LogInformation("📥 Telegram Response Status: {StatusCode}", response.StatusCode);
				_logger.LogInformation("📥 Telegram Response Body: {Body}", responseBody);

				if (!response.IsSuccessStatusCode)
				{
					_logger.LogError("❌ Telegram API Error: {StatusCode} - {Body}", response.StatusCode, responseBody);
					throw new Exception($"Telegram API Error: {response.StatusCode} - {responseBody}");
				}

				_logger.LogInformation("✅ Message sent successfully to Telegram");
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "❌ Exception when sending message to Telegram");
				throw;
			}
		}
	}
}