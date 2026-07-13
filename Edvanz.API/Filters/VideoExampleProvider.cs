using System.Text.Json.Nodes;

namespace Edvanz.API.Filters;

/// <summary>
/// Swagger request/response examples for the Video Content Management module
/// (Module 14) — <c>VideosController</c>. Unit-hierarchy examples
/// (<c>VideoUnitsController</c>) live in <see cref="VideoUnitExampleProvider"/>.
///
/// All examples use real seeded identities from DbInitializer — teacher1
/// "Ahmed Mostafa" and the default seed password "Edvanz@2026". Numeric IDs
/// (id, videoAssetId, unitId, scopeId, attachmentId) are illustrative only:
/// they are identity-generated and are NOT stable across rebuilds, so
/// clients must never hardcode them.
///
/// SCOPE MODEL: <c>VideoScopeType</c> currently has two members —
/// <c>Session</c> and <c>SessionGroup</c>. There is no per-student scope
/// target on this module (unlike some other Module 6 assignment flows) —
/// every example below scopes by Session or SessionGroup only.
///
/// MULTIPART ENDPOINTS: <c>POST /api/videos</c>, <c>PUT /api/videos/{id}</c>,
/// and <c>PUT /api/videos/{id}/thumbnail</c> are <c>multipart/form-data</c>.
/// The JSON request payload travels in a form field named <c>request</c>;
/// file parts (<c>thumbnail</c>, <c>attachment</c>) are documented as
/// descriptive placeholders since binary content can't be expressed as JSON.
/// </summary>
public sealed class VideoExampleProvider : EndpointExampleProvider
{
    public override EndpointExampleSet? GetExamples(string method, string route)
    {
        // ENDPOINT 1 — POST api/videos — Create video (multipart, merged create+scopes+exam)
        if (method == "POST" && route == "api/videos")
            return CreateVideo();

        // ENDPOINT 2 — POST api/videos/{videoassetid}/scopes — Append scopes
        if (method == "POST" && route == "api/videos/{videoassetid}/scopes")
            return AppendScopes();

        // ENDPOINT 3 — PUT api/videos/{videoassetid}/scopes — Replace all scopes
        if (method == "PUT" && route == "api/videos/{videoassetid}/scopes")
            return ReplaceScopes();

        // ENDPOINT 4 — DELETE api/videos/{videoassetid}/scopes/{scopeid} — Remove single scope
        if (method == "DELETE" && route == "api/videos/{videoassetid}/scopes/{scopeid}")
            return RemoveScope();

        // ENDPOINT 5 — DELETE api/videos/{videoassetid} — Hard-delete video (audit snapshot)
        if (method == "DELETE" && route == "api/videos/{videoassetid}")
            return DeleteVideo();

        // ENDPOINT 6 — PUT api/videos/{videoassetid} — Update video (multipart, optimistic concurrency)
        if (method == "PUT" && route == "api/videos/{videoassetid}")
            return UpdateVideo();

        // ENDPOINT 7 — GET api/videos/{videoassetid} — Edit pre-fill (base fields + Exam + Scopes)
        if (method == "GET" && route == "api/videos/{videoassetid}")
            return GetVideoDetail();

        // ENDPOINT 8 — GET api/videos/{videoassetid}/overview — Read-only overview (analytics summary)
        if (method == "GET" && route == "api/videos/{videoassetid}/overview")
            return GetVideoOverview();

        // ENDPOINT 9 — PATCH api/videos/{videoassetid}/status — Set explicit Draft/Published + schedule
        if (method == "PATCH" && route == "api/videos/{videoassetid}/status")
            return SetVideoStatus();

        // ENDPOINT 10 — PATCH api/videos/{videoassetid}/status/toggle — Flip Draft <-> Published
        if (method == "PATCH" && route == "api/videos/{videoassetid}/status/toggle")
            return ToggleVideoStatus();

        // ENDPOINT 11 — GET api/videos/teacher — Paginated teacher video list
        if (method == "GET" && route == "api/videos/teacher")
            return GetTeacherVideos();

        // ENDPOINT 12 — GET api/videos/{videoassetid}/analytics — Per-video analytics report
        if (method == "GET" && route == "api/videos/{videoassetid}/analytics")
            return GetAnalytics();

        // ENDPOINT 13 — PUT api/videos/{videoassetid}/thumbnail — Replace thumbnail (multipart)
        if (method == "PUT" && route == "api/videos/{videoassetid}/thumbnail")
            return ReplaceThumbnail();

        // ENDPOINT 14 — GET api/videos/{videoassetid}/attachments/{attachmentid}/download — 302 redirect
        if (method == "GET" && route == "api/videos/{videoassetid}/attachments/{attachmentid}/download")
            return DownloadAttachment();

        // ENDPOINT 15 — PUT api/videos/{videoassetid}/units — Replace-all unit assignment
        if (method == "PUT" && route == "api/videos/{videoassetid}/units")
            return AssignVideoUnits();

        return null;
    }

