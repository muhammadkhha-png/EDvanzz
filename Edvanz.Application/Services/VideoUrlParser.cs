using System.Text.RegularExpressions;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Enums;

namespace Edvanz.Application.Services;

/// <summary>
/// Default <see cref="IVideoUrlParser"/> implementation. Pure logic — no
/// HTTP calls, no database, no logging. Stateless; safe as singleton.
///
/// Lives in the Application layer (not Infrastructure) because it has no
/// I/O dependencies. Same placement rationale as
/// <see cref="AssignmentScopeResolver"/> — Application services that
/// orchestrate domain logic without touching external systems.
///
/// Compiled regexes are static readonly fields — initialized once and
/// shared across calls.
/// </summary>
public sealed class VideoUrlParser : IVideoUrlParser
{
    // ══════════════════════════════════════════════════════════════════════
    // YOUTUBE URL FORMS — recognized by these regexes
    //
    //   https://www.youtube.com/watch?v={11}            ← classic
    //   https://m.youtube.com/watch?v={11}              ← mobile
    //   https://youtu.be/{11}                           ← short link
    //   https://www.youtube.com/embed/{11}              ← embed
    //   https://www.youtube.com/shorts/{11}             ← shorts
    //
    // YouTube videoIds are 11 chars from [A-Za-z0-9_-]. We anchor each regex
    // and capture into a named group "id" so the extraction is uniform.
    // Regex.Match.Success means we matched; the group is then the externalId.
    // ══════════════════════════════════════════════════════════════════════

    private static readonly Regex YouTubeWatchRegex = new(
        @"^https?://(www\.|m\.)?youtube\.com/watch\?(?:[^#]*&)?v=(?<id>[A-Za-z0-9_-]{11})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex YouTubeShortLinkRegex = new(
        @"^https?://youtu\.be/(?<id>[A-Za-z0-9_-]{11})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex YouTubeEmbedRegex = new(
        @"^https?://(www\.)?youtube\.com/embed/(?<id>[A-Za-z0-9_-]{11})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex YouTubeShortsRegex = new(
        @"^https?://(www\.|m\.)?youtube\.com/shorts/(?<id>[A-Za-z0-9_-]{11})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // ══════════════════════════════════════════════════════════════════════
    // GOOGLE DRIVE URL FORM
    //
    //   https://drive.google.com/file/d/{fileId}/(view|edit|preview)?...
    //
    // Drive fileIds are 25-44 chars from [A-Za-z0-9_-]. We allow the trailing
    // segment to be view, edit, or preview, with optional query string.
    // ══════════════════════════════════════════════════════════════════════

    private static readonly Regex GoogleDriveRegex = new(
        @"^https?://drive\.google\.com/file/d/(?<id>[A-Za-z0-9_-]{20,})(?:/|$)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // ══════════════════════════════════════════════════════════════════════
    // PUBLIC API
    // ══════════════════════════════════════════════════════════════════════

    /// <inheritdoc />
    public bool TryParse(string rawUrl, out VideoUrlParseResult result)
    {
        var outcome = Parse(rawUrl);
        if (outcome.IsSuccess)
        {
            result = outcome.Success!;
            return true;
        }

        result = null!;
        return false;
    }

    /// <inheritdoc />
    public VideoUrlParseOutcome Parse(string rawUrl)
    {
        // Empty / whitespace / null → InvalidUrl.
        if (string.IsNullOrWhiteSpace(rawUrl))
            return new VideoUrlParseOutcome { Failure = VideoUrlParseFailure.InvalidUrl };

        var trimmed = rawUrl.Trim();

        // Cheap syntactic-URL gate: must be a well-formed absolute URI.
        // We do NOT use Uri.TryCreate's content checks; just structure.
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
            return new VideoUrlParseOutcome { Failure = VideoUrlParseFailure.InvalidUrl };

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return new VideoUrlParseOutcome { Failure = VideoUrlParseFailure.InvalidUrl };

        // YouTube — try each form in turn.
        var youtubeId =
            TryYouTube(trimmed, YouTubeWatchRegex)
            ?? TryYouTube(trimmed, YouTubeShortLinkRegex)
            ?? TryYouTube(trimmed, YouTubeEmbedRegex)
            ?? TryYouTube(trimmed, YouTubeShortsRegex);

        if (youtubeId is not null)
        {
            return new VideoUrlParseOutcome
            {
                Success = new VideoUrlParseResult
                {
                    SourceType = VideoSourceType.YouTube,
                    ExternalId = youtubeId
                }
            };
        }

        // Google Drive.
        var driveId = TryYouTube(trimmed, GoogleDriveRegex);
        if (driveId is not null)
        {
            return new VideoUrlParseOutcome
            {
                Success = new VideoUrlParseResult
                {
                    SourceType = VideoSourceType.GoogleDrive,
                    ExternalId = driveId
                }
            };
        }

        // The URL was syntactically valid but didn't match any supported
        // provider. Distinct from "not a URL at all" so the service layer
        // returns UnsupportedSource not InvalidUrl.
        return new VideoUrlParseOutcome { Failure = VideoUrlParseFailure.UnsupportedSource };
    }

    /// <inheritdoc />
    public string BuildEmbedUrl(VideoSourceType sourceType, string externalId)
    {
        if (string.IsNullOrWhiteSpace(externalId))
            throw new ArgumentException("ExternalId must be non-empty.", nameof(externalId));

        return sourceType switch
        {
            // YouTube embed — Flutter's secure WebView loads this.
            VideoSourceType.YouTube
                => $"https://www.youtube.com/embed/{externalId}",

            // Google Drive preview — supports embed without sign-in for
            // publicly-shared files. The teacher is responsible for setting
            // the share permission to "Anyone with the link".
            VideoSourceType.GoogleDrive
                => $"https://drive.google.com/file/d/{externalId}/preview",

            _ => throw new ArgumentOutOfRangeException(
                nameof(sourceType),
                sourceType,
                "Unsupported video source type. Add a branch here when a new provider is added.")
        };
    }

    // ══════════════════════════════════════════════════════════════════════
    // PRIVATE HELPERS
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Runs a compiled regex against the URL and returns the captured
    /// <c>id</c> group when successful, otherwise <c>null</c>. Named
    /// generically so it serves both YouTube and Drive — the regex itself
    /// encodes the provider distinction.
    /// </summary>
    private static string? TryYouTube(string url, Regex regex)
    {
        var match = regex.Match(url);
        return match.Success ? match.Groups["id"].Value : null;
    }
}
