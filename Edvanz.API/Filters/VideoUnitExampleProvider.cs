using System.Text.Json.Nodes;

namespace Edvanz.API.Filters;

/// <summary>
/// Swagger request/response examples for the Video Content Management module
/// (Module 14) — <c>VideoUnitsController</c> (Track C / G-UNIT, unit
/// hierarchy). Video-level examples (<c>VideosController</c>) live in
/// <see cref="VideoExampleProvider"/>.
///
/// All examples use real seeded identities from DbInitializer — teacher1
/// "Ahmed Mostafa". Numeric IDs (id, unitId) are illustrative only: they are
/// identity-generated and are NOT stable across rebuilds, so clients must
/// never hardcode them.
///
/// A unit is a purely organizational grouping of videos — it carries no
/// scope/recipients of its own beyond the optional create-time
/// <c>Scopes</c> field on <c>VideoUnitRequest</c> (kept for forward
/// compatibility with the unit-level scope DTOs; the controller does not
/// currently expose dedicated unit-scope append/replace/delete endpoints).
/// </summary>
public sealed class VideoUnitExampleProvider : EndpointExampleProvider
{
    public override EndpointExampleSet? GetExamples(string method, string route)
    {
        // ENDPOINT 1 — POST api/video-units — Create unit
        if (method == "POST" && route == "api/video-units")
            return CreateUnit();

        // ENDPOINT 2 — PUT api/video-units/{unitid} — Rename/update unit
        if (method == "PUT" && route == "api/video-units/{unitid}")
            return UpdateUnit();

        // ENDPOINT 3 — DELETE api/video-units/{unitid} — Soft-delete unit
        if (method == "DELETE" && route == "api/video-units/{unitid}")
            return DeleteUnit();

        // ENDPOINT 4 — GET api/video-units — Paginated unit list with rolled-up aggregates
        if (method == "GET" && route == "api/video-units")
            return GetTeacherUnits();

        // ENDPOINT 5 — GET api/video-units/{unitid}/videos — Videos-in-unit drill-down
        if (method == "GET" && route == "api/video-units/{unitid}/videos")
            return GetVideosInUnit();

        // ENDPOINT 6 — GET api/video-units/{unitid} — Unit detail with its scopes
        if (method == "GET" && route == "api/video-units/{unitid}")
            return GetUnitById();

        return null;
    }

    // ─────────────────────────── shared builders ───────────────────────────

    /// <summary>
    /// Fresh scope row array matching <c>VideoScopeInputDto</c> — the shape
    /// used both for <c>VideoUnitRequest.Scopes</c> at create time and for
    /// the <c>GET /{unitid}</c> response.
    /// </summary>
    private static JsonArray SeededScopeInputArray() => new()
    {
        new JsonObject { ["scopeType"] = "Session", ["sessionId"] = 33, ["sessionGroupId"] = (long?)null },
        new JsonObject { ["scopeType"] = "SessionGroup", ["sessionId"] = (long?)null, ["sessionGroupId"] = 5 }
    };

    /// <summary>
    /// Fresh row for the teacher unit list (endpoint 4) — matches
    /// <c>TeacherVideoUnitListItemDto</c> exactly.
    /// </summary>
    private static JsonObject SeededUnitListItem() => new()
    {
        ["id"] = 3,                                                             // illustrative — do not hardcode
        ["title"] = "Mathematics — Unit 1",
        ["description"] = "Algebra fundamentals: linear equations through quadratics.",
        ["videoCount"] = 4,
        ["seenStudentCount"] = 12,
        ["unseenStudentCount"] = 6,
        ["createdAt"] = "2026-06-15T07:30:00Z"
    };

    /// <summary>
    /// Fresh row for the videos-in-unit drill-down (endpoint 5) — matches
    /// <c>TeacherVideoListItemDto</c>, same shape as
    /// <c>VideoExampleProvider.SeededTeacherVideoListItem</c>.
    /// </summary>
    private static JsonObject SeededVideoInUnitRow() => new()
    {
        ["id"] = 42,
        ["title"] = "Newton's Laws — Lecture 1",
        ["description"] = "Watch before next class.",
        ["sourceUrl"] = "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
        ["sourceType"] = "YouTube",
        ["durationSeconds"] = 2730,
        ["studentsInScope"] = 18,
        ["totalOpens"] = 27,
        ["seenStudentCount"] = 12,
        ["unseenStudentCount"] = 6,
        ["createdAt"] = "2026-06-20T08:00:00Z"
    };

