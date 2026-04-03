using System.Text.Json.Serialization;

namespace Edvanz.Domain.Enums;

/// <summary>
/// Identifies the type of payment report to generate.
/// REQ-PAY-048 through REQ-PAY-065: Ten report types.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PaymentReportType : byte
{
    /// <summary>
    /// REQ-PAY-052: Single Student Payment History Report.
    /// </summary>
    SingleStudentHistory = 1,

    /// <summary>
    /// REQ-PAY-053: Session Payment Report.
    /// </summary>
    SessionPayment = 2,

    /// <summary>
    /// REQ-PAY-054: All Sessions Payment Summary Report.
    /// </summary>
    AllSessionsSummary = 3,

    /// <summary>
    /// REQ-PAY-055: Session Group Payment Report.
    /// </summary>
    SessionGroupPayment = 4,

    /// <summary>
    /// REQ-PAY-056: Unpaid Students Report.
    /// </summary>
    UnpaidStudents = 5,

    /// <summary>
    /// REQ-PAY-057: Consecutive Non-Payment Report.
    /// </summary>
    ConsecutiveNonPayment = 6,

    /// <summary>
    /// REQ-PAY-058: Collector Performance Report.
    /// </summary>
    CollectorPerformance = 7,

    /// <summary>
    /// REQ-PAY-059: Assistant Wallet History Report.
    /// </summary>
    AssistantWalletHistory = 8,

    /// <summary>
    /// REQ-PAY-060: Daily Collection Report.
    /// </summary>
    DailyCollection = 9,

    /// <summary>
    /// REQ-PAY-061: Period Revenue Report.
    /// </summary>
    PeriodRevenue = 10
}