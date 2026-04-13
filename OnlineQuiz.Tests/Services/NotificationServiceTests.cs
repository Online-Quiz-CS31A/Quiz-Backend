using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OnlineQuiz.DTOs;
using OnlineQuiz.IRepository;
using OnlineQuiz.Models;
using OnlineQuiz.Services;
using OnlineQuiz.Utilities;
using Xunit;

namespace OnlineQuiz.Tests.Services
{
    public class NotificationServiceTests
    {
        // In-memory fakes
        private class InMemoryNotificationRepository : INotificationRepository
        {
            private readonly Dictionary<int, Notification> _store = new();
            private int _nextId = 1;

            public Task<Notification> CreateAsync(Notification notification)
            {
                // Simulate identity assignment
                var id = _nextId++;
                notification.NotificationId = id;
                _store[id] = Clone(notification);
                return Task.FromResult(Clone(notification));
            }

            public Task<List<Notification>> CreateBatchAsync(List<Notification> notifications)
            {
                foreach (var n in notifications)
                {
                    var id = _nextId++;
                    n.NotificationId = id;
                    _store[id] = Clone(n);
                }
                return Task.FromResult(notifications.Select(Clone).ToList());
            }

            public Task<Notification?> GetByIdAsync(int notificationId)
            {
                _store.TryGetValue(notificationId, out var n);
                return Task.FromResult(n != null ? Clone(n) : null);
            }

            public Task<List<Notification>> GetByUserIdAsync(int userId)
            {
                var list = _store.Values.Where(n => n.UserId == userId).Select(Clone).ToList();
                return Task.FromResult(list);
            }

            public Task<Notification> UpdateAsync(Notification notification)
            {
                if (!_store.ContainsKey(notification.NotificationId))
                {
                    throw new ArgumentException("Notification not found");
                }
                _store[notification.NotificationId] = Clone(notification);
                return Task.FromResult(Clone(notification));
            }

            public Task<bool> DeleteAsync(int notificationId)
            {
                var removed = _store.Remove(notificationId);
                return Task.FromResult(removed);
            }

            public Task<int> BulkDeleteAsync(List<int> notificationIds)
            {
                var count = 0;
                foreach (var id in notificationIds)
                {
                    if (_store.Remove(id)) count++;
                }
                return Task.FromResult(count);
            }

            public Task<bool> MarkAllAsReadAsync(int userId)
            {
                foreach (var kv in _store.ToList())
                {
                    if (kv.Value.UserId == userId && !kv.Value.IsRead)
                    {
                        kv.Value.IsRead = true;
                        _store[kv.Key] = Clone(kv.Value);
                    }
                }
                return Task.FromResult(true);
            }

            private static Notification Clone(Notification n) => new Notification
            {
                NotificationId = n.NotificationId,
                UserId = n.UserId,
                Type = n.Type,
                Title = n.Title,
                Message = n.Message,
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt
            };
        }

        private class InMemoryUserRoleRepository : IUserRoleRepository
        {
            private readonly Dictionary<int, UserRole> _store = new();
            private readonly HashSet<int> _admins = new();

            public void AddAdmin(int userId)
            {
                _admins.Add(userId);
                _store[userId] = new UserRole { UserId = userId, RoleId = RoleConstants.Admin };
            }

            public Task<bool> IsAdminAsync(int userId) => Task.FromResult(_admins.Contains(userId));

            public Task<UserRole> CreateAsync(UserRole role)
            {
                _store[role.UserId] = Clone(role);
                if (role.RoleId == RoleConstants.Admin)
                {
                    _admins.Add(role.UserId);
                }
                return Task.FromResult(Clone(role));
            }

            public Task<List<UserRole>> GetByUserIdAsync(int userId)
            {
                _store.TryGetValue(userId, out var role);
                var list = role != null ? new List<UserRole> { Clone(role) } : new List<UserRole>();
                return Task.FromResult(list);
            }

            public Task<List<UserRole>> GetAllAsync()
            {
                return Task.FromResult(_store.Values.Select(Clone).ToList());
            }

            public Task<bool> DeleteByUserIdAsync(int userId)
            {
                var removed = _store.Remove(userId);
                _admins.Remove(userId);
                return Task.FromResult(removed);
            }

            private static UserRole Clone(UserRole role) => new UserRole
            {
                UserId = role.UserId,
                RoleId = role.RoleId
            };
        }