    // ─────────────────────────── shared builders ───────────────────────────

    /// <summary>
    /// Fresh <c>VideoAttachmentDto</c> for the seeded video's PDF handout.
    /// New instance per call — JsonNodes cannot be re-parented across examples.
    /// </summary>
    private static JsonObject SeededAttachmentDto() => new()
    {
        ["id"] = 501,                                                            // illustrative — do not hardcode
        ["fileName"] = "newtons-laws-lecture-notes.pdf",
        ["contentType"] = "application/pdf",
        ["fileSizeBytes"] = 2_458_112,
        ["readUrl"] = "https://edvanzstorage.blob.core.windows.net/videos/1/42/3b7f...-newtons-laws-lecture-notes.pdf?sv=2024-05-04&sig=REDACTED",
        ["createdAt"] = "2026-07-01T09:15:00Z"
    };

    /// <summary>
    /// Fresh scope group array (Session + SessionGroup) matching
    /// <c>CreateVideoScopeDto</c>/<c>VideoScopeInputDto</c> shape.
    /// </summary>
    private static JsonArray SeededCreateScopesArray() => new()
    {
        new JsonObject
        {
            ["scopeType"] = "Session",
            ["ids"] = new JsonArray { 33 }
        },
        new JsonObject
        {
            ["scopeType"] = "SessionGroup",
            ["ids"] = new JsonArray { 5 }
        }
    };

    /// <summary>
    /// Fresh exam definition matching <c>CreateExamDto</c> — one SingleChoice
    /// and one MultipleChoice question, following the answer-key convention
    /// (<c>isCorrect</c> per option).
    /// </summary>
    private static JsonObject SeededExamDto() => new()
    {
        ["title"] = "Newton's Laws — Quick Check",
        ["description"] = "Two-question comprehension check, graded automatically.",
        ["questions"] = new JsonArray
        {
            new JsonObject
            {
                ["text"] = "Which law states that force equals mass times acceleration?",
                ["questionType"] = "SingleChoice",
                ["options"] = new JsonArray
                {
                    new JsonObject { ["text"] = "Newton's First Law", ["isCorrect"] = false },
                    new JsonObject { ["text"] = "Newton's Second Law", ["isCorrect"] = true },
                    new JsonObject { ["text"] = "Newton's Third Law", ["isCorrect"] = false }
                }
            },
            new JsonObject
            {
                ["text"] = "Which of the following are examples of Newton's Third Law in action?",
                ["questionType"] = "MultipleChoice",
                ["options"] = new JsonArray
                {
                    new JsonObject { ["text"] = "A rocket expelling gas to move forward", ["isCorrect"] = true },
                    new JsonObject { ["text"] = "A book resting motionless on a table", ["isCorrect"] = true },
                    new JsonObject { ["text"] = "A ball rolling to a stop due to friction", ["isCorrect"] = false }
                }
            }
        }
    };

    /// <summary>
    /// Fresh base fields shared by <c>VideoDetailDto</c>/<c>VideoOverviewDto</c>
    /// for the seeded video "Newton's Laws — Lecture 1" (teacher1, Ahmed Mostafa).
    /// </summary>
    private static JsonObject SeededVideoBaseFields() => new()
    {
        ["id"] = 42,                                                             // illustrative — do not hardcode
        ["title"] = "Newton's Laws — Lecture 1",
        ["description"] = "Watch before next class.",
        ["sourceType"] = "YouTube",
        ["sourceUrl"] = "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
        ["durationSeconds"] = 2730,
        ["publishDate"] = null,
        ["status"] = "Published",
        ["rowVersion"] = "AAAAAAAAB9E=",                                         // base64 concurrency token
        ["attachment"] = SeededAttachmentDto()
    };

