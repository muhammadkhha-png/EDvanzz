using Edvanz.Application.IservicesContract;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.Services
{
    public class SmsSender : ISmsSender
    {
        public Task<(bool success, string? failureReason)> SendAsync(string toPhone, string message, string encryptedCredentials, string senderId)
        {
            throw new NotImplementedException();
        }
    }
}