    private EndpointExampleSet CreateUnit() => new()
    {
        RequestBodyExamples = new Dictionary<string, JsonNode>
        {
            ["Minimal — title only"] = new JsonObject
            {
                ["title"] = "Mathematics — Unit 1",
                ["description"] = (string?)null,
                ["scopes"] = (string?)null
            },
            ["Full — title, description, and initial scopes"] = new JsonObject
            {
                ["title"] = "Mathematics — Unit 1",
                ["description"] = "Algebra fundamentals: linear equations through quadratics.",
                ["scopes"] = SeededScopeInputArray()
            }
        },
        Responses = new Dictionary<string, JsonNode>
        {
            ["201"] = SuccessEnvelope("Unit created successfully.", new JsonObject
            {
                ["videoUnitId"] = 3
            }),
            ["401"] = FailureEnvelope("Unauthorized."),
            ["403"] = FailureEnvelope("You do not have permission to manage videos.")
        }
    };

    private EndpointExampleSet UpdateUnit() => new()
    {
        RequestBody = new JsonObject
        {
            ["title"] = "Mathematics — Unit 1 (Revised)",
            ["description"] = "Algebra fundamentals: linear equations through quadratics, updated syllabus.",
            ["scopes"] = (string?)null
        },
        Responses = new Dictionary<string, JsonNode>
        {
            ["200"] = SuccessEnvelope("Unit updated successfully.", true),
            ["401"] = FailureEnvelope("Unauthorized."),
            ["403"] = FailureEnvelope("You do not have permission to manage videos."),
            ["404"] = FailureEnvelope("Unit not found.")
        }
    };

    private EndpointExampleSet DeleteUnit() => new()
    {
        Responses = new Dictionary<string, JsonNode>
        {
            ["200"] = SuccessEnvelope("Unit deleted successfully.", true),
            ["401"] = FailureEnvelope("Unauthorized."),
            ["403"] = FailureEnvelope("You do not have permission to manage videos."),
            ["404"] = FailureEnvelope("Unit not found.")
        }
    };

    private EndpointExampleSet GetTeacherUnits() => new()
    {
        Responses = new Dictionary<string, JsonNode>
        {
            ["200"] = SuccessEnvelope("Operation completed successfully.", new JsonObject
            {
                ["data"] = new JsonArray { SeededUnitListItem(), SeededUnitListItem() },
                ["page"] = 1,
                ["pageSize"] = 20,
                ["totalCount"] = 2,
                ["totalPages"] = 1
            }),
            ["401"] = FailureEnvelope("Unauthorized."),
            ["403"] = FailureEnvelope("You do not have permission to view videos.")
        }
    };

    private EndpointExampleSet GetVideosInUnit() => new()
    {
        Responses = new Dictionary<string, JsonNode>
        {
            ["200"] = SuccessEnvelope("Operation completed successfully.", new JsonObject
            {
                ["data"] = new JsonArray { SeededVideoInUnitRow() },
                ["page"] = 1,
                ["pageSize"] = 20,
                ["totalCount"] = 1,
                ["totalPages"] = 1
            }),
            ["401"] = FailureEnvelope("Unauthorized."),
            ["403"] = FailureEnvelope("You do not have permission to view videos."),
            ["404"] = FailureEnvelope("Unit not found.")
        }
    };

    private EndpointExampleSet GetUnitById() => new()
    {
        Responses = new Dictionary<string, JsonNode>
        {
            ["200"] = SuccessEnvelope("Operation completed successfully.", new JsonObject
            {
                ["id"] = 3,
                ["title"] = "Mathematics — Unit 1",
                ["description"] = "Algebra fundamentals: linear equations through quadratics.",
                ["scopes"] = SeededScopeInputArray()
            }),
            ["401"] = FailureEnvelope("Unauthorized."),
            ["403"] = FailureEnvelope("You do not have permission to view videos."),
            ["404"] = FailureEnvelope("Unit not found.")
        }
    };
}
