using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AIHUBOS.Services
{
    public interface IEmailService
    {
        Task<bool> SendOtpEmailAsync(string toEmail, string otpCode, string userName);
    }

    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<bool> SendOtpEmailAsync(string toEmail, string otpCode, string userName)
        {
            try
            {
                // Lấy thông tin từ appsettings.json
                var smtpHost = _configuration["EmailSettings:SmtpHost"] ?? "smtp.gmail.com";
                var smtpPort = int.Parse(_configuration["EmailSettings:SmtpPort"] ?? "587");
                var senderEmail = _configuration["EmailSettings:SenderEmail"];
                var senderName = _configuration["EmailSettings:SenderName"] ?? "AIHUBOS System";
                var appPassword = _configuration["EmailSettings:AppPassword"];

                // Validate configuration
                if (string.IsNullOrEmpty(senderEmail) || string.IsNullOrEmpty(appPassword))
                {
                    _logger.LogError("Email configuration is missing. Please check appsettings.json");
                    return false;
                }

                using (var client = new SmtpClient(smtpHost, smtpPort))
                {
                    client.EnableSsl = true;
                    client.UseDefaultCredentials = false;
                    client.Credentials = new NetworkCredential(senderEmail, appPassword);
                    client.Timeout = 30000; // 30 seconds timeout

                    var mailMessage = new MailMessage
                    {
                        From = new MailAddress(senderEmail, senderName),
                        Subject = "Mã OTP Đặt Lại Mật Khẩu - AIHUBOS System",
                        IsBodyHtml = true,
                        Body = GenerateOtpEmailBody(otpCode, userName)
                    };

                    mailMessage.To.Add(toEmail);

                    await client.SendMailAsync(mailMessage);
                    _logger.LogInformation($"OTP email sent successfully to {toEmail}");
                    return true;
                }
            }
            catch (SmtpException smtpEx)
            {
                _logger.LogError($"SMTP Error sending email: {smtpEx.Message}");
                _logger.LogError($"Status Code: {smtpEx.StatusCode}");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error sending email: {ex.Message}");
                _logger.LogError($"Stack Trace: {ex.StackTrace}");
                return false;
            }
        }

        private string GenerateOtpEmailBody(string otpCode, string userName)
        {
            return $@"
<!DOCTYPE html>
<html lang='vi'>
<head>
    <meta charset='utf-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <style>
        * {{
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }}
        body {{
            font-family: 'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif;
            line-height: 1.6;
            color: #334155; /* matches --text */
            background: #f8fafc; /* matches --light */
            padding: 20px 0;
        }}
        .email-wrapper {{
            max-width: 680px;
            margin: 0 auto;
            background: #f8fafc;
        }}
        .email-container {{
            background: #ffffff;
            border-radius: 20px;
            overflow: hidden;
            box-shadow: 0 8px 24px rgba(15, 23, 42, 0.08);
            border: 1px solid #e2e8f0; /* matches --border */
        }}
        .header {{
            /* header gradient uses primary -> secondary from your root */
            background: linear-gradient(135deg, #1a1f4a 0%, #6366f1 100%);
            color: #f8fafc; /* light text */
            padding: 40px 30px;
            text-align: center;
        }}
        .header-icon {{
            font-size: 56px;
            margin-bottom: 16px;
            opacity: 0.95;
        }}
        .header h1 {{
            margin: 0 0 8px 0;
            font-size: 28px;
            font-weight: 700;
            letter-spacing: -0.5px;
        }}
        .header p {{
            margin: 0;
            font-size: 14px;
            opacity: 0.95;
        }}
        .content {{
            padding: 40px 35px;
        }}
        .greeting {{
            font-size: 16px;
            margin-bottom: 20px;
            color: #334155;
        }}
        .greeting strong {{
            color: #1a1f4a; /* primary */
            font-weight: 600;
        }}
        .message {{
            font-size: 15px;
            color: #495057;
            margin-bottom: 30px;
            line-height: 1.7;
        }}
        .otp-box {{
            background: #f5f7fb;
            border: 2px dashed rgba(99,102,241,0.18); /* subtle secondary accent */
            border-radius: 16px;
            padding: 30px;
            text-align: center;
            margin: 30px 0;
        }}
        .otp-label {{
            font-size: 13px;
            color: #64748b; /* muted */
            font-weight: 600;
            text-transform: uppercase;
            letter-spacing: 1px;
            margin-bottom: 12px;
        }}
        .otp-code {{
            font-size: 48px;
            font-weight: 700;
            color: #1a1f4a; /* primary */
            letter-spacing: 12px;
            font-family: 'Courier New', monospace;
            margin: 15px 0;
            text-shadow: 0 2px 4px rgba(26,31,74,0.06);
        }}
        .otp-expiry {{
            font-size: 13px;
            color: #64748b;
            margin-top: 12px;
        }}
        .otp-expiry strong {{
            color: #1a1f4a;
            font-weight: 600;
        }}
        .warning-box {{
            background: #f3f6ff;
            border-left: 4px solid rgba(99,102,241,0.18);
            border-radius: 12px;
            padding: 20px 24px;
            margin: 30px 0;
        }}
        .warning-box .warning-title {{
            color: #1a1f4a;
            font-weight: 700;
            font-size: 15px;
            margin-bottom: 12px;
            display: flex;
            align-items: center;
            gap: 8px;
        }}
        .warning-list {{
            margin: 0;
            padding-left: 20px;
            color: #1a1f4a;
        }}
        .warning-list li {{
            margin: 8px 0;
            font-size: 14px;
            line-height: 1.6;
        }}
        .warning-list strong {{
            font-weight: 700;
        }}
        .support-text {{
            font-size: 14px;
            color: #495057;
            margin: 30px 0 20px 0;
            padding: 16px;
            background: #f8f9fb;
            border-radius: 10px;
            text-align: center;
        }}
        .signature {{
            margin-top: 40px;
            font-size: 15px;
            color: #495057;
        }}
        .signature strong {{
            color: #1a1f4a;
            font-weight: 700;
        }}
        .footer {{
            background: #f8f9fb;
            padding: 30px;
            text-align: center;
            border-top: 1px solid #e2e8f0;
        }}
        .footer p {{
            margin: 8px 0;
            font-size: 13px;
            color: #64748b;
        }}
        .footer-links {{
            margin-top: 12px;
        }}
        .footer-links a {{
            color: #1a1f4a;
            text-decoration: none;
            font-weight: 600;
            margin: 0 10px;
            transition: color 0.2s;
        }}
        .footer-links a:hover {{
            color: #6366f1;
        }}
        .divider {{
            height: 1px;
            background: linear-gradient(to right, transparent, #e2e8f0, transparent);
            margin: 25px 0;
        }}
        @media only screen and (max-width: 600px) {{
            .email-wrapper {{
                padding: 10px;
            }}
            .header {{
                padding: 32px 24px;
            }}
            .header-icon {{
                font-size: 48px;
            }}
            .header h1 {{
                font-size: 24px;
            }}
            .content {{
                padding: 32px 24px;
            }}
            .otp-code {{
                font-size: 36px;
                letter-spacing: 8px;
            }}
            .otp-box {{
                padding: 24px 16px;
            }}
            .warning-box {{
                padding: 16px 20px;
            }}
            .footer {{
                padding: 24px 16px;
            }}
        }}
    </style>
</head>
<body>
    <div class='email-wrapper'>
        <div class='email-container'>
            <!-- Header -->
            <div class='header'>
                <div class='header-icon'>🔐</div>
                <h1>Đặt Lại Mật Khẩu</h1>
                <p>Xác thực tài khoản của bạn</p>
            </div>

            <!-- Content -->
            <div class='content'>
                <div class='greeting'>
                    Xin chào <strong>{userName}</strong>,
                </div>

                <div class='message'>
                    Chúng tôi nhận được yêu cầu đặt lại mật khẩu cho tài khoản của bạn. 
                    Để tiếp tục quá trình này, vui lòng sử dụng mã OTP bên dưới:
                </div>

                <!-- OTP Box -->
                <div class='otp-box'>
                    <div class='otp-label'>MÃ XÁC THỰC CỦA BẠN</div>
                    <div class='otp-code'>{otpCode}</div>
                    <div class='otp-expiry'>
                        Mã có hiệu lực trong <strong>5 phút</strong>
                    </div>
                </div>

                <!-- Warning Box -->
                <div class='warning-box'>
                    <div class='warning-title'>
                        ⚠️ Lưu ý quan trọng
                    </div>
                    <ul class='warning-list'>
                        <li>Mã OTP có hiệu lực trong <strong>5 phút</strong> kể từ khi nhận email</li>
                        <li><strong>Không chia sẻ</strong> mã này với bất kỳ ai, kể cả nhân viên AIHUB</li>
                        <li>Nếu bạn không yêu cầu đặt lại mật khẩu, vui lòng <strong>bỏ qua email này</strong></li>
                        <li>Để bảo mật tài khoản, hãy thay đổi mật khẩu ngay nếu nghi ngờ có truy cập trái phép</li>
                    </ul>
                </div>

                <div class='divider'></div>

                <!-- Support -->
                <div class='support-text'>
                    💬 Cần hỗ trợ? Liên hệ với chúng tôi qua email hoặc hotline để được giúp đỡ nhanh chóng.
                </div>

                <!-- Signature -->
                <div class='signature'>
                    Trân trọng,<br>
                    <strong>AIHUB Team</strong>
                </div>
            </div>

            <!-- Footer -->
            <div class='footer'>
                <p><strong>AIHUB</strong></p>
                <p>Email này được gửi tự động, vui lòng không trả lời trực tiếp.</p>
                <p>&copy; 2025 AIHUB. All rights reserved.</p>
                <div class='footer-links'>
                    <a href='#'>Chính sách bảo mật</a>
                    <a href='#'>Điều khoản sử dụng</a>
                    <a href='#'>Liên hệ hỗ trợ</a>
                </div>
            </div>
        </div>
    </div>
</body>
</html>";
        }

    }
}