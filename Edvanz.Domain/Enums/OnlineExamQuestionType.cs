namespace Edvanz.Domain.Enums;

/// <summary>
/// Question type for an <see cref="Entities.OnlineExamQuestion"/>.
/// SingleChoice — exactly one option must be IsCorrect (all-or-nothing grading, §3.4).
/// MultipleChoice — one or more options must be IsCorrect (partial credit, QB/§3.4).
/// App-enforced at create/edit time — not a DB CHECK constraint (no polymorphic FK here).
/// Numeric values are part of the persisted contract — never reorder.
/// </summary>
public enum OnlineExamQuestionType : byte
{
    SingleChoice = 1,
    MultipleChoice = 2
}