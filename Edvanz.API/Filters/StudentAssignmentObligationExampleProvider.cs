using System.Text.Json.Nodes;

namespace Edvanz.API.Filters;

/// <summary>
/// Swagger examples for StudentAssignmentObligationsController (Module 6 offline exams —
/// student-facing). Teacher-facing examples live in a future AssignmentObligationsExampleProvider;
/// this one only covers the student surface.
/// Uses seeded identities from DbInitializer (teacher1 "Ahmed Mostafa", student "Youssef Tarek").
/// Numeric IDs are illustrative — identity-generated, not stable across rebuilds.
/// </summary>
public sealed class StudentAssignmentObligationExampleProvider : EndpointExampleProvider
{
    public override EndpointExampleSet? GetExamples(string httpMethod, string normalizedRoute)
    {
        if (httpMethod == "GET" && normalizedRoute == "api/assignmentobligations/student/teachers/{teacherid}/exams")
            return GetMyExams();

        return null;
    }

    // ─────────────────────────── shared builders ───────────────────────────

    // NOTE: 401 body is illustrative — Unauthorized() in the controller actually returns
    // an empty 401 with no JSON body. Documented as an envelope here for readability,
    // same simplification StudentOnlineExamExampleProvider already makes.
    private static readonly JsonObject Unauthorized401 = FailureEnvelope("Unauthorized.");

    private static readonly JsonObject StudentUserNotFound404 = FailureEnvelope("StudentUserNotFound");

    private EndpointExampleSet GetMyExams() => new()
    {
        Responses = new Dictionary<string, JsonNode>
        {
            ["200"] = SuccessEnvelope("Success", new JsonObject
            {
                ["totalCount"] = 4,
                ["page"] = 1,
                ["pageSize"] = 20,
                ["totalPages"] = 1,
                ["data"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["examId"] = 24,
                        ["examName"] = "Midterm — Chapter 3",
                        ["description"] = "Bring calculator, no phones allowed",
                        ["date"] = "2026-08-01",
                        ["score"] = (decimal?)null,
                        ["scorePercentage"] = (decimal?)null,
                        ["status"] = "Pending"
                    },
                    new JsonObject
                    {
                        ["examId"] = 19,
                        ["examName"] = "Chapter 2 Quiz",
                        ["description"] = (string?)null,
                        ["date"] = "2026-07-10",
                        ["score"] = 17.5m,
                        ["scorePercentage"] = 87.5m,
                        ["status"] = "AttendedWithGrade"
                    },
                    new JsonObject
                    {
                        ["examId"] = 15,
                        ["examName"] = "Chapter 2 Quiz — Makeup Session",
                        ["description"] = (string?)null,
                        ["date"] = "2026-06-20",
                        ["score"] = (decimal?)null,
                        ["scorePercentage"] = (decimal?)null,
                        ["status"] = "Attended"
                    },
                    new JsonObject
                    {
                        ["examId"] = 12,
                        ["examName"] = "Chapter 1 Exam",
                        ["description"] = (string?)null,
                        ["date"] = "2026-06-01",
                        ["score"] = (decimal?)null,
                        ["scorePercentage"] = (decimal?)null,
                        ["status"] = "DidNotAttend"
                    }
                }
            }),
            ["401"] = Unauthorized401,
            ["404"] = StudentUserNotFound404
        },

        ResponseExamples = new Dictionary<string, IReadOnlyDictionary<string, JsonNode>>
        {
            ["403"] = new Dictionary<string, JsonNode>
            {
                ["No active link with this teacher"] = FailureEnvelope("TeacherLinkNotFound"),
                ["Linked but roster record was removed"] = FailureEnvelope("StudentEnrollmentRemoved")
            }
        }
    };
}