using OnlineQuiz.DTOs;
using OnlineQuiz.IRepository;
using OnlineQuiz.IServices;
using OnlineQuiz.Models;
using OnlineQuiz.Utilities;

namespace OnlineQuiz.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;
        private readonly IStudentRepository _studentRepository;
        private readonly ITeacherRepository _teacherRepository;
        private readonly IUserRoleRepository _userRoleRepository;
        private readonly IExportImportLogService _exportImportLogService;

        public UserService(
            IUserRepository userRepository,
            IStudentRepository studentRepository,
            ITeacherRepository teacherRepository,
            IUserRoleRepository userRoleRepository,
            IExportImportLogService exportImportLogService)
        {
            _userRepository = userRepository;
            _studentRepository = studentRepository;
            _teacherRepository = teacherRepository;
            _userRoleRepository = userRoleRepository;
            _exportImportLogService = exportImportLogService;
        }

        public async Task<UserResponseDto> CreateUserAsync(CreateUserDto createUserDto)
        {
            // Validate role-specific requirements
            if (createUserDto.RoleId == RoleConstants.Student && string.IsNullOrEmpty(createUserDto.StudentId))
            {
                throw new ArgumentException("StudentId is required for students");
            }

            // Check if email already exists
            var existingUser = await _userRepository.GetByEmailAsync(createUserDto.Email);
            if (existingUser != null)
            {
                throw new InvalidOperationException($"User with email {createUserDto.Email} already exists");
            }

            // Create User entity
            var user = new User
            {
                Email = createUserDto.Email,
                PasswordHash = PasswordHasher.HashPassword(createUserDto.Password),
                FullName = createUserDto.FullName,
                Status = "Active",
                ContactNumber = createUserDto.ContactNumber ?? string.Empty,
                EmergencyContactNumber = createUserDto.EmergencyContactNumber ?? string.Empty,
                CreatedBy = createUserDto.CreatedBy,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Insert user
            var createdUser = await _userRepository.CreateAsync(user);

            try
            {
                // Create UserRole entry
                var userRole = new UserRole
                {
                    UserId = createdUser.UserId,
                    RoleId = createUserDto.RoleId
                };
                await _userRoleRepository.CreateAsync(userRole);

                // Create role-specific entity based on RoleId
                switch (createUserDto.RoleId)
                {
                    case RoleConstants.Student: // Student
                        var student = new Student
                        {
                            UserId = createdUser.UserId,
                            StudentId = createUserDto.StudentId!,
                            YearLevel = createUserDto.YearLevel,
                            Section = createUserDto.Section,
                            Course = createUserDto.Course
                        };
                        await _studentRepository.CreateAsync(student);
                        break;

                    case RoleConstants.Teacher: // Teacher
                        var teacher = new Teacher
                        {
                            UserId = createdUser.UserId,
                            Department = createUserDto.Department
                        };
                        await _teacherRepository.CreateAsync(teacher);
                        break;

                    case RoleConstants.Admin: // Admin - no additional table needed
                        break;
                }

                // Return the created user
                return await GetUserByIdAsync(createdUser.UserId) 
                    ?? throw new InvalidOperationException("Failed to retrieve created user");
            }
            catch (Exception)
            {
                // Manual Rollback: Delete the partially created user
                await _userRepository.DeleteAsync(createdUser.UserId);
                throw; // Re-throw the original exception
            }
        }

        public async Task<UserResponseDto?> GetUserByIdAsync(int userId)
        {
            // Fetch all data in parallel for better performance
            var userTask = _userRepository.GetByIdAsync(userId);
            var userRolesTask = _userRoleRepository.GetByUserIdAsync(userId);
            var studentTask = _studentRepository.GetByUserIdAsync(userId);
            var teacherTask = _teacherRepository.GetByUserIdAsync(userId);

            await Task.WhenAll(userTask, userRolesTask, studentTask, teacherTask);

            var user = await userTask;
            if (user == null)
            {
                return null;
            }

            var userRoles = await userRolesTask;
            var userRole = userRoles.FirstOrDefault();
            
            if (userRole == null)
            {
                throw new InvalidOperationException($"User {userId} has no role assigned");
            }

            var response = new UserResponseDto
            {
                UserId = user.UserId,
                Email = user.Email,
                FullName = user.FullName,
                Status = user.Status,
                ContactNumber = user.ContactNumber,
                EmergencyContactNumber = user.EmergencyContactNumber,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt,
                CreatedBy = user.CreatedBy,
                RoleId = userRole.RoleId,
                RoleName = GetRoleName(userRole.RoleId)
            };

            // Populate role-specific data from the already-fetched parallel queries
            switch (userRole.RoleId)
            {
                case RoleConstants.Student: // Student
                    var student = await studentTask;
                    if (student != null)
                    {
                        response.Student = new StudentData
                        {
                            StudentId = student.StudentId,
                            YearLevel = student.YearLevel,
                            Section = student.Section,
                            Course = student.Course
                        };
                    }
                    break;

                case RoleConstants.Teacher: // Teacher
                    var teacher = await teacherTask;
                    if (teacher != null)
                    {
                        response.Teacher = new TeacherData
                        {
                            Department = teacher.Department
                        };
                    }
                    break;
            }

            return response;
        }

        public async Task<List<UserResponseDto>> GetAllUsersAsync()
        {
            // Fetch all data in parallel
            var usersTask = _userRepository.GetAllAsync();
            var userRolesTask = _userRoleRepository.GetAllAsync();
            var studentsTask = _studentRepository.GetAllAsync();
            var teachersTask = _teacherRepository.GetAllAsync();

            await Task.WhenAll(usersTask, userRolesTask, studentsTask, teachersTask);

            var users = await usersTask;
            var userRoles = await userRolesTask;
            var students = await studentsTask;
            var teachers = await teachersTask;

            // Create dictionaries for fast lookup
            var userRoleMap = userRoles.GroupBy(ur => ur.UserId).ToDictionary(g => g.Key, g => g.First());
            var studentMap = students.ToDictionary(s => s.UserId);
            var teacherMap = teachers.ToDictionary(t => t.UserId);

            var userResponses = new List<UserResponseDto>();

            foreach (var user in users)
            {
                // Skip users without roles (data integrity issue, but handle gracefully)
                if (!userRoleMap.TryGetValue(user.UserId, out var userRole))
                {
                    continue;
                }

                var response = new UserResponseDto
                {
                    UserId = user.UserId,
                    Email = user.Email,
                    FullName = user.FullName,
                    Status = user.Status,
                    ContactNumber = user.ContactNumber,
                    EmergencyContactNumber = user.EmergencyContactNumber,
                    CreatedAt = user.CreatedAt,
                    UpdatedAt = user.UpdatedAt,
                    CreatedBy = user.CreatedBy,
                    RoleId = userRole.RoleId,
                    RoleName = GetRoleName(userRole.RoleId)
                };

                // Populate role-specific data
                switch (userRole.RoleId)
                {
                    case RoleConstants.Student: // Student
                        if (studentMap.TryGetValue(user.UserId, out var student))
                        {
                            response.Student = new StudentData
                            {
                                StudentId = student.StudentId,
                                YearLevel = student.YearLevel,
                                Section = student.Section,
                                Course = student.Course
                            };
                        }
                        break;

                    case RoleConstants.Teacher: // Teacher
                        if (teacherMap.TryGetValue(user.UserId, out var teacher))
                        {
                            response.Teacher = new TeacherData
                            {
                                Department = teacher.Department
                            };
                        }
                        break;
                }

                userResponses.Add(response);
            }

            return userResponses;
        }

        public async Task<PagedResult<UserResponseDto>> GetAllUsersPagedAsync(PaginationParams paginationParams)
        {
            // Get all users first (we need total count)
            var allUsers = await GetAllUsersAsync();
            
            var totalCount = allUsers.Count;
            var items = allUsers
                .Skip((paginationParams.PageNumber - 1) * paginationParams.PageSize)
                .Take(paginationParams.PageSize)
                .ToList();

            return new PagedResult<UserResponseDto>
            {
                Items = items,
                TotalCount = totalCount,
                PageNumber = paginationParams.PageNumber,
                PageSize = paginationParams.PageSize
            };
        }

        public async Task<UserResponseDto> UpdateUserAsync(int userId, UpdateUserDto updateUserDto)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                throw new InvalidOperationException($"User with ID {userId} not found");
            }

            // Get user role to determine which entity to update
            var userRoles = await _userRoleRepository.GetByUserIdAsync(userId);
            var userRole = userRoles.FirstOrDefault();
            
            if (userRole == null)
            {
                throw new InvalidOperationException($"User {userId} has no role assigned");
            }

            // Update User entity
            if (!string.IsNullOrEmpty(updateUserDto.Email))
                user.Email = updateUserDto.Email;
            
            if (!string.IsNullOrEmpty(updateUserDto.FullName))
                user.FullName = updateUserDto.FullName;
            
            if (!string.IsNullOrEmpty(updateUserDto.Status))
                user.Status = updateUserDto.Status;
            
            if (updateUserDto.ContactNumber != null)
                user.ContactNumber = updateUserDto.ContactNumber;
            
            if (updateUserDto.EmergencyContactNumber != null)
                user.EmergencyContactNumber = updateUserDto.EmergencyContactNumber;

            await _userRepository.UpdateAsync(user);

            // Update role-specific data
            switch (userRole.RoleId)
            {
                case RoleConstants.Student: // Student
                    var student = await _studentRepository.GetByUserIdAsync(userId);
                    if (student != null)
                    {
                        if (updateUserDto.YearLevel.HasValue)
                            student.YearLevel = updateUserDto.YearLevel;
                        
                        if (updateUserDto.Section != null)
                            student.Section = updateUserDto.Section;
                        
                        if (updateUserDto.Course != null)
                            student.Course = updateUserDto.Course;

                        await _studentRepository.UpdateAsync(student);
                    }
                    break;

                case RoleConstants.Teacher: // Teacher
                    var teacher = await _teacherRepository.GetByUserIdAsync(userId);
                    if (teacher != null && updateUserDto.Department != null)
                    {
                        teacher.Department = updateUserDto.Department;
                        await _teacherRepository.UpdateAsync(teacher);
                    }
                    break;
            }

            // Return updated user
            return await GetUserByIdAsync(userId) 
                ?? throw new InvalidOperationException("Failed to retrieve updated user");
        }

        public async Task<bool> DeleteUserAsync(int userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                throw new InvalidOperationException($"User with ID {userId} not found");
            }

            // Delete user (cascades to Student/Teacher/UserRole due to database constraints)
            return await _userRepository.DeleteAsync(userId);
        }

        public async Task<int> BulkDeleteAsync(List<int> userIds)
        {
            if (!userIds.Any()) return 0;

            // Admin-only operation - authorization should be enforced at controller level
            // Delete users (cascades to Student/Teacher/UserRole due to database constraints)
            return await _userRepository.BulkDeleteAsync(userIds);
        }

        public async Task ResetPasswordAsync(int userId, string newPassword)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                throw new InvalidOperationException($"User with ID {userId} not found");
            }

            user.PasswordHash = PasswordHasher.HashPassword(newPassword);
            await _userRepository.UpdateAsync(user);
        }

        public async Task<BulkUserImportResultDto> BulkCreateUsersFromExcelAsync(Stream fileStream, string fileName, int createdByUserId)
        {
            var result = new BulkUserImportResultDto();
            var errors = new List<UserImportErrorDto>();
            var createdUsers = new List<UserResponseDto>();

            // Create initial log entry
            var log = await _exportImportLogService.CreateLogAsync(new CreateExportImportLogDto
            {
                UserId = createdByUserId,
                Type = ExportImportConstants.Types.BulkImport,
                FileName = fileName
            });

            result.LogId = log.LogId;

            try
            {
                // Update log status to InProgress
                await _exportImportLogService.UpdateLogStatusAsync(log.LogId, new UpdateExportImportLogDto
                {
                    Status = ExportImportConstants.Statuses.InProgress
                }, createdByUserId);

                // Parse Excel file using ClosedXML
                using var workbook = new ClosedXML.Excel.XLWorkbook(fileStream);
                var worksheet = workbook.Worksheet(1); // First worksheet

                // Validate headers
                var headerRow = worksheet.Row(1);
                var expectedHeaders = new[] { "FullName", "Email", "Password", "Role", "StudentId", "YearLevel", "Section", "Course", "Department", "ContactNumber", "EmergencyContactNumber" };
                
                for (int i = 0; i < expectedHeaders.Length; i++)
                {
                    var cellValue = headerRow.Cell(i + 1).GetString().Trim();
                    if (!cellValue.Equals(expectedHeaders[i], StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException($"Invalid column header at position {i + 1}. Expected '{expectedHeaders[i]}', got '{cellValue}'");
                    }
                }

                // Process data rows
                var rows = worksheet.RowsUsed().Skip(1); // Skip header row
                int rowNumber = 1; // Start from 1 (header is 0)

                foreach (var row in rows)
                {
                    rowNumber++;
                    result.TotalRows++;

                    try
                    {
                        // Parse row data
                        var rowData = new UserImportRowDto
                        {
                            RowNumber = rowNumber,
                            FullName = row.Cell(1).GetString().Trim(),
                            Email = row.Cell(2).GetString().Trim(),
                            Password = row.Cell(3).GetString().Trim(),
                            Role = row.Cell(4).GetString().Trim(),
                            StudentId = row.Cell(5).GetString().Trim(),
                            YearLevel = row.Cell(6).TryGetValue(out int yearLevel) ? yearLevel : null,
                            Section = row.Cell(7).GetString().Trim(),
                            Course = row.Cell(8).GetString().Trim(),
                            Department = row.Cell(9).GetString().Trim(),
                            ContactNumber = row.Cell(10).GetString().Trim(),
                            EmergencyContactNumber = row.Cell(11).GetString().Trim()
                        };

                        // Validate required fields
                        var validationErrors = new List<string>();

                        if (string.IsNullOrWhiteSpace(rowData.FullName))
                            validationErrors.Add("FullName is required");

                        if (string.IsNullOrWhiteSpace(rowData.Email))
                            validationErrors.Add("Email is required");
                        else if (!IsValidEmail(rowData.Email))
                            validationErrors.Add("Invalid email format");

                        if (string.IsNullOrWhiteSpace(rowData.Password))
                            validationErrors.Add("Password is required");
                        else if (rowData.Password.Length < 6)
                            validationErrors.Add("Password must be at least 6 characters");

                        if (string.IsNullOrWhiteSpace(rowData.Role))
                            validationErrors.Add("Role is required");

                        // Convert role name to RoleId
                        int roleId;
                        if (!string.IsNullOrWhiteSpace(rowData.Role))
                        {
                            roleId = rowData.Role.ToLower() switch
                            {
                                "admin" => RoleConstants.Admin,
                                "teacher" => RoleConstants.Teacher,
                                "student" => RoleConstants.Student,
                                _ => 0
                            };

                            if (roleId == 0)
                                validationErrors.Add("Role must be 'Admin', 'Teacher', or 'Student'");

                            // Validate student-specific fields
                            if (roleId == RoleConstants.Student && string.IsNullOrWhiteSpace(rowData.StudentId))
                                validationErrors.Add("StudentId is required for Student role");
                        }
                        else
                        {
                            roleId = 0;
                        }

                        if (validationErrors.Any())
                        {
                            errors.Add(new UserImportErrorDto
                            {
                                RowNumber = rowNumber,
                                ErrorMessage = string.Join("; ", validationErrors),
                                FullName = rowData.FullName,
                                Email = rowData.Email,
                                Role = rowData.Role
                            });
                            result.FailureCount++;
                            continue;
                        }

                        // Create user DTO
                        var createUserDto = new CreateUserDto
                        {
                            FullName = rowData.FullName,
                            Email = rowData.Email,
                            Password = rowData.Password,
                            RoleId = roleId,
                            StudentId = string.IsNullOrWhiteSpace(rowData.StudentId) ? null : rowData.StudentId,
                            YearLevel = rowData.YearLevel,
                            Section = string.IsNullOrWhiteSpace(rowData.Section) ? null : rowData.Section,
                            Course = string.IsNullOrWhiteSpace(rowData.Course) ? null : rowData.Course,
                            Department = string.IsNullOrWhiteSpace(rowData.Department) ? null : rowData.Department,
                            ContactNumber = string.IsNullOrWhiteSpace(rowData.ContactNumber) ? null : rowData.ContactNumber,
                            EmergencyContactNumber = string.IsNullOrWhiteSpace(rowData.EmergencyContactNumber) ? null : rowData.EmergencyContactNumber,
                            CreatedBy = createdByUserId
                        };

                        // Create user (optimized for bulk import - skip GetUserByIdAsync)
                        var createdUser = await CreateUserForBulkImportAsync(createUserDto);
                        createdUsers.Add(createdUser);
                        result.SuccessCount++;
                    }
                    catch (Exception ex)
                    {
                        // Handle user creation errors (e.g., duplicate email)
                        var cellValues = new
                        {
                            FullName = row.Cell(1).GetString().Trim(),
                            Email = row.Cell(2).GetString().Trim(),
                            Role = row.Cell(4).GetString().Trim()
                        };

                        errors.Add(new UserImportErrorDto
                        {
                            RowNumber = rowNumber,
                            ErrorMessage = ex.Message,
                            FullName = cellValues.FullName,
                            Email = cellValues.Email,
                            Role = cellValues.Role
                        });
                        result.FailureCount++;
                    }
                }

                result.Errors = errors;
                result.CreatedUsers = createdUsers;

                // Update log with final status
                var finalStatus = result.FailureCount == 0 
                    ? ExportImportConstants.Statuses.Completed 
                    : (result.SuccessCount > 0 ? ExportImportConstants.Statuses.PartiallyCompleted : ExportImportConstants.Statuses.Failed);

                await _exportImportLogService.UpdateLogStatusAsync(log.LogId, new UpdateExportImportLogDto
                {
                    Status = finalStatus,
                    CompletedAt = DateTime.UtcNow,
                    ErrorMessage = result.FailureCount > 0 ? $"{result.FailureCount} row(s) failed to import" : null
                }, createdByUserId);

                return result;
            }
            catch (Exception ex)
            {
                // Update log with error status
                await _exportImportLogService.UpdateLogStatusAsync(log.LogId, new UpdateExportImportLogDto
                {
                    Status = ExportImportConstants.Statuses.Failed,
                    CompletedAt = DateTime.UtcNow,
                    ErrorMessage = ex.Message
                }, createdByUserId);

                throw;
            }
        }

        /// <summary>
        /// Optimized user creation for bulk import - skips GetUserByIdAsync to reduce DB calls
        /// </summary>
        private async Task<UserResponseDto> CreateUserForBulkImportAsync(CreateUserDto createUserDto)
        {
            // Validate role-specific requirements
            if (createUserDto.RoleId == RoleConstants.Student && string.IsNullOrEmpty(createUserDto.StudentId))
            {
                throw new ArgumentException("StudentId is required for students");
            }

            // Check if email already exists
            var existingUser = await _userRepository.GetByEmailAsync(createUserDto.Email);
            if (existingUser != null)
            {
                throw new InvalidOperationException($"User with email {createUserDto.Email} already exists");
            }

            // Create User entity
            var user = new User
            {
                Email = createUserDto.Email,
                PasswordHash = PasswordHasher.HashPassword(createUserDto.Password),
                FullName = createUserDto.FullName,
                Status = "Active",
                ContactNumber = createUserDto.ContactNumber ?? string.Empty,
                EmergencyContactNumber = createUserDto.EmergencyContactNumber ?? string.Empty,
                CreatedBy = createUserDto.CreatedBy,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Insert user
            var createdUser = await _userRepository.CreateAsync(user);

            try
            {
                // Create UserRole entry
                var userRole = new UserRole
                {
                    UserId = createdUser.UserId,
                    RoleId = createUserDto.RoleId
                };
                await _userRoleRepository.CreateAsync(userRole);

                // Create role-specific entity based on RoleId
                switch (createUserDto.RoleId)
                {
                    case RoleConstants.Student:
                        var student = new Student
                        {
                            UserId = createdUser.UserId,
                            StudentId = createUserDto.StudentId!,
                            YearLevel = createUserDto.YearLevel,
                            Section = createUserDto.Section,
                            Course = createUserDto.Course
                        };
                        await _studentRepository.CreateAsync(student);
                        break;

                    case RoleConstants.Teacher:
                        var teacher = new Teacher
                        {
                            UserId = createdUser.UserId,
                            Department = createUserDto.Department
                        };
                        await _teacherRepository.CreateAsync(teacher);
                        break;

                    case RoleConstants.Admin:
                        break;
                }

                // Return lightweight DTO without additional DB call
                var response = new UserResponseDto
                {
                    UserId = createdUser.UserId,
                    Email = createdUser.Email,
                    FullName = createdUser.FullName,
                    Status = createdUser.Status,
                    RoleId = createUserDto.RoleId,
                    RoleName = GetRoleName(createUserDto.RoleId),
                    ContactNumber = createdUser.ContactNumber,
                    EmergencyContactNumber = createdUser.EmergencyContactNumber,
                    CreatedAt = createdUser.CreatedAt,
                    UpdatedAt = createdUser.UpdatedAt,
                    CreatedBy = createdUser.CreatedBy
                };

                // Add role-specific data
                if (createUserDto.RoleId == RoleConstants.Student)
                {
                    response.Student = new StudentData
                    {
                        StudentId = createUserDto.StudentId!,
                        YearLevel = createUserDto.YearLevel,
                        Section = createUserDto.Section,
                        Course = createUserDto.Course
                    };
                }
                else if (createUserDto.RoleId == RoleConstants.Teacher)
                {
                    response.Teacher = new TeacherData
                    {
                        Department = createUserDto.Department
                    };
                }

                return response;
            }
            catch (Exception)
            {
                // Manual Rollback: Delete the partially created user
                await _userRepository.DeleteAsync(createdUser.UserId);
                throw;
            }
        }

        private bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }

        private string GetRoleName(int roleId)
        {
            return roleId switch
            {
                RoleConstants.Admin => "Admin",
                RoleConstants.Teacher => "Teacher",
                RoleConstants.Student => "Student",
                _ => "Unknown"
            };
        }

        // Archive operations
        public async Task<UserResponseDto> ArchiveUserAsync(int userId, int archivedBy)
        {
            var archivedUser = await _userRepository.ArchiveAsync(userId, archivedBy);
            if (archivedUser == null)
            {
                throw new InvalidOperationException($"User with ID {userId} not found");
            }

            if (archivedUser.Status != EntityStatusConstants.Archived)
            {
                throw new InvalidOperationException($"User with ID {userId} was already archived");
            }

            // Return lightweight DTO without additional DB calls - archive only updates status
            return new UserResponseDto
            {
                UserId = archivedUser.UserId,
                Email = archivedUser.Email,
                FullName = archivedUser.FullName,
                Status = archivedUser.Status,
                ContactNumber = archivedUser.ContactNumber,
                EmergencyContactNumber = archivedUser.EmergencyContactNumber,
                CreatedAt = archivedUser.CreatedAt,
                UpdatedAt = archivedUser.UpdatedAt,
                CreatedBy = archivedUser.CreatedBy,
                ArchivedAt = archivedUser.ArchivedAt,
                ArchivedBy = archivedUser.ArchivedBy
            };
        }

        public async Task<UserResponseDto> UnarchiveUserAsync(int userId)
        {
            var unarchivedUser = await _userRepository.UnarchiveAsync(userId);
            if (unarchivedUser == null)
            {
                throw new InvalidOperationException($"User with ID {userId} not found");
            }

            if (unarchivedUser.Status != EntityStatusConstants.Active)
            {
                throw new InvalidOperationException($"User with ID {userId} was not archived");
            }

            // Return lightweight DTO without additional DB calls - unarchive only updates status
            return new UserResponseDto
            {
                UserId = unarchivedUser.UserId,
                Email = unarchivedUser.Email,
                FullName = unarchivedUser.FullName,
                Status = unarchivedUser.Status,
                ContactNumber = unarchivedUser.ContactNumber,
                EmergencyContactNumber = unarchivedUser.EmergencyContactNumber,
                CreatedAt = unarchivedUser.CreatedAt,
                UpdatedAt = unarchivedUser.UpdatedAt,
                CreatedBy = unarchivedUser.CreatedBy,
                ArchivedAt = unarchivedUser.ArchivedAt,
                ArchivedBy = unarchivedUser.ArchivedBy
            };
        }

        public async Task<BulkArchiveResponseDto> BulkArchiveUsersAsync(List<int> userIds, int archivedBy)
        {
            var response = new BulkArchiveResponseDto
            {
                TotalRequested = userIds.Count
            };

            foreach (var userId in userIds)
            {
                try
                {
                    await ArchiveUserAsync(userId, archivedBy);
                    response.SuccessfulIds.Add(userId);
                    response.SuccessCount++;
                }
                catch (Exception ex)
                {
                    response.Errors.Add(new ArchiveErrorDto
                    {
                        Id = userId,
                        Error = ex.Message
                    });
                    response.FailureCount++;
                }
            }

            response.Message = $"Archived {response.SuccessCount} of {response.TotalRequested} users";
            return response;
        }

        public async Task<BulkArchiveResponseDto> BulkUnarchiveUsersAsync(List<int> userIds)
        {
            var response = new BulkArchiveResponseDto
            {
                TotalRequested = userIds.Count
            };

            foreach (var userId in userIds)
            {
                try
                {
                    await UnarchiveUserAsync(userId);
                    response.SuccessfulIds.Add(userId);
                    response.SuccessCount++;
                }
                catch (Exception ex)
                {
                    response.Errors.Add(new ArchiveErrorDto
                    {
                        Id = userId,
                        Error = ex.Message
                    });
                    response.FailureCount++;
                }
            }

            response.Message = $"Unarchived {response.SuccessCount} of {response.TotalRequested} users";
            return response;
        }

        public async Task<List<UserResponseDto>> GetArchivedUsersAsync()
        {
            var archivedUsers = await _userRepository.GetArchivedAsync();
            if (!archivedUsers.Any())
            {
                return new List<UserResponseDto>();
            }

            // Batch fetch all related data
            var userIds = archivedUsers.Select(u => u.UserId).ToList();
            var userRolesTask = _userRoleRepository.GetAllAsync();
            var studentsTask = _studentRepository.GetAllAsync();
            var teachersTask = _teacherRepository.GetAllAsync();

            await Task.WhenAll(userRolesTask, studentsTask, teachersTask);

            var allUserRoles = await userRolesTask;
            var allStudents = await studentsTask;
            var allTeachers = await teachersTask;

            // Create lookup dictionaries
            var userRoleMap = allUserRoles.Where(ur => userIds.Contains(ur.UserId))
                .GroupBy(ur => ur.UserId)
                .ToDictionary(g => g.Key, g => g.First());
            var studentMap = allStudents.Where(s => userIds.Contains(s.UserId))
                .ToDictionary(s => s.UserId);
            var teacherMap = allTeachers.Where(t => userIds.Contains(t.UserId))
                .ToDictionary(t => t.UserId);

            var userResponseDtos = new List<UserResponseDto>();

            foreach (var user in archivedUsers)
            {
                if (!userRoleMap.TryGetValue(user.UserId, out var userRole))
                {
                    continue; // Skip users without roles
                }

                var response = new UserResponseDto
                {
                    UserId = user.UserId,
                    Email = user.Email,
                    FullName = user.FullName,
                    Status = user.Status,
                    ContactNumber = user.ContactNumber,
                    EmergencyContactNumber = user.EmergencyContactNumber,
                    CreatedAt = user.CreatedAt,
                    UpdatedAt = user.UpdatedAt,
                    CreatedBy = user.CreatedBy,
                    ArchivedAt = user.ArchivedAt,
                    ArchivedBy = user.ArchivedBy,
                    RoleId = userRole.RoleId,
                    RoleName = GetRoleName(userRole.RoleId)
                };

                // Populate role-specific data
                switch (userRole.RoleId)
                {
                    case RoleConstants.Student:
                        if (studentMap.TryGetValue(user.UserId, out var student))
                        {
                            response.Student = new StudentData
                            {
                                StudentId = student.StudentId,
                                YearLevel = student.YearLevel,
                                Section = student.Section,
                                Course = student.Course
                            };
                        }
                        break;

                    case RoleConstants.Teacher:
                        if (teacherMap.TryGetValue(user.UserId, out var teacher))
                        {
                            response.Teacher = new TeacherData
                            {
                                Department = teacher.Department
                            };
                        }
                        break;
                }

                userResponseDtos.Add(response);
            }

            return userResponseDtos;
        }

        public async Task<PagedResult<UserResponseDto>> GetArchivedUsersPagedAsync(PaginationParams paginationParams)
        {
            var archivedUsers = await _userRepository.GetArchivedAsync();
            var totalCount = archivedUsers.Count;
            
            // Apply pagination at the data level
            var pagedUsers = archivedUsers
                .Skip((paginationParams.PageNumber - 1) * paginationParams.PageSize)
                .Take(paginationParams.PageSize)
                .ToList();

            if (!pagedUsers.Any())
            {
                return new PagedResult<UserResponseDto>
                {
                    Items = new List<UserResponseDto>(),
                    TotalCount = totalCount,
                    PageNumber = paginationParams.PageNumber,
                    PageSize = paginationParams.PageSize
                };
            }

            // Batch fetch all related data for this page only
            var userIds = pagedUsers.Select(u => u.UserId).ToList();
            var userRolesTask = _userRoleRepository.GetAllAsync();
            var studentsTask = _studentRepository.GetAllAsync();
            var teachersTask = _teacherRepository.GetAllAsync();

            await Task.WhenAll(userRolesTask, studentsTask, teachersTask);

            var allUserRoles = await userRolesTask;
            var allStudents = await studentsTask;
            var allTeachers = await teachersTask;

            // Create lookup dictionaries
            var userRoleMap = allUserRoles.Where(ur => userIds.Contains(ur.UserId))
                .GroupBy(ur => ur.UserId)
                .ToDictionary(g => g.Key, g => g.First());
            var studentMap = allStudents.Where(s => userIds.Contains(s.UserId))
                .ToDictionary(s => s.UserId);
            var teacherMap = allTeachers.Where(t => userIds.Contains(t.UserId))
                .ToDictionary(t => t.UserId);

            var userResponseDtos = new List<UserResponseDto>();

            foreach (var user in pagedUsers)
            {
                if (!userRoleMap.TryGetValue(user.UserId, out var userRole))
                {
                    continue; // Skip users without roles
                }

                var response = new UserResponseDto
                {
                    UserId = user.UserId,
                    Email = user.Email,
                    FullName = user.FullName,
                    Status = user.Status,
                    ContactNumber = user.ContactNumber,
                    EmergencyContactNumber = user.EmergencyContactNumber,
                    CreatedAt = user.CreatedAt,
                    UpdatedAt = user.UpdatedAt,
                    CreatedBy = user.CreatedBy,
                    ArchivedAt = user.ArchivedAt,
                    ArchivedBy = user.ArchivedBy,
                    RoleId = userRole.RoleId,
                    RoleName = GetRoleName(userRole.RoleId)
                };

                // Populate role-specific data
                switch (userRole.RoleId)
                {
                    case RoleConstants.Student:
                        if (studentMap.TryGetValue(user.UserId, out var student))
                        {
                            response.Student = new StudentData
                            {
                                StudentId = student.StudentId,
                                YearLevel = student.YearLevel,
                                Section = student.Section,
                                Course = student.Course
                            };
                        }
                        break;

                    case RoleConstants.Teacher:
                        if (teacherMap.TryGetValue(user.UserId, out var teacher))
                        {
                            response.Teacher = new TeacherData
                            {
                                Department = teacher.Department
                            };
                        }
                        break;
                }

                userResponseDtos.Add(response);
            }

            return new PagedResult<UserResponseDto>
            {
                Items = userResponseDtos,
                TotalCount = totalCount,
                PageNumber = paginationParams.PageNumber,
                PageSize = paginationParams.PageSize
            };
        }

        public async Task<ArchiveStatisticsDto> GetUserArchiveStatisticsAsync()
        {
            var allUsers = await _userRepository.GetAllIncludingArchivedAsync();
            
            var activeCount = allUsers.Count(u => u.Status == EntityStatusConstants.Active);
            var archivedCount = allUsers.Count(u => u.Status == EntityStatusConstants.Archived);
            var inactiveCount = allUsers.Count(u => u.Status == EntityStatusConstants.Inactive);
            var totalCount = allUsers.Count;

            return new ArchiveStatisticsDto
            {
                EntityType = "User",
                ActiveCount = activeCount,
                ArchivedCount = archivedCount,
                InactiveCount = inactiveCount,
                TotalCount = totalCount,
                ArchivePercentage = totalCount > 0 ? (decimal)archivedCount / totalCount * 100 : 0
            };
        }
    }
}
