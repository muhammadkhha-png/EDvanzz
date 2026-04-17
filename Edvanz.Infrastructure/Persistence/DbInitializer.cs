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
        }
    }

}
