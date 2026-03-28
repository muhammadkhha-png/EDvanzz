using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace Edvanz.Application.Dtos
{
    using Microsoft.Extensions.Localization;
    using System.Net;

    public class Result<T>
    {
        public bool IsSuccess { get; set; }
        public string? Message { get; set; }
        public HttpStatusCode StatusCode { get; set; }
        public T? Data { get; set; }

        public static Result<T> Success(
            T data,
            IStringLocalizer localizer,
            string? messageKey = "Success",
            HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            return new Result<T>
            {
                IsSuccess = true,
                Data = data,
                Message = localizer[messageKey],
                StatusCode = statusCode
            };
        }

        public static Result<T> Success(
            T data,
            HttpStatusCode statusCode)
        {
            return new Result<T>
            {
                IsSuccess = true,
                Data = data,
                StatusCode = statusCode
            };
        }

        public static Result<T> Failure(
            IStringLocalizer localizer,
            string messageKey,
            HttpStatusCode statusCode = HttpStatusCode.BadRequest)
        {
            return new Result<T>
            {
                IsSuccess = false,
                Message = localizer[messageKey],
                StatusCode = statusCode
            };
        }

        public static Result<T> Failure(
            string message,
            HttpStatusCode statusCode = HttpStatusCode.BadRequest)
        {
            return new Result<T>
            {
                IsSuccess = false,
                Message = message,
                StatusCode = statusCode
            };
        }
    }
}
