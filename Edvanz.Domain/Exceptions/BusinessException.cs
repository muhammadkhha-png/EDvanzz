using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace Edvanz.Domain.Exceptions
{
    public class BusinessException : Exception
    {
        public string MessageKey { get; }
        public HttpStatusCode StatusCode { get; }

        public BusinessException(string messageKey, HttpStatusCode statusCode = HttpStatusCode.BadRequest)
            : base(messageKey)
        {
            MessageKey = messageKey;
            StatusCode = statusCode;
        }

    }
    public class InvalidPermissionsException : BusinessException
    {
        public InvalidPermissionsException()
            : base("InvalidPermissions") { }
    }

    public class InvalidTemplatesException : BusinessException
    {
        public InvalidTemplatesException()
            : base("InvalidTemplates") { }
    }

    public class AssistantPermissionsOutOfScopeException : BusinessException
    {
        public AssistantPermissionsOutOfScopeException()
            : base("AssisstantPermissionsOutOfTeacherScopePermissions", HttpStatusCode.Forbidden) { }
    }
}
