using System.Text.Json.Serialization;

namespace Edvanz.Domain.Enums;

/// <summary>
/// Valid columns to sort the session list by.
/// REQ-SES-044/045: Supports sorting sessions in the session list view.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SessionSortBy
{
    /// <summary>
    /// Sort by the date the session was created (CreateAt).
    /// Default sort — newest first.
    /// </summary>
    DateAdded,

    /// <summary>
    /// Sort alphabetically by session name.
    /// REQ-SES-NFR-003: Supports Arabic and English.
    /// </summary>
    SessionName,

    /// <summary>
    /// Sort by session start date.
    /// </summary>
    StartDate,

    /// <summary>
    /// Sort by student count (computed at query time).
    /// REQ-SES-NFR-009: Live student count on session cards.
    /// </summary>
    StudentCount
}