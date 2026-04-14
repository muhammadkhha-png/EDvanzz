using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.IservicesContract
{
    public interface ISmsSender
    {
        Task<(bool success, string? failureReason)> SendAsync(
            string toPhone, string message, string encryptedCredentials, string senderId);
    }
}
