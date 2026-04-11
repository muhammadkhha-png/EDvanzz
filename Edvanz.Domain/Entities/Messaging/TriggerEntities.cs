using Edvanz.Domain.Entities.ShareProp;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace Edvanz.Domain.Entities.Messaging
{
    public class AutomatedTrigger : BaseEntity
    {
        [ForeignKey(nameof(Teacher))]
        public long TeacherId { get; set; }
        public Teacher Teacher { get; set; } = null!;

        [ForeignKey(nameof(MessageTemplate))]
        public long MessageTemplateId { get; set; }
        public MessageTemplate MessageTemplate { get; set; } = null!;

        public TriggerEventType EventType { get; set; }  // StudentAttended | PaymentReceived | ...
        public bool IsActive { get; set; } = true;

        // Immediate = بعد الحدث مباشرة | Scheduled = وقت محدد
        public SendTimingType SendTiming { get; set; }
        public TimeSpan? ScheduledTime { get; set; }    // لو SendTiming = Scheduled

        // Scope: All | Session | SessionGroup
        public TriggerScope Scope { get; set; } = TriggerScope.All;
        public long? SessionId { get; set; }
        public long? SessionGroupId { get; set; }

        // للـ threshold-based triggers زي ConsecutiveAbsenceAlert
        public int? ThresholdValue { get; set; }

        public DateTime UpdatedAt { get; set; }
    }

    public enum TriggerEventType
    {
        // Attendance 
        StudentAttended = 1,
        StudentAbsent = 2,
        ConsecutiveAbsenceAlert = 3,

        // Payment 
        PaymentReceived = 4,
        PaymentNotReceived = 5,
        ConsecutiveNonPayment = 6,
        PartialPaymentRecorded = 7,

        // Exam 
        ExamGradeRecorded = 8,
        GradeBelowThreshold = 9,
        ExamNotAttended = 10,

        // Homework 
        HomeworkMarkedDone = 11,
        HomeworkMarkedNotDone = 12,
        HomeworkGradeRecorded = 13,

        // Manual 
        SessionCancelled = 14,
        ExamRescheduled = 15,
        CustomAnnouncement = 16,
    }

  
}
