using Edvanz.Application.ServiceContract;
using Microsoft.Extensions.Logging;

namespace Edvanz.Infrastructure.Services;

/// <summary>
/// No-op stub implementation of IMessagingIntegrationService.
/// Step 7.1: Logs the notification call but does not send any actual messages.
/// Replace with the real implementation when the Messaging Module (Module 8) is built.
///
/// Register in DI:
///   services.AddScoped&lt;IMessagingIntegrationService, StubMessagingIntegrationService&gt;();
/// </summary>
public class StubMessagingIntegrationService : IMessagingIntegrationService
{
    private readonly ILogger<StubMessagingIntegrationService> _logger;

    public StubMessagingIntegrationService(ILogger<StubMessagingIntegrationService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task NotifyStudentAbsenceAsync(
        long teacherId, long teacherStudentId, string studentName,
        int consecutiveAbsences, string sessionName, DateTime occurrenceDate)
    {
        _logger.LogInformation(
            "[MessagingStub] Absence notification: Teacher={TeacherId}, Student={StudentName} ({StudentId}), " +
            "Session={SessionName}, Date={Date}, ConsecutiveAbsences={Count}",
            teacherId, studentName, teacherStudentId, sessionName,
            occurrenceDate.ToString("yyyy-MM-dd"), consecutiveAbsences);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task NotifyConsecutiveAbsenceThresholdAsync(
        long teacherId, long teacherStudentId, string studentName,
        int consecutiveAbsences, string sessionName)
    {
        _logger.LogWarning(
            "[MessagingStub] Consecutive absence threshold: Teacher={TeacherId}, Student={StudentName} ({StudentId}), " +
            "Session={SessionName}, ConsecutiveAbsences={Count}",
            teacherId, studentName, teacherStudentId, sessionName, consecutiveAbsences);

        return Task.CompletedTask;
    }
}