using Edvanz.Domain.Entities.ShareProp;
using Edvanz.Domain.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace Edvanz.Domain.Entities;

/// <summary>
/// One recorded sign-in or sign-out, for ANY account on the platform.
///
/// WHY IT EXISTS: until this table, only assistants had a login history —
/// <see cref="LoginActivityAssistantLog"/> is keyed on <c>AssistantId</c> and was written by the
/// assistant branch of the login path alone. Every other account type, teachers included, had
/// nothing but <c>User.LastLoginAt</c>: a single timestamp that is overwritten on the next sign-in.
/// So the one question a support call opens with — "they say they could not get in on Tuesday" —
/// had no answer for the accounts that actually pay.
///
/// KEYED ON <c>UserId</c>, deliberately, rather than on a role-specific id. Teacher, assistant,
/// student, parent and admin accounts all reach the platform through the same <c>Users</c> row, so
/// one table covers every account type that exists and every one added later. The assistant-specific
/// table is kept for the rows it already holds, but is no longer written to — a second writer would
/// double every assistant's history wherever the two are read together.
/// </summary>
public class UserLoginActivity : BaseEntity
{
    /// <summary>The account that signed in. Every role lands here.</summary>
    [ForeignKey(nameof(User))]
    public long UserId { get; set; }

    public User User { get; set; } = null!;

    /// <summary>login | logout.</summary>
    public LoginAcitvityActionType ActionType { get; set; }

    /// <summary>
    /// The User-Agent as sent. Stored verbatim rather than parsed: the mobile app reports something
    /// like <c>Dart/3.12 (dart:io)</c> and a browser reports its own string, and a support call is
    /// answered faster by the raw value than by a tidy label that lost the detail.
    /// </summary>
    public string? DeviceOrBrowser { get; set; }

    /// <summary>Remote address of the request. Egyptian carrier CGNAT means this identifies a
    /// network rather than a device — useful as corroboration, never as identity.</summary>
    public string? IpAddress { get; set; }
}
