using System.Text.Json.Nodes;

namespace Edvanz.API.Filters;

/// <summary>
/// Swagger examples for the Online Exam Module — OnlineExamsController (teacher/assistant).
/// Student-facing examples live in StudentOnlineExamExampleProvider.
/// Uses seeded identities from DbInitializer (teacher1 "Ahmed Mostafa").
/// Numeric IDs are illustrative — identity-generated, not stable across rebuilds.
/// </summary>
public sealed class OnlineExamExampleProvider : EndpointExampleProvider
{
    public override EndpointExampleSet? GetExamples(string httpMethod, string normalizedRoute)
    {
        if (httpMethod == "POST" && normalizedRoute == "api/online-exams") return Create();
        if (httpMethod == "PUT" && normalizedRoute == "api/online-exams/{onlineexamid}") return Update();
        if (httpMethod == "GET" && normalizedRoute == "api/online-exams/{onlineexamid}") return GetById();
        if (httpMethod == "GET" && normalizedRoute == "api/online-exams") return GetList();
        if (httpMethod == "GET" && normalizedRoute == "api/online-exams/{onlineexamid}/overview") return GetOverview();
        if (httpMethod == "GET" && normalizedRoute == "api/online-exams/{onlineexamid}/scope-analysis") return GetScopeAnalysis();
        if (httpMethod == "GET" && normalizedRoute == "api/online-exams/{onlineexamid}/questions") return GetQuestions();
        if (httpMethod == "GET" && normalizedRoute == "api/online-exams/{onlineexamid}/questions/overview") return GetQuestionsOverview();
        if (httpMethod == "POST" && normalizedRoute == "api/online-exams/{onlineexamid}/questions") return AddQuestion();
        if (httpMethod == "POST" && normalizedRoute == "api/online-exams/{onlineexamid}/questions/bulk") return AddQuestionsBulk();
        if (httpMethod == "PUT" && normalizedRoute == "api/online-exams/{onlineexamid}/questions") return ReplaceQuestions();
        if (httpMethod == "PATCH" && normalizedRoute == "api/online-exams/{onlineexamid}/status") return UpdateStatus();
        if (httpMethod == "PATCH" && normalizedRoute == "api/online-exams/{onlineexamid}/students/{teacherstudentid}/status") return UpdateStudentStatus();
        if (httpMethod == "DELETE" && normalizedRoute == "api/online-exams/{onlineexamid}") return Delete();
        return null;
    }

    // ─────────────────────────── shared builders ───────────────────────────

    private static JsonObject SeededScope() => new()
    {
        ["scopeType"] = "Session",
        ["sessionId"] = 33,
        ["sessionGroupId"] = (long?)null
    };

    private static JsonObject SeededOption(string text, bool correct) => new()
    {
        ["optionText"] = text,
        ["isCorrect"] = correct
    };

    private static JsonObject SeededQuestion() => new()
    {
        ["questionText"] = "What is the capital of Egypt?",
        ["questionType"] = "SingleChoice",
        ["degree"] = 5,
        ["options"] = new JsonArray { SeededOption("Cairo", true), SeededOption("Alexandria", false) }
    };

    private static JsonObject ExamDetailPayload() => new()
    {
        ["id"] = 7,
        ["teacherSubjectId"] = 12,
        ["subjectName"] = "Mathematics",
        ["title"] = "Midterm — Algebra Basics",
        ["status"] = "Draft",
        ["startDateTime"] = "2026-08-01T09:00:00Z",
        ["endDateTime"] = "2026-08-01T11:00:00Z",
        ["passPercentage"] = 60,
        ["visibility"] = true,
        ["scopes"] = new JsonArray { SeededScope() },
        ["rowVersion"] = "AAAAAAAAB9E="
    };

    // ─────────────────────────── per-endpoint ───────────────────────────

    private EndpointExampleSet Create() => new()
    {
        RequestBody = new JsonObject
        {
            ["teacherSubjectId"] = 12,
            ["title"] = "Midterm — Algebra Basics",
            ["description"] = "Covers chapters 1-4",
            ["instructions"] = "You have until the window closes. No going back once submitted.",
            ["startDateTime"] = "2026-08-01T09:00:00Z",
            ["endDateTime"] = "2026-08-01T11:00:00Z",
            ["passPercentage"] = 60,
            ["visibility"] = true,
            ["scopes"] = new JsonArray { SeededScope() },
            ["questions"] = new JsonArray { SeededQuestion() }
        },
        Responses = new Dictionary<string, JsonNode>
        {
            ["201"] = SuccessEnvelope("Exam created successfully", ExamDetailPayload()),
            ["400"] = FailureEnvelope("Pass percentage must be between 0 and 100")
        }
    };

    private EndpointExampleSet Update() => new()
    {
        RequestBody = new JsonObject
        {
            ["teacherSubjectId"] = 12,
            ["title"] = "Midterm — Algebra Basics (updated)",
            ["startDateTime"] = "2026-08-01T09:00:00Z",
            ["endDateTime"] = "2026-08-01T12:00:00Z",
            ["passPercentage"] = 65,
            ["visibility"] = true,
            ["rowVersion"] = "AAAAAAAAB9E="
        },
        Responses = new Dictionary<string, JsonNode>
        {
            ["200"] = SuccessEnvelope("Exam updated successfully", ExamDetailPayload()),
            ["409"] = FailureEnvelope("This exam was changed by someone else — please refresh and try again")
        }
    };

