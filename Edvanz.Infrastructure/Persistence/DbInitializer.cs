using Edvanz.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace Edvanz.Infrastructure.Persistence
{
    public class DbInitializer
    {
        public static async Task SeedAsync(EdvanzDbContext context)
        {
            if (!context.Models.Any())
            {
                context.Models.AddRange(
                    new Module { Name = "Student" },
                    new Module { Name = "Session" },
                    new Module { Name = "Attendance" },
                     new Module { Name = "Payment" },
                     new Module { Name = "Event-Based Payment" },
                     new Module { Name = "Exams And Homework" },
                     new Module { Name = "Messaging" }

                );

                await context.SaveChangesAsync();
            }
            // 2️⃣ Get Modules with Ids
            var modules = await context.Models
                .ToDictionaryAsync(m => m.Name.Trim(), m => m.Id);

            // 3️⃣ Seed Permissions
            if (!context.Permissions.Any())
            {
                var permissions = new List<Permission>
            {
                // Student
                new Permission { Name = "ViewList", ModuleId = modules["Student"], IsRestricted = false },
                new Permission { Name = "ViewProfile", ModuleId = modules["Student"], IsRestricted = false },
                new Permission { Name = "Add", ModuleId = modules["Student"], IsRestricted = false },
                new Permission { Name = "Edit", ModuleId = modules["Student"], IsRestricted = false },
                new Permission { Name = "Delete", ModuleId = modules["Student"], IsRestricted = false },
                new Permission { Name = "Import", ModuleId = modules["Student"], IsRestricted = false },
                new Permission { Name = "ExportReports", ModuleId = modules["Student"], IsRestricted = false },
                new Permission { Name = "ViewBarcodes", ModuleId = modules["Student"], IsRestricted = false },
                // Session
                new Permission { Name = "View", ModuleId = modules["Session"], IsRestricted = false },
                new Permission { Name = "Create", ModuleId = modules["Session"], IsRestricted = false },
                new Permission { Name = "Edit", ModuleId = modules["Session"], IsRestricted = false },
                new Permission { Name = "Delete", ModuleId = modules["Session"], IsRestricted = false },
                new Permission { Name = "ViewGroups", ModuleId = modules["Session"], IsRestricted = false },
                new Permission { Name = "AssignStudents", ModuleId = modules["Session"], IsRestricted = false },
                new Permission { Name = "ManageGroups", ModuleId = modules["Session"], IsRestricted = false },
                new Permission { Name = "ViewMembership", ModuleId = modules["Session"], IsRestricted = false },
                new Permission { Name = "ManageMembership", ModuleId = modules["Session"], IsRestricted = false },
                // Attendance
                new Permission { Name = "Take", ModuleId = modules["Attendance"], IsRestricted = false },
                new Permission { Name = "Edit", ModuleId = modules["Attendance"], IsRestricted = false },
                new Permission { Name = "ViewHistory", ModuleId = modules["Attendance"], IsRestricted = false },
                new Permission { Name = "ViewAbsenceOverview", ModuleId = modules["Attendance"], IsRestricted = false },
                new Permission { Name = "GenerateReports", ModuleId = modules["Attendance"], IsRestricted = false },
                // Payment
                new Permission { Name = "Collect", ModuleId = modules["Payment"], IsRestricted = false },
                new Permission { Name = "ViewHistor", ModuleId = modules["Payment"], IsRestricted = false },
                new Permission { Name = "EditHistory", ModuleId = modules["Payment"], IsRestricted = true },
                new Permission { Name = "ViewUnpaidStudents", ModuleId = modules["Payment"], IsRestricted = false },
                new Permission { Name = "ViewCollectorSummary", ModuleId = modules["Payment"], IsRestricted = false },
                new Permission { Name = "GenerateReports", ModuleId = modules["Payment"], IsRestricted = false },
               // Event-Based Payment
                new Permission { Name = "View", ModuleId = modules["Event-Based Payment"], IsRestricted = false },
                new Permission { Name = "Create", ModuleId = modules["Event-Based Payment"], IsRestricted = false },
                new Permission { Name = "Edit", ModuleId = modules["Event-Based Payment"], IsRestricted = false },
                new Permission { Name = "Delete", ModuleId = modules["Event-Based Payment"], IsRestricted = false },
                new Permission { Name = "CollectPayment", ModuleId = modules["Event-Based Payment"], IsRestricted = false },
                new Permission { Name = "GenerateReports", ModuleId = modules["Event-Based Payment"], IsRestricted = false },
                // Exams & Homework
                new Permission { Name = "View", ModuleId = modules["Exams And Homework"], IsRestricted = false },
                new Permission { Name = "Create", ModuleId = modules["Exams And Homework"], IsRestricted = false },
                new Permission { Name = "Edit", ModuleId = modules["Exams And Homework"], IsRestricted = false },
                new Permission { Name = "Delete", ModuleId = modules["Exams And Homework"], IsRestricted = false }, 
                new Permission { Name = "RecordExamAttendanceAndGrades", ModuleId = modules["Exams And Homework"], IsRestricted = false },
                new Permission { Name = "RecordHomeworkCompletion", ModuleId = modules["Exams And Homework"], IsRestricted = false },
                new Permission { Name = "EnterPendingGrades", ModuleId = modules["Exams And Homework"], IsRestricted = false },
                new Permission { Name = "GenerateReports", ModuleId = modules["Exams And Homework"], IsRestricted = false },
                // Messaging
                new Permission { Name = "ViewHistory", ModuleId = modules["Messaging"], IsRestricted = false },
                new Permission { Name = "SendManual", ModuleId = modules["Messaging"], IsRestricted = false },
                new Permission { Name = "ManageTemplates", ModuleId = modules["Messaging"], IsRestricted = false },
                new Permission { Name = "ConfigureAutomatedTriggers", ModuleId = modules["Messaging"], IsRestricted = false },
                //new Permission { Name = "ConfigureChannels", ModuleId = modules["Messaging"], IsRestricted = true } // to delete

            };

                context.Permissions.AddRange(permissions);

                await context.SaveChangesAsync();
            }

            // 4️⃣ Seed StudentCapacityPackages
            if (!context.StudentCapacityPackages.Any())
            {
                var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

                var packages = new List<StudentCapacityPackage>
    {
        new StudentCapacityPackage
        {
            Name = "Up to 300",
            MinStudents = 0,
            MaxStudents = 300,
            IsActive = true,
            DisplayOrder = 1,
            CreateAt = now,
            MonthlyPriceEGP = 0 // will be updated by admin later
        },
        new StudentCapacityPackage
        {
            Name = "300 to 500",
            MinStudents = 300,
            MaxStudents = 500,
            IsActive = true,
            DisplayOrder = 2,
            CreateAt = now,
            MonthlyPriceEGP = 0
        },
        new StudentCapacityPackage
        {
            Name = "500 to 800",
            MinStudents = 500,
            MaxStudents = 800,
            IsActive = true,
            DisplayOrder = 3,
            CreateAt = now,
            MonthlyPriceEGP = 0
        },
        new StudentCapacityPackage
        {
            Name = "800 to 1200",
            MinStudents = 800,
            MaxStudents = 1200,
            IsActive = true,
            DisplayOrder = 4,
            CreateAt = now,
            MonthlyPriceEGP = 0
        },
        new StudentCapacityPackage
        {
            Name = "1200 to 1500",
            MinStudents = 1200,
            MaxStudents = 1500,
            IsActive = true,
            DisplayOrder = 5,
            CreateAt = now,
            MonthlyPriceEGP = 0
        },
        new StudentCapacityPackage
        {
            Name = "1500 to 3000",
            MinStudents = 1500,
            MaxStudents = 3000,
            IsActive = true,
            DisplayOrder = 6,
            CreateAt = now,
            MonthlyPriceEGP = 0
        },
        new StudentCapacityPackage
        {
            Name = "3000+",
            MinStudents = 3000,
            MaxStudents = null,
            IsActive = true,
            DisplayOrder = 7,
            CreateAt = now,
            MonthlyPriceEGP = 0
        }
    };

                context.StudentCapacityPackages.AddRange(packages);
                await context.SaveChangesAsync();
            }
        }
    }

}
