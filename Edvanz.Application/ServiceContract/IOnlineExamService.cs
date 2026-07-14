using Edvanz.Application.Dtos;
using Edvanz.Domain.Interfaces;

namespace Edvanz.Application.ServiceContract;

public interface IOnlineExamService
{
    Task<Result<OnlineExamDetailDto>> CreateAsync(long teacherId, long actingUserId, CreateOnlineExamRequest request);
    Task<Result<OnlineExamDetailDto>> UpdateAsync(long teacherId, long actingUserId, long onlineExamId, UpdateOnlineExamRequest request);
    Task<Result<OnlineExamDetailDto>> GetByIdAsync(long teacherId, long onlineExamId);
    Task<Result<PaginatedResponse<List<OnlineExamListItemDto>>>> GetListAsync(long teacherId, OnlineExamListRequest request);
    Task<Result<OnlineExamOverviewDto>> GetOverviewAsync(long teacherId, long onlineExamId);
    Task<Result<List<OnlineExamScopeAnalysisRowDto>>> GetScopeAnalysisAsync(long teacherId, long onlineExamId);
    Task<Result<List<OnlineExamQuestionRow>>> GetQuestionsAsync(long teacherId, long onlineExamId);
    Task<Result<OnlineExamQuestionsOverviewDto>> GetQuestionsOverviewAsync(long teacherId, long onlineExamId);
    Task<Result<bool>> AddQuestionAsync(long teacherId, long onlineExamId, CreateOnlineExamQuestionDto request);
    Task<Result<bool>> AddQuestionsBulkAsync(long teacherId, long onlineExamId, List<CreateOnlineExamQuestionDto> request);
    Task<Result<bool>> ReplaceQuestionsAsync(long teacherId, long onlineExamId, ReplaceOnlineExamQuestionsRequest request);
    Task<Result<bool>> UpdateStatusAsync(long teacherId, long onlineExamId, UpdateOnlineExamStatusRequest request);
    Task<Result<bool>> DeleteAsync(long teacherId, long onlineExamId);
    Task<Result<OnlineExamStatsDto>> UpdateStudentStatusAsync(
    long teacherId, long onlineExamId, long teacherStudentId, UpdateOnlineExamStudentStatusRequest request);
}