        private class InMemoryEnrollmentRepository : IEnrollmentRepository
        {
            private readonly Dictionary<int, Enrollment> _store = new();
            private readonly Dictionary<int, List<int>> _courseToEnrollmentIds = new();
            private int _nextId = 1;

            public void Seed(int courseId, List<int> userIds)
            {
                foreach (var uid in userIds)
                {
                    var e = new Enrollment { EnrollmentId = _nextId, CourseId = courseId, UserId = uid, EnrolledBy = uid };
                    _store[_nextId] = Clone(e);
                    if (!_courseToEnrollmentIds.ContainsKey(courseId)) _courseToEnrollmentIds[courseId] = new List<int>();
                    _courseToEnrollmentIds[courseId].Add(_nextId);
                    _nextId++;
                }
            }

            public Task<Enrollment> CreateAsync(Enrollment enrollment)
            {
                enrollment.EnrollmentId = _nextId++;
                _store[enrollment.EnrollmentId] = Clone(enrollment);
                if (!_courseToEnrollmentIds.ContainsKey(enrollment.CourseId)) _courseToEnrollmentIds[enrollment.CourseId] = new List<int>();
                _courseToEnrollmentIds[enrollment.CourseId].Add(enrollment.EnrollmentId);
                return Task.FromResult(Clone(enrollment));
            }

            public Task<bool> ExistsAsync(int studentId, int courseId)
            {
                var exists = _store.Values.Any(e => e.UserId == studentId && e.CourseId == courseId);
                return Task.FromResult(exists);
            }

            public Task<List<Enrollment>> GetByCourseIdAsync(int courseId)
            {
                if (!_courseToEnrollmentIds.TryGetValue(courseId, out var ids))
                {
                    return Task.FromResult(new List<Enrollment>());
                }
                var list = ids.Select(id => Clone(_store[id])).ToList();
                return Task.FromResult(list);
            }

            public Task<bool> DeleteAsync(int enrollmentId)
            {
                if (!_store.TryGetValue(enrollmentId, out var e))
                {
                    return Task.FromResult(false);
                }
                _store.Remove(enrollmentId);
                if (_courseToEnrollmentIds.TryGetValue(e.CourseId, out var ids))
                {
                    ids.Remove(enrollmentId);
                }
                return Task.FromResult(true);
            }

            public Task<int> CountByCourseIdAsync(int courseId)
            {
                var count = _store.Values.Count(e => e.CourseId == courseId);
                return Task.FromResult(count);
            }

            public Task<Dictionary<int, int>> CountByCourseIdsAsync(List<int> courseIds)
            {
                var dict = new Dictionary<int, int>();
                foreach (var cid in courseIds)
                {
                    dict[cid] = _store.Values.Count(e => e.CourseId == cid);
                }
                return Task.FromResult(dict);
            }

            public Task<int> BulkDeleteByIdsAsync(List<int> enrollmentIds)
            {
                var count = 0;
                foreach (var id in enrollmentIds)
                {
                    if (awaitDelete(id)) count++;
                }
                return Task.FromResult(count);
            }

            public Task<int> BulkDeleteByCourseAndStudentsAsync(int courseId, List<int> studentIds)
            {
                var toDelete = _store.Values
                    .Where(e => e.CourseId == courseId && studentIds.Contains(e.UserId))
                    .Select(e => e.EnrollmentId)
                    .ToList();
                var count = 0;
                foreach (var id in toDelete)
                {
                    if (awaitDelete(id)) count++;
                }
                return Task.FromResult(count);
            }

            private bool awaitDelete(int enrollmentId)
            {
                if (!_store.TryGetValue(enrollmentId, out var e)) return false;
                _store.Remove(enrollmentId);
                if (_courseToEnrollmentIds.TryGetValue(e.CourseId, out var ids)) ids.Remove(enrollmentId);
                return true;
            }

            private static Enrollment Clone(Enrollment e) => new Enrollment
            {
                EnrollmentId = e.EnrollmentId,
                UserId = e.UserId,
                CourseId = e.CourseId,
                EnrolledAt = e.EnrolledAt,
                EnrolledBy = e.EnrolledBy,
                Section = e.Section,
                Course = e.Course
            };
        }

