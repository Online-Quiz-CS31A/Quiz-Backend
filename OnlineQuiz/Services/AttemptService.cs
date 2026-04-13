using Mapster;
using OnlineQuiz.DTOs;
using OnlineQuiz.IRepository;
using OnlineQuiz.IServices;
using OnlineQuiz.Models;
using OnlineQuiz.Utilities;

namespace OnlineQuiz.Services
{
    public class AttemptService : IAttemptService
    {
        private readonly IAttemptRepository _attemptRepository;
        private readonly IQuizRepository _quizRepository;
        private readonly ICourseRepository _courseRepository;
        private readonly IEnrollmentRepository _enrollmentRepository;
        private readonly IUserRepository _userRepository;
        private readonly IUserRoleRepository _userRoleRepository;
        private readonly IAttemptAnswerRepository _answerRepository;

        public AttemptService(
            IAttemptRepository attemptRepository,
            IQuizRepository quizRepository,
            ICourseRepository courseRepository,
            IEnrollmentRepository enrollmentRepository,
            IUserRepository userRepository,
            IUserRoleRepository userRoleRepository,
            IAttemptAnswerRepository answerRepository)
        {
            _attemptRepository = attemptRepository;
            _quizRepository = quizRepository;
            _courseRepository = courseRepository;
            _enrollmentRepository = enrollmentRepository;
            _userRepository = userRepository;
            _userRoleRepository = userRoleRepository;
            _answerRepository = answerRepository;
        }

        public async Task<AttemptResponseDto> StartAttemptAsync(StartAttemptDto startAttemptDto)
        {
            var quiz = await _quizRepository.GetByIdAsync(startAttemptDto.QuizId);
            if (quiz == null)
            {
                throw new ArgumentException($"Quiz with ID {startAttemptDto.QuizId} not found");
            }

            if (!quiz.IsPublished)
            {
                throw new InvalidOperationException("Cannot start attempt on unpublished quiz");
            }

            // Check if student is enrolled
            var isEnrolled = await _enrollmentRepository.ExistsAsync(startAttemptDto.StudentId, quiz.CourseId);
            if (!isEnrolled)
            {
                throw new UnauthorizedAccessException("Student is not enrolled in this course");
            }

            var attempt = new Attempt
            {
                QuizId = startAttemptDto.QuizId,
                UserId = startAttemptDto.StudentId,
                StartedAt = DateTime.UtcNow
            };

            var createdAttempt = await _attemptRepository.CreateAsync(attempt);
            var student = await _userRepository.GetByIdAsync(startAttemptDto.StudentId);

            return new AttemptResponseDto
            {
                AttemptId = createdAttempt.AttemptId,
                UserId = createdAttempt.UserId,
                StudentName = student?.FullName,
                QuizId = createdAttempt.QuizId,
                QuizTitle = quiz.Title,
                StartedAt = createdAttempt.StartedAt,
                SubmittedAt = createdAttempt.SubmittedAt,
                Score = createdAttempt.Score,
                TimeSpentSeconds = createdAttempt.TimeSpentSeconds
            };
        }

        public async Task<AttemptResponseDto?> GetAttemptByIdAsync(int attemptId, int userId)
        {
            var attempt = await _attemptRepository.GetByIdAsync(attemptId);
            if (attempt == null)
            {
                return null;
            }

            // Authorization: User must own the attempt OR be the course instructor
            if (attempt.UserId != userId)
            {
                var quiz = await _quizRepository.GetByIdAsync(attempt.QuizId);
                if (quiz != null)
                {
                    var course = await _courseRepository.GetByIdAsync(quiz.CourseId);
                    if (course == null || course.InstructorUserId != userId)
                    {
                        throw new UnauthorizedAccessException("You do not have permission to view this attempt");
                    }
                }
                else
                {
                    throw new UnauthorizedAccessException("You do not have permission to view this attempt");
                }
            }

            var quizData = await _quizRepository.GetByIdAsync(attempt.QuizId);
            var student = await _userRepository.GetByIdAsync(attempt.UserId);

            return new AttemptResponseDto
            {
                AttemptId = attempt.AttemptId,
                UserId = attempt.UserId,
                StudentName = student?.FullName,
                QuizId = attempt.QuizId,
                QuizTitle = quizData?.Title,
                StartedAt = attempt.StartedAt,
                SubmittedAt = attempt.SubmittedAt,
                Score = attempt.Score,
                TimeSpentSeconds = attempt.TimeSpentSeconds
            };
        }

