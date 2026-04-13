using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OnlineQuiz.Controllers;
using OnlineQuiz.DTOs;
using OnlineQuiz.IServices;
using OnlineQuiz.Utilities;
using System.Security.Claims;
using Xunit;

namespace OnlineQuiz.Tests.Controllers
{
    internal class FakeExportImportLogService : IExportImportLogService
    {
        public bool ReturnNullOnGetById { get; set; }
        public bool ThrowUnauthorizedOnUpdate { get; set; }
        public bool ThrowArgumentOnUpdate { get; set; }
        public bool ThrowUnauthorizedOnDelete { get; set; }

        public Task<ExportImportLogResponseDto> CreateLogAsync(CreateExportImportLogDto createLogDto)
            => Task.FromResult(new ExportImportLogResponseDto
            {
                LogId = 50,
                UserId = createLogDto.UserId,
                Type = createLogDto.Type,
                FileName = createLogDto.FileName,
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            });

        public Task<ExportImportLogResponseDto?> GetLogByIdAsync(int logId, int userId)
        {
            if (ReturnNullOnGetById) return Task.FromResult<ExportImportLogResponseDto?>(null);
            return Task.FromResult<ExportImportLogResponseDto?>(new ExportImportLogResponseDto
            {
                LogId = logId,
                UserId = userId,
                Type = "Export",
                FileName = "grades.xlsx",
                Status = "Completed",
                CreatedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow
            });
        }

        public Task<List<ExportImportLogResponseDto>> GetLogsForUserAsync(int userId)
            => Task.FromResult(new List<ExportImportLogResponseDto>
            {
                new ExportImportLogResponseDto { LogId = 1, UserId = userId, Type = "Export", FileName = "a.csv", Status = "Completed", CreatedAt = DateTime.UtcNow },
                new ExportImportLogResponseDto { LogId = 2, UserId = userId, Type = "Import", FileName = "b.csv", Status = "Failed", CreatedAt = DateTime.UtcNow },
            });

        public Task<ExportImportLogResponseDto> UpdateLogStatusAsync(int logId, UpdateExportImportLogDto updateLogDto, int userId)
        {
            if (ThrowArgumentOnUpdate) throw new ArgumentException("Not found");
            if (ThrowUnauthorizedOnUpdate) throw new UnauthorizedAccessException("Forbidden");
            return Task.FromResult(new ExportImportLogResponseDto
            {
                LogId = logId,
                UserId = userId,
                Type = "Export",
                FileName = "grades.xlsx",
                Status = updateLogDto.Status,
                CreatedAt = DateTime.UtcNow,
                CompletedAt = updateLogDto.CompletedAt,
                ErrorMessage = updateLogDto.ErrorMessage
            });
        }

        public Task<bool> DeleteLogAsync(int logId, int userId)
        {
            if (ThrowUnauthorizedOnDelete) throw new UnauthorizedAccessException("Forbidden");
            return Task.FromResult(true);
        }
    }

    internal class FakeActivityLogServiceForLogs : IActivityLogService
    {
        public List<CreateActivityLogDto> Logged { get; } = new();

        public Task<ActivityLogDto> LogActivityAsync(CreateActivityLogDto dto)
        {
            Logged.Add(dto);
            return Task.FromResult(new ActivityLogDto
            {
                ActivityLogId = DateTime.UtcNow.Ticks,
                UserId = dto.UserId,
                Action = dto.Action,
                Entity = dto.Entity,
                Description = dto.Description,
                CreatedAt = DateTime.UtcNow
            });
        }

        public Task<List<ActivityLogDto>> GetActivityLogsAsync(ActivityLogFilterDto filter) => Task.FromResult(new List<ActivityLogDto>());
        public Task<List<ActivityLogDto>> GetUserActivityLogsAsync(int userId, int? days = null) => Task.FromResult(new List<ActivityLogDto>());
        public Task<ActivityLogStatisticsDto> GetActivityStatisticsAsync(int? userId = null, int? days = 30) => Task.FromResult(new ActivityLogStatisticsDto());
        public Task<ActivityLogDto?> GetActivityLogByIdAsync(long activityLogId) => Task.FromResult<ActivityLogDto?>(null);
    }

    public class ExportImportLogControllerTests
    {
        private static ExportImportLogController CreateController(FakeExportImportLogService logService, FakeActivityLogServiceForLogs activity)
        {
            var controller = new ExportImportLogController(logService, activity);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, "200"),
                        new Claim(ClaimTypes.Role, "Admin")
                    }, "TestAuth"))
                }
            };
            return controller;
        }

        [Fact]
        public async Task CreateLog_ReturnsCreated_And_LogsActivity()
        {
            var logSvc = new FakeExportImportLogService();
            var activity = new FakeActivityLogServiceForLogs();
            var controller = CreateController(logSvc, activity);

            var dto = new CreateExportImportLogDto { UserId = 200, Type = "Export", FileName = "grades.xlsx" };
            var result = await controller.CreateLog(dto);

            var created = Assert.IsType<CreatedAtActionResult>(result.Result);
            var payload = Assert.IsType<ExportImportLogResponseDto>(created.Value);
            Assert.Equal("Export", payload.Type);
            Assert.Contains(activity.Logged, l => l.Action == ActivityLogConstants.Actions.EXPORT);
        }

        [Fact]
        public async Task GetLogById_ReturnsNotFound_WhenMissing()
        {
            var logSvc = new FakeExportImportLogService { ReturnNullOnGetById = true };
            var activity = new FakeActivityLogServiceForLogs();
            var controller = CreateController(logSvc, activity);

            var result = await controller.GetLogById(999);
            var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
            Assert.NotNull(notFound.Value);
        }

        [Fact]
        public async Task GetMyLogs_ReturnsOk_List()
        {
            var logSvc = new FakeExportImportLogService();
            var activity = new FakeActivityLogServiceForLogs();
            var controller = CreateController(logSvc, activity);

            var result = await controller.GetMyLogs();
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var list = Assert.IsType<List<ExportImportLogResponseDto>>(ok.Value);
            Assert.True(list.Count >= 2);
        }

        [Fact]
        public async Task UpdateLog_ReturnsOk_WhenValid()
        {
            var logSvc = new FakeExportImportLogService();
            var activity = new FakeActivityLogServiceForLogs();
            var controller = CreateController(logSvc, activity);

            var dto = new UpdateExportImportLogDto { Status = "Completed", CompletedAt = DateTime.UtcNow };
            var result = await controller.UpdateLog(50, dto);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var payload = Assert.IsType<ExportImportLogResponseDto>(ok.Value);
            Assert.Equal("Completed", payload.Status);
        }

        [Fact]
        public async Task DeleteLog_ReturnsNoContent_WhenValid()
        {
            var logSvc = new FakeExportImportLogService();
            var activity = new FakeActivityLogServiceForLogs();
            var controller = CreateController(logSvc, activity);

            var result = await controller.DeleteLog(50);
            Assert.IsType<NoContentResult>(result);
        }
    }
}