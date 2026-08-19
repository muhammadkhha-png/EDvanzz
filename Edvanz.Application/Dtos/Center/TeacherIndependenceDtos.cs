using Edvanz.Domain.Enums;

namespace Edvanz.Application.Dtos.Center;

/// <summary>A center-owned teacher submits a request to leave the center and get their own account.</summary>
public class SubmitIndependenceRequestDto
{
    /// <summary>Optional reason the teacher wants to go independent (max 500).</summary>
    public string? Note { get; set; }
}

/// <summary>SuperAdmin rejects a teacher's independence request with a reason shown to the teacher.</summary>
public class RejectIndependenceRequestDto
{
    public string? RejectionReason { get; set; }
}

/// <summary>The teacher's view of their own independence request (status page).</summary>
public class TeacherIndependenceRequestDto
{
    public long RequestId { get; set; }
    public SubscriptionRequestStatus Status { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? Note { get; set; }
    /// <summary>Set when the request was rejected.</summary>
    public string? RejectionReason { get; set; }
}

/// <summary>A row in the SuperAdmin independence-request queue.</summary>
public class IndependenceRequestQueueItemDto
{
    public long RequestId { get; set; }
    public long TeacherId { get; set; }
    public string TeacherName { get; set; } = string.Empty;
    public string TeacherCode { get; set; } = string.Empty;
    public long CenterId { get; set; }
    public string CenterName { get; set; } = string.Empty;
    public string CenterCode { get; set; } = string.Empty;
    public string? Note { get; set; }
    public DateTime RequestedAt { get; set; }
    public SubscriptionRequestStatus Status { get; set; }
}