        public async Task<List<AttemptResponseDto>> GetAttemptsForQuizAsync(int quizId, int teacherId)
        {
            var quiz = await _quizRepository.GetByIdAsync(quizId);
            if (quiz == null)
            {
                throw new ArgumentException($"Quiz with ID {quizId} not found");
            }

            var course = await _courseRepository.GetByIdAsync(quiz.CourseId);
            if (course == null || course.InstructorUserId != teacherId)
            {
                throw new UnauthorizedAccessException("Only the course instructor can view quiz attempts");
            }

            var attempts = await _attemptRepository.GetByQuizIdAsync(quizId);
            var response = new List<AttemptResponseDto>();

            // Collect user IDs
            var userIds = attempts.Select(a => a.UserId).Distinct().ToList();
            
            // Batch fetch students
            var students = await _userRepository.GetByIdsAsync(userIds);
            var studentMap = students.ToDictionary(u => u.UserId, u => u.FullName);

            foreach (var attempt in attempts)
            {
                string? studentName = null;
                if (studentMap.TryGetValue(attempt.UserId, out var name))
                {
                    studentName = name;
                }

                response.Add(new AttemptResponseDto
                {
                    AttemptId = attempt.AttemptId,
                    UserId = attempt.UserId,
                    StudentName = studentName,
                    QuizId = attempt.QuizId,
                    QuizTitle = quiz.Title,
                    StartedAt = attempt.StartedAt,
                    SubmittedAt = attempt.SubmittedAt,
                    Score = attempt.Score,
                    TimeSpentSeconds = attempt.TimeSpentSeconds
                });
            }

            return response;
        }

        public async Task<List<AttemptResponseDto>> GetAttemptsForStudentAsync(int studentId)
        {
            var attempts = await _attemptRepository.GetByStudentIdAsync(studentId);
            var response = new List<AttemptResponseDto>();

            if (!attempts.Any()) return response;

            // Collect IDs
            var quizIds = attempts.Select(a => a.QuizId).Distinct().ToList();
            var userIds = attempts.Select(a => a.UserId).Distinct().ToList();

            // Batch fetch
            var quizzes = await _quizRepository.GetByIdsAsync(quizIds);
            var students = await _userRepository.GetByIdsAsync(userIds);

            var quizMap = quizzes.ToDictionary(q => q.QuizId, q => q.Title);
            var studentMap = students.ToDictionary(u => u.UserId, u => u.FullName);

            foreach (var attempt in attempts)
            {
                string? quizTitle = null;
                if (quizMap.TryGetValue(attempt.QuizId, out var title))
                {
                    quizTitle = title;
                }

                string? studentName = null;
                if (studentMap.TryGetValue(attempt.UserId, out var name))
                {
                    studentName = name;
                }

                response.Add(new AttemptResponseDto
                {
                    AttemptId = attempt.AttemptId,
                    UserId = attempt.UserId,
                    StudentName = studentName,
                    QuizId = attempt.QuizId,
                    QuizTitle = quizTitle,
                    StartedAt = attempt.StartedAt,
                    SubmittedAt = attempt.SubmittedAt,
                    Score = attempt.Score,
                    TimeSpentSeconds = attempt.TimeSpentSeconds
                });
            }

            return response;
        }

