using System;
using System.Collections.Generic;

namespace Edvanz.Application.Dtos.Exams;

// ══════════════════════════════════════════════════════════════════════════════
// TEACHER-SIDE: one student's exam grades (student profile → "Exam grades")
//
// REQ-EXH-026. The teacher opens a student and wants the same thing the student's
// own app shows them: every exam they sat, what they scored, and where that put
// them. This merges the two exam modules the teacher runs — PAPER exams
// (AssignmentType.Exam occurrences) and ONLINE exams — into ONE date-descending
// list, because a tutor thinks in "امتحانات", not in delivery mechanisms.
//
// The merge happens BEFORE paging, so `totalCount` describes exactly the
// population the rows are drawn from (BUG-17). A page taken from either source
// alone would be a page of the wrong set.
// ══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// One exam on a student's grade list — paper or online, told the same way.
/// </summary>
public class StudentExamResultDto
{
    /// <summary>
    /// WHICH MODULE this row came from: <c>"paper"</c> (an offline/in-class exam) or
    /// <c>"online"</c>. A string rather than an enum so it reads in Swagger and can grow;
    /// the app renders it as a chip. The two id spaces are SEPARATE — a paper row's
    /// <see cref="ExamId"/> is an <c>AssignmentOccurrence</c> id, an online row's is an
    /// <c>OnlineExam</c> id — so never key a client-side map on the id alone.
    /// </summary>
    public string Kind { get; set; } = "paper";

    /// <summary>Occurrence id (paper) or online-exam id. Unique only WITHIN a <see cref="Kind"/>.</summary>
    public long ExamId { get; set; }

    public string ExamName { get; set; } = null!;

    /// <summary>The exam's day, teacher-local. Paper: the occurrence's due date. Online: the local start day.</summary>
    public DateOnly Date { get; set; }

    /// <summary>What the student scored. Null when the exam is not graded yet, or they never sat it.</summary>
    public decimal? Score { get; set; }

    /// <summary>The exam's maximum, so the app can render "40 / 50" rather than a bare percentage.</summary>
    public decimal? MaxGrade { get; set; }

    /// <summary>Score ÷ MaxGrade × 100, rounded to one decimal. Null whenever either side is missing.</summary>
    public decimal? ScorePercentage { get; set; }

    /// <summary>
    /// The student's place among the classmates who sat the same exam, by grade descending.
    /// Competition ranking — ties share a rank. Null when this student is not graded.
    /// PAPER EXAMS ONLY: an online exam's cohort is not computed here, so it is always null
    /// for <c>"online"</c> rows and the app must not present a blank as "unranked".
    /// </summary>
    public int? Rank { get; set; }

    /// <summary>How many graded students the <see cref="Rank"/> is out of, including this one. Paper only.</summary>
    public int? GroupSize { get; set; }

    /// <summary>
    /// Plain state for the chip: <c>Pending</c> / <c>Attended</c> / <c>AttendedWithGrade</c> /
    /// <c>Absent</c> for paper, and the online report status (<c>Submitted</c>, <c>Missed</c>,
    /// <c>Blocked</c>, …) for online. Serialized as a STRING by the global converter.
    /// </summary>
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// Totals for the whole of this student's exam history — NOT for the loaded page.
/// Measured on every row the list is drawn from, so the header can never disagree with
/// the list beneath it however far the teacher has paged.
/// </summary>
public class StudentExamResultsSummaryDto
{
    /// <summary>Every exam this student has an obligation for, both kinds.</summary>
    public int TotalExams { get; set; }

    /// <summary>How many of those carry an actual grade.</summary>
    public int GradedExams { get; set; }

    /// <summary>
    /// Mean percentage across the GRADED exams only, one decimal. Null when nothing is graded —
    /// never 0, which would read as "scored nothing" rather than "nothing marked yet".
    /// </summary>
    public decimal? AveragePercentage { get; set; }
}

/// <summary>The page of rows plus the whole-history summary that labels it.</summary>
public class StudentExamResultsDto
{
    public StudentExamResultsSummaryDto Summary { get; set; } = new();
    public List<StudentExamResultDto> Exams { get; set; } = new();
}
