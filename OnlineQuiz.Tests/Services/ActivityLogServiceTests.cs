using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OnlineQuiz.DTOs;
using OnlineQuiz.IRepository;
using OnlineQuiz.Models;
using OnlineQuiz.Services;
using Xunit;

namespace OnlineQuiz.Tests.Services
{
    // In-memory fake repositories tailored for ActivityLogService tests
    public class FakeActivityLogRepositoryForServiceTests : IActivityLogRepository
    {
        private readonly List<ActivityLog> _store = new();

        public Task<ActivityLog> CreateAsync(ActivityLog log)
        {
            log.ActivityLogId = _store.Count + 1;
            _store.Add(log);
            return Task.FromResult(log);
        }

        public Task<ActivityLog?> GetByIdAsync(long id)
        {
            return Task.FromResult(_store.FirstOrDefault(l => l.ActivityLogId == id));
        }

        public Task<List<ActivityLog>> GetAllAsync()
        {
            return Task.FromResult(_store.ToList());
        }

        public Task<List<ActivityLog>> GetByUserIdAsync(int userId)
        {
            var list = _store.Where(l => l.UserId == userId).ToList();
            return Task.FromResult(list);
        }

        public Task<List<ActivityLog>> GetByActionAsync(string action)
        {
            var list = _store.Where(l => string.Equals(l.Action, action, StringComparison.OrdinalIgnoreCase)).ToList();
            return Task.FromResult(list);
        }

        public Task<List<ActivityLog>> GetByEntityAsync(string entity, long? entityId = null)
        {
            var query = _store.Where(l => string.Equals(l.Entity, entity, StringComparison.OrdinalIgnoreCase));
            if (entityId.HasValue)
            {
                query = query.Where(l => l.EntityId == entityId.Value);
            }
            return Task.FromResult(query.ToList());
        }

        public Task<List<ActivityLog>> GetFilteredAsync(ActivityLogFilterDto filter)
        {
            IEnumerable<ActivityLog> query = _store;
            if (filter.UserId.HasValue)
                query = query.Where(l => l.UserId == filter.UserId.Value);
            if (filter.StartDate.HasValue)
                query = query.Where(l => l.CreatedAt >= filter.StartDate.Value);
            if (filter.EndDate.HasValue)
                query = query.Where(l => l.CreatedAt <= filter.EndDate.Value);

            // Basic paging
            var page = (filter.PageNumber == 0) ? 1 : filter.PageNumber;
            var size = (filter.PageSize == 0) ? 20 : filter.PageSize;
            query = query.Skip((page - 1) * size).Take(size);

            return Task.FromResult(query.ToList());
        }

        public Task<List<ActivityLog>> GetRecentAsync(int count)
        {
            var recent = _store.OrderByDescending(l => l.CreatedAt).Take(count).ToList();
            return Task.FromResult(recent);
        }

        // Helpers for tests
        public void Seed(IEnumerable<ActivityLog> logs)
        {
            foreach (var log in logs)
            {
                log.ActivityLogId = _store.Count + 1;
                _store.Add(log);
            }
        }
    }

    public class FakeUserRepositoryForActivityLogServiceTests : IUserRepository
    {
        private readonly Dictionary<int, User> _users = new();

        public FakeUserRepositoryForActivityLogServiceTests()
        {
            // Seed default users
            _users[1] = new User { UserId = 1, FullName = "Alice Admin", Email = "alice@example.com" };
            _users[2] = new User { UserId = 2, FullName = "Bob Student", Email = "bob@example.com" };
        }

        public Task<int> CountAsync() => Task.FromResult(_users.Count);

        public Task<User?> GetByIdAsync(int userId)
        {
            _users.TryGetValue(userId, out var user);
            return Task.FromResult(user);
        }

        public Task<List<User>> GetByIdsAsync(List<int> userIds)
        {
            var list = userIds.Select(id => _users.TryGetValue(id, out var u) ? u : null)
                .Where(u => u != null)
                .Cast<User>()
                .ToList();
            return Task.FromResult(list);
        }