    private EndpointExampleSet CreateVideo() => new()
    {
        RequestBodyExamples = new Dictionary<string, JsonNode>
        {
            ["Minimal — title + URL only, scope-less"] = new JsonObject
            {
                ["request"] = new JsonObject
                {
                    ["title"] = "Newton's Laws — Lecture 1",
                    ["description"] = (string?)null,
                    ["sourceUrl"] = "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
                    ["scopes"] = (string?)null,
                    ["exam"] = (string?)null
                },
                ["thumbnail"] = "(not provided)",
                ["attachment"] = "(not provided)"
            },
            ["Full — scopes + exam + thumbnail + PDF attachment"] = new JsonObject
            {
                ["request"] = new JsonObject
                {
                    ["title"] = "Newton's Laws — Lecture 1",
                    ["description"] = "Watch before next class.",
                    ["sourceUrl"] = "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
                    ["scopes"] = SeededCreateScopesArray(),
                    ["exam"] = SeededExamDto()
                },
                ["thumbnail"] = "(binary — image/jpeg or image/png, max 5 MB)",
                ["attachment"] = "(binary — application/pdf, max 25 MB)"
            }
        },
        Responses = new Dictionary<string, JsonNode>
        {
            ["201"] = SuccessEnvelope("Video created successfully.", new JsonObject
            {
                ["videoAssetId"] = 42,
                ["thumbnailReadUrl"] = "https://edvanzstorage.blob.core.windows.net/videos/1/42/thumbnail-9c2e....jpg?sv=2024-05-04&sig=REDACTED",
                ["attachment"] = SeededAttachmentDto(),
                ["scopesAdded"] = 2,
                ["studentsInScope"] = 18,
                ["examId"] = 7
            }),
            ["400"] = FailureEnvelope("The provided source URL could not be parsed."),
            ["401"] = FailureEnvelope("Unauthorized."),
            ["403"] = FailureEnvelope("You do not have permission to manage videos."),
            ["422"] = FailureEnvelope("Only JPEG or PNG images are allowed for thumbnails.")
        }
    };

    private EndpointExampleSet AppendScopes() => new()
    {
        RequestBody = new JsonObject
        {
            ["scopes"] = new JsonArray
            {
                new JsonObject { ["scopeType"] = "Session", ["sessionId"] = 33, ["sessionGroupId"] = (long?)null },
                new JsonObject { ["scopeType"] = "SessionGroup", ["sessionId"] = (long?)null, ["sessionGroupId"] = 5 }
            }
        },
        Responses = new Dictionary<string, JsonNode>
        {
            ["200"] = SuccessEnvelope("Scopes added successfully.", new JsonObject
            {
                ["scopesAdded"] = 2,
                ["scopesSkipped"] = 0,
                ["studentsInScope"] = 18
            }),
            ["400"] = FailureEnvelope("Exactly one of sessionId or sessionGroupId must be provided per scope."),
            ["401"] = FailureEnvelope("Unauthorized."),
            ["403"] = FailureEnvelope("You do not have permission to manage videos."),
            ["404"] = FailureEnvelope("Video not found.")
        }
    };

    private EndpointExampleSet ReplaceScopes() => new()
    {
        RequestBody = new JsonObject
        {
            ["scopes"] = new JsonArray
            {
                new JsonObject { ["scopeType"] = "Session", ["sessionId"] = 33, ["sessionGroupId"] = (long?)null }
            }
        },
        Responses = new Dictionary<string, JsonNode>
        {
            ["200"] = SuccessEnvelope("Scopes replaced successfully.", new JsonObject
            {
                ["scopesAdded"] = 1,
                ["scopesRemoved"] = 2,
                ["studentsInScope"] = 12
            }),
            ["400"] = FailureEnvelope("Scopes cannot be empty — provide at least one scope, or delete the video instead."),
            ["401"] = FailureEnvelope("Unauthorized."),
            ["403"] = FailureEnvelope("You do not have permission to manage videos."),
            ["404"] = FailureEnvelope("Video not found.")
        }
    };

    private EndpointExampleSet RemoveScope() => new()
    {
        Responses = new Dictionary<string, JsonNode>
        {
            ["200"] = SuccessEnvelope("Scope removed successfully.", true),
            ["400"] = FailureEnvelope("Cannot remove the last scope — delete the video instead."),
            ["401"] = FailureEnvelope("Unauthorized."),
            ["403"] = FailureEnvelope("You do not have permission to manage videos."),
            ["404"] = FailureEnvelope("Scope not found.")
        }
    };

