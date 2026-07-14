using Edvanz.Application.Dtos;

namespace Edvanz.Application.ServiceContract;

public interface IStudentOnlineExamService
{
    Task<Result<StudentOnlineExamListDto>> GetMyExamsAsync(long teacherId, long teacherStudentId);
    Task<Result<OnlineExamTakeScreenDto>> GetTakeScreenAsync(long teacherId, long teacherStudentId, long onlineExamId);
    Task<Result<OnlineExamReviewDto>> GetReviewAsync(long teacherId, long teacherStudentId, long onlineExamId);
    Task<Result<OnlineExamStatsDto>> SubmitAnswerAsync(
    long teacherId, long teacherStudentId, long onlineExamId, SubmitOnlineExamAnswerRequest request);

    Task<Result<OnlineExamStatsDto>> SubmitExamAsync(
        long teacherId, long teacherStudentId, long onlineExamId, SubmitOnlineExamRequest request);

    Task<Result<OnlineExamStatsDto>> GetResultAsync(
        long teacherId, long teacherStudentId, long onlineExamId);
}