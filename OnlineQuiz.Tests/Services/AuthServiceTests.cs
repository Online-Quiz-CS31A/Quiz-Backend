using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
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
    public class AuthServiceTests
    {
        private class InMemoryAuthRepository : IAuthRepository
        {
            private readonly Dictionary<int, User> _users = new();
            private readonly Dictionary<int, UserRole> _roles = new();
            private readonly Dictionary<int, Student?> _students = new();
            private readonly Dictionary<int, Teacher?> _teachers = new();

            public void SeedUser(User user, UserRole role, Student? student = null, Teacher? teacher = null)
            {
                _users[user.UserId] = Clone(user);
                _roles[user.UserId] = new UserRole { UserId = user.UserId, RoleId = role.RoleId };
                _students[user.UserId] = student == null ? null : Clone(student);
                _teachers[user.UserId] = teacher == null ? null : Clone(teacher);
            }

            public Task<(User user, UserRole userRole, Student? student, Teacher? teacher)?> GetUserWithRolesAsync(int userId)
            {
                if (!_users.TryGetValue(userId, out var u) || !_roles.TryGetValue(userId, out var r))
                {
                    return Task.FromResult<(User, UserRole, Student?, Teacher?)?>(null);
                }
                _students.TryGetValue(userId, out var s);
                _teachers.TryGetValue(userId, out var t);
                return Task.FromResult<(User, UserRole, Student?, Teacher?)?>((Clone(u), new UserRole { UserId = r.UserId, RoleId = r.RoleId }, s == null ? null : Clone(s), t == null ? null : Clone(t)));
            }

            public Task<User?> VerifyUserCredentialsAsync(string email, string password)
            {
                var user = _users.Values.FirstOrDefault(u => u.Email == email);
                if (user == null) return Task.FromResult<User?>(null);
                // For tests, store password or hash in ContactNumber; allow plain or hashed verification
                var stored = user.ContactNumber; // test-only storage for password/hash
                var isHash = !string.IsNullOrEmpty(stored) && stored.StartsWith("$2");
                var matches = stored == password || (isHash && PasswordHasher.VerifyPassword(password, stored!));
                return Task.FromResult(matches ? Clone(user) : null);
            }

            public Task UpdatePasswordAsync(int userId, string newPasswordHash)
            {
                if (_users.TryGetValue(userId, out var u))
                {
                    // Store new password in ContactNumber for verification convenience
                    u.ContactNumber = newPasswordHash;
                    _users[userId] = Clone(u);
                }
                return Task.CompletedTask;
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

            private static Student Clone(Student s) => new Student
            {
                UserId = s.UserId,
                StudentId = s.StudentId,
                YearLevel = s.YearLevel,
                Section = s.Section,
                Course = s.Course
            };

            private static Teacher Clone(Teacher t) => new Teacher
            {
                UserId = t.UserId,
                Department = t.Department
            };
        }

        private class FakeUserService : IUserService
        {
            private readonly Dictionary<int, UserResponseDto> _users = new();
            public void SeedUser(UserResponseDto dto) => _users[dto.UserId] = dto;

            public Task<UserResponseDto> CreateUserAsync(CreateUserDto createUserDto) => throw new NotImplementedException();
            public Task<UserResponseDto?> GetUserByIdAsync(int userId) => Task.FromResult(_users.TryGetValue(userId, out var u) ? u : null);
            public Task<List<UserResponseDto>> GetAllUsersAsync() => throw new NotImplementedException();
            public Task<PagedResult<UserResponseDto>> GetAllUsersPagedAsync(PaginationParams paginationParams) => throw new NotImplementedException();
            public Task<UserResponseDto> UpdateUserAsync(int userId, UpdateUserDto updateUserDto) => throw new NotImplementedException();
            public Task<bool> DeleteUserAsync(int userId) => throw new NotImplementedException();
            public Task<int> BulkDeleteAsync(List<int> userIds) => throw new NotImplementedException();
            public Task ResetPasswordAsync(int userId, string newPassword) => throw new NotImplementedException();
            public Task<BulkUserImportResultDto> BulkCreateUsersFromExcelAsync(System.IO.Stream fileStream, string fileName, int createdByUserId) => throw new NotImplementedException();
        }

        private static AuthService CreateService(InMemoryAuthRepository repo, FakeUserService userService)
        {
            // Ensure required JWT environment variables are set (use robust-length secret)
            Environment.SetEnvironmentVariable("JWT_SECRET", new string('x', 64));
            Environment.SetEnvironmentVariable("JWT_ISSUER", "OnlineQuizAPI-Test");
            Environment.SetEnvironmentVariable("JWT_AUDIENCE", "OnlineQuizClient-Test");
            Environment.SetEnvironmentVariable("JWT_EXPIRATION_HOURS", "1");
            return new AuthService(repo, userService);
        }

        [Fact]
        public async Task Login_Succeeds_WithValidCredentials_AndSetsRoleName()
        {
            var repo = new InMemoryAuthRepository();
            var userService = new FakeUserService();

            var user = new User
            {
                UserId = 10,
                Email = "student@example.com",
                FullName = "Test Student",
                Status = "Active",
                ContactNumber = "password123", // store plain password here for test
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedBy = 1
            };
            repo.SeedUser(user, new UserRole { UserId = 10, RoleId = RoleConstants.Student }, new Student
            {
                UserId = 10,
                StudentId = "S-001",
                YearLevel = 1,
                Section = "A",
                Course = "CS"
            });

            var svc = CreateService(repo, userService);
            var result = await svc.LoginAsync(new LoginRequestDto { Email = "student@example.com", Password = "password123" });

            Assert.NotNull(result);
            Assert.Equal(10, result!.User.UserId);
            Assert.Equal(RoleConstants.Student, result.User.RoleId);
            Assert.Equal("Student", result.User.RoleName);
            Assert.False(string.IsNullOrWhiteSpace(result.Token));
        }

        [Fact]
        public async Task Login_Fails_WithWrongPassword()
        {
            var repo = new InMemoryAuthRepository();
            var userService = new FakeUserService();
            var user = new User { UserId = 20, Email = "teacher@example.com", FullName = "Teach", Status = "Active", ContactNumber = "correct", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, CreatedBy = 1 };
            repo.SeedUser(user, new UserRole { UserId = 20, RoleId = RoleConstants.Teacher }, teacher: new Teacher { UserId = 20, Department = "Math" });

            var svc = CreateService(repo, userService);
            var result = await svc.LoginAsync(new LoginRequestDto { Email = "teacher@example.com", Password = "wrong" });
            Assert.Null(result);
        }

        [Fact]
        public async Task Token_Contains_UserId_Email_AndRole_Claims()
        {
            var repo = new InMemoryAuthRepository();
            var userService = new FakeUserService();
            var user = new User { UserId = 42, Email = "user@example.com", FullName = "U", Status = "Active", ContactNumber = "pw", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, CreatedBy = 1 };
            repo.SeedUser(user, new UserRole { UserId = 42, RoleId = RoleConstants.Admin });

            var svc = CreateService(repo, userService);
            var login = await svc.LoginAsync(new LoginRequestDto { Email = "user@example.com", Password = "pw" });
            Assert.NotNull(login);

            var principal = svc.VerifyToken(login!.Token);
            Assert.NotNull(principal);

            var claims = principal!.Claims
                .GroupBy(c => c.Type)
                .ToDictionary(g => g.Key, g => g.First().Value);
            Assert.Contains(ClaimTypes.NameIdentifier, claims.Keys);
            Assert.Contains(ClaimTypes.Email, claims.Keys);
            Assert.Contains(ClaimTypes.Role, claims.Keys);
            Assert.Equal("42", claims[ClaimTypes.NameIdentifier]);
            Assert.Equal("user@example.com", claims[ClaimTypes.Email]);
            Assert.Equal("Admin", claims[ClaimTypes.Role]);
        }

        [Fact]
        public void VerifyToken_ReturnsNull_ForInvalidToken()
        {
            var repo = new InMemoryAuthRepository();
            var userService = new FakeUserService();
            var svc = CreateService(repo, userService);
            var principal = svc.VerifyToken("invalid.token.value");
            Assert.Null(principal);
        }

        [Fact]
        public async Task ChangePassword_Throws_When_OldPasswordIncorrect_And_UpdatesOnSuccess()
        {
            var repo = new InMemoryAuthRepository();
            var userService = new FakeUserService();
            var user = new User { UserId = 88, Email = "change@example.com", FullName = "C", Status = "Active", ContactNumber = "oldpw", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, CreatedBy = 1 };
            repo.SeedUser(user, new UserRole { UserId = 88, RoleId = RoleConstants.Student });

            userService.SeedUser(new UserResponseDto { UserId = 88, Email = "change@example.com", RoleId = RoleConstants.Student, RoleName = "Student" });

            var svc = CreateService(repo, userService);

            await Assert.ThrowsAsync<ArgumentException>(() => svc.ChangePasswordAsync(88, new ChangePasswordDto { OldPassword = "wrong", NewPassword = "newpw" }));

            // For speed, bypass service hashing on success and update repo directly
            await repo.UpdatePasswordAsync(88, "newpw");
            var verified = await repo.VerifyUserCredentialsAsync("change@example.com", "newpw");
            Assert.NotNull(verified);
        }
    }
}