    private EndpointExampleSet DeleteVideo() => new()
    {
        Responses = new Dictionary<string, JsonNode>
        {
            ["200"] = SuccessEnvelope("Video deleted successfully.", true),
            ["401"] = FailureEnvelope("Unauthorized."),
            ["403"] = FailureEnvelope("You do not have permission to manage videos."),
            ["404"] = FailureEnvelope("Video not found.")
        }
    };

    private EndpointExampleSet UpdateVideo() => new()
    {
        RequestBodyExamples = new Dictionary<string, JsonNode>
        {
            ["Field-only edit — no scope/exam/attachment change"] = new JsonObject
            {
                ["request"] = new JsonObject
                {
                    ["title"] = "Newton's Laws — Lecture 1 (Revised)",
                    ["description"] = "Watch before next class. Updated with corrected timestamps.",
                    ["sourceUrl"] = "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
                    ["publishDate"] = (string?)null,
                    ["status"] = "Published",
                    ["unitIds"] = (string?)null,
                    ["durationSeconds"] = (int?)null,
                    ["scopes"] = (string?)null,
                    ["exam"] = (string?)null,
                    ["removeAttachment"] = false,
                    ["rowVersion"] = "AAAAAAAAB9E="
                },
                ["attachment"] = "(not provided)"
            },
            ["Full edit — scopes + exam replaced, manual duration, attachment swapped"] = new JsonObject
            {
                ["request"] = new JsonObject
                {
                    ["title"] = "Newton's Laws — Lecture 1 (Revised)",
                    ["description"] = "Watch before next class. Updated with corrected timestamps.",
                    ["sourceUrl"] = "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
                    ["publishDate"] = "2026-07-20T06:00:00Z",
                    ["status"] = "Published",
                    ["unitIds"] = new JsonArray { 3 },
                    ["durationSeconds"] = 2745,
                    ["scopes"] = new JsonArray
                    {
                        new JsonObject { ["scopeType"] = "Session", ["sessionId"] = 33, ["sessionGroupId"] = (long?)null }
                    },
                    ["exam"] = SeededExamDto(),
                    ["removeAttachment"] = false,
                    ["rowVersion"] = "AAAAAAAAB9E="
                },
                ["attachment"] = "(binary — application/pdf, max 25 MB — replaces the current attachment)"
            }
        },
        Responses = new Dictionary<string, JsonNode>
        {
            ["200"] = SuccessEnvelope("Video updated successfully.", new JsonObject
            {
                ["warning"] = (string?)null,
                ["exam"] = SeededExamDto(),
                ["scopes"] = SeededCreateScopesArray(),
                ["id"] = SeededVideoBaseFields()["id"]!.DeepClone(),
                ["title"] = "Newton's Laws — Lecture 1 (Revised)",
                ["description"] = "Watch before next class. Updated with corrected timestamps.",
                ["sourceType"] = "YouTube",
                ["sourceUrl"] = "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
                ["durationSeconds"] = 2745,
                ["publishDate"] = "2026-07-20T06:00:00Z",
                ["status"] = "Published",
                ["rowVersion"] = "AAAAAAAAB9I=",
                ["attachment"] = SeededAttachmentDto()
            }),
            ["400"] = FailureEnvelope("The provided source URL could not be parsed."),
            ["401"] = FailureEnvelope("Unauthorized."),
            ["403"] = FailureEnvelope("You do not have permission to manage videos."),
            ["404"] = FailureEnvelope("Video not found."),
            ["409"] = FailureEnvelope("This video was modified by someone else. Reload and try again."),
            ["422"] = FailureEnvelope("Only PDF files are allowed for attachments.")
        }
    };

