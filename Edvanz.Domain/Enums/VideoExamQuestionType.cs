namespace Edvanz.Domain.Enums;

/// <summary>
/// Question type for a <see cref="Entities.VideoExamQuestion"/>. Both types are
/// answer-option-based; they differ only in how many options may be marked
/// correct — enforced at the application layer, not by a DB CHECK constraint
/// (no polymorphic FK here, unlike VideoScopeType).
///
/// SingleChoice — exactly one option must be IsCorrect.
/// MultipleChoice — one or more options must be IsCorrect.
///
/// Numeric values are part of the persisted contract — never reorder.
/// </summary>
public enum VideoExamQuestionType : byte
{
    SingleChoice = 1,
    MultipleChoice = 2,
}