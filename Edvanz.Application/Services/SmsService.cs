using Edvanz.Application.IservicesContract;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.Services
{
    public class SmsService:ISmsService
    {
        public Task SendSmsAsync(string phoneNumber, string message)
        {
            // call Twilio API here
            Console.WriteLine($"Send SMS to {phoneNumber}: {message}");
            return Task.CompletedTask;
        }
    }
}
