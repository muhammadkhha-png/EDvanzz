using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using Microsoft.Extensions.Localization;

namespace Edvanz.Application.Dtos
{
    public class Result<T>
    {
        public bool IsSuccess { get; set; }
        public string? Message { get; set; }

        /// <summary>
        /// Stable, language-independent code the frontend can branch on (e.g. "GradeExceedsMax").
        /// Populated from the message key on localizer-based results; null when a result is built
        /// directly from an already-localized string.
        /// </summary>
        public string? Code { get; set; }

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
                Code = messageKey,
                StatusCode = statusCode
            };
        }

        /// <summary>
        /// Success with a FORMATTED, localized message (placeholders filled from <paramref name="args"/>),
        /// keeping the stable <see cref="Code"/> = <paramref name="messageKey"/>. Mirror of the formatted
        /// <c>Failure</c> overload — used e.g. for the same-day soft-confirm warning that names the
        /// collector / amount / month.
        /// </summary>
        public static Result<T> Success(
            T data,
            IStringLocalizer localizer,
            string messageKey,
            object?[] args,
            HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            return new Result<T>
            {
                IsSuccess = true,
                Data = data,
                Message = localizer[messageKey, args],
                Code = messageKey,
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
                Code = messageKey,
                StatusCode = statusCode
            };
        }

        /// <summary>
        /// Failure with a formatted, localized message (e.g. a message that names the offending
        /// entity). Keeps the stable <see cref="Code"/> = <paramref name="messageKey"/> for the client.
        /// </summary>
        public static Result<T> Failure(
            IStringLocalizer localizer,
            string messageKey,
            object?[] args,
            HttpStatusCode statusCode = HttpStatusCode.BadRequest)
        {
            return new Result<T>
            {
                IsSuccess = false,
                Message = localizer[messageKey, args],
                Code = messageKey,
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

        /// <summary>
        /// Propagates a failure surfaced by a helper of a different generic type, PRESERVING its
        /// <see cref="Message"/>, <see cref="Code"/> and <see cref="StatusCode"/>. Use this instead of
        /// re-wrapping via the plain-string overload, which would drop the stable <see cref="Code"/>
        /// the client branches on (see PAY-8 / CC-5).
        /// </summary>
        public static Result<T> Failure<TOther>(Result<TOther> source)
        {
            return new Result<T>
            {
                IsSuccess = false,
                Message = source.Message,
                Code = source.Code,
                StatusCode = source.StatusCode
            };
        }
    }
}