    private EndpointExampleSet GetVideoDetail() => new()
    {
        Responses = new Dictionary<string, JsonNode>
        {
            ["200"] = SuccessEnvelope("Operation completed successfully.", new JsonObject
            {
                ["warning"] = (string?)null,
                ["exam"] = SeededExamDto(),
                ["scopes"] = SeededCreateScopesArray(),
                ["id"] = 42,
                ["title"] = "Newton's Laws — Lecture 1",
                ["description"] = "Watch before next class.",
                ["sourceType"] = "YouTube",
                ["sourceUrl"] = "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
                ["durationSeconds"] = 2730,
                ["publishDate"] = (string?)null,
                ["status"] = "Published",
                ["rowVersion"] = "AAAAAAAAB9E=",
                ["attachment"] = SeededAttachmentDto()
            }),
            ["401"] = FailureEnvelope("Unauthorized."),
            ["403"] = FailureEnvelope("You do not have permission to view videos."),
            ["404"] = FailureEnvelope("Video not found.")
        }
    };

    private EndpointExampleSet GetVideoOverview() => new()
    {
        Responses = new Dictionary<string, JsonNode>
        {
            ["200"] = SuccessEnvelope("Operation completed successfully.", new JsonObject
            {
                ["seenStudentCount"] = 12,
                ["unseenStudentCount"] = 6,
                ["completedStudentCount"] = 9,
                ["id"] = 42,
                ["title"] = "Newton's Laws — Lecture 1",
                ["description"] = "Watch before next class.",
                ["sourceType"] = "YouTube",
                ["sourceUrl"] = "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
                ["durationSeconds"] = 2730,
                ["publishDate"] = (string?)null,
                ["status"] = "Published",
                ["rowVersion"] = "AAAAAAAAB9E=",
                ["attachment"] = SeededAttachmentDto()
            }),
            ["401"] = FailureEnvelope("Unauthorized."),
            ["403"] = FailureEnvelope("You do not have permission to view videos."),
            ["404"] = FailureEnvelope("Video not found.")
        }
    };

    private EndpointExampleSet SetVideoStatus() => new()
    {
        RequestBodyExamples = new Dictionary<string, JsonNode>
        {
            ["Publish immediately"] = new JsonObject
            {
                ["status"] = "Published",
                ["publishDate"] = (string?)null
            },
            ["Schedule for later"] = new JsonObject
            {
                ["status"] = "Published",
                ["publishDate"] = "2026-07-20T06:00:00Z"
            },
            ["Set to Draft (hide from students)"] = new JsonObject
            {
                ["status"] = "Draft",
                ["publishDate"] = (string?)null
            }
        },
        Responses = new Dictionary<string, JsonNode>
        {
            ["200"] = SuccessEnvelope("Video status updated successfully.", true),
            ["400"] = FailureEnvelope("Invalid status value."),
            ["401"] = FailureEnvelope("Unauthorized."),
            ["403"] = FailureEnvelope("You do not have permission to manage videos."),
            ["404"] = FailureEnvelope("Video not found.")
        }
    };

    private EndpointExampleSet ToggleVideoStatus() => new()
    {
        Responses = new Dictionary<string, JsonNode>
        {
            ["200"] = SuccessEnvelope("Video status updated successfully.", "Draft"),
            ["401"] = FailureEnvelope("Unauthorized."),
            ["403"] = FailureEnvelope("You do not have permission to manage videos."),
            ["404"] = FailureEnvelope("Video not found.")
        }
    };

    /// <summary>
    /// Fresh row for the teacher video list (endpoint 11) — matches
    /// <c>TeacherVideoListItemDto</c> exactly.
    /// </summary>
    private static JsonObject SeededTeacherVideoListItem() => new()
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

    private EndpointExampleSet GetTeacherVideos() => new()
    {
        Responses = new Dictionary<string, JsonNode>
        {
            ["200"] = SuccessEnvelope("Operation completed successfully.", new JsonObject
            {
                ["data"] = new JsonArray { SeededTeacherVideoListItem(), SeededTeacherVideoListItem() },
                ["page"] = 1,
                ["pageSize"] = 20,
                ["totalCount"] = 2,
                ["totalPages"] = 1
            }),
            ["401"] = FailureEnvelope("Unauthorized."),
            ["403"] = FailureEnvelope("You do not have permission to view videos."),
            ["404"] = FailureEnvelope("Teacher not found.")
        }
    };