        public async Task<PagedResult<AttemptResponseDto>> GetAttemptsForQuizPagedAsync(int quizId, int teacherId, PaginationParams paginationParams)
        {
            var allAttempts = await GetAttemptsForQuizAsync(quizId, teacherId);

            var totalCount = allAttempts.Count;
            var paginatedAttempts = allAttempts
                .Skip((paginationParams.PageNumber - 1) * paginationParams.PageSize)
                .Take(paginationParams.PageSize)
                .ToList();

            return new PagedResult<AttemptResponseDto>
            {
                Items = paginatedAttempts,
                TotalCount = totalCount,
                PageNumber = paginationParams.PageNumber,
                PageSize = paginationParams.PageSize
            };
        }

        public async Task<PagedResult<AttemptResponseDto>> GetAttemptsForStudentPagedAsync(int studentId, PaginationParams paginationParams)
        {
            var allAttempts = await GetAttemptsForStudentAsync(studentId);

            var totalCount = allAttempts.Count;
            var paginatedAttempts = allAttempts
                .Skip((paginationParams.PageNumber - 1) * paginationParams.PageSize)
                .Take(paginationParams.PageSize)
                .ToList();

            return new PagedResult<AttemptResponseDto>
            {
                Items = paginatedAttempts,
                TotalCount = totalCount,
                PageNumber = paginationParams.PageNumber,
                PageSize = paginationParams.PageSize
            };
        }

        public async Task<AttemptResponseDto> SubmitAttemptAsync(int attemptId, SubmitAttemptDto submitAttemptDto, int studentId)
        {
            var attempt = await _attemptRepository.GetByIdAsync(attemptId);
            if (attempt == null)
            {
                throw new ArgumentException($"Attempt with ID {attemptId} not found");
            }

            if (attempt.UserId != studentId)
            {
                throw new UnauthorizedAccessException("You can only submit your own attempts");
            }

            if (attempt.SubmittedAt != null)
            {
                throw new InvalidOperationException("Attempt has already been submitted");
            }

            // Check time limit
            var quiz = await _quizRepository.GetByIdAsync(attempt.QuizId);
            if (quiz != null && quiz.TimeLimitMinutes.HasValue && quiz.TimeLimitMinutes.Value > 0)
            {
                var deadline = attempt.StartedAt.AddMinutes(quiz.TimeLimitMinutes.Value);
                // Add 2 minutes buffer for latency/clock skew
                if (DateTime.UtcNow > deadline.AddMinutes(2))
                {
                    throw new InvalidOperationException($"Time limit exceeded. The quiz should have been submitted by {deadline}");
                }
            }

            // Calculate score server-side based on correct answers
            var calculatedScore = await CalculateScoreAsync(attemptId, attempt.QuizId);

            attempt.SubmittedAt = DateTime.UtcNow;
            attempt.Score = calculatedScore;
            attempt.TimeSpentSeconds = submitAttemptDto.TimeSpentSeconds;

            var updatedAttempt = await _attemptRepository.UpdateAsync(attempt);

            var student = await _userRepository.GetByIdAsync(updatedAttempt.UserId);

            return new AttemptResponseDto
            {
                AttemptId = updatedAttempt.AttemptId,
                UserId = updatedAttempt.UserId,
                StudentName = student?.FullName,
                QuizId = updatedAttempt.QuizId,
                QuizTitle = quiz?.Title,
                StartedAt = updatedAttempt.StartedAt,
                SubmittedAt = updatedAttempt.SubmittedAt,
                Score = updatedAttempt.Score,
                TimeSpentSeconds = updatedAttempt.TimeSpentSeconds
            };
        }

        public async Task<bool> DeleteAttemptAsync(int attemptId, int userId)
        {
            var attempt = await _attemptRepository.GetByIdAsync(attemptId);
            if (attempt == null)
            {
                throw new ArgumentException($"Attempt with ID {attemptId} not found");
            }

            // Check permissions
            bool isOwner = attempt.UserId == userId;
            bool isInstructor = false;

            var quiz = await _quizRepository.GetByIdAsync(attempt.QuizId);
            if (quiz != null)
            {
                var course = await _courseRepository.GetByIdAsync(quiz.CourseId);
                if (course != null && course.InstructorUserId == userId)
                {
                    isInstructor = true;
                }
            }

            if (!isOwner && !isInstructor)
            {
                throw new UnauthorizedAccessException("You do not have permission to delete this attempt");
            }

            // If student (owner) and not instructor, can only delete if NOT submitted
            if (isOwner && !isInstructor && attempt.SubmittedAt != null)
            {
                throw new InvalidOperationException("Cannot delete submitted attempts");
            }

            return await _attemptRepository.DeleteAsync(attemptId);
        }

