using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OnlineQuiz.Controllers;
using OnlineQuiz.DTOs;
using OnlineQuiz.IServices;
using System.Net;
using System.Security.Claims;
using Xunit;

namespace OnlineQuiz.Tests.Controllers
{
    internal class FakeActivityLogServiceForController : IActivityLogService
    {
        public CreateActivityLogDto? LastLoggedDto { get; private set; }
        public bool ThrowArgumentOnLog { get; set; }
        public bool ReturnNullOnGetById { get; set; }

        public Task<ActivityLogDto> LogActivityAsync(CreateActivityLogDto dto)
        {
            if (ThrowArgumentOnLog) throw new ArgumentException("Invalid activity payload");
            LastLoggedDto = dto;
            return Task.FromResult(new ActivityLogDto
            {
                ActivityLogId = DateTime.UtcNow.Ticks,
                UserId = dto.UserId,
                Action = dto.Action,
                Entity = dto.Entity,
                EntityId = dto.EntityId,
                Description = dto.Description,
                IpAddress = dto.IpAddress,
                UserAgent = dto.UserAgent,
                CreatedAt = DateTime.UtcNow
            });
        }

        public Task<List<ActivityLogDto>> GetActivityLogsAsync(ActivityLogFilterDto filter)
            => Task.FromResult(new List<ActivityLogDto> { new ActivityLogDto { ActivityLogId = 1, UserId = 10, Action = "Login", Entity = "Auth", CreatedAt = DateTime.UtcNow } });

        public Task<List<ActivityLogDto>> GetUserActivityLogsAsync(int userId, int? days = null)
            => Task.FromResult(new List<ActivityLogDto> { new ActivityLogDto { ActivityLogId = 2, UserId = userId, Action = "Viewed", Entity = "Course", CreatedAt = DateTime.UtcNow } });

        public Task<ActivityLogStatisticsDto> GetActivityStatisticsAsync(int? userId = null, int? days = 30)
            => Task.FromResult(new ActivityLogStatisticsDto { TotalActions = 5, UniqueUsers = 2 });

        public Task<ActivityLogDto?> GetActivityLogByIdAsync(long activityLogId)
            => Task.FromResult(ReturnNullOnGetById ? null : new ActivityLogDto { ActivityLogId = activityLogId, UserId = 10, Action = "Login", Entity = "Auth", CreatedAt = DateTime.UtcNow });
    }

    public class ActivityLogControllerTests
    {
        private static ActivityLogController CreateController(FakeActivityLogServiceForController? svc = null, ClaimsIdentity? identity = null)
        {
            var controller = new ActivityLogController(svc ?? new FakeActivityLogServiceForController());
            var httpContext = new DefaultHttpContext();
            if (identity != null)
            {
                httpContext.User = new ClaimsPrincipal(identity);
            }
            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
            return controller;
        }

