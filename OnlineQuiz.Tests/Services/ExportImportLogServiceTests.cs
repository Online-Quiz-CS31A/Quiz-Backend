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
    public class ExportImportLogServiceTests
    {
        private class InMemoryExportImportLogRepository : IExportImportLogRepository
        {
            private readonly Dictionary<int, ExportImportLog> _store = new();

            public void Seed(int id, ExportImportLog log)
            {
                _store[id] = Clone(log);
            }

            public Task<ExportImportLog> CreateAsync(ExportImportLog log)
            {
                // Simulate creation without enforcing ID generation
                var id = _store.Count == 0 ? 1 : _store.Keys.Max() + 1;
                // Try to set a likely ID property if exists; otherwise rely on dictionary key
                // Note: entity may not have a public setter for ID; skipping explicit set
                _store[id] = Clone(log);
                return Task.FromResult(Clone(log));
            }

            public Task<ExportImportLog?> GetByIdAsync(int logId)
            {
                _store.TryGetValue(logId, out var l);
                return Task.FromResult(l != null ? Clone(l) : null);
            }

            public Task<List<ExportImportLog>> GetByUserIdAsync(int userId)
            {
                var list = _store.Values.Where(l => l.UserId == userId).Select(Clone).ToList();
                return Task.FromResult(list);
            }

            public Task<ExportImportLog> UpdateAsync(ExportImportLog log)
            {
                // Find by first matching record for same user and same file name
                var kv = _store.FirstOrDefault(kv => kv.Value.UserId == log.UserId && kv.Value.FileName == log.FileName);
                if (kv.Equals(default(KeyValuePair<int, ExportImportLog>)))
                {
                    throw new ArgumentException("Log not found");
                }
                _store[kv.Key] = Clone(log);
                return Task.FromResult(Clone(log));
            }

            public Task<bool> DeleteAsync(int logId)
            {
                var removed = _store.Remove(logId);
                return Task.FromResult(removed);
            }

            private static ExportImportLog Clone(ExportImportLog l) => new ExportImportLog
            {
                UserId = l.UserId,
                Type = l.Type,
                Status = l.Status,
                FileName = l.FileName,
                ErrorMessage = l.ErrorMessage,
                CreatedAt = l.CreatedAt,
                CompletedAt = l.CompletedAt
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

        private ExportImportLogService CreateService(InMemoryExportImportLogRepository? repo = null, InMemoryUserRoleRepository? roles = null)
        {
            return new ExportImportLogService(repo ?? new InMemoryExportImportLogRepository(), roles ?? new InMemoryUserRoleRepository());
        }

        [Fact]
        public async Task CreateLog_SetsPendingStatus_AndCreatedAt()
        {
            var service = CreateService();
            var dto = new CreateExportImportLogDto
            {
                UserId = 11,
                Type = "Export",
                FileName = "scores.xlsx"
            };

            var result = await service.CreateLogAsync(dto);
            Assert.NotNull(result);
            Assert.Equal("Pending", result.Status);
            Assert.Equal(dto.Type, result.Type);
            Assert.Equal(dto.FileName, result.FileName);
            Assert.True(result.CreatedAt <= DateTime.UtcNow);
        }

        [Fact]
        public async Task GetLogById_ReturnsNull_WhenMissing()
        {
            var service = CreateService();
            var result = await service.GetLogByIdAsync(logId: 404, userId: 1);
            Assert.Null(result);
        }

        [Fact]
        public async Task GetLogById_ThrowsUnauthorized_ForNonOwnerNonAdmin()
        {
            var repo = new InMemoryExportImportLogRepository();
            var roles = new InMemoryUserRoleRepository();
            var service = CreateService(repo, roles);

            repo.Seed(5, new ExportImportLog { UserId = 10, Type = "Import", Status = "Pending", FileName = "users.xlsx", CreatedAt = DateTime.UtcNow });

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetLogByIdAsync(logId: 5, userId: 20));
        }

        [Fact]
        public async Task GetLogById_AdminCanViewOthers()
        {
            var repo = new InMemoryExportImportLogRepository();
            var roles = new InMemoryUserRoleRepository();
            roles.AddAdmin(1);
            var service = CreateService(repo, roles);

            repo.Seed(3, new ExportImportLog { UserId = 7, Type = "Export", Status = "Pending", FileName = "scores.xlsx", CreatedAt = DateTime.UtcNow });
            var result = await service.GetLogByIdAsync(logId: 3, userId: 1);
            Assert.NotNull(result);
            Assert.Equal(7, result!.UserId);
        }

        [Fact]
        public async Task GetLogsForUser_ReturnsList()
        {
            var repo = new InMemoryExportImportLogRepository();
            var service = CreateService(repo);

            repo.Seed(1, new ExportImportLog { UserId = 2, Type = "Import", Status = "Pending", FileName = "users.xlsx", CreatedAt = DateTime.UtcNow });
            repo.Seed(2, new ExportImportLog { UserId = 2, Type = "Export", Status = "Success", FileName = "scores.xlsx", CreatedAt = DateTime.UtcNow });
            repo.Seed(3, new ExportImportLog { UserId = 3, Type = "Export", Status = "Failed", FileName = "scores2.xlsx", CreatedAt = DateTime.UtcNow });

            var result = await service.GetLogsForUserAsync(userId: 2);
            Assert.Equal(2, result.Count);
            Assert.All(result, r => Assert.Equal(2, r.UserId));
        }

        [Fact]
        public async Task UpdateLogStatus_ThrowsArgument_WhenMissing()
        {
            var service = CreateService();
            var dto = new UpdateExportImportLogDto { Status = "Success", CompletedAt = DateTime.UtcNow };
            await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateLogStatusAsync(logId: 404, updateLogDto: dto, userId: 1));
        }

        [Fact]
        public async Task UpdateLogStatus_ThrowsUnauthorized_ForNonOwnerNonAdmin()
        {
            var repo = new InMemoryExportImportLogRepository();
            var roles = new InMemoryUserRoleRepository();
            var service = CreateService(repo, roles);

            repo.Seed(7, new ExportImportLog { UserId = 50, Type = "Import", Status = "Pending", FileName = "users.xlsx", CreatedAt = DateTime.UtcNow });
            var dto = new UpdateExportImportLogDto { Status = "Success", CompletedAt = DateTime.UtcNow };

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.UpdateLogStatusAsync(logId: 7, updateLogDto: dto, userId: 51));
        }

        [Fact]
        public async Task UpdateLogStatus_UpdatesFields()
        {
            var repo = new InMemoryExportImportLogRepository();
            var roles = new InMemoryUserRoleRepository();
            var service = CreateService(repo, roles);

            repo.Seed(9, new ExportImportLog { UserId = 8, Type = "Export", Status = "Pending", FileName = "scores.xlsx", CreatedAt = DateTime.UtcNow });
            var dto = new UpdateExportImportLogDto { Status = "Failed", CompletedAt = DateTime.UtcNow, ErrorMessage = "Validation error" };

            var updated = await service.UpdateLogStatusAsync(logId: 9, updateLogDto: dto, userId: 8);
            Assert.Equal("Failed", updated.Status);
            Assert.Equal("Validation error", updated.ErrorMessage);
            Assert.NotNull(updated.CompletedAt);
        }

        [Fact]
        public async Task DeleteLog_ReturnsFalse_WhenMissing()
        {
            var service = CreateService();
            var deleted = await service.DeleteLogAsync(logId: 404, userId: 1);
            Assert.False(deleted);
        }

        [Fact]
        public async Task DeleteLog_ThrowsUnauthorized_ForNonOwnerNonAdmin()
        {
            var repo = new InMemoryExportImportLogRepository();
            var roles = new InMemoryUserRoleRepository();
            var service = CreateService(repo, roles);

            repo.Seed(10, new ExportImportLog { UserId = 2, Type = "Export", Status = "Success", FileName = "scores.xlsx", CreatedAt = DateTime.UtcNow });

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.DeleteLogAsync(logId: 10, userId: 3));
        }

        [Fact]
        public async Task DeleteLog_AdminCanDeleteOthers()
        {
            var repo = new InMemoryExportImportLogRepository();
            var roles = new InMemoryUserRoleRepository();
            roles.AddAdmin(99);
            var service = CreateService(repo, roles);

            repo.Seed(12, new ExportImportLog { UserId = 2, Type = "Export", Status = "Success", FileName = "scores.xlsx", CreatedAt = DateTime.UtcNow });
            var deleted = await service.DeleteLogAsync(logId: 12, userId: 99);
            Assert.True(deleted);
        }
    }
}