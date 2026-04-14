using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.IservicesContract
{
    public interface IWhatsAppSender
    {
        Task<(bool success, string? failureReason)> SendAsync(
            string toPhone, string message, string encryptedCredentials, string senderNumber);
    }
}
