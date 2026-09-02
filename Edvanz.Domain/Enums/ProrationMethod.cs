using System.Text.Json.Serialization;

namespace Edvanz.Domain.Enums;

/// <summary>
/// How the app SUGGESTS a joining-month (first-month) amount for a new student when the teacher
/// has proration enabled (<see cref="Edvanz.Domain.Entities.TeacherConfiguration.IsProratedPaymentEnabled"/>).
/// A method only ever produces a SUGGESTION — the teacher/assistant can always accept it or type an
/// exact per-student amount (which becomes a sticky manual override, see
/// <see cref="Edvanz.Domain.Entities.PaymentPeriod.IsProrationManual"/>).
///
/// REQ-PAY-021/022 (teacher-decided proration, 2026-09-02). Stored as int (default EF enum mapping).
/// Existing teachers default to <see cref="ByPercentage"/> — the exact behaviour they have today.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ProrationMethod
{
    /// <summary>
    /// Percentage-of-month tiers (the current, default model): the join day selects a
    /// <see cref="Edvanz.Domain.Entities.TeacherProratedTier"/> fraction of the full month.
    /// Anchored to the student's first attended class day.
    /// </summary>
    ByPercentage = 0,

    /// <summary>
    /// Count-of-classes model: bill (scheduled classes from the first attended class through
    /// month-end) ÷ (total classes that month) of the full amount. Anchored to the first class date.
    /// </summary>
    ByClasses = 1,

    /// <summary>
    /// The app never guesses — every new student's first month starts at the full price and a person
    /// types the exact joining amount on the collect screen.
    /// </summary>
    Manual = 2
}
