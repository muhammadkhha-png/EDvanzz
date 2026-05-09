using Edvanz.Domain.Enums;

namespace Edvanz.Application.ServiceContract;

/// <summary>
/// Pure-logic helper that parses a teacher-supplied URL into its provider
/// and provider-specific external id. No I/O — never calls YouTube or Drive
/// APIs (the spec explicitly lists provider validation as out-of-scope; the
/// frontend player surfaces "video not found" if the URL is bogus).
///
/// Why this is its own contract:
/// <list type="bullet">
///   <item>It's used in two places — Story A create flow and the response
///         builder for Start (where the embed URL is built from the parsed
///         id, not re-parsed from the raw URL).</item>
///   <item>Adding a third provider requires only one implementation
///         change — same pattern as <c>ITeacherCodeGenerator</c> isolating
///         a single concern behind a small interface.</item>
///   <item>Pure logic with no dependencies, so it tests trivially without
///         a database or HTTP fixture.</item>
/// </list>
///
/// Stateless. Scoped (or singleton) lifetime — same as
/// <see cref="ITeacherCodeGenerator"/>.
/// </summary>
public interface IVideoUrlParser
{
    /// <summary>
    /// Attempts to recognise the URL as one of the supported providers and
    /// extract its external id.
    ///
    /// Recognised YouTube URL formats — all normalize to the same 11-char
    /// videoId:
    /// <list type="bullet">
    ///   <item><c>https://www.youtube.com/watch?v={id}</c></item>
    ///   <item><c>https://youtu.be/{id}</c></item>
    ///   <item><c>https://www.youtube.com/embed/{id}</c></item>
    ///   <item><c>https://www.youtube.com/shorts/{id}</c></item>
    ///   <item>The above with optional <c>m.</c> subdomain or extra query
    ///         parameters such as <c>t=</c> or <c>list=</c>.</item>
    /// </list>
    ///
    /// Recognised Google Drive URL format:
    /// <list type="bullet">
    ///   <item><c>https://drive.google.com/file/d/{fileId}/(view|edit|preview)?...</c></item>
    /// </list>
    /// </summary>
    /// <param name="rawUrl">The URL pasted by the teacher. Trimmed and
    /// validated for syntactic URL shape by the implementation; the caller
    /// passes it verbatim.</param>
    /// <param name="result">When the method returns <c>true</c>, contains the
    /// parsed result; otherwise default.</param>
    /// <returns><c>true</c> when the URL is a syntactically valid URL of one
    /// of the two supported providers; <c>false</c> otherwise. Caller
    /// distinguishes "not a URL at all" from "URL but unsupported provider"
    /// via <see cref="VideoUrlParseFailure"/>.</returns>
    bool TryParse(string rawUrl, out VideoUrlParseResult result);

    /// <summary>
    /// Convenience overload that returns the failure reason instead of just
    /// <c>false</c>. Service layer uses this to map to the correct error
    /// localization key (<c>InvalidUrl</c> vs <c>UnsupportedSource</c>).
    /// </summary>
    VideoUrlParseOutcome Parse(string rawUrl);

    /// <summary>
    /// Builds the canonical embed URL for the given (provider, externalId)
    /// pair. Returned to Flutter on the Start endpoint so the secure WebView
    /// loads the embed-format URL, not the raw watch URL the teacher pasted.
    /// </summary>
    string BuildEmbedUrl(VideoSourceType sourceType, string externalId);
}

/// <summary>
/// Successful URL-parse result. Both fields are required when present.
/// </summary>
public sealed class VideoUrlParseResult
{
    public required VideoSourceType SourceType { get; init; }
    public required string ExternalId { get; init; }
}

/// <summary>
/// Why a parse failed, mapped to the localization key the service layer
/// returns to the client.
/// </summary>
public enum VideoUrlParseFailure
{
    /// <summary>Empty, not a URL, or fundamentally malformed.</summary>
    InvalidUrl = 1,

    /// <summary>Syntactically a URL but doesn't match YouTube or Drive.</summary>
    UnsupportedSource = 2
}

/// <summary>
/// Discriminated outcome — exactly one of <see cref="Success"/> or
/// <see cref="Failure"/> is non-null. The service layer pattern-matches on
/// this so it doesn't need <c>out</c> parameters.
/// </summary>
public sealed class VideoUrlParseOutcome
{
    public VideoUrlParseResult? Success { get; init; }
    public VideoUrlParseFailure? Failure { get; init; }

    public bool IsSuccess => Success is not null;
}
