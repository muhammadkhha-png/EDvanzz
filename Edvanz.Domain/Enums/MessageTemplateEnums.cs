using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Domain.Enums
{
    public enum BlockType { Dynamic = 1, CustomText = 2 }

    public enum DynamicBlockKey
    {
        StudentName = 1,
        StudentCode = 2,
        SessionName = 3,
        Date = 4,
        AttendanceStatus = 5,
        AbsenceCount = 6,
        PaymentStatus = 7,
        PaymentAmount = 8,
        PaymentPeriod = 9,
        ExamName = 10,
        ExamGrade = 11,
        ExamMaxGrade = 12,
        HomeworkName = 13,
        HomeworkStatus = 14,
    }

    public enum RecipientTarget { Student = 1, Parent = 2, Both = 3 }
}