        private static ClaimsIdentity AdminIdentity()
            => new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "10"),
                new Claim(ClaimTypes.Role, "Admin")
            }, "TestAuth");

        private static ClaimsIdentity StudentIdentity(string userId = "20")
            => new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Role, "Student")
            }, "TestAuth");

        [Fact]
        public async Task GetActivityLogs_ReturnsForbid_WhenNotAdmin()
        {
            var controller = CreateController(identity: StudentIdentity());
            var result = await controller.GetActivityLogs(new ActivityLogFilterDto());
            Assert.IsType<ForbidResult>(result.Result);
        }

        [Fact]
        public async Task GetActivityLogs_ReturnsOk_WhenAdmin()
        {
            var svc = new FakeActivityLogServiceForController();
            var controller = CreateController(svc, AdminIdentity());
            var result = await controller.GetActivityLogs(new ActivityLogFilterDto());
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var list = Assert.IsType<List<ActivityLogDto>>(ok.Value);
            Assert.NotEmpty(list);
        }

        [Fact]
        public async Task GetActivityLogById_ReturnsForbid_WhenNotAdmin()
        {
            var controller = CreateController(identity: StudentIdentity());
            var result = await controller.GetActivityLogById(5);
            Assert.IsType<ForbidResult>(result.Result);
        }

        [Fact]
        public async Task GetActivityLogById_ReturnsNotFound_WhenMissing()
        {
            var svc = new FakeActivityLogServiceForController { ReturnNullOnGetById = true };
            var controller = CreateController(svc, AdminIdentity());
            var result = await controller.GetActivityLogById(404);
            Assert.IsType<NotFoundObjectResult>(result.Result);
        }

        [Fact]
        public async Task GetActivityLogById_ReturnsOk_WhenFound()
        {
            var svc = new FakeActivityLogServiceForController();
            var controller = CreateController(svc, AdminIdentity());
            var result = await controller.GetActivityLogById(7);
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var payload = Assert.IsType<ActivityLogDto>(ok.Value);
            Assert.Equal(7, payload.ActivityLogId);
        }

        [Fact]
        public async Task GetUserActivityLogs_ReturnsForbid_WhenNotAdminAndNotSelf()
        {
            var controller = CreateController(identity: StudentIdentity("21"));
            var result = await controller.GetUserActivityLogs(userId: 22);
            Assert.IsType<ForbidResult>(result.Result);
        }

        [Fact]
        public async Task GetUserActivityLogs_ReturnsOk_WhenAdmin()
        {
            var svc = new FakeActivityLogServiceForController();
            var controller = CreateController(svc, AdminIdentity());
            var result = await controller.GetUserActivityLogs(userId: 22);
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var list = Assert.IsType<List<ActivityLogDto>>(ok.Value);
            Assert.NotEmpty(list);
        }

        [Fact]
        public async Task GetMyActivityLogs_ReturnsUnauthorized_WhenMissingUserIdClaim()
        {
            var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, "Student") }, "TestAuth");
            var controller = CreateController(identity: identity);
            var result = await controller.GetMyActivityLogs();
            Assert.IsType<UnauthorizedObjectResult>(result.Result);
        }

        [Fact]
        public async Task GetMyActivityLogs_ReturnsOk_WhenValid()
        {
            var svc = new FakeActivityLogServiceForController();
            var controller = CreateController(svc, StudentIdentity("30"));
            var result = await controller.GetMyActivityLogs();
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var list = Assert.IsType<List<ActivityLogDto>>(ok.Value);
            Assert.NotEmpty(list);
        }

        [Fact]
        public async Task GetActivityStatistics_ReturnsForbid_WhenNotAdmin()
        {
            var controller = CreateController(identity: StudentIdentity());
            var result = await controller.GetActivityStatistics();
            Assert.IsType<ForbidResult>(result.Result);
        }

        [Fact]
        public async Task GetActivityStatistics_ReturnsOk_WhenAdmin()
        {
            var svc = new FakeActivityLogServiceForController();
            var controller = CreateController(svc, AdminIdentity());
            var result = await controller.GetActivityStatistics();
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var stats = Assert.IsType<ActivityLogStatisticsDto>(ok.Value);
            Assert.True(stats.TotalActions >= 0);
        }

        [Fact]
        public async Task CreateActivityLog_ReturnsCreated_And_EnrichesIpAndUserAgent()
        {
            var svc = new FakeActivityLogServiceForController();
            var controller = CreateController(svc, AdminIdentity());

            // Simulate incoming request context
            controller.ControllerContext.HttpContext.Request.Headers["User-Agent"] = "UnitTestAgent/1.0";
            controller.ControllerContext.HttpContext.Connection.RemoteIpAddress = IPAddress.Parse("127.0.0.1");

            var dto = new CreateActivityLogDto
            {
                UserId = 10,
                Action = "Login",
                Entity = "Auth"
                // IpAddress and UserAgent intentionally left null to be enriched
            };

            var result = await controller.CreateActivityLog(dto);
            var created = Assert.IsType<CreatedAtActionResult>(result.Result);
            var payload = Assert.IsType<ActivityLogDto>(created.Value);
            Assert.Equal("Login", payload.Action);

            Assert.NotNull(svc.LastLoggedDto);
            Assert.False(string.IsNullOrWhiteSpace(svc.LastLoggedDto!.IpAddress));
            Assert.False(string.IsNullOrWhiteSpace(svc.LastLoggedDto!.UserAgent));
            Assert.Equal("UnitTestAgent/1.0", svc.LastLoggedDto!.UserAgent);
            Assert.Equal("127.0.0.1", svc.LastLoggedDto!.IpAddress);
        }

        [Fact]
        public async Task CreateActivityLog_ReturnsBadRequest_OnArgumentException()
        {
            var svc = new FakeActivityLogServiceForController { ThrowArgumentOnLog = true };
            var controller = CreateController(svc, AdminIdentity());
            var dto = new CreateActivityLogDto { UserId = 10, Action = "Bad", Entity = "Test" };
            var result = await controller.CreateActivityLog(dto);
            Assert.IsType<BadRequestObjectResult>(result.Result);
        }
    }
}