        public async Task<int> BulkDeleteAttemptsAsync(List<int> attemptIds, int userId)
        {
            if (!attemptIds.Any()) return 0;

            // Fetch all attempts to verify authorization
            var attempts = new List<Attempt>();
            foreach (var id in attemptIds)
            {
                var attempt = await _attemptRepository.GetByIdAsync(id);
                if (attempt != null)
                {
                    attempts.Add(attempt);
                }
            }

            if (!attempts.Any()) return 0;

            // Get unique quiz IDs and course IDs
            var quizIds = attempts.Select(a => a.QuizId).Distinct().ToList();
            var quizzes = await _quizRepository.GetByIdsAsync(quizIds);
            var courseIds = quizzes.Select(q => q.CourseId).Distinct().ToList();
            var courses = new List<Course>();
            foreach (var courseId in courseIds)
            {
                var course = await _courseRepository.GetByIdAsync(courseId);
                if (course != null)
                {
                    courses.Add(course);
                }
            }

            // Check if user is a teacher for any of these courses
            bool isTeacher = courses.Any(c => c.InstructorUserId == userId);

            // Validate each attempt
            foreach (var attempt in attempts)
            {
                bool isOwner = attempt.UserId == userId;
                
                // Find the course for this attempt
                var quiz = quizzes.FirstOrDefault(q => q.QuizId == attempt.QuizId);
                bool isInstructor = quiz != null && courses.Any(c => c.CourseId == quiz.CourseId && c.InstructorUserId == userId);
                
                if (!isOwner && !isInstructor)
                {
                    throw new UnauthorizedAccessException($"You do not have permission to delete attempt {attempt.AttemptId}");
                }
                
                // If student (owner) and not instructor, can only delete if NOT submitted
                if (isOwner && !isInstructor && attempt.SubmittedAt != null)
                {
                    throw new InvalidOperationException($"Cannot delete submitted attempt {attempt.AttemptId}");
                }
            }

            // All checks passed, proceed with bulk delete
            return await _attemptRepository.BulkDeleteAsync(attemptIds);
        }

        public async Task<(byte[] FileContent, string FileName)> ExportQuizScoresToExcelAsync(int userId, int? quizId, int? courseId)
        {
            // Verify user authorization
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                throw new ArgumentException($"User with ID {userId} not found");
            }

            // Check authorization based on filters
            if (quizId.HasValue)
            {
                var quiz = await _quizRepository.GetByIdAsync(quizId.Value);
                if (quiz == null)
                {
                    throw new ArgumentException($"Quiz with ID {quizId} not found");
                }

                var course = await _courseRepository.GetByIdAsync(quiz.CourseId);
                if (course != null && course.InstructorUserId != userId)
                {
                    // Not the instructor, must be admin
                    var userRoles = await GetUserRolesAsync(userId);
                    if (!userRoles.Contains(Utilities.RoleConstants.Admin))
                    {
                        throw new UnauthorizedAccessException("Only the course instructor or admin can export this quiz's scores");
                    }
                }
            }
            else if (courseId.HasValue)
            {
                var course = await _courseRepository.GetByIdAsync(courseId.Value);
                if (course == null)
                {
                    throw new ArgumentException($"Course with ID {courseId} not found");
                }

                if (course.InstructorUserId != userId)
                {
                    // Not the instructor, must be admin
                    var userRoles = await GetUserRolesAsync(userId);
                    if (!userRoles.Contains(Utilities.RoleConstants.Admin))
                    {
                        throw new UnauthorizedAccessException("Only the course instructor or admin can export this course's scores");
                    }
                }
            }
            else
            {
                // No filter - admin only
                var userRoles = await GetUserRolesAsync(userId);
                if (!userRoles.Contains(Utilities.RoleConstants.Admin))
                {
                    throw new UnauthorizedAccessException("Only administrators can export all scores");
                }
            }

