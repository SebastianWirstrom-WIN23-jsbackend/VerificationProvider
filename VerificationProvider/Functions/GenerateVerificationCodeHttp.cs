using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.IO;
using System.Threading.Tasks;
using VerificationProvider.Services;
using VerificationProvider.Models;

namespace VerificationProvider.Functions
{
    public class GenerateVerificationCodeHttp(ILogger<GenerateVerificationCodeHttp> logger, IVerificationService verificationService)
    {
        private readonly ILogger<GenerateVerificationCodeHttp> _logger = logger;
        private readonly IVerificationService _verificationService = verificationService;

        [Function("GenerateVerificationCodeHttp")]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req)
        {
            try
            {
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                var verificationRequest = JsonSerializer.Deserialize<VerificationRequestModel>(requestBody);

                if (verificationRequest != null)
                {
                    var code = _verificationService.GenerateCode();
                    if (!string.IsNullOrEmpty(code))
                    {
                        var result = await _verificationService.SaveVerificationRequest(verificationRequest, code);
                        if (result)
                        {
                            var emailRequest = _verificationService.GenerateEmailRequest(verificationRequest, code);
                            if (emailRequest != null)
                            {
                                var payload = _verificationService.GenerateServiceBusEmailRequest(emailRequest);
                                if (!string.IsNullOrEmpty(payload))
                                {
                                    return new OkObjectResult(payload);
                                }
                            }
                        }
                    }
                }
                return new BadRequestResult();
            }
            catch (Exception ex)
            {
                _logger.LogError($"ERROR in GenerateVerificationCodeHttp.Run: {ex.Message}");
                return new StatusCodeResult(StatusCodes.Status500InternalServerError);
            }
        }
    }
}