        // Unused in these tests
        public Task<List<User>> GetRecentRegistrationsAsync(int days) => Task.FromResult(new List<User>());
        public Task<User?> GetByEmailAsync(string email) => Task.FromResult(_users.Values.FirstOrDefault(u => u.Email == email));
        public Task<User> CreateAsync(User user)
        {
            user.UserId = _users.Count == 0 ? 1 : _users.Keys.Max() + 1;
            _users[user.UserId] = user;
            return Task.FromResult(user);
        }
        public Task<User> UpdateAsync(User user)
        {
            _users[user.UserId] = user;
            return Task.FromResult(user);
        }
        public Task<bool> DeleteAsync(int userId)
        {
            return Task.FromResult(_users.Remove(userId));
        }

        public Task<List<User>> GetAllAsync()
        {
            return Task.FromResult(_users.Values.ToList());
        }

        public Task<int> CountByRoleAsync(int roleId)
        {
            // Roles not tracked in this fake; return 0.
            return Task.FromResult(0);
        }

        public Task<int> BulkDeleteAsync(List<int> userIds)
        {
            int count = 0;
            foreach (var id in userIds)
            {
                if (_users.Remove(id)) count++;
            }
            return Task.FromResult(count);
        }
    }

    public class ActivityLogServiceTests
    {
        private ActivityLogService CreateService(
            FakeActivityLogRepositoryForServiceTests? logRepo = null,
            FakeUserRepositoryForActivityLogServiceTests? userRepo = null)
        {
            logRepo ??= new FakeActivityLogRepositoryForServiceTests();
            userRepo ??= new FakeUserRepositoryForActivityLogServiceTests();
            return new ActivityLogService(logRepo, userRepo);
        }

        [Fact]
        public async Task LogActivityAsync_InvalidAction_ThrowsArgumentException()
        {
            var svc = CreateService();
            var dto = new CreateActivityLogDto
            {
                UserId = 1,
                Action = "InvalidAction",
                Entity = "Course",
                Description = "x",
                IpAddress = "127.0.0.1",
                UserAgent = "TestAgent"
            };
            await Assert.ThrowsAsync<ArgumentException>(() => svc.LogActivityAsync(dto));
        }

        [Fact]
        public async Task LogActivityAsync_InvalidEntity_ThrowsArgumentException()
        {
            var svc = CreateService();
            var dto = new CreateActivityLogDto
            {
                UserId = 1,
                Action = "Create",
                Entity = "NotAnEntity",
                Description = "x",
                IpAddress = "127.0.0.1",
                UserAgent = "TestAgent"
            };
            await Assert.ThrowsAsync<ArgumentException>(() => svc.LogActivityAsync(dto));
        }

        [Fact]
        public async Task LogActivityAsync_Valid_ReturnsDtoWithUserInfo()
        {
            var logRepo = new FakeActivityLogRepositoryForServiceTests();
            var userRepo = new FakeUserRepositoryForActivityLogServiceTests();
            var svc = CreateService(logRepo, userRepo);

            var dto = new CreateActivityLogDto
            {
                UserId = 1,
                Action = "CREATE",
                Entity = "Course",
                Description = "Created course",
                IpAddress = "10.0.0.1",
                UserAgent = "UnitTest"
            };

            var result = await svc.LogActivityAsync(dto);
            Assert.NotNull(result);
            Assert.Equal(1, result.UserId);
            Assert.Equal("Alice Admin", result.UserFullName);
            Assert.Equal("alice@example.com", result.UserEmail);
            Assert.Equal("CREATE", result.Action);
            Assert.Equal("Course", result.Entity);
            Assert.Equal("10.0.0.1", result.IpAddress);
            Assert.Equal("UnitTest", result.UserAgent);
        }

        [Fact]
        public async Task GetActivityLogByIdAsync_NotFound_ReturnsNull()
        {
            var logRepo = new FakeActivityLogRepositoryForServiceTests();
            var svc = CreateService(logRepo);
            var result = await svc.GetActivityLogByIdAsync(999);
            Assert.Null(result);
        }