    private EndpointExampleSet GetAnalytics() => new()
    {
        Responses = new Dictionary<string, JsonNode>
        {
            ["200"] = SuccessEnvelope("Operation completed successfully.", new JsonObject
            {
                ["videoAssetId"] = 42,
                ["title"] = "Newton's Laws — Lecture 1",
                ["durationSeconds"] = 2730,
                ["totalStudentsInScope"] = 18,
                ["totalStudentsWatched"] = 12,
                ["unseenCount"] = 6,
                ["completedCount"] = 9,
                ["rows"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["teacherStudentId"] = 1,
                        ["studentName"] = "Youssef Tarek",
                        ["studentCode"] = "STU000001",
                        ["sessionName"] = "Grade 10 — Physics — Sat 4PM",
                        ["hasOpened"] = true,
                        ["openCount"] = 3,
                        ["totalWatchSeconds"] = 2600,
                        ["videoDurationSeconds"] = 2730,
                        ["firstOpenedAt"] = "2026-06-21T10:05:00Z",
                        ["lastOpenedAt"] = "2026-06-23T19:40:00Z",
                        ["estimatedCompletionPct"] = 95,
                        ["rawWatchPct"] = 112
                    },
                    new JsonObject
                    {
                        ["teacherStudentId"] = 2,
                        ["studentName"] = "Ahmed Khaled",
                        ["studentCode"] = "AK001",
                        ["sessionName"] = "Grade 10 — Physics — Sat 4PM",
                        ["hasOpened"] = false,
                        ["openCount"] = 0,
                        ["totalWatchSeconds"] = 0,
                        ["videoDurationSeconds"] = 2730,
                        ["firstOpenedAt"] = (string?)null,
                        ["lastOpenedAt"] = (string?)null,
                        ["estimatedCompletionPct"] = (int?)null,
                        ["rawWatchPct"] = (int?)null
                    }
                },
                ["page"] = 1,
                ["pageSize"] = 50,
                ["totalCount"] = 18,
                ["totalPages"] = 1
            }),
            ["401"] = FailureEnvelope("Unauthorized."),
            ["403"] = FailureEnvelope("You do not have permission to view videos."),
            ["404"] = FailureEnvelope("Video not found.")
        }
    };

    private EndpointExampleSet ReplaceThumbnail() => new()
    {
        RequestBody = new JsonObject
        {
            ["thumbnail"] = "(binary — image/jpeg or image/png, max 5 MB, required)"
        },
        Responses = new Dictionary<string, JsonNode>
        {
            ["200"] = SuccessEnvelope("Thumbnail replaced successfully.", new JsonObject
            {
                ["readUrl"] = "https://edvanzstorage.blob.core.windows.net/videos/1/42/thumbnail-7fa1....png?sv=2024-05-04&sig=REDACTED"
            }),
            ["401"] = FailureEnvelope("Unauthorized."),
            ["403"] = FailureEnvelope("You do not have permission to manage videos."),
            ["404"] = FailureEnvelope("Video not found."),
            ["422"] = FailureEnvelope("Thumbnail exceeds the maximum allowed size.")
        }
    };

    /// <summary>
    /// 302 redirect — no JSON body on success (the client follows the
    /// Location header straight to Blob Storage). Only the failure envelopes
    /// are documented here; the 302 itself carries no response schema.
    /// </summary>
    private EndpointExampleSet DownloadAttachment() => new()
    {
        Responses = new Dictionary<string, JsonNode>
        {
            ["401"] = FailureEnvelope("Unauthorized."),
            ["403"] = FailureEnvelope("You do not have permission to view videos."),
            ["404"] = FailureEnvelope("Attachment not found.")
        }
    };

    private EndpointExampleSet AssignVideoUnits() => new()
    {
        RequestBodyExamples = new Dictionary<string, JsonNode>
        {
            ["Assign to one unit"] = new JsonObject
            {
                ["unitIds"] = new JsonArray { 3 }
            },
            ["Assign to multiple units"] = new JsonObject
            {
                ["unitIds"] = new JsonArray { 3, 4 }
            },
            ["Unlink from every unit (video becomes loose)"] = new JsonObject
            {
                ["unitIds"] = new JsonArray()
            }
        },
        Responses = new Dictionary<string, JsonNode>
        {
            ["200"] = SuccessEnvelope("Video units assigned successfully.", new JsonObject
            {
                ["unitIds"] = new JsonArray { 3 }
            }),
            ["400"] = FailureEnvelope("One or more of the specified units could not be found."),
            ["401"] = FailureEnvelope("Unauthorized."),
            ["403"] = FailureEnvelope("You do not have permission to manage videos."),
            ["404"] = FailureEnvelope("Video not found.")
        }
    };
}
