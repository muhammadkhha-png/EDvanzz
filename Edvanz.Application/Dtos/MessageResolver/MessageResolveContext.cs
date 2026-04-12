using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Application.Dtos.MessageResolver
{
    public class MessageResolveContext
    {
        // Always available
        public long StudentId { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public string StudentCode { get; set; } = string.Empty;
        public string? SessionName { get; set; }
        public DateTime Date { get; set; } = DateTime.UtcNow;

        // Attendance
        public string? AttendanceStatus { get; set; }  // "Attended" | "Absent"
        public int? AbsenceCount { get; set; }

        // Payment
        public string? PaymentStatus { get; set; }  // "Paid" | "Not Paid"
        public decimal? PaymentAmount { get; set; }
        public string? PaymentPeriod { get; set; }  // "January 2025"

        // Exam
        public string? ExamName { get; set; }
        public decimal? ExamGrade { get; set; }
        public decimal? ExamMaxGrade { get; set; }

        // Homework
        public string? HomeworkName { get; set; }
        public string? HomeworkStatus { get; set; }  // "Done" | "Not Done"
    }
}
