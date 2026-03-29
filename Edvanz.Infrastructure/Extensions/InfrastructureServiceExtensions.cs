using Edvanz.Domain.Interfaces;
using Edvanz.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Edvanz.Infrastructure.Extensions;

/// <summary>
/// Registers Infrastructure layer services into the DI container.
/// Called from Program.cs alongside AddApplication().
/// </summary>
public static class InfrastructureServiceExtensions
{
    public static void AddInfrastructure(this IServiceCollection services)
    {
        // Teacher code generator (AAM-FR-03.3 / AAM-NFR-03)
        services.AddScoped<ITeacherCodeGenerator, TeacherCodeGenerator>();

        // Student account code generator (AAM-FR-05.3 — StudentUser account code)
        services.AddScoped<IStudentAccountCodeGenerator, StudentAccountCodeGenerator>();

        // Student code generator (REQ-STU-007 — teacher-scoped A1→Z999 sequential codes)
        services.AddScoped<IStudentCodeGenerator, StudentCodeGeneratorService>();
    }
}