    private EndpointExampleSet GetById() => new()
    {
        Responses = new Dictionary<string, JsonNode>
        {
            ["200"] = SuccessEnvelope("Success", ExamDetailPayload()),
            ["404"] = FailureEnvelope("Exam not found")
        }
    };

    private EndpointExampleSet GetList() => new()
    {
        Responses = new Dictionary<string, JsonNode>
        {
            ["200"] = SuccessEnvelope("Success", new JsonObject
            {
                ["totalCount"] = 1,
                ["page"] = 1,
                ["pageSize"] = 20,
                ["totalPages"] = 1,
                ["data"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["id"] = 7,
                        ["title"] = "Midterm — Algebra Basics",
                        ["subjectName"] = "Mathematics",
                        ["status"] = "Published",
                        ["assignedCount"] = 25,
                        ["passedCount"] = 10,
                        ["failedCount"] = 3,
                        ["blockedCount"] = 0,
                        ["inProgressCount"] = 2
                    }
                }
            })
        }
    };

    private EndpointExampleSet GetOverview() => new()
    {
        Responses = new Dictionary<string, JsonNode>
        {
            ["200"] = SuccessEnvelope("Success", new JsonObject
            {
                ["examGrade"] = 20,
                ["passGrade"] = 12,
                ["assignedCount"] = 25,
                ["averagePercentage"] = 68.5,
                ["highestPercentage"] = 95,
                ["lowestPercentage"] = 30,
                ["passedCount"] = 10,
                ["failedCount"] = 3,
                ["blockedCount"] = 0,
                ["inProgressCount"] = 2,
                ["scopes"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["id"] = 1,
                        ["scopeType"] = "Session",
                        ["sessionId"] = 33,
                        ["sessionName"] = "Session A",
                        ["assignedCount"] = 25
                    }
                }
            })
        }
    };

    private EndpointExampleSet GetScopeAnalysis() => new()
    {
        Responses = new Dictionary<string, JsonNode>
        {
            ["200"] = SuccessEnvelope("Success", new JsonArray
            {
                new JsonObject
                {
                    ["teacherStudentId"] = 101,
                    ["studentName"] = "Youssef Tarek",
                    ["studentCode"] = "STU000001",
                    ["status"] = "Passed",
                    ["percentage"] = 80,
                    ["score"] = 16,
                    ["isOutOfScope"] = false
                },
                new JsonObject
                {
                    ["teacherStudentId"] = 205,
                    ["studentName"] = "Nour Adel",
                    ["studentCode"] = "STU000205",
                    ["status"] = "Failed",
                    ["percentage"] = 40,
                    ["score"] = 8,
                    ["isOutOfScope"] = true
                }
            })
        }
    };

    private EndpointExampleSet GetQuestions() => new()
    {
        Responses = new Dictionary<string, JsonNode>
        {
            ["200"] = SuccessEnvelope("Success", new JsonArray { SeededQuestion() })
        }
    };

    private EndpointExampleSet GetQuestionsOverview() => new()
    {
        Responses = new Dictionary<string, JsonNode>
        {
            ["200"] = SuccessEnvelope("Success", new JsonObject
            {
                ["singleChoiceCount"] = 8,
                ["multipleChoiceCount"] = 2,
                ["status"] = "Draft"
            })
        }
    };

    private EndpointExampleSet AddQuestion() => new()
    {
        RequestBody = SeededQuestion(),
        Responses = new Dictionary<string, JsonNode>
        {
            ["200"] = SuccessEnvelope("Exam updated successfully", true),
            ["409"] = FailureEnvelope("Questions can only be edited while the exam is a draft")
        }
    };

    private EndpointExampleSet AddQuestionsBulk() => new()
    {
        RequestBody = new JsonArray { SeededQuestion() },
        Responses = new Dictionary<string, JsonNode>
        {
            ["200"] = SuccessEnvelope("Exam updated successfully", true)
        }
    };

    private EndpointExampleSet ReplaceQuestions() => new()
    {
        RequestBody = new JsonObject { ["questions"] = new JsonArray { SeededQuestion() } },
        Responses = new Dictionary<string, JsonNode>
        {
            ["200"] = SuccessEnvelope("Exam updated successfully", true)
        }
    };

    private EndpointExampleSet UpdateStatus() => new()
    {
        RequestBody = new JsonObject { ["status"] = "Published", ["rowVersion"] = "AAAAAAAAB9E=" },
        Responses = new Dictionary<string, JsonNode>
        {
            ["200"] = SuccessEnvelope("Exam published successfully", true),
            ["400"] = FailureEnvelope("The exam needs at least one question and one scope before publishing"),
            ["409"] = FailureEnvelope("Can't unpublish — students have already submitted this exam")
        }
    };

    private EndpointExampleSet UpdateStudentStatus() => new()
    {
        RequestBody = new JsonObject { ["status"] = "Blocked" },
        Responses = new Dictionary<string, JsonNode>
        {
            ["200"] = SuccessEnvelope("Exam updated successfully", new JsonObject
            {
                ["percentage"] = 0,
                ["stars"] = 0,
                ["notAnswered"] = 10,
                ["correct"] = 0,
                ["wrong"] = 0
            })
        }
    };

    private EndpointExampleSet Delete() => new()
    {
        Responses = new Dictionary<string, JsonNode>
        {
            ["204"] = SuccessEnvelope("Exam deleted successfully", null!)
        }
    };
}