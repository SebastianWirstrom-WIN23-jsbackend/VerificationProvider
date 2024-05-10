using Azure.Messaging.ServiceBus;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using VerificationProvider.Data.Contexts;
using VerificationProvider.Models;


namespace VerificationProvider.Services;

public class VerificationService(IServiceProvider serviceProvider, ILogger<VerificationService> logger) : IVerificationService
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly ILogger<VerificationService> _logger = logger;

    public VerificationRequestModel UnpackVerificationRequest(ServiceBusReceivedMessage message)
    {
        try
        {
            var verificationRequest = JsonConvert.DeserializeObject<VerificationRequestModel>(message.Body.ToString());
            if (verificationRequest != null && !string.IsNullOrEmpty(verificationRequest.Email))
            {
                return verificationRequest;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"ERROR : GenerateVerificationCode.UnpackVerificationRequest() :: {ex.Message}");
        }
        return null!;
    }

    public string GenerateCode()
    {
        try
        {
            var rnd = new Random();
            var code = rnd.Next(100000, 999999);

            return code.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError($"ERROR : GenerateVerificationCode.GenerateCode() :: {ex.Message}");
        }
        return null!;
    }

    public async Task<bool> SaveVerificationRequest(VerificationRequestModel verificationRequest, string code)
    {
        try
        {
            using var context = _serviceProvider.GetRequiredService<DataContext>();

            var existingRequest = await context.VerificationRequests.FirstOrDefaultAsync(x => x.Email == verificationRequest.Email);
            if (existingRequest != null)
            {
                existingRequest.Code = code;
                existingRequest.ExpiryDate = DateTime.Now.AddMinutes(5);
                context.Entry(existingRequest).State = EntityState.Modified;
            }
            else
            {
                context.VerificationRequests.Add(new Data.Entities.VerificationRequestEntity()
                {
                    Email = verificationRequest.Email,
                    Code = code
                });
            }

            await context.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"ERROR : GenerateVerificationRequest.SaveVerificationRequest() :: {ex.Message}");
        }
        return false;
    }

    public EmailRequestModel GenerateEmailRequest(VerificationRequestModel verificationRequest, string code)
    {
        try
        {
            if (!string.IsNullOrEmpty(verificationRequest.Email) && !string.IsNullOrEmpty(code))
            {
                var emailRequest = new EmailRequestModel()
                {
                    To = verificationRequest.Email,
                    Subject = $"Verification Code: {code}",
                    HtmlBody = $@"
                        <html lang='en'>
                            <head>
                                <meta charset='UTF-8'>
                                <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                            </head>
                            <body>
                                <div style='color: #191919; max-width: 500px'>
                                    <div style='background-color: #4F85F6; color: white; text-align: center; padding: 20px 0;'>
                                        <h1 style='font-weight: 400;'>Verification Code</h1>
                                    </div>
                                    <div style='background-color: #f4f4f4; padding: 1rem 2rem;'>
                                        <p>Dear user,</p>
                                        <p>We've received a request to sign in to your account using e-mail {verificationRequest.Email}. Please verify your account using this code below. </p>
                                        <p style='font-weight: 700; text-align: center; font-size: 48px; letter-spacing: 1,5'>[{code}]</p>
                                        <div style='color: #191919; font-size: 11px;'>
                                            <p>If you didn't request this code, it is possible someone is trying to access your account, and you should change password immediately.</p>
                                        </div>
                                    </div>
                                    <div style='color: #191919; text-align: center; font-size: 11px;'>
                                        <p>Copyright Silicon, Sveavägen 1, SE-123 23 Örebro, Sweden</p>
                                    </div>
                                </div>
                            </body>
                        </html>

                    ",
                    PlainText = $"Please verify your account using this code: {code}. If you didn't request this code, it is possible someone is trying to access your account, and you should change password immediately."
                };

                return emailRequest;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"ERROR : EmailGenerateVerificationCodeSender.GenerateEmailRequest() :: {ex.Message}");
        }
        return null!;
    }

    public string GenerateServiceBusEmailRequest(EmailRequestModel emailRequest)
    {
        try
        {
            var payload = JsonConvert.SerializeObject(emailRequest);
            if (!string.IsNullOrEmpty(payload))
            {
                return payload;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"ERROR : GenerateVerificationCode.GenerateServiceBusEmailRequest() :: {ex.Message}");
        }
        return null!;
    }
}
