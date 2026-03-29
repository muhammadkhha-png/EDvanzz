using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Domain.Interfaces
{
    /// <summary>
    /// Extended repository interface for the User module ecosystem.
    /// Centralizes all domain-specific query methods used by Teacher, Student, and Parent services.
    /// 
    /// WHY THIS EXISTS:
    /// Previously, services called generic repo methods with raw expression predicates directly
    /// (e.g., FindAsync(u => u.Id == id && u.UserType == UserType.Teacher)). This violated
    /// Onion Architecture by scattering query logic across the Application layer.
    /// 
    /// Now, all query logic lives here in named methods. If a query changes, you edit ONE method
    /// in the repo — not every service that uses it.
    /// 
    /// Inherits from IGenericRepo&lt;User, long&gt; so basic User CRUD is still available.
    /// </summary>
    public interface IUserRepo : IGenericRepo<User, long>
    {
        // ══════════════════════════════════════════════
        // USER ENTITY QUERIES
        // ══════════════════════════════════════════════

        /// <summary>
        /// Finds a user by phone number.
        /// Used by OtpService and AuthService for phone-based lookup.
        /// </summary>
        Task<User?> GetByPhoneAsync(string phone);

        /// <summary>
        /// Finds a user by email address.
        /// Used by UserService during registration duplicate checks.
        /// </summary>
        Task<User?> GetByEmail(string email);

        /// <summary>
        /// Finds a user by username.
        /// Used by UserService during registration duplicate checks.
        /// </summary>
        Task<User?> GetByUserName(string userName);

        /// <summary>
        /// Finds a user by Id and UserType.
        /// Used by Teacher/Student/Parent services during initialization to validate
        /// that the user exists and has the correct type before creating type-specific records.
        /// </summary>
        Task<User?> GetByIdAndTypeAsync(long userId, UserType userType);

        /// <summary>
        /// Finds a user by Id.
        /// Used across services when loading a user record for display (e.g., teacher's full name).
        /// </summary>
        Task<User?> GetUserByIdAsync(long userId);

        /// <summary>
        /// Retrieves all users (no filter). Used by TeacherService.GetTeachersAsync for bulk join.
        /// </summary>
        Task<IReadOnlyList<User>> GetAllUsersAsync();

        /// <summary>
        /// Finds an existing user that matches any of the unique credential fields:
        /// phone number, username, or email (if email is not null/empty).
        /// Used by UserService.AddUser during registration to detect duplicate accounts.
        /// 
        /// Returns the first matching user, or null if no match found.
        /// The caller checks which field matched to return the appropriate error message.
        /// </summary>
        /// <param name="phoneNumber">The phone number to check for duplicates.</param>
        /// <param name="username">The username to check for duplicates.</param>
        /// <param name="email">The email to check for duplicates (skipped if null or empty).</param>
        Task<User?> FindExistingUserByCredentialsAsync(string phoneNumber, string username, string? email);

        // ══════════════════════════════════════════════
        // TEACHER ENTITY QUERIES
        // ══════════════════════════════════════════════

        /// <summary>
        /// Finds a teacher by Id (respects global soft-delete query filter).
        /// </summary>
        Task<Teacher?> GetTeacherByIdAsync(long teacherId);

        /// <summary>
        /// Finds an active (non-deleted) teacher by Id.
        /// Explicitly checks DeletedAt == null for use cases outside query filter scope.
        /// </summary>
        Task<Teacher?> GetActiveTeacherByIdAsync(long teacherId);

        /// <summary>
        /// Checks if a teacher record already exists for the given UserId.
        /// Used during initialization to prevent duplicates.
        /// </summary>
        Task<bool> TeacherExistsByUserIdAsync(long userId);

        /// <summary>
        /// Finds an active teacher by their unique TeacherCode.
        /// Used by Student and Parent linking flows (AAM-FR-05.5) and by GetTeacherByCodeAsync.
        /// </summary>
        Task<Teacher?> GetActiveTeacherByCodeAsync(string teacherCode);

        /// <summary>
        /// Retrieves all teachers (no filter). Used by GetTeachersAsync for bulk operations.
        /// </summary>
        Task<IReadOnlyList<Teacher>> GetAllTeachersAsync();

        /// <summary>
        /// Adds a new Teacher entity.
        /// </summary>
        Task AddTeacherAsync(Teacher teacher);

        /// <summary>
        /// Updates an existing Teacher entity.
        /// </summary>
        Task UpdateTeacherAsync(Teacher teacher);

        // ══════════════════════════════════════════════
        // TEACHER SUBJECT QUERIES
        // ══════════════════════════════════════════════

        /// <summary>
        /// Retrieves all subject associations for a specific teacher.
        /// </summary>
        Task<IReadOnlyList<TeacherSubject>> GetTeacherSubjectsByTeacherIdAsync(long teacherId);

        /// <summary>
        /// Retrieves all teacher-subject records (no filter). Used for bulk operations.
        /// </summary>
        Task<IReadOnlyList<TeacherSubject>> GetAllTeacherSubjectsAsync();

        /// <summary>
        /// Adds a new TeacherSubject association.
        /// </summary>
        Task AddTeacherSubjectAsync(TeacherSubject teacherSubject);

        /// <summary>
        /// Deletes all subject associations for a teacher. Used during profile update.
        /// </summary>
        Task DeleteTeacherSubjectsAsync(IEnumerable<TeacherSubject> subjects);

        // ══════════════════════════════════════════════
        // SUBJECT QUERIES
        // ══════════════════════════════════════════════

        /// <summary>
        /// Finds a subject by Id.
        /// </summary>
        Task<Subject?> GetSubjectByIdAsync(long subjectId);

        /// <summary>
        /// Checks if a subject exists and is active by Id.
        /// Used during teacher initialization and profile update to validate subject selections.
        /// </summary>
        Task<bool> SubjectExistsAndActiveAsync(long subjectId);

        /// <summary>
        /// Retrieves all active subjects.
        /// </summary>
        Task<IReadOnlyList<Subject>> GetActiveSubjectsAsync();

        /// <summary>
        /// Retrieves all subjects (no filter). Used for bulk operations in GetTeachersAsync.
        /// </summary>
        Task<IReadOnlyList<Subject>> GetAllSubjectsAsync();

        // ══════════════════════════════════════════════
        // TEACHER CONFIGURATION QUERIES
        // ══════════════════════════════════════════════

        /// <summary>
        /// Finds the configuration for a specific teacher.
        /// </summary>
        Task<TeacherConfiguration?> GetConfigurationByTeacherIdAsync(long teacherId);

        /// <summary>
        /// Adds a new TeacherConfiguration entity.
        /// </summary>
        Task AddConfigurationAsync(TeacherConfiguration configuration);

        /// <summary>
        /// Updates an existing TeacherConfiguration entity.
        /// </summary>
        Task UpdateConfigurationAsync(TeacherConfiguration configuration);

        // ══════════════════════════════════════════════
        // TEACHER PRORATED TIER QUERIES
        // ══════════════════════════════════════════════

        /// <summary>
        /// Retrieves all prorated tiers for a specific configuration.
        /// </summary>
        Task<IReadOnlyList<TeacherProratedTier>> GetProratedTiersByConfigIdAsync(long configurationId);

        /// <summary>
        /// Adds multiple prorated tiers in bulk.
        /// </summary>
        Task AddProratedTiersAsync(IEnumerable<TeacherProratedTier> tiers);

        /// <summary>
        /// Adds a single prorated tier.
        /// </summary>
        Task AddProratedTierAsync(TeacherProratedTier tier);

        /// <summary>
        /// Deletes all prorated tiers for a configuration. Used during configuration update.
        /// </summary>
        Task DeleteProratedTiersAsync(IEnumerable<TeacherProratedTier> tiers);

        // ══════════════════════════════════════════════
        // TEACHER SUBSCRIPTION QUERIES
        // ══════════════════════════════════════════════

        /// <summary>
        /// Retrieves active or expiring-soon subscriptions for a teacher.
        /// Used by GetActiveSubscriptionAsync.
        /// </summary>
        Task<IReadOnlyList<TeacherSubscription>> GetActiveSubscriptionsByTeacherIdAsync(long teacherId);

        /// <summary>
        /// Retrieves all subscriptions (no filter). Used for bulk operations in GetTeachersAsync.
        /// </summary>
        Task<IReadOnlyList<TeacherSubscription>> GetAllSubscriptionsAsync();

        // ══════════════════════════════════════════════
        // STUDENT CAPACITY PACKAGE QUERIES
        // ══════════════════════════════════════════════

        /// <summary>
        /// Retrieves all active student capacity packages.
        /// </summary>
        Task<IReadOnlyList<StudentCapacityPackage>> GetActiveCapacityPackagesAsync();

        /// <summary>
        /// Finds an active capacity package by Id.
        /// Used during teacher profile update and configuration to validate and set capacity.
        /// </summary>
        Task<StudentCapacityPackage?> GetActiveCapacityPackageByIdAsync(long packageId);

        /// <summary>
        /// Finds a capacity package by Id (ignoring active status). Used in profile DTO builder.
        /// </summary>
        Task<StudentCapacityPackage?> GetCapacityPackageByIdAsync(long packageId);

        // ══════════════════════════════════════════════
        // STUDENT USER ENTITY QUERIES
        // ══════════════════════════════════════════════

        /// <summary>
        /// Finds an active (non-deleted) student user by Id.
        /// </summary>
        Task<StudentUser?> GetActiveStudentUserByIdAsync(long studentUserId);

        /// <summary>
        /// Checks if a StudentUser record already exists for the given UserId.
        /// Used during initialization to prevent duplicates.
        /// </summary>
        Task<bool> StudentUserExistsByUserIdAsync(long userId);

        /// <summary>
        /// Finds a student user by their unique StudentAccountCode (non-deleted).
        /// Used by Parent module for Method A child linking (AAM-FR-06.3).
        /// </summary>
        Task<StudentUser?> GetStudentUserByAccountCodeAsync(string accountCode);

        /// <summary>
        /// Finds a student user by Id (no soft-delete check). Used in BuildChildDtoAsync.
        /// </summary>
        Task<StudentUser?> GetStudentUserByIdAsync(long studentUserId);

        /// <summary>
        /// Adds a new StudentUser entity.
        /// </summary>
        Task AddStudentUserAsync(StudentUser studentUser);

        /// <summary>
        /// Updates an existing StudentUser entity.
        /// </summary>
        Task UpdateStudentUserAsync(StudentUser studentUser);

        // ══════════════════════════════════════════════
        // STUDENT TEACHER LINK QUERIES
        // ══════════════════════════════════════════════

        /// <summary>
        /// Counts active links for a specific student user.
        /// Used for profile summary (linked teacher count).
        /// </summary>
        Task<int> CountActiveStudentTeacherLinksAsync(long studentUserId);

        /// <summary>
        /// Retrieves all active links for a specific student user.
        /// Used by GetLinkedTeachersAsync and parent BuildChildDtoAsync (Method A).
        /// </summary>
        Task<IReadOnlyList<StudentTeacherLink>> GetActiveStudentTeacherLinksAsync(long studentUserId);

        /// <summary>
        /// Finds an active link between a specific student and teacher.
        /// Used by UnlinkTeacherAsync.
        /// </summary>
        Task<StudentTeacherLink?> GetActiveStudentTeacherLinkAsync(long studentUserId, long teacherId);

        /// <summary>
        /// Checks if an active link already exists between a student and teacher.
        /// Used during LinkTeacherAsync to prevent duplicate dashboard entries (AAM-FR-05.7).
        /// </summary>
        Task<bool> StudentTeacherLinkExistsAsync(long studentUserId, long teacherId);

        /// <summary>
        /// Adds a new StudentTeacherLink entity.
        /// </summary>
        Task AddStudentTeacherLinkAsync(StudentTeacherLink link);

        /// <summary>
        /// Updates an existing StudentTeacherLink entity.
        /// </summary>
        Task UpdateStudentTeacherLinkAsync(StudentTeacherLink link);

        // ══════════════════════════════════════════════
        // TEACHER STUDENT (TEACHER-SCOPED RECORD) QUERIES
        // ══════════════════════════════════════════════

        /// <summary>
        /// Finds a teacher's student record by teacher Id, student code, hashed token, and not deleted.
        /// Used during the 3-credential linking flow (AAM-FR-05.5).
        /// </summary>
        Task<TeacherStudent?> GetTeacherStudentByLinkingCredentialsAsync(
            long teacherId, string studentCode, string hashedToken);

        // ══════════════════════════════════════════════
        // PARENT USER ENTITY QUERIES
        // ══════════════════════════════════════════════

        /// <summary>
        /// Finds an active (non-deleted) parent user by Id.
        /// </summary>
        Task<ParentUser?> GetActiveParentUserByIdAsync(long parentUserId);

        /// <summary>
        /// Checks if a ParentUser record already exists for the given UserId.
        /// Used during initialization to prevent duplicates.
        /// </summary>
        Task<bool> ParentUserExistsByUserIdAsync(long userId);

        /// <summary>
        /// Adds a new ParentUser entity.
        /// </summary>
        Task AddParentUserAsync(ParentUser parentUser);

        /// <summary>
        /// Updates an existing ParentUser entity.
        /// </summary>
        Task UpdateParentUserAsync(ParentUser parentUser);

        // ══════════════════════════════════════════════
        // PARENT CHILD QUERIES
        // ══════════════════════════════════════════════

        /// <summary>
        /// Counts active children for a specific parent user.
        /// </summary>
        Task<int> CountActiveChildrenAsync(long parentUserId);

        /// <summary>
        /// Retrieves all active children for a specific parent user.
        /// Used by GetDashboardAsync.
        /// </summary>
        Task<IReadOnlyList<ParentChild>> GetActiveChildrenAsync(long parentUserId);

        /// <summary>
        /// Finds an active child by Id and parent Id.
        /// Used for child-specific operations.
        /// </summary>
        Task<ParentChild?> GetActiveChildAsync(long parentUserId, long childId);

        /// <summary>
        /// Checks if a child is already linked to a parent by the student user Id.
        /// Used during Method A child linking to prevent duplicates.
        /// </summary>
        Task<bool> ChildAlreadyLinkedAsync(long parentUserId, long studentUserId);

        /// <summary>
        /// Adds a new ParentChild entity.
        /// </summary>
        Task AddParentChildAsync(ParentChild parentChild);

        /// <summary>
        /// Updates an existing ParentChild entity.
        /// </summary>
        Task UpdateParentChildAsync(ParentChild parentChild);

        // ══════════════════════════════════════════════
        // PARENT CHILD TEACHER LINK QUERIES
        // ══════════════════════════════════════════════

        /// <summary>
        /// Retrieves all active teacher links for a specific parent child (Method B).
        /// </summary>
        Task<IReadOnlyList<ParentChildTeacherLink>> GetActiveParentChildTeacherLinksAsync(long parentChildId);

        /// <summary>
        /// Checks if a teacher is already linked to a parent's child (Method B).
        /// </summary>
        Task<bool> ParentChildTeacherLinkExistsAsync(long parentChildId, long teacherId);

        /// <summary>
        /// Finds an active teacher link for a parent's child by teacher Id (Method B).
        /// Used by UnlinkTeacherFromChildAsync.
        /// </summary>
        Task<ParentChildTeacherLink?> GetActiveParentChildTeacherLinkAsync(long parentChildId, long teacherId);

        /// <summary>
        /// Adds a new ParentChildTeacherLink entity.
        /// </summary>
        Task AddParentChildTeacherLinkAsync(ParentChildTeacherLink link);

        /// <summary>
        /// Updates an existing ParentChildTeacherLink entity.
        /// </summary>
        Task UpdateParentChildTeacherLinkAsync(ParentChildTeacherLink link);
    }
}