        [Fact]
        public async Task GetActivityLogByIdAsync_Found_ReturnsDto()
        {
            var logRepo = new FakeActivityLogRepositoryForServiceTests();
            var userRepo = new FakeUserRepositoryForActivityLogServiceTests();
            var svc = CreateService(logRepo, userRepo);

            await logRepo.CreateAsync(new ActivityLog
            {
                UserId = 2,
                Action = "UPDATE",
                Entity = "Quiz",
                Description = "Updated quiz",
                CreatedAt = DateTime.UtcNow
            });

            var dto = await svc.GetActivityLogByIdAsync(1);
            Assert.NotNull(dto);
            Assert.Equal(2, dto!.UserId);
            Assert.Equal("Bob Student", dto.UserFullName);
            Assert.Equal("bob@example.com", dto.UserEmail);
            Assert.Equal("UPDATE", dto.Action);
            Assert.Equal("Quiz", dto.Entity);
        }

        [Fact]
        public async Task GetActivityLogsAsync_FiltersAndMapsUserInfo()
        {
            var logRepo = new FakeActivityLogRepositoryForServiceTests();
            var userRepo = new FakeUserRepositoryForActivityLogServiceTests();
            logRepo.Seed(new[]
            {
                new ActivityLog { UserId = 1, Action = "CREATE", Entity = "Course", Description = "d1", CreatedAt = DateTime.UtcNow.AddDays(-1) },
                new ActivityLog { UserId = 2, Action = "DELETE", Entity = "Quiz", Description = "d2", CreatedAt = DateTime.UtcNow }
            });

            var svc = CreateService(logRepo, userRepo);
            var filter = new ActivityLogFilterDto { UserId = 1, PageSize = 10 };
            var list = await svc.GetActivityLogsAsync(filter);
            Assert.Single(list);
            Assert.Equal("Alice Admin", list[0].UserFullName);
            Assert.Equal("CREATE", list[0].Action);
        }

        [Fact]
        public async Task GetUserActivityLogsAsync_DaysFilterApplies()
        {
            var logRepo = new FakeActivityLogRepositoryForServiceTests();
            var userRepo = new FakeUserRepositoryForActivityLogServiceTests();
            logRepo.Seed(new[]
            {
                new ActivityLog { UserId = 1, Action = "CREATE", Entity = "Course", Description = "old", CreatedAt = DateTime.UtcNow.AddDays(-10) },
                new ActivityLog { UserId = 1, Action = "UPDATE", Entity = "Course", Description = "recent", CreatedAt = DateTime.UtcNow.AddDays(-1) }
            });
            var svc = CreateService(logRepo, userRepo);
            var list = await svc.GetUserActivityLogsAsync(1, days: 7);
            Assert.Single(list);
            Assert.Equal("recent", list[0].Description);
        }

        [Fact]
        public async Task GetActivityStatisticsAsync_ComputesCountsAndRecent()
        {
            var logRepo = new FakeActivityLogRepositoryForServiceTests();
            var userRepo = new FakeUserRepositoryForActivityLogServiceTests();
            logRepo.Seed(new[]
            {
                new ActivityLog { UserId = 1, Action = "CREATE", Entity = "Course", Description = "d1", CreatedAt = DateTime.UtcNow.AddMinutes(-30) },
                new ActivityLog { UserId = 2, Action = "UPDATE", Entity = "Quiz", Description = "d2", CreatedAt = DateTime.UtcNow.AddMinutes(-20) },
                new ActivityLog { UserId = 1, Action = "DELETE", Entity = "Course", Description = "d3", CreatedAt = DateTime.UtcNow.AddMinutes(-10) }
            });

            var svc = CreateService(logRepo, userRepo);
            var stats = await svc.GetActivityStatisticsAsync(null, 30);
            Assert.Equal(3, stats.TotalActions);
            Assert.Equal(2, stats.UniqueUsers);
            Assert.True(stats.ActionsByType.ContainsKey("CREATE"));
            Assert.True(stats.EntitiesAffected.ContainsKey("Course"));
            Assert.NotNull(stats.RecentActivity);
            Assert.True(stats.RecentActivity.Count <= 10);
            // Verify mapping includes user names
            Assert.Contains(stats.RecentActivity, a => a.UserFullName == "Alice Admin" || a.UserFullName == "Bob Student");
        }
    }
}
