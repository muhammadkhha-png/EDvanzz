using Edvanz.Application.Dtos;
using Edvanz.Application.Dtos.Auth;
using Edvanz.Application.Dtos.ParentUser;
using Edvanz.Application.Dtos.StudentUser;
using Edvanz.Application.Dtos.Teacher;
using Edvanz.Application.Dtos.UserDto;
using Edvanz.Application.IservicesContract;
using Edvanz.Application.ServiceContract;
using Edvanz.Domain.Entities;
using Edvanz.Domain.Enums;
using Edvanz.Domain.Interfaces;
using Edvanz.Domain.Resources;
using Edvanz.Domain.ServiceContract;
using Microsoft.Extensions.Localization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Edvanz.Application.Services
{
    /// <summary>
    /// Implements user registration operations.
    /// Creates the shared User record and delegates type-specific initialization
    /// to ITeacherService, IStudentUserService, or IParentUserService.
    /// 
    /// All database access goes through IUnitOfWork.Users (IUserRepo) — no direct
    /// GetRepository calls with raw expression predicates.
    /// 
    /// TRANSACTION SAFETY:
    /// AddUser starts a transaction that wraps both the User row creation and the
    /// type-specific initialization. The type-specific services detect HasActiveTransaction
    /// and participate in this outer transaction rather than starting their own.
    /// </summary>
    // FIX I3: Renamed from IuserService to IUserService (PascalCase per .NET conventions)
    public class UserService : IUserService
    {
        private readonly IStringLocalizer<Messages> _localizer;
        private readonly IOtpService _otpService;
        // FIX I3: Renamed from passServicr to _passwordService (correct spelling, underscore prefix)
        private readonly IPasswordService _passwordService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService currentUserService;
        private readonly ITeacherService teacherService;
        private readonly IStudentUserService studentService;
        private readonly IParentUserService parentUserService;
        private readonly IFileStorageService _fileStorage;

        /// <summary>
        /// Initializes a new instance of UserService with required dependencies.
        /// </summary>
        /// <param name="localizer">String localizer for multilingual error/success messages.</param>
        /// <param name="otpService">OTP generation and verification service.</param>
        /// <param name="passwordService">Password hashing and verification service.</param>
        /// <param name="unitOfWork">Unit of work for database operations.</param>
        public UserService(
            IStringLocalizer<Messages> localizer,
            IOtpService otpService,
            IPasswordService passwordService,
            IUnitOfWork unitOfWork,ICurrentUserService _currentUserService,ITeacherService teacherService,IStudentUserService studentService ,IParentUserService parentUserService,
            IFileStorageService fileStorage)
        {
            _localizer = localizer;
            _otpService = otpService;
            _passwordService = passwordService;
            _unitOfWork = unitOfWork;
            currentUserService = _currentUserService;
            this.teacherService = teacherService;
            this.studentService = studentService;
            this.parentUserService = parentUserService;
            _fileStorage = fileStorage;
        }

        /// <inheritdoc />
       
        public async Task<Result<string?>> AddUser(SigupDto user)
        {
            if (user == null)
                return Result<string?>.Failure(_localizer, "cann't add empty user");
            var allowedSelfRegistration = new[] { UserType.Teacher, UserType.Student, UserType.Parent };
            if (!allowedSelfRegistration.Contains(user.userType))
                return Result<string?>.Failure(_localizer, "InvalidUserType");
            if (user.password != user.confirmedPassword)
                return Result<string?>.Failure(_localizer, "password must be equail confirmed password");
            if (string.IsNullOrWhiteSpace(user.phoneNumber))
                return Result<string?>.Failure(_localizer, "PhoneNumberRequired");

            if (!PhoneNumberValidator.IsValidEgyptianMobile(user.phoneNumber))
                return Result<string?>.Failure(_localizer, "PhoneNumberInvalidFormat");
            var existingUser = await _unitOfWork.Users.FindExistingUserByCredentialsAsync(
                user.phoneNumber, user.username, user.email);

            if (existingUser != null)
            {
                if (existingUser.PhoneNumber == user.phoneNumber)
                    return Result<string?>.Failure(_localizer, "repeatedPhoneNumber");

                if (existingUser.Username == user.username)
                    return Result<string?>.Failure(_localizer, "repeatedUserName");

                if (!string.IsNullOrEmpty(user.email) && existingUser.Email == user.email)
                    return Result<string?>.Failure(_localizer, "repeatedEmail");
            }
            if(user.userType==UserType.Teacher && (user.subjectIds == null || user.subjectIds.Count == 0) )
            {
                return Result<string>.Failure(_localizer, "SubjectRequired");
            }
               

            // =============================
            // Hash password
            // =============================

            var hashedPass = _passwordService.HashPassword(user.password);

            var addedUser = new User
            {
                CreateAt = DateTime.UtcNow,
                CreateByUserId = currentUserService.UserId,
                Email = user.email,
                FullName = user.fullName,
                Username = user.username,
                PhoneNumber = user.phoneNumber,
                IsActive = true,
                PasswordHashed = hashedPass,
                UserType = user.userType
            };

            await _unitOfWork.BeginTransactionAsync();

            try
            {
                await _unitOfWork.Users.AddAsync(addedUser);

                await _unitOfWork.SaveChangesAsync();

                // National-ID image (optional) — stored server-side in the private uploads container
                // and recorded in the central registry, since sign-up has no pre-account JWT to
                // authorize a separate /api/upload. Access = owner + SuperAdmin via the gated endpoint.
                if (user.idImage is not null)
                {
                    string ext = Path.GetExtension(user.idImage.FileName);
                    string blobPath = $"files/{addedUser.Id}/{Guid.NewGuid():N}{ext}";
                    await using (var stream = user.idImage.OpenReadStream())
                    {
                        await _fileStorage.UploadAsync(blobPath, stream, user.idImage.ContentType);
                    }

                    var idFile = new FileObject
                    {
                        PublicId = Guid.NewGuid(),
                        OwnerUserId = addedUser.Id,
                        TeacherId = null,
                        Category = FileCategory.NationalIdImage,
                        Status = FileStatus.Attached,
                        BlobPath = blobPath,
                        ContentType = user.idImage.ContentType,
                        SizeBytes = user.idImage.Length,
                        OriginalName = Path.GetFileName(user.idImage.FileName) ?? string.Empty,
                        CreateAt = DateTime.UtcNow,
                    };
                    await _unitOfWork.FileObjectsRepo.AddAsync(idFile);
                    await _unitOfWork.SaveChangesAsync();

                    addedUser.IdImageFileId = idFile.Id;
                    await _unitOfWork.SaveChangesAsync();
                }

                switch (user.userType)
                {
                    case UserType.Teacher:

                        var teacherDto = new CreateTeacherDto
                        {
                            UserId = addedUser.Id,
                            CreatedByUserId = currentUserService.UserId,
                            LanguagePreference = user.languagePreference,
                            CustomSubject = user.customSubject,
                            StudentCapacity = user.studentCapacity ?? 50,
                            SubjectIds = user.subjectIds ?? new List<long>()
                        };

                        var teacherResult = await teacherService.InitializeTeacherAsync(teacherDto);

                        if (!teacherResult.IsSuccess)
                        {
                            await _unitOfWork.RollbackAsync();
                            return Result<string?>.Failure(_localizer, teacherResult.Message);
                        }

                        break;


                    case UserType.Student:

                        var studentDto = new CreateStudentUserDto
                        {
                            UserId = addedUser.Id,
                            LanguagePreference = user.languagePreference
                        };

                        var studentResult = await studentService.InitializeStudentUserAsync(studentDto);

                        if (!studentResult.IsSuccess)
                        {
                            await _unitOfWork.RollbackAsync();
                            return Result<string?>.Failure(_localizer, studentResult.Message);
                        }

                        break;


                    case UserType.Parent:

                        var parentDto = new CreateParentUserDto
                        {
                            UserId = addedUser.Id,
                            LanguagePreference = user.languagePreference
                        };

                        var parentResult = await parentUserService.InitializeParentUserAsync(parentDto);

                        if (!parentResult.IsSuccess)
                        {
                            await _unitOfWork.RollbackAsync();
                            return Result<string?>.Failure(_localizer, parentResult.Message);
                        }

                        break;
                }

                await _unitOfWork.CommitAsync();

                return Result<string?>.Success(null, _localizer, "SuccessSaving");
            }
            catch
            {
                await _unitOfWork.RollbackAsync();
                throw;
            }
        }

        public static class PhoneNumberValidator
        {
            // Egyptian mobile: 11 digits starting with 010/011/012/015.
            // Centralized so Teacher/Student/Parent/Google paths share the same rule.
            private static readonly Regex Pattern =
                new(@"^01[0125]\d{8}$", RegexOptions.Compiled);

            public static bool IsValidEgyptianMobile(string? phone) =>
                !string.IsNullOrWhiteSpace(phone) && Pattern.IsMatch(phone);
        }

        //public async Task<Result<string>> DeactiveUser(long userId)
        //{
        //    var user =await _unitOfWork.Users.GetByIdAsync(userId);
        //    if (user == null)
        //        return Result<string>.Failure(_localizer, "UserNotFound");
        //    if(user.IsActive == false)
        //        return Result<string>.Failure(_localizer, "UserAlreadyDeactive");

        //}
    }
}