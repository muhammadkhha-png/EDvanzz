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
using System.Text;

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
            IUnitOfWork unitOfWork,ICurrentUserService _currentUserService,ITeacherService teacherService,IStudentUserService studentService ,IParentUserService parentUserService)
        {
            _localizer = localizer;
            _otpService = otpService;
            _passwordService = passwordService;
            _unitOfWork = unitOfWork;
            currentUserService = _currentUserService;
            this.teacherService = teacherService;
            this.studentService = studentService;
            this.parentUserService = parentUserService;
        }

        /// <inheritdoc />
        //public async Task<Result<AddUserDto?>> AddUser(SigupDto user)
        //{
        //    if (user == null)
        //        return Result<AddUserDto?>.Failure(_localizer, "cann't add empty user");

        //    if (user.password != user.confirmedPassword)
        //        return Result<AddUserDto?>.Failure(_localizer, "password must be equail confirmed password");

        //    // FIX V1: Replaced raw FindAsync expression with named repo method.
        //    // Previously: unitOfWork.Users.FindAsync(u => u.PhoneNumber == ... || u.Username == ... || ...)
        //    // Now: all query logic is encapsulated in the repo — if the duplicate check logic
        //    // ever needs to change, you edit it in ONE place (UserRepo), not here.
        //    var existingUser = await _unitOfWork.Users.FindExistingUserByCredentialsAsync(
        //        user.phoneNumber, user.username, user.email);

        //    if (existingUser != null)
        //    {
        //        if (existingUser.PhoneNumber == user.phoneNumber)
        //            return Result<AddUserDto?>.Failure(_localizer, "repeatedPhoneNumber");
        //        if (existingUser.Username == user.username)
        //            return Result<AddUserDto?>.Failure(_localizer, "repeatedUserName");
        //        if (!string.IsNullOrEmpty(user.email) && existingUser.Email == user.email)
        //            return Result<AddUserDto?>.Failure(_localizer, "repeatedEmail");
        //    }

        //    // FIX I3: Renamed from HasedPass to hashedPass (camelCase for local variables)
        //    var hashedPass = _passwordService.HashPassword(user.password);
        //    byte[]? imageBytes = null;

        //    if (user.idImage != null)
        //    {
        //        using var ms = new MemoryStream();
        //        await user.idImage.CopyToAsync(ms);
        //        imageBytes = ms.ToArray();
        //    }

        //    var addedUser = new User()
        //    {
        //        // FIX B2 (partial): Use DateTime.UtcNow consistently instead of DateTime.Now
        //        CreateAt = DateTime.UtcNow,
        //        CreateByUserId = currentUserService.UserId,
        //        Email = user.email,
        //        FullName = user.fullName,
        //        Username = user.username,
        //        PhoneNumber = user.phoneNumber,
        //        IdImage = imageBytes,
        //        IsActive = true,
        //        PasswordHashed = hashedPass,
        //        UserType = user.userType
        //    };

        //    await _unitOfWork.BeginTransactionAsync();
        //    await _unitOfWork.Users.AddAsync(addedUser);
        //    switch (user.userType)
        //    {
        //        case Domain.Enums.UserType.Teacher:
        //            CreateTeacherDto teacherDto = new CreateTeacherDto()
        //            {
        //                LanguagePreference = user.languagePreference,
        //                UserId = addedUser.Id,
        //                CreatedByUserId = currentUserService.UserId,
        //                CustomSubject = user.customSubject,
        //                StudentCapacity = (int)user.studentCapacity,
        //                SubjectIds= user.subjectIds

        //            };
        //           var isTeacherAdded= await teacherService.InitializeTeacherAsync(teacherDto);
        //            if(!isTeacherAdded.IsSuccess)
        //            {
        //                return Result<AddUserDto?>.Failure(_localizer, isTeacherAdded.Message);
        //            }
        //            break;

        //        default:
        //            return Result<AddUserDto?>.Failure(_localizer, "Invalid user type");
        //    }
        //    //To Do handle Add In Users Custom Table
        //    var res = await _unitOfWork.SaveChangesAsync();
        //    if (res > 0)
        //    {
        //        await _unitOfWork.CommitAsync();
        //        return Result<AddUserDto?>.Success(user, _localizer, "SuccessSaving");
        //    }
        //    else
        //    {
        //        await _unitOfWork.RollbackAsync();
        //        return Result<AddUserDto?>.Failure(_localizer, "error in saving");
        //    }
        //}
        public async Task<Result<string?>> AddUser(SigupDto user)
        {
            if (user == null)
                return Result<string?>.Failure(_localizer, "cann't add empty user");

            if (user.password != user.confirmedPassword)
                return Result<string?>.Failure(_localizer, "password must be equail confirmed password");

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

            // =============================
            // Validate user type properties
            // =============================

            //switch (user.userType)
            //{
            //    case UserType.Teacher:

            //        if ((user.subjectIds == null || user.subjectIds.Count == 0) &&
            //            string.IsNullOrWhiteSpace(user.customSubject))
            //            return Result<string?>.Failure(_localizer, "TeacherMustHaveSubject");

            //        if (user.studentCapacity == null || user.studentCapacity <= 0)
            //            return Result<string?>.Failure(_localizer, "StudentCapacityRequired");
            //        if (user.idImage == null)
            //            return Result<string?>.Failure(_localizer, "TeacherIdImageRequired");
            //        if (user.idImage.Length > 5 * 1024 * 1024)
            //            return Result<string?>.Failure(_localizer, "ImageSizeTooLarge");

            //        var allowedTypes = new[] { "image/jpeg", "image/png" };

            //        if (!allowedTypes.Contains(user.idImage.ContentType))
            //            return Result<string?>.Failure(_localizer, "InvalidImageType");
            //        break;


            //    //case UserType.Student:

            //    //    if (string.IsNullOrWhiteSpace(user.languagePreference))
            //    //        return Result<AddUserDto?>.Failure(_localizer, "LanguagePreferenceRequired");

            //    //    break;


            //    //case UserType.Parent:

            //    //    if (string.IsNullOrWhiteSpace(user.languagePreference))
            //    //        return Result<AddUserDto?>.Failure(_localizer, "LanguagePreferenceRequired");

            //    //    break;


            //    default:
            //        return Result<string?>.Failure(_localizer, "InvalidUserType");
            //}

            // =============================
            // Hash password
            // =============================

            var hashedPass = _passwordService.HashPassword(user.password);

            byte[]? imageBytes = null;

            if (user.idImage != null)
            {
                using var ms = new MemoryStream();
                await user.idImage.CopyToAsync(ms);
                imageBytes = ms.ToArray();
            }

            var addedUser = new User
            {
                CreateAt = DateTime.UtcNow,
                CreateByUserId = currentUserService.UserId,
                Email = user.email,
                FullName = user.fullName,
                Username = user.username,
                PhoneNumber = user.phoneNumber,
                IdImage = imageBytes,
                IsActive = true,
                PasswordHashed = hashedPass,
                UserType = user.userType
            };

            await _unitOfWork.BeginTransactionAsync();

            try
            {
                await _unitOfWork.Users.AddAsync(addedUser);

                await _unitOfWork.SaveChangesAsync();

                switch (user.userType)
                {
                    case UserType.Teacher:

                        var teacherDto = new CreateTeacherDto
                        {
                            UserId = addedUser.Id,
                            CreatedByUserId = currentUserService.UserId,
                            LanguagePreference = user.languagePreference,
                            CustomSubject = user.customSubject,
                            StudentCapacity = user.studentCapacity!.Value,
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