            // Fetch attempts with filters
            var attempts = await _attemptRepository.GetAllAttemptsForExportAsync(quizId, courseId);

            if (!attempts.Any())
            {
                throw new InvalidOperationException("No submitted attempts found for the specified filters");
            }

            // Collect related data
            var quizIds = attempts.Select(a => a.QuizId).Distinct().ToList();
            var userIds = attempts.Select(a => a.UserId).Distinct().ToList();

            var quizzes = await _quizRepository.GetByIdsAsync(quizIds);
            var users = await _userRepository.GetByIdsAsync(userIds);
            
            var courseIds = quizzes.Select(q => q.CourseId).Distinct().ToList();
            var courses = new Dictionary<int, Course>();
            foreach (var cId in courseIds)
            {
                var c = await _courseRepository.GetByIdAsync(cId);
                if (c != null) courses[cId] = c;
            }

            // Prepare data for export
            var exportData = new List<ScoreExportDataDto>();
            var quizMap = quizzes.ToDictionary(q => q.QuizId);
            var userMap = users.ToDictionary(u => u.UserId);

            foreach (var attempt in attempts)
            {
                var quiz = quizMap.GetValueOrDefault(attempt.QuizId);
                var student = userMap.GetValueOrDefault(attempt.UserId);
                var course = quiz != null ? courses.GetValueOrDefault(quiz.CourseId) : null;

                // Get student ID from Student table
                var studentRecord = await _userRepository.GetByIdAsync(attempt.UserId);
                string studentId = studentRecord?.Email ?? "N/A";

                exportData.Add(new ScoreExportDataDto
                {
                    StudentId = studentId,
                    StudentName = student?.FullName ?? "Unknown",
                    StudentEmail = student?.Email ?? "N/A",
                    QuizTitle = quiz?.Title ?? "Unknown",
                    CourseName = course?.Name ?? "Unknown",
                    Score = attempt.Score,
                    StartedAt = attempt.StartedAt,
                    SubmittedAt = attempt.SubmittedAt,
                    TimeSpentMinutes = attempt.TimeSpentSeconds.HasValue ? attempt.TimeSpentSeconds.Value / 60 : null
                });
            }

            // Generate Excel file
            using var workbook = new ClosedXML.Excel.XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Quiz Scores");

            // Add headers
            worksheet.Cell(1, 1).Value = "Student ID";
            worksheet.Cell(1, 2).Value = "Student Name";
            worksheet.Cell(1, 3).Value = "Student Email";
            worksheet.Cell(1, 4).Value = "Quiz Title";
            worksheet.Cell(1, 5).Value = "Course Name";
            worksheet.Cell(1, 6).Value = "Score";
            worksheet.Cell(1, 7).Value = "Started At";
            worksheet.Cell(1, 8).Value = "Submitted At";
            worksheet.Cell(1, 9).Value = "Time Spent (Minutes)";

            // Style headers
            var headerRow = worksheet.Row(1);
            headerRow.Style.Font.Bold = true;
            headerRow.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightGray;

            // Add data rows
            int row = 2;
            foreach (var data in exportData)
            {
                worksheet.Cell(row, 1).Value = data.StudentId;
                worksheet.Cell(row, 2).Value = data.StudentName;
                worksheet.Cell(row, 3).Value = data.StudentEmail;
                worksheet.Cell(row, 4).Value = data.QuizTitle;
                worksheet.Cell(row, 5).Value = data.CourseName;
                worksheet.Cell(row, 6).Value = (double)data.Score;
                worksheet.Cell(row, 7).Value = data.StartedAt.ToString("yyyy-MM-dd HH:mm:ss");
                worksheet.Cell(row, 8).Value = data.SubmittedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "N/A";
                worksheet.Cell(row, 9).Value = data.TimeSpentMinutes?.ToString() ?? "N/A";
                row++;
            }

