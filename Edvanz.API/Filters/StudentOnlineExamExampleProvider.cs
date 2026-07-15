using System.Text.Json.Nodes;

namespace Edvanz.API.Filters;

/// <summary>Swagger examples for StudentOnlineExamsController (S1–S6).</summary>
public sealed class StudentOnlineExamExampleProvider : EndpointExampleProvider
{
    public override EndpointExampleSet? GetExamples(string httpMethod, string normalizedRoute)
    {
        if (httpMethod == "GET" && normalizedRoute == "api/online-exams/student/teachers/{teacherid}") return GetMyExams();
        if (httpMethod == "GET" && normalizedRoute == "api/online-exams/student/teachers/{teacherid}/{onlineexamid}/questions") return GetTakeScreen();
        if (httpMethod == "POST" && normalizedRoute == "api/online-exams/student/teachers/{teacherid}/{onlineexamid}/answers") return SubmitAnswer();
        if (httpMethod == "POST" && normalizedRoute == "api/online-exams/student/teachers/{teacherid}/{onlineexamid}/submit") return SubmitExam();
        if (httpMethod == "GET" && normalizedRoute == "api/online-exams/student/teachers/{teacherid}/{onlineexamid}/result") return GetResult();
        if (httpMethod == "GET" && normalizedRoute == "api/online-exams/student/teachers/{teacherid}/{onlineexamid}/answers") return GetReview();
        return null;
    }

    private static readonly JsonObject Unauthorized401 = FailureEnvelope("Unauthorized.");
    private static readonly JsonObject NoActiveLink403 = FailureEnvelope("No active link found between your account and this teacher");
    private static readonly JsonObject NotAssignedToExam403 = FailureEnvelope("You're not assigned to this exam");
    private static readonly JsonObject StudentUserNotFound404 = FailureEnvelope("We couldn't find this student account. Please check and try again");
    private static readonly JsonObject ExamNotFound404 = FailureEnvelope("Exam not found");

    private static JsonObject StatsPayload(decimal pct, int stars, int notAnswered, int correct, int wrong) => new()
    {
        ["percentage"] = pct,
        ["stars"] = stars,
        ["notAnswered"] = notAnswered,
        ["correct"] = correct,
        ["wrong"] = wrong
    };

    private EndpointExampleSet GetMyExams() => new()
    {
        Responses = new Dictionary<string, JsonNode>
        {
            ["200"] = SuccessEnvelope("Success", new JsonObject
            {
                ["upcoming"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["examId"] = 7,
                        ["examName"] = "Midterm — Algebra Basics",
                        ["examDate"] = "2026-08-01",
                        ["examTime"] = "09:00:00",
                        ["duration"] = "02:00:00",
                        ["questionsCount"] = 10,
                        ["examDegree"] = 20,
                        ["studentDegree"] = (decimal?)null,
                        ["studentStatus"] = (string?)null
                    }
                },
                ["past"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["examId"] = 4,
                        ["examName"] = "Quiz — Fractions",
                        ["examDate"] = "2026-06-15",
                        ["examTime"] = "10:00:00",
                        ["duration"] = "00:30:00",
                        ["questionsCount"] = 5,
                        ["examDegree"] = 10,
                        ["studentDegree"] = 8,
                        ["studentStatus"] = "Passed"
                    }
                }
            }),
            ["401"] = Unauthorized401,
            ["403"] = NoActiveLink403,
            ["404"] = StudentUserNotFound404
        }
    };

    private EndpointExampleSet GetTakeScreen() => new()
    {
        Responses = new Dictionary<string, JsonNode>
        {
            ["200"] = SuccessEnvelope("Success", new JsonObject
            {
                ["examId"] = 7,
                ["examName"] = "Midterm — Algebra Basics",
                ["examDegree"] = 20,
                ["startDateTime"] = "2026-08-01T09:00:00Z",
                ["endDateTime"] = "2026-08-01T11:00:00Z",
                ["questions"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["id"] = 1,
                        ["questionText"] = "What is the capital of Egypt?",
                        ["questionType"] = "SingleChoice",
                        ["degree"] = 5,
                        ["sortOrder"] = 0,
                        ["options"] = new JsonArray
                        {
                            new JsonObject { ["id"] = 1, ["optionText"] = "Cairo", ["sortOrder"] = 0 },
                            new JsonObject { ["id"] = 2, ["optionText"] = "Alexandria", ["sortOrder"] = 1 }
                        }
                    }
                }
            }),
            ["401"] = Unauthorized401,
            ["403"] = NotAssignedToExam403,
            ["404"] = ExamNotFound404
        }
    };

    private EndpointExampleSet SubmitAnswer() => new()
    {
        RequestBody = new JsonObject { ["questionId"] = 1, ["selectedOptionIds"] = new JsonArray { 1 } },
        Responses = new Dictionary<string, JsonNode>
        {
            ["200"] = SuccessEnvelope("Success", StatsPayload(50, 1, 9, 1, 0)),
            ["400"] = FailureEnvelope("A single-choice question must have exactly one correct option"),
            ["401"] = Unauthorized401,
            ["403"] = NotAssignedToExam403,
            ["404"] = ExamNotFound404,
            ["409"] = FailureEnvelope("You've already submitted this exam")
        }
    };

    private EndpointExampleSet SubmitExam() => new()
    {
        RequestBody = new JsonObject
        {
            ["answers"] = new JsonArray
            {
                new JsonObject { ["questionId"] = 1, ["selectedOptionIds"] = new JsonArray { 1 } }
            }
        },
        Responses = new Dictionary<string, JsonNode>
        {
            ["200"] = SuccessEnvelope("Success", StatsPayload(80, 4, 0, 8, 2)),
            ["400"] = FailureEnvelope("One or more selected options are invalid for this question"),
            ["401"] = Unauthorized401,
            ["403"] = NotAssignedToExam403,
            ["404"] = ExamNotFound404,
            ["409"] = FailureEnvelope("You've already submitted this exam")
        }
    };

    private EndpointExampleSet GetResult() => new()
    {
        Responses = new Dictionary<string, JsonNode>
        {
            ["200"] = SuccessEnvelope("Success", StatsPayload(80, 4, 0, 8, 2)),
            ["401"] = Unauthorized401,
            ["403"] = NoActiveLink403,
            ["404"] = FailureEnvelope("No attempt found for this exam")
        }
    };

    private EndpointExampleSet GetReview() => new()
    {
        Responses = new Dictionary<string, JsonNode>
        {
            ["200"] = SuccessEnvelope("Success", new JsonObject
            {
                ["examId"] = 7,
                ["examName"] = "Midterm — Algebra Basics",
                ["finalized"] = true,
                ["reportStatus"] = "Passed",
                ["score"] = 16,
                ["percentage"] = 80,
                ["questions"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["questionId"] = 1,
                        ["questionText"] = "What is the capital of Egypt?",
                        ["questionType"] = "SingleChoice",
                        ["degree"] = 5,
                        ["awardedDegree"] = 5,
                        ["options"] = new JsonArray
                        {
                            new JsonObject { ["optionId"] = 1, ["optionText"] = "Cairo", ["isSelected"] = true, ["isCorrect"] = true },
                            new JsonObject { ["optionId"] = 2, ["optionText"] = "Alexandria", ["isSelected"] = false, ["isCorrect"] = false }
                        }
                    }
                }
            }),
            ["401"] = Unauthorized401,
            ["403"] = NoActiveLink403,
            ["404"] = ExamNotFound404
        }
    };
}
