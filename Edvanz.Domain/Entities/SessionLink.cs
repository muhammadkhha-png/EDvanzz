using Edvanz.Domain.Entities.ShareProp;
using System.ComponentModel.DataAnnotations.Schema;

namespace Edvanz.Domain.Entities;

/// <summary>
/// Represents a symmetric membership link between two sessions.
/// REQ-SES-032: Session Linking ("Membership") — formally links two or more sessions
///              to indicate they represent the same content delivered to different groups.
/// REQ-SES-036: Symmetric — if A is linked to B, B is automatically linked to A.
/// BR-SES-003: Only sessions with identical occurrence type configuration can be linked.
/// 
/// DESIGN: Single-row-per-pair with canonical ordering (SessionId &lt; LinkedSessionId).
/// This eliminates duplicate storage and ensures symmetric lookup with one query:
///   WHERE SessionId = @id OR LinkedSessionId = @id
/// 
/// REQ-SES-038: Accessible by the Attendance Module for cross-session attendance.
/// REQ-SES-043: NoAction-deleted when either linked session is deleted.
/// </summary>
public class SessionLink : BaseEntity
{
    /// <summary>
    /// The session with the lower Id in the pair (canonical ordering).
    /// FK to Sessions table with NoAction delete.
    /// </summary>
    [ForeignKey(nameof(Session))]
    public long SessionId { get; set; }
    public Session Session { get; set; } = null!;

    /// <summary>
    /// The session with the higher Id in the pair (canonical ordering).
    /// FK to Sessions table with NoAction delete (via trigger or application logic
    /// since SQL Server doesn't allow multiple NoAction paths — handled in OnModelCreating).
    /// </summary>
    [ForeignKey(nameof(LinkedSession))]
    public long LinkedSessionId { get; set; }
    public Session LinkedSession { get; set; } = null!;
}