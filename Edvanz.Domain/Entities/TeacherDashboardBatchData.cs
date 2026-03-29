namespace Edvanz.Domain.Entities;

/// <summary>
/// FIX DB-1: Batch-loaded data container for teacher dashboard rendering.
/// Used by StudentUserService and ParentUserService to eliminate N+1 query patterns.
/// 
/// Instead of loading Teacher, User, TeacherSubject, Subject, and TeacherConfiguration
/// one-by-one per linked teacher (4×N database round-trips), the repo loads ALL related
/// data for a set of teacher IDs in 4 total queries and returns it in this container.
/// 
/// The service then resolves each teacher's display data from the in-memory dictionaries
/// with zero additional database calls.
/// </summary>
public class TeacherDashboardBatchData
{
    /// <summary>
    /// Teachers keyed by Teacher.Id.
    /// </summary>
    public Dictionary<long, Teacher> Teachers { get; set; } = new();

    /// <summary>
    /// User records keyed by User.Id. Used to resolve Teacher's FullName.
    /// </summary>
    public Dictionary<long, User> Users { get; set; } = new();

    /// <summary>
    /// TeacherSubject associations grouped by TeacherId.
    /// </summary>
    public Dictionary<long, IReadOnlyList<TeacherSubject>> TeacherSubjects { get; set; } = new();

    /// <summary>
    /// Subject lookup keyed by Subject.Id. Used to resolve subject NameEn/NameAr.
    /// </summary>
    public Dictionary<long, Subject> Subjects { get; set; } = new();

    /// <summary>
    /// TeacherConfiguration keyed by TeacherId. Used for visibility settings.
    /// </summary>
    public Dictionary<long, TeacherConfiguration> Configurations { get; set; } = new();
}