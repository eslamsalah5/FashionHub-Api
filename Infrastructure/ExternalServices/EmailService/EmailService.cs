using Application.Models;
using Application.Services.Interfaces;
using Domain.Entities;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Infrastructure.ExternalServices.EmailService
{
    public class EmailService : IEmailService
    {
        private readonly EmailSettings _emailSettings;

        public EmailService(IOptions<EmailSettings> emailSettings)
        {
            _emailSettings = emailSettings.Value;
        }

        public async Task SendEmailAsync(string to, string subject, string body)
        {
            var email = new MimeMessage();
            email.From.Add(new MailboxAddress(_emailSettings.DisplayName, _emailSettings.Email));
            email.To.Add(MailboxAddress.Parse(to));
            email.Subject = subject;

            var builder = new BodyBuilder { HtmlBody = body };
            email.Body = builder.ToMessageBody();

            using var smtp = new SmtpClient();
            await smtp.ConnectAsync(_emailSettings.Host, _emailSettings.Port, SecureSocketOptions.StartTls);
            await smtp.AuthenticateAsync(_emailSettings.Email, _emailSettings.Password);
            await smtp.SendAsync(email);
            await smtp.DisconnectAsync(true);
        }

        private string GetEmailTemplate(string content)
        {
            return $@"
            <!DOCTYPE html>
            <html lang='en'>
            <head>
                <meta charset='UTF-8'>
                <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                <title>Ra7ala Transportation</title>
                <style>
                    /* Reset styles */
                    * {{
                        margin: 0;
                        padding: 0;
                        box-sizing: border-box;
                    }}
                    
                    /* Base styles */
                    body {{
                        font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
                        line-height: 1.6;
                        color: #333333;
                        background-color: #f9f9f9;
                        margin: 0;
                        padding: 0;
                    }}
                    
                    p {{
                        margin-bottom: 16px;
                        font-size: 16px;
                    }}
                    
                    strong {{
                        font-weight: 600;
                    }}
                    
                    /* Container styles */
                    .container {{
                        max-width: 600px;
                        margin: 20px auto;
                        background: #ffffff;
                        border-radius: 12px;
                        overflow: hidden;
                        box-shadow: 0 3px 10px rgba(0, 0, 0, 0.08);
                    }}
                    
                    /* Header styles */
                    .header {{
                        background-color: #FFD166;
                        padding: 30px 20px;
                        text-align: center;
                        position: relative;
                    }}
                    
                    .header::after {{
                        content: '';
                        position: absolute;
                        bottom: -15px;
                        left: 50%;
                        transform: translateX(-50%);
                        width: 30px;
                        height: 30px;
                        background-color: #FFD166;
                        transform: translateX(-50%) rotate(45deg);
                        z-index: 1;
                    }}
                    
                    .header h1 {{
                        margin: 0;
                        font-size: 22px;
                        color: #333333;
                        font-weight: 600;
                        letter-spacing: 0.5px;
                        margin-top: 8px;
                    }}
                    
                    .logo {{
                        font-family: 'Arial', sans-serif;
                        font-size: 38px;
                        color: #333333;
                        font-weight: 700;
                        letter-spacing: -0.5px;
                        font-style: italic;
                    }}
                    
                    /* Content styles */
                    .content {{
                        padding: 40px;
                        position: relative;
                        z-index: 2;
                    }}
                    
                    /* Credentials box */
                    .credentials {{
                        background-color: #f9f9f9;
                        border-radius: 8px;
                        padding: 20px;
                        margin: 25px 0;
                        border-left: 4px solid #FFD166;
                        box-shadow: 0 2px 5px rgba(0,0,0,0.05);
                    }}
                    
                    .credentials p {{
                        margin-bottom: 10px;
                    }}
                    
                    /* Button style */
                    .button {{
                        display: inline-block;
                        background-color: #FFD166;
                        color: #333333;
                        text-decoration: none;
                        padding: 14px 32px;
                        border-radius: 30px;
                        margin-top: 20px;
                        font-weight: 600;
                        text-align: center;
                        transition: all 0.3s ease;
                        box-shadow: 0 2px 5px rgba(0,0,0,0.1);
                    }}
                    
                    .button:hover {{
                        background-color: #FFC233;
                        transform: translateY(-2px);
                        box-shadow: 0 4px 8px rgba(0,0,0,0.15);
                    }}
                    
                    /* Footer styles */
                    .footer {{
                        text-align: center;
                        padding: 25px 20px;
                        font-size: 14px;
                        color: #777777;
                        background-color: #f5f5f5;
                        border-top: 1px solid #eeeeee;
                    }}
                    
                    /* Social links */
                    .social {{
                        margin: 18px 0 12px;
                    }}
                    
                    .social a {{
                        display: inline-block;
                        margin: 0 10px;
                        color: #666666;
                        text-decoration: none;
                        font-weight: 500;
                        transition: color 0.3s;
                    }}
                    
                    .social a:hover {{
                        color: #FFD166;
                    }}
                    
                    /* Warning text */
                    .warning {{
                        color: #e74c3c;
                        font-weight: 600;
                        padding: 8px 0;
                    }}
                    
                    /* Token display */
                    .token {{
                        font-family: monospace;
                        background-color: #f0f0f0;
                        padding: 18px;
                        border-radius: 8px;
                        margin: 20px 0;
                        font-size: 18px;
                        letter-spacing: 1px;
                        text-align: center;
                        border: 1px dashed #ccc;
                        box-shadow: inset 0 1px 3px rgba(0,0,0,0.05);
                    }}
                    
                    /* Headings */
                    h2 {{
                        color: #333333;
                        font-size: 24px;
                        margin-bottom: 20px;
                        position: relative;
                        padding-bottom: 12px;
                    }}
                    
                    h2::after {{
                        content: '';
                        position: absolute;
                        bottom: 0;
                        left: 0;
                        width: 60px;
                        height: 3px;
                        background-color: #FFD166;
                    }}
                    
                    /* Highlight text */
                    .highlight {{
                        background-color: #FFF7E0;
                        padding: 3px 6px;
                        border-radius: 3px;
                        font-weight: 500;
                    }}
                    
                    /* Places badge */
                    .places-badge {{
                        background-color: #FFF7E0;
                        color: #333333;
                        padding: 6px 12px;
                        border-radius: 20px;
                        font-weight: bold;
                        display: inline-block;
                        margin: 10px 0;
                        box-shadow: 0 1px 3px rgba(0,0,0,0.1);
                    }}
                    
                    /* Travel icons */
                    .travel-icon {{
                        font-size: 24px;
                        margin: 0 5px;
                        color: #FFD166;
                    }}
                    
                    /* Info box */
                    .info-box {{
                        background-color: #F0F8FF;
                        border-left: 4px solid #4AA5E8;
                        padding: 15px;
                        margin: 20px 0;
                        border-radius: 4px;
                    }}
                    
                    /* Company slogan */
                    .slogan {{
                        font-style: italic;
                        color: #555;
                        text-align: center;
                        margin: 20px 0;
                        font-size: 16px;
                    }}
                    
                    /* Divider */
                    .divider {{
                        height: 1px;
                        background-color: #eee;
                        margin: 25px 0;
                    }}
                </style>
            </head>
            <body>
                <div class='container'>
                    <div class='header'>
                        <div class='logo'>Ra7ala</div>
                        <h1>Your Trusted Travel Companion</h1>
                    </div>
                    <div class='content'>
                        {content}
                        
                        <div class='slogan'>
                            Where convenience meets efficiency!
                        </div>
                    </div>
                    <div class='footer'>
                        <div>At Ra7ala, we believe journeys should be effortless, reliable, and enjoyable.</div>
                        <div class='places-badge'>10+ Places</div>
                        <div class='social'>
                            <a href='#'>Facebook</a>
                            <a href='#'>Twitter</a>
                            <a href='#'>Instagram</a>
                        </div>
                        <div style='margin-top: 15px;'>&copy; {DateTime.UtcNow.Year} Ra7ala Transportation. All rights reserved.</div>
                    </div>
                </div>
            </body>
            </html>";
        }

        public async Task SendPasswordResetEmailAsync(string toEmail, string username, string resetToken)
        {
            var subject = "Password Reset Request";
            var content = $@"
                <h2>Password Reset</h2>
                <p>Hello {username},</p>
                <p>You've requested to reset your password. Please use the token below:</p>
                
                <div class='info-box'>
                    <p>If you didn't request a password reset, please ignore this email or contact our support team immediately.</p>
                </div>
                
                <div class='divider'></div>
                
                <p>Thank you for choosing Ra7ala for your transportation needs.</p>
                <p>Best regards,<br>The Ra7ala Team</p>
                <a href='{_emailSettings.FrontendBaseUrl}/reset-password?email={toEmail}&token={Uri.EscapeDataString(resetToken)}' class='button'>Reset Password</a>
            ";

            await SendEmailAsync(toEmail, subject, GetEmailTemplate(content));
        }

        public async Task SendPasswordChangedNotificationAsync(string toEmail, string username)
        {
            var subject = "Password Changed Successfully";
            var content = $@"
                <h2>Password Changed</h2>
                <p>Hello {username},</p>
                <p>We're writing to confirm that your password has been changed successfully. <span class='travel-icon'>✓</span></p>
                
                <div class='info-box'>
                    <p>If you did not make this change, please contact our support team immediately, as your account may have been compromised.</p>
                </div>
                
                <div class='divider'></div>
                
                <p>Join Ra7ala and discover a <span class='highlight'>smarter way to travel</span>—where convenience meets efficiency!</p>
                <p>Best regards,<br>The Ra7ala Team</p>
                <a href='{_emailSettings.FrontendBaseUrl}/login' class='button'>Login to Your Account</a>
            ";

            await SendEmailAsync(toEmail, subject, GetEmailTemplate(content));
        }

        public async Task SendUserCredientialsEmailAsync(string toEmail, string username, string password)
        {
            var subject = "Welcome to Ra7ala - Your Account Credentials";
            var content = $@"
                <h2>Welcome to Ra7ala!</h2>
                <p>Hello,</p>
                <p>Your account has been created successfully! <span class='travel-icon'>🚌</span></p>
                
                <p>We're excited to have you join our platform for <span class='highlight'>seamless and convenient bus travel</span>!</p>
                
                <p>Below are your login credentials:</p>
                <div class='credentials'>
                    <p><strong>Username/Email:</strong> {username}</p>
                    <p><strong>Password:</strong> {password}</p>
                </div>
                
                <p class='warning'>Please change your password after your first login for security reasons.</p>
                
                <div class='divider'></div>
                
                <p>With Ra7ala, booking your transportation is just a few clicks away.</p>
                <p>Our user-friendly interface, real-time availability updates, and secure payment options provide a smooth, stress-free travel solution tailored to your needs.</p>
                
                <p>Best regards,<br>The Ra7ala Team</p>
                <a href='{_emailSettings.FrontendBaseUrl}/login' class='button'>Login to Your Account</a>
            ";

            await SendEmailAsync(toEmail, subject, GetEmailTemplate(content));
        }
        
        public async Task SendCompanyUserCredientialsEmailAsync(string toEmail, string username, string password, string fullName, string roleName, string companyName)
        {
            var subject = $"Welcome to Ra7ala - Your {roleName} Account at {companyName}";
            var content = $@"
                <h2>Welcome to Ra7ala!</h2>
                <p>Hello {fullName},</p>
                <p>You have been added as a <strong>{roleName}</strong> at <strong>{companyName}</strong>. <span class='travel-icon'>🏢</span></p>
                
                <div class='divider'></div>
                
                <p>Our platform is designed to connect travelers with a wide range of bus services, making transportation more accessible than ever. Whether customers are commuting daily, planning a weekend getaway, or embarking on an adventure across cities, Ra7ala ensures a <span class='highlight'>hassle-free booking experience</span> with just a few clicks.</p>
                
                <p>Below are your login credentials:</p>
                <div class='credentials'>
                    <p><strong>Username:</strong> {username}</p>
                    <p><strong>Password:</strong> {password}</p>
                </div>
                
                <p class='warning'>Please change your password immediately after your first login for security reasons.</p>
                
                <div class='info-box'>
                    <p>If you have any questions about your role or responsibilities, please contact your company administrator.</p>
                </div>
                
                <p>We're excited to have you on board and look forward to working together to provide exceptional transportation services!</p>
                
                <p>Best regards,<br>The Ra7ala Team</p>
                <a href='{_emailSettings.FrontendBaseUrl}/login' class='button'>Login to Your Account</a>
            ";

            await SendEmailAsync(toEmail, subject, GetEmailTemplate(content));
        }
    }
}