        private NotificationService CreateService(
            InMemoryNotificationRepository? repo = null,
            InMemoryUserRoleRepository? roleRepo = null,
            InMemoryEnrollmentRepository? enrollRepo = null)
        {
            return new NotificationService(repo ?? new InMemoryNotificationRepository(), roleRepo ?? new InMemoryUserRoleRepository(), enrollRepo ?? new InMemoryEnrollmentRepository());
        }

        [Fact]
        public async Task CreateNotification_SetsCreatedAt_AndReturnsResponse()
        {
            var repo = new InMemoryNotificationRepository();
            var service = CreateService(repo);

            var dto = new CreateNotificationDto
            {
                UserId = 7,
                Type = NotificationConstants.TypeQuiz,
                Title = "New Quiz",
                Message = "A new quiz is available"
            };

            var result = await service.CreateNotificationAsync(dto, createdBy: 99);

            Assert.NotNull(result);
            Assert.Equal(dto.UserId, result.UserId);
            Assert.Equal(dto.Type, result.Type);
            Assert.Equal(dto.Title, result.Title);
            Assert.Equal(dto.Message, result.Message);
            Assert.True(result.CreatedAt <= DateTime.UtcNow);
        }

        [Fact]
        public async Task GetNotificationById_ThrowsUnauthorized_ForNonOwnerNonAdmin()
        {
            var repo = new InMemoryNotificationRepository();
            var roles = new InMemoryUserRoleRepository();
            var service = CreateService(repo, roles);

            var created = await repo.CreateAsync(new Notification { UserId = 10, Type = NotificationConstants.TypeSystem, Title = "t", Message = "m", IsRead = false, CreatedAt = DateTime.UtcNow });

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetNotificationByIdAsync(created.NotificationId, userId: 20));
        }

        [Fact]
        public async Task GetNotificationById_AdminCanViewOthers()
        {
            var repo = new InMemoryNotificationRepository();
            var roles = new InMemoryUserRoleRepository();
            roles.AddAdmin(1);
            var service = CreateService(repo, roles);

            var created = await repo.CreateAsync(new Notification { UserId = 10, Type = NotificationConstants.TypeSystem, Title = "t", Message = "m", IsRead = false, CreatedAt = DateTime.UtcNow });

            var result = await service.GetNotificationByIdAsync(created.NotificationId, userId: 1);
            Assert.NotNull(result);
            Assert.Equal(10, result!.UserId);
        }

        [Fact]
        public async Task GetNotificationById_ReturnsNull_WhenNotFound()
        {
            var service = CreateService();
            var result = await service.GetNotificationByIdAsync(notificationId: 404, userId: 1);
            Assert.Null(result);
        }

        [Fact]
        public async Task MarkAsRead_SetsFlag_AndReturnsDto()
        {
            var repo = new InMemoryNotificationRepository();
            var service = CreateService(repo);

            var created = await repo.CreateAsync(new Notification { UserId = 5, Type = NotificationConstants.TypeSystem, Title = "t", Message = "m", IsRead = false, CreatedAt = DateTime.UtcNow });
            var result = await service.MarkAsReadAsync(created.NotificationId, userId: 5);

            Assert.True(result.IsRead);
        }

        [Fact]
        public async Task MarkAsRead_ThrowsArgument_WhenMissing()
        {
            var service = CreateService();
            await Assert.ThrowsAsync<ArgumentException>(() => service.MarkAsReadAsync(notificationId: 1234, userId: 1));
        }

