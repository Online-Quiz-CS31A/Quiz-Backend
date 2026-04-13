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
    internal class FakeNotificationService : INotificationService
    {
        public bool ReturnNullOnGetById { get; set; }
        public bool ThrowUnauthorizedOnDelete { get; set; }
        public bool ThrowArgumentOnMarkAsRead { get; set; }
        public bool ThrowUnauthorizedOnMarkAsRead { get; set; }

        public Task<NotificationResponseDto> CreateNotificationAsync(CreateNotificationDto createNotificationDto, int createdBy)
            => Task.FromResult(new NotificationResponseDto
            {
                NotificationId = 123,
                UserId = createNotificationDto.UserId,
                Type = createNotificationDto.Type,
                Title = createNotificationDto.Title,
                Message = createNotificationDto.Message,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });

        public Task<NotificationResponseDto?> GetNotificationByIdAsync(int notificationId, int userId)
            => Task.FromResult(ReturnNullOnGetById ? null : new NotificationResponseDto
            {
                NotificationId = notificationId,
                UserId = userId,
                Type = NotificationConstants.TypeSystem,
                Title = "Test",
                Message = "Hello",
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });

        public Task<List<NotificationResponseDto>> GetNotificationsForUserAsync(int userId)
            => Task.FromResult(new List<NotificationResponseDto>
            {
                new NotificationResponseDto { NotificationId = 1, UserId = userId, Title = "A", Message = "msg", Type = NotificationConstants.TypeSystem, CreatedAt = DateTime.UtcNow },
                new NotificationResponseDto { NotificationId = 2, UserId = userId, Title = "B", Message = "msg2", Type = NotificationConstants.TypeCourse, CreatedAt = DateTime.UtcNow },
            });

        public Task<NotificationResponseDto> MarkAsReadAsync(int notificationId, int userId)
        {
            if (ThrowArgumentOnMarkAsRead) throw new ArgumentException("Invalid notification id");
            if (ThrowUnauthorizedOnMarkAsRead) throw new UnauthorizedAccessException("Forbidden");
            return Task.FromResult(new NotificationResponseDto
            {
                NotificationId = notificationId,
                UserId = userId,
                Title = "Read",
                Message = "Marked",
                Type = NotificationConstants.TypeSystem,
                IsRead = true,
                CreatedAt = DateTime.UtcNow
            });
        }

        public Task<bool> DeleteNotificationAsync(int notificationId, int userId)
        {
            if (ThrowUnauthorizedOnDelete) throw new UnauthorizedAccessException("Forbidden");
            return Task.FromResult(true);
        }

        public Task<bool> MarkAllAsReadAsync(int userId) => Task.FromResult(true);
        public Task NotifyStudentsOfNewQuizAsync(int quizId, int courseId, string quizTitle) => Task.CompletedTask;
        public Task<int> BulkDeleteNotificationsAsync(List<int> notificationIds, int userId) => Task.FromResult(notificationIds.Count);
    }

    internal class FakeUserServiceMinimal : IUserService
    {
        public Task<UserResponseDto> CreateUserAsync(CreateUserDto createUserDto) => throw new NotImplementedException();
        public Task<UserResponseDto?> GetUserByIdAsync(int userId) => throw new NotImplementedException();
        public Task<List<UserResponseDto>> GetAllUsersAsync() => throw new NotImplementedException();
        public Task<PagedResult<UserResponseDto>> GetAllUsersPagedAsync(PaginationParams paginationParams) => throw new NotImplementedException();
        public Task<UserResponseDto> UpdateUserAsync(int userId, UpdateUserDto updateUserDto) => throw new NotImplementedException();
        public Task<bool> DeleteUserAsync(int userId) => throw new NotImplementedException();
        public Task<int> BulkDeleteAsync(List<int> userIds) => throw new NotImplementedException();
        public Task ResetPasswordAsync(int userId, string newPassword) => throw new NotImplementedException();
        public Task<BulkUserImportResultDto> BulkCreateUsersFromExcelAsync(Stream fileStream, string fileName, int createdByUserId) => throw new NotImplementedException();
    }

    internal class FakeActivityLogServiceForNotifications : IActivityLogService
    {
        public Task<ActivityLogDto> LogActivityAsync(CreateActivityLogDto dto)
            => Task.FromResult(new ActivityLogDto { ActivityLogId = 1, UserId = dto.UserId, Action = dto.Action, Entity = dto.Entity, EntityId = dto.EntityId, CreatedAt = DateTime.UtcNow });

        public Task<List<ActivityLogDto>> GetActivityLogsAsync(ActivityLogFilterDto filter)
            => Task.FromResult(new List<ActivityLogDto>());

        public Task<List<ActivityLogDto>> GetUserActivityLogsAsync(int userId, int? days = null)
            => Task.FromResult(new List<ActivityLogDto>());

        public Task<ActivityLogStatisticsDto> GetActivityStatisticsAsync(int? userId = null, int? days = 30)
            => Task.FromResult(new ActivityLogStatisticsDto());

        public Task<ActivityLogDto?> GetActivityLogByIdAsync(long activityLogId)
            => Task.FromResult<ActivityLogDto?>(null);
    }

    public class NotificationControllerTests
    {
        private static NotificationController CreateController(FakeNotificationService svc, ClaimsIdentity identity)
        {
            var controller = new NotificationController(svc, new FakeUserServiceMinimal(), new FakeActivityLogServiceForNotifications());
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            };
            return controller;
        }

        private static ClaimsIdentity AdminIdentity()
            => new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "10"),
                new Claim(ClaimTypes.Role, "Admin")
            }, "TestAuth");

        [Fact]
        public async Task CreateNotification_ReturnsCreated_WhenValidType()
        {
            var svc = new FakeNotificationService();
            var controller = CreateController(svc, AdminIdentity());

            var dto = new CreateNotificationDto
            {
                UserId = 10,
                Type = NotificationConstants.TypeSystem,
                Title = "System Notice",
                Message = "All good"
            };

            var result = await controller.CreateNotification(dto);
            var created = Assert.IsType<CreatedAtActionResult>(result.Result);
            var payload = Assert.IsType<NotificationResponseDto>(created.Value);
            Assert.Equal("System Notice", payload.Title);
            Assert.False(payload.IsRead);
        }

        [Fact]
        public async Task CreateNotification_ReturnsBadRequest_OnInvalidType()
        {
            var svc = new FakeNotificationService();
            var controller = CreateController(svc, AdminIdentity());

            var dto = new CreateNotificationDto
            {
                UserId = 10,
                Type = "InvalidType",
                Title = "Should Fail",
                Message = "Bad type"
            };

            var result = await controller.CreateNotification(dto);
            var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.NotNull(bad.Value);
        }

        [Fact]
        public async Task GetMyNotifications_ReturnsOk_WithList()
        {
            var svc = new FakeNotificationService();
            var controller = CreateController(svc, AdminIdentity());

            var result = await controller.GetMyNotifications();
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var list = Assert.IsType<List<NotificationResponseDto>>(ok.Value);
            Assert.True(list.Count >= 2);
        }

        [Fact]
        public async Task GetNotification_ReturnsNotFound_WhenMissing()
        {
            var svc = new FakeNotificationService { ReturnNullOnGetById = true };
            var controller = CreateController(svc, AdminIdentity());

            var result = await controller.GetNotification(999);
            Assert.IsType<NotFoundResult>(result.Result);
        }

        [Fact]
        public async Task MarkAsRead_ReturnsOk_WhenValid()
        {
            var svc = new FakeNotificationService();
            var controller = CreateController(svc, AdminIdentity());

            var result = await controller.MarkAsRead(5);
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var payload = Assert.IsType<NotificationResponseDto>(ok.Value);
            Assert.True(payload.IsRead);
        }

        [Fact]
        public async Task DeleteNotification_ReturnsNoContent_WhenValid()
        {
            var svc = new FakeNotificationService();
            var controller = CreateController(svc, AdminIdentity());

            var result = await controller.DeleteNotification(5);
            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task BulkDeleteNotifications_ReturnsNoContent_WhenValid()
        {
            var svc = new FakeNotificationService();
            var controller = CreateController(svc, AdminIdentity());

            var dto = new BulkDeleteNotificationsDto { NotificationIds = new List<int> { 1, 2, 3 } };
            var result = await controller.BulkDeleteNotifications(dto);
            Assert.IsType<NoContentResult>(result);
        }
    }
}
