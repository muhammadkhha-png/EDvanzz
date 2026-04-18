using Edvanz.Application.IservicesContract;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Text;

namespace Edvanz.Application.Services
{
    public class WhatsAppSender : IWhatsAppSender
    {
        private readonly HttpClient _http;
        private readonly IConfiguration _config;

        public WhatsAppSender(HttpClient http, IConfiguration config)
        {
            _http = http;
            _config = config;
        }

        public async Task<(bool success, string? failureReason)> SendAsync(
            string toPhone, string message,
            string encryptedCredentials,  
            string senderNumber)           
        {
            var serviceUrl = _config["WhatsApp:ServiceUrl"];  

            try
            {
                var response = await _http.PostAsJsonAsync($"{serviceUrl}/send", new
                {
                    phone = toPhone,
                    message = message
                });

                if (response.IsSuccessStatusCode)
                    return (true, null);

                var body = await response.Content.ReadFromJsonAsync<WhatsAppResponse>();
                return (false, body?.Reason ?? "UnknownError");
            }
            catch (HttpRequestException ex)
            {
                return (false, $"ServiceUnreachable: {ex.Message}");
            }
        }
    }

    public record WhatsAppResponse(bool Success, string? Reason);
}
