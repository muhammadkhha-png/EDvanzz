using Edvanz.Domain.Enums;

namespace Edvanz.Application.Dtos.Center;

// ─────────────────────────────────────────────────────────────────────────────
// ADMIN (SuperAdmin) — center provisioning
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>SuperAdmin request to create a Center account (login + Center row + code).</summary>
public class CreateCenterDto
{
    public string Name { get; set; } = null!;
    public string Username { get; set; } = null!;
    public string Password { get; set; } = null!;
    public string? FullName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string? LanguagePreference { get; set; }
    // NOTE: the revenue-share % is NOT set here. It is the center's own business arrangement with its
    // teachers, so the CENTER sets it (PUT /api/center/settings) — never the SuperAdmin/dashboard.
}

/// <summary>SuperAdmin-facing center summary row.</summary>
public class CenterListItemDto
{
    public long CenterId { get; set; }
    public string Name { get; set; } = null!;
    public string CenterCode { get; set; } = null!;
    public decimal DefaultRevenueSharePercent { get; set; }
    public AccountStatus AccountStatus { get; set; }
    public int TeacherCount { get; set; }
    public int FullTeacherCount { get; set; }
    public int ManagerialTeacherCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

// ─────────────────────────────────────────────────────────────────────────────
// CENTER — self-service (teacher management + overview)
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Center-level settings the CENTER controls (not the SuperAdmin).</summary>
public class CenterSettingsDto
{
    public long CenterId { get; set; }
    public string Name { get; set; } = null!;
    public string CenterCode { get; set; } = null!;
    /// <summary>Default % of each teacher's revenue the center takes (per-teacher overridable).</summary>
    public decimal DefaultRevenueSharePercent { get; set; }
    /// <summary>Center-wide default student-code mode ("Auto" | "Manual").</summary>
    public GenerationMode StudentCodeGenerationMode { get; set; }
}

/// <summary>Center updates its own settings. Omitted fields are left unchanged.</summary>
public class UpdateCenterSettingsDto
{
    public string? Name { get; set; }
    public decimal? DefaultRevenueSharePercent { get; set; }
    public GenerationMode? StudentCodeGenerationMode { get; set; }
}

/// <summary>Center creates a teacher PROFILE (no login — the User row is created IsActive=false).</summary>
public class CreateCenterTeacherDto
{
    /// <summary>The teacher's display name (stored on the identity User.FullName).</summary>
    public string FullName { get; set; } = null!;
    public List<long> SubjectIds { get; set; } = new();
    public string? CustomSubject { get; set; }
    public string? LanguagePreference { get; set; }
    /// <summary>Full (students/parents allowed) or Managerial (roster-only).</summary>
    public SubscriptionPlanType PlanType { get; set; } = SubscriptionPlanType.Full;
    public int StudentCapacity { get; set; } = 500;
    /// <summary>Optional per-teacher override of the center's default revenue-share %.</summary>
    public decimal? RevenueSharePercentOverride { get; set; }
    /// <summary>Optional per-teacher override of the center's student-code mode (null = inherit center default).</summary>
    public GenerationMode? StudentCodeModeOverride { get; set; }
}

/// <summary>Center edits a teacher profile.</summary>
public class UpdateCenterTeacherDto
{
    public string? FullName { get; set; }
    public SubscriptionPlanType? PlanType { get; set; }
    public int? StudentCapacity { get; set; }
    public decimal? RevenueSharePercentOverride { get; set; }
    /// <summary>Per-teacher student-code mode override (null = inherit center default).</summary>
    public GenerationMode? StudentCodeModeOverride { get; set; }
}

/// <summary>Center-facing teacher row.</summary>
public class CenterTeacherListItemDto
{
    public long TeacherId { get; set; }
    public string FullName { get; set; } = null!;
    public string TeacherCode { get; set; } = null!;
    public SubscriptionPlanType? PlanType { get; set; }
    public int StudentCapacity { get; set; }
    /// <summary>Effective revenue-share % = override ?? center default.</summary>
    public decimal EffectiveRevenueSharePercent { get; set; }
    public decimal? RevenueSharePercentOverride { get; set; }
    /// <summary>Per-teacher code-mode override (null = inheriting the center default).</summary>
    public GenerationMode? StudentCodeModeOverride { get; set; }
    /// <summary>The effective code mode after applying override ?? center default (for display).</summary>
    public GenerationMode EffectiveStudentCodeMode { get; set; }
    public AccountStatus AccountStatus { get; set; }
    public int StudentCount { get; set; }

    /// <summary>Whether this teacher has a working login (identity User.IsActive). Center-owned
    /// teachers start login-less; the center enables login per teacher.</summary>
    public bool LoginEnabled { get; set; }

    /// <summary>The login username the teacher signs in with, when login is enabled (null otherwise —
    /// the system-generated placeholder username is never surfaced).</summary>
    public string? LoginUsername { get; set; }
}

/// <summary>Center home / overview snapshot.</summary>
public class CenterOverviewDto
{
    public long CenterId { get; set; }
    public string Name { get; set; } = null!;
    public string CenterCode { get; set; } = null!;
    public decimal DefaultRevenueSharePercent { get; set; }

    public int TeacherCount { get; set; }
    public int FullTeacherCount { get; set; }
    public int ManagerialTeacherCount { get; set; }
    public int StudentCount { get; set; }

    // Quota entitlement from the current center subscription (nulls when none active).
    public bool HasActiveSubscription { get; set; }
    public int? FullTeacherSlots { get; set; }
    public int? ManagerialTeacherSlots { get; set; }
    public int? StudentCapacityTotal { get; set; }
    public int? StudentCapacityUnderFull { get; set; }
    public int? StudentCapacityUnderManagerial { get; set; }
    public DateTime? SubscriptionEndDate { get; set; }
}

/// <summary>Center enables (or re-points) a teacher's login: sets the username the teacher signs in
/// with + an initial password, and activates the identity so the teacher can log in normally.</summary>
public class EnableCenterTeacherLoginDto
{
    /// <summary>Login username the teacher will use — unique across all accounts.</summary>
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(50, MinimumLength = 4)]
    public string Username { get; set; } = null!;

    /// <summary>Initial password (the center hands it to the teacher; the teacher can keep or the
    /// center can reset it later).</summary>
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(100, MinimumLength = 8)]
    public string Password { get; set; } = null!;
}

/// <summary>Center resets a teacher's password (no old-password needed — center-managed). Revokes the
/// teacher's live sessions.</summary>
public class ResetCenterTeacherPasswordDto
{
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(100, MinimumLength = 8)]
    public string NewPassword { get; set; } = null!;

    [System.ComponentModel.DataAnnotations.Required]
    public string ConfirmPassword { get; set; } = null!;
}