        [Fact]
        public async Task MarkAsRead_ThrowsUnauthorized_WhenNotOwner()
        {
            var repo = new InMemoryNotificationRepository();
            var service = CreateService(repo);
            var created = await repo.CreateAsync(new Notification { UserId = 55, Type = NotificationConstants.TypeSystem, Title = "t", Message = "m", IsRead = false, CreatedAt = DateTime.UtcNow });

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.MarkAsReadAsync(created.NotificationId, userId: 56));
        }

        [Fact]
        public async Task DeleteNotification_ReturnsFalse_WhenMissing()
        {
            var service = CreateService();
            var deleted = await service.DeleteNotificationAsync(notificationId: 999, userId: 1);
            Assert.False(deleted);
        }

        [Fact]
        public async Task DeleteNotification_ThrowsUnauthorized_ForNonOwnerNonAdmin()
        {
            var repo = new InMemoryNotificationRepository();
            var roles = new InMemoryUserRoleRepository();
            var service = CreateService(repo, roles);
            var created = await repo.CreateAsync(new Notification { UserId = 2, Type = NotificationConstants.TypeSystem, Title = "t", Message = "m", IsRead = false, CreatedAt = DateTime.UtcNow });

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.DeleteNotificationAsync(created.NotificationId, userId: 3));
        }

        [Fact]
        public async Task DeleteNotification_AdminCanDeleteOthers()
        {
            var repo = new InMemoryNotificationRepository();
            var roles = new InMemoryUserRoleRepository();
            roles.AddAdmin(99);
            var service = CreateService(repo, roles);
            var created = await repo.CreateAsync(new Notification { UserId = 2, Type = NotificationConstants.TypeSystem, Title = "t", Message = "m", IsRead = false, CreatedAt = DateTime.UtcNow });

            var deleted = await service.DeleteNotificationAsync(created.NotificationId, userId: 99);
            Assert.True(deleted);
        }

        [Fact]
        public async Task MarkAllAsRead_ReturnsTrue()
        {
            var repo = new InMemoryNotificationRepository();
            var service = CreateService(repo);

            await repo.CreateAsync(new Notification { UserId = 7, Type = NotificationConstants.TypeSystem, Title = "t", Message = "m", IsRead = false, CreatedAt = DateTime.UtcNow });
            await repo.CreateAsync(new Notification { UserId = 7, Type = NotificationConstants.TypeSystem, Title = "t2", Message = "m2", IsRead = false, CreatedAt = DateTime.UtcNow });

            var result = await service.MarkAllAsReadAsync(userId: 7);
            Assert.True(result);
        }

        [Fact]
        public async Task BulkDelete_IgnoresNonExistent_DeletesOwned_ReturnsCount()
        {
            var repo = new InMemoryNotificationRepository();
            var service = CreateService(repo);

            var n1 = await repo.CreateAsync(new Notification { UserId = 1, Type = NotificationConstants.TypeSystem, Title = "t1", Message = "m1", IsRead = false, CreatedAt = DateTime.UtcNow });
            var n2 = await repo.CreateAsync(new Notification { UserId = 1, Type = NotificationConstants.TypeSystem, Title = "t2", Message = "m2", IsRead = false, CreatedAt = DateTime.UtcNow });
            var nOther = await repo.CreateAsync(new Notification { UserId = 2, Type = NotificationConstants.TypeSystem, Title = "t3", Message = "m3", IsRead = false, CreatedAt = DateTime.UtcNow });

            var ids = new List<int> { n1.NotificationId, n2.NotificationId, 404, nOther.NotificationId };
            var count = await service.BulkDeleteNotificationsAsync(ids, userId: 1);

            Assert.Equal(2, count);
            // Ensure other's notification remains
            var stillThere = await repo.GetByIdAsync(nOther.NotificationId);
            Assert.NotNull(stillThere);
        }

        [Fact]
        public async Task NotifyStudentsOfNewQuiz_CreatesBatch_ForEnrollments()
        {
            var repo = new InMemoryNotificationRepository();
            var roles = new InMemoryUserRoleRepository();
            var enrollRepo = new InMemoryEnrollmentRepository();
            enrollRepo.Seed(courseId: 10, userIds: new List<int> { 101, 102, 103 });
            var service = CreateService(repo, roles, enrollRepo);

            await service.NotifyStudentsOfNewQuizAsync(quizId: 77, courseId: 10, quizTitle: "Midterm");

            var user101 = await repo.GetByUserIdAsync(101);
            var user102 = await repo.GetByUserIdAsync(102);
            var user103 = await repo.GetByUserIdAsync(103);
            Assert.Single(user101);
            Assert.Single(user102);
            Assert.Single(user103);
            Assert.All(user101, n => Assert.Equal(NotificationConstants.TypeQuiz, n.Type));
        }

        [Fact]
        public async Task NotifyStudentsOfNewQuiz_NoEnrollments_NoNotifications()
        {
            var repo = new InMemoryNotificationRepository();
            var service = CreateService(repo, new InMemoryUserRoleRepository(), new InMemoryEnrollmentRepository());

            await service.NotifyStudentsOfNewQuizAsync(quizId: 12, courseId: 99, quizTitle: "Quiz");

            var none = await repo.GetByUserIdAsync(200);
            Assert.Empty(none);
        }
    }
}