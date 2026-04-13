using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OnlineQuiz.DTOs;
using OnlineQuiz.IRepository;
using OnlineQuiz.IServices;
using OnlineQuiz.Models;
using OnlineQuiz.Services;
using OnlineQuiz.Utilities;
using Xunit;

namespace OnlineQuiz.Tests.Services
{
    public class UserServiceTests
    {
        // In-memory fakes for repositories and dependent service
        private class InMemoryUserRepository : IUserRepository
        {
            private readonly Dictionary<int, User> _store = new();
            private int _nextId = 1;

            public Task<User?> GetByEmailAsync(string email)
            {
                var u = _store.Values.FirstOrDefault(x => x.Email == email);
                return Task.FromResult<User?>(u == null ? null : Clone(u));
            }

            public Task<User?> GetByIdAsync(int userId)
            {
                _store.TryGetValue(userId, out var u);
                return Task.FromResult<User?>(u == null ? null : Clone(u));
            }

            public Task<List<User>> GetAllAsync()
            {
                return Task.FromResult(_store.Values.Select(Clone).ToList());
            }

            public Task<User> CreateAsync(User user)
            {
                user.UserId = _nextId++;
                _store[user.UserId] = Clone(user);
                return Task.FromResult(Clone(user));
            }

            public Task<User> UpdateAsync(User user)
            {
                if (!_store.ContainsKey(user.UserId)) throw new InvalidOperationException("User not found");
                _store[user.UserId] = Clone(user);
                return Task.FromResult(Clone(user));
            }

            public Task<bool> DeleteAsync(int userId)
            {
                return Task.FromResult(_store.Remove(userId));
            }

            public Task<int> BulkDeleteAsync(List<int> userIds)
            {
                var count = 0;
                foreach (var id in userIds)
                {
                    if (_store.Remove(id)) count++;
                }
                return Task.FromResult(count);
            }

            public Task<List<User>> GetByIdsAsync(List<int> userIds)
            {
                var result = _store.Values.Where(u => userIds.Contains(u.UserId)).Select(Clone).ToList();
                return Task.FromResult(result);
            }

            public Task<int> CountAsync()
            {
                return Task.FromResult(_store.Count);
            }

            public Task<int> CountByRoleAsync(int roleId)
            {
                // Roles are not tracked in this in-memory repo
                return Task.FromResult(0);
            }

            public Task<List<User>> GetRecentRegistrationsAsync(int days)
            {
                var result = _store.Values.Select(Clone).ToList();
                return Task.FromResult(result);
            }

            private static User Clone(User u) => new User
            {
                UserId = u.UserId,
                Email = u.Email,
                PasswordHash = u.PasswordHash,
                FullName = u.FullName,
                Status = u.Status,
                ContactNumber = u.ContactNumber,
                EmergencyContactNumber = u.EmergencyContactNumber,
                CreatedAt = u.CreatedAt,
                UpdatedAt = u.UpdatedAt,
                CreatedBy = u.CreatedBy
            };
        }

        private class InMemoryStudentRepository : IStudentRepository
        {
            private readonly Dictionary<int, Student> _store = new();

            public Task<Student> CreateAsync(Student student)
            {
                _store[student.UserId] = Clone(student);
                return Task.FromResult(Clone(student));
            }

            public Task<Student?> GetByUserIdAsync(int userId)
            {
                _store.TryGetValue(userId, out var s);
                return Task.FromResult<Student?>(s == null ? null : Clone(s));
            }

            public Task<List<Student>> GetAllAsync()
            {
                return Task.FromResult(_store.Values.Select(Clone).ToList());
            }

            public Task<Student> UpdateAsync(Student student)
            {
                _store[student.UserId] = Clone(student);
                return Task.FromResult(Clone(student));
            }

            public Task<List<Student>> GetByIdsAsync(List<int> userIds)
            {
                var result = _store.Values.Where(s => userIds.Contains(s.UserId)).Select(Clone).ToList();
                return Task.FromResult(result);
            }

            public Task<bool> DeleteAsync(int userId)
            {
                return Task.FromResult(_store.Remove(userId));
            }

            private static Student Clone(Student s) => new Student
            {
                UserId = s.UserId,
                StudentId = s.StudentId,
                YearLevel = s.YearLevel,
                Section = s.Section,
                Course = s.Course
            };
        }

        private class InMemoryTeacherRepository : ITeacherRepository
        {
            private readonly Dictionary<int, Teacher> _store = new();

            public Task<Teacher> CreateAsync(Teacher teacher)
            {
                _store[teacher.UserId] = Clone(teacher);
                return Task.FromResult(Clone(teacher));
            }

            public Task<Teacher?> GetByUserIdAsync(int userId)
            {
                _store.TryGetValue(userId, out var t);
                return Task.FromResult<Teacher?>(t == null ? null : Clone(t));
            }

            public Task<List<Teacher>> GetAllAsync()
            {
                return Task.FromResult(_store.Values.Select(Clone).ToList());
            }

            public Task<Teacher> UpdateAsync(Teacher teacher)
            {
                _store[teacher.UserId] = Clone(teacher);
                return Task.FromResult(Clone(teacher));
            }

            public Task<bool> DeleteAsync(int userId)
            {
                return Task.FromResult(_store.Remove(userId));
            }

            private static Teacher Clone(Teacher t) => new Teacher
            {
                UserId = t.UserId,
                Department = t.Department
            };
        }

        private class InMemoryUserRoleRepository : IUserRoleRepository
        {
            private readonly Dictionary<int, UserRole> _store = new();
            private readonly HashSet<int> _admins = new();

            public Task<UserRole> CreateAsync(UserRole role)
            {
                _store[role.UserId] = new UserRole { UserId = role.UserId, RoleId = role.RoleId };
                if (role.RoleId == RoleConstants.Admin)
                {
                    _admins.Add(role.UserId);
                }
                return Task.FromResult(new UserRole { UserId = role.UserId, RoleId = role.RoleId });
            }

            public Task<List<UserRole>> GetByUserIdAsync(int userId)
            {
                _store.TryGetValue(userId, out var r);
                var list = r == null ? new List<UserRole>() : new List<UserRole> { new UserRole { UserId = r.UserId, RoleId = r.RoleId } };
                return Task.FromResult(list);
            }

            public Task<List<UserRole>> GetAllAsync()
            {
                return Task.FromResult(_store.Values.Select(r => new UserRole { UserId = r.UserId, RoleId = r.RoleId }).ToList());
            }

            public Task<bool> DeleteByUserIdAsync(int userId)
            {
                return Task.FromResult(_store.Remove(userId));
            }

            public Task<bool> IsAdminAsync(int userId)
            {
                return Task.FromResult(_admins.Contains(userId));
            }
        }

        private class DummyExportImportLogService : IExportImportLogService
        {
            public Task<ExportImportLogResponseDto> CreateLogAsync(CreateExportImportLogDto createLogDto)
            {
                var resp = new ExportImportLogResponseDto
                {
                    LogId = 1,
                    UserId = createLogDto.UserId,
                    Type = createLogDto.Type,
                    FileName = createLogDto.FileName,
                    Status = ExportImportConstants.Statuses.Pending,
                    CreatedAt = DateTime.UtcNow
                };
                return Task.FromResult(resp);
            }

            public Task<ExportImportLogResponseDto?> GetLogByIdAsync(int logId, int userId)
            {
                return Task.FromResult<ExportImportLogResponseDto?>(null);
            }

            public Task<List<ExportImportLogResponseDto>> GetLogsForUserAsync(int userId)
            {
                return Task.FromResult(new List<ExportImportLogResponseDto>());
            }

            public Task<ExportImportLogResponseDto> UpdateLogStatusAsync(int logId, UpdateExportImportLogDto updateLogDto, int userId)
            {
                var resp = new ExportImportLogResponseDto
                {
                    LogId = logId,
                    UserId = userId,
                    Type = ExportImportConstants.Types.Import,
                    FileName = string.Empty,
                    Status = updateLogDto.Status,
                    CreatedAt = DateTime.UtcNow,
                    CompletedAt = updateLogDto.CompletedAt,
                    ErrorMessage = updateLogDto.ErrorMessage
                };
                return Task.FromResult(resp);
            }

            public Task<bool> DeleteLogAsync(int logId, int userId)
            {
                return Task.FromResult(true);
            }
        }

        private static UserService CreateService(
            InMemoryUserRepository? userRepo = null,
            InMemoryStudentRepository? studentRepo = null,
            InMemoryTeacherRepository? teacherRepo = null,
            InMemoryUserRoleRepository? roleRepo = null)
        {
            return new UserService(
                userRepo ?? new InMemoryUserRepository(),
                studentRepo ?? new InMemoryStudentRepository(),
                teacherRepo ?? new InMemoryTeacherRepository(),
                roleRepo ?? new InMemoryUserRoleRepository(),
                new DummyExportImportLogService());
        }

        [Fact]
        public async Task CreateUser_AssignsRoleId_AndCreatesRoleSpecificEntity()
        {
            var userRepo = new InMemoryUserRepository();
            var studentRepo = new InMemoryStudentRepository();
            var roleRepo = new InMemoryUserRoleRepository();
            var svc = CreateService(userRepo, studentRepo, null, roleRepo);
            // Seed via repos directly to avoid heavy hashing during tests
            var seededUser = await userRepo.CreateAsync(new User
            {
                Email = "stud@example.com",
                PasswordHash = "hash",
                FullName = "Student Name",
                Status = "Active",
                ContactNumber = string.Empty,
                EmergencyContactNumber = string.Empty,
                CreatedBy = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await roleRepo.CreateAsync(new UserRole { UserId = seededUser.UserId, RoleId = RoleConstants.Student });
            await studentRepo.CreateAsync(new Student { UserId = seededUser.UserId, StudentId = "S-100", YearLevel = 2, Section = "B", Course = "IT" });

            var created = await svc.GetUserByIdAsync(seededUser.UserId);
            Assert.NotNull(created);
            Assert.Equal(RoleConstants.Student, created.RoleId);
            Assert.Equal("Student", created.RoleName);
            Assert.NotNull(created.Student);
            Assert.Equal("S-100", created.Student!.StudentId);

            var roles = await roleRepo.GetByUserIdAsync(created!.UserId);
            Assert.Single(roles);
            Assert.Equal(RoleConstants.Student, roles[0].RoleId);
        }

        [Fact]
        public async Task CreateUser_RequiresStudentId_ForStudentRole()
        {
            var svc = CreateService();
            var dto = new CreateUserDto
            {
                Email = "stud2@example.com",
                Password = "pw123456",
                FullName = "Student2",
                RoleId = RoleConstants.Student,
                CreatedBy = 1
            };

            await Assert.ThrowsAsync<ArgumentException>(() => svc.CreateUserAsync(dto));
        }

        [Fact]
        public async Task GetUserById_MapsTo_UserResponseDto_Correctly()
        {
            var userRepo = new InMemoryUserRepository();
            var studentRepo = new InMemoryStudentRepository();
            var roleRepo = new InMemoryUserRoleRepository();
            var svc = CreateService(userRepo, studentRepo, null, roleRepo);

            // Seed user directly via repos to simulate existing data
            var createdUser = await userRepo.CreateAsync(new User
            {
                Email = "stud3@example.com",
                PasswordHash = "hash",
                FullName = "Student3",
                Status = "Active",
                ContactNumber = "",
                EmergencyContactNumber = "",
                CreatedBy = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            await roleRepo.CreateAsync(new UserRole { UserId = createdUser.UserId, RoleId = RoleConstants.Student });
            await studentRepo.CreateAsync(new Student { UserId = createdUser.UserId, StudentId = "S-200", YearLevel = 3, Section = "C", Course = "CS" });

            var dto = await svc.GetUserByIdAsync(createdUser.UserId);
            Assert.NotNull(dto);
            Assert.Equal(createdUser.UserId, dto!.UserId);
            Assert.Equal("stud3@example.com", dto.Email);
            Assert.Equal("Student3", dto.FullName);
            Assert.Equal(RoleConstants.Student, dto.RoleId);
            Assert.Equal("Student", dto.RoleName);
            Assert.NotNull(dto.Student);
            Assert.Equal("S-200", dto.Student!.StudentId);
        }

        [Fact]
        public async Task UpdateUser_UpdatesRoleSpecificData_ForTeacher()
        {
            var userRepo = new InMemoryUserRepository();
            var teacherRepo = new InMemoryTeacherRepository();
            var roleRepo = new InMemoryUserRoleRepository();
            var svc = CreateService(userRepo, null, teacherRepo, roleRepo);

            var createdUser = await userRepo.CreateAsync(new User
            {
                Email = "teach@example.com",
                PasswordHash = "hash",
                FullName = "Teacher",
                Status = "Active",
                ContactNumber = "",
                EmergencyContactNumber = "",
                CreatedBy = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });

            await roleRepo.CreateAsync(new UserRole { UserId = createdUser.UserId, RoleId = RoleConstants.Teacher });
            await teacherRepo.CreateAsync(new Teacher { UserId = createdUser.UserId, Department = "Science" });

            var updated = await svc.UpdateUserAsync(createdUser.UserId, new UpdateUserDto { Department = "Mathematics" });
            Assert.Equal("Mathematics", updated.Teacher!.Department);
        }
    }
}