            // Auto-fit columns
            worksheet.Columns().AdjustToContents();

            // Save to memory stream
            using var stream = new System.IO.MemoryStream();
            workbook.SaveAs(stream);
            var fileContent = stream.ToArray();

            // Generate filename
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            string fileName;
            if (quizId.HasValue)
            {
                var quiz = quizMap.GetValueOrDefault(quizId.Value);
                fileName = $"Quiz_Scores_{quiz?.Title ?? "Unknown"}_{timestamp}.xlsx";
            }
            else if (courseId.HasValue)
            {
                var course = courses.GetValueOrDefault(courseId.Value);
                fileName = $"Course_Scores_{course?.Name ?? "Unknown"}_{timestamp}.xlsx";
            }
            else
            {
                fileName = $"All_Quiz_Scores_{timestamp}.xlsx";
            }

            // Sanitize filename
            fileName = string.Join("_", fileName.Split(System.IO.Path.GetInvalidFileNameChars()));

            return (fileContent, fileName);
        }

        private async Task<List<int>> GetUserRolesAsync(int userId)
        {
            var userRoles = await _userRoleRepository.GetByUserIdAsync(userId);
            return userRoles.Select(ur => ur.RoleId).ToList();
        }

        /// <summary>
        /// Calculate score by comparing student answers against correct choices
        /// </summary>
        private async Task<decimal> CalculateScoreAsync(int attemptId, int quizId)
        {
            // 1. Fetch all answers for this attempt
            var answers = await _answerRepository.GetByAttemptIdAsync(attemptId);
            
            if (!answers.Any())
            {
                return 0; // No answers submitted
            }

            // 2. Fetch all questions for this quiz
            var questions = await _quizRepository.GetQuestionsByQuizIdAsync(quizId);
            
            if (!questions.Any())
            {
                return 0; // No questions in quiz
            }

            // 3. Fetch all choices for these questions
            var questionIds = questions.Select(q => q.QuestionId).ToList();
            var allChoices = await _quizRepository.GetChoicesByQuestionIdsAsync(questionIds);
            
            // 4. Group choices by question for fast lookup
            var choicesMap = allChoices.GroupBy(c => c.QuestionId)
                .ToDictionary(g => g.Key, g => g.ToList());
            
            // 5. Create question lookup
            var questionMap = questions.ToDictionary(q => q.QuestionId);

            decimal earnedPoints = 0;
            decimal totalPoints = questions.Sum(q => q.Points);

            // 6. Grade each answer
            foreach (var answer in answers)
            {
                if (!questionMap.TryGetValue(answer.QuestionId, out var question))
                {
                    continue; // Question not found, skip
                }

                bool isCorrect = false;

                // Grade based on question type
                if (question.Type == QuestionTypeConstants.Single || question.Type == QuestionTypeConstants.Multiple)
                {
                    // Check if student's choice is marked as correct
                    if (answer.ChoiceId.HasValue && choicesMap.TryGetValue(question.QuestionId, out var choices))
                    {
                        var selectedChoice = choices.FirstOrDefault(c => c.ChoiceId == answer.ChoiceId.Value);
                        isCorrect = selectedChoice?.IsCorrect ?? false;
                    }
                }
                else if (QuestionTypeConstants.IsEssayType(question.Type))
                {
                    // Essay questions require manual grading - leave IsCorrect as null
                    // Don't update IsCorrect here, it will be set by the teacher
                    continue; // Skip updating this answer, teacher will grade it manually
                }
                // Text questions default to false (require manual grading)
                // Can be extended with keyword matching or other logic

                // 7. Update IsCorrect in database
                answer.IsCorrect = isCorrect;
                await _answerRepository.UpdateAsync(answer);

                // 8. Add points if correct
                if (isCorrect)
                {
                    earnedPoints += question.Points;
                }
            }

            // 9. Calculate percentage score (0-100)
            if (totalPoints == 0)
            {
                return 0;
            }

            return Math.Round((earnedPoints / totalPoints) * 100, 2);
        }
    }
}
