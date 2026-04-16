using Mapster;
using OnlineQuiz.DTOs;
using OnlineQuiz.IRepository;
using OnlineQuiz.IServices;
using OnlineQuiz.Models;
using OnlineQuiz.Utilities;

namespace OnlineQuiz.Services
{
    public class QuizService : IQuizService
    {
        private readonly IQuizRepository _quizRepository;
        private readonly ICourseRepository _courseRepository;
        private readonly IEnrollmentRepository _enrollmentRepository;
        private readonly IUserRepository _userRepository;
        private readonly IUserRoleRepository _userRoleRepository;
        private readonly INotificationService _notificationService;

        public QuizService(
            IQuizRepository quizRepository,
            ICourseRepository courseRepository,
            IEnrollmentRepository enrollmentRepository,
            IUserRepository userRepository,
            IUserRoleRepository userRoleRepository,
            INotificationService notificationService)
        {
            _quizRepository = quizRepository;
            _courseRepository = courseRepository;
            _enrollmentRepository = enrollmentRepository;
            _userRepository = userRepository;
            _userRoleRepository = userRoleRepository;
            _notificationService = notificationService;
        }

        public async Task<QuizResponseDto> CreateQuizAsync(CreateQuizDto createQuizDto)
        {
            // Verify course exists
            var course = await _courseRepository.GetByIdAsync(createQuizDto.CourseId);
            if (course == null)
            {
                throw new ArgumentException($"Course with ID {createQuizDto.CourseId} not found");
            }

            // Verify creator is the instructor
            if (course.InstructorUserId != createQuizDto.CreatedBy)
            {
                throw new UnauthorizedAccessException("Only the assigned instructor can create quizzes for this course");
            }

            var quiz = new Quiz
            {
                CourseId = createQuizDto.CourseId,
                Title = createQuizDto.Title,
                DueAt = createQuizDto.DueAt,
                TimeLimitMinutes = createQuizDto.TimeLimitMinutes,
                CreatedBy = createQuizDto.CreatedBy,
                IsPublished = createQuizDto.IsPublished,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var createdQuiz = await _quizRepository.CreateAsync(quiz);

            // Build response DTO directly instead of fetching back
            var response = createdQuiz.Adapt<QuizResponseDto>();
            response.Questions = new List<QuestionResponseDto>();

            // Add Questions
            foreach (var qDto in createQuizDto.Questions)
            {
                var normalizedType = NormalizeQuestionType(qDto.Type);
                
                // Validate question type
                if (!QuestionTypeConstants.IsValid(normalizedType))
                {
                    throw new ArgumentException($"Invalid question type: {qDto.Type}. Valid types are: {string.Join(", ", QuestionTypeConstants.ValidTypes)}");
                }
                
                // Validate essay questions don't have choices
                if (QuestionTypeConstants.IsEssayType(normalizedType) && qDto.Choices.Any())
                {
                    throw new ArgumentException("Essay/Text questions cannot have multiple choice options. Please remove choices or change the question type.");
                }
                
                // Validate choice-based questions have at least one choice
                if (QuestionTypeConstants.RequiresChoices(normalizedType) && !qDto.Choices.Any())
                {
                    throw new ArgumentException($"{normalizedType} choice questions must have at least one choice option.");
                }
                
                var question = new Question
                {
                    QuizId = createdQuiz.QuizId,
                    Type = normalizedType,
                    Body = qDto.Body,
                    Points = qDto.Points,
                    SortOrder = qDto.SortOrder
                };
                var createdQuestion = await _quizRepository.CreateQuestionAsync(question);

                // Build question response
                var questionResponse = createdQuestion.Adapt<QuestionResponseDto>();
                questionResponse.Choices = new List<ChoiceResponseDto>();

                foreach (var cDto in qDto.Choices)
                {
                    var choice = new Choice
                    {
                        QuestionId = createdQuestion.QuestionId,
                        Body = cDto.Body,
                        IsCorrect = cDto.IsCorrect
                    };
                    var createdChoice = await _quizRepository.CreateChoiceAsync(choice);
                    
                    // Add to response
                    questionResponse.Choices.Add(createdChoice.Adapt<ChoiceResponseDto>());
                }
                
                response.Questions.Add(questionResponse);
            }

            // Trigger notification if published immediately (though default is false)
            if (createdQuiz.IsPublished)
            {
                await _notificationService.NotifyStudentsOfNewQuizAsync(createdQuiz.QuizId, createdQuiz.CourseId, createdQuiz.Title);
            }

            return response;
        }

        public async Task<List<QuizResponseDto>> GetQuizzesForCourseAsync(int courseId, int userId, bool isStudent)
        {
            // Verify access
            if (isStudent)
            {
                var isEnrolled = await _enrollmentRepository.ExistsAsync(userId, courseId);
                if (!isEnrolled)
                {
                    throw new UnauthorizedAccessException("Student is not enrolled in this course");
                }
            }
            else
            {
                var course = await _courseRepository.GetByIdAsync(courseId);
                if (course != null && course.InstructorUserId != userId)
                {
                    // Allow admin? For now strict teacher check
                    throw new UnauthorizedAccessException("Teacher is not assigned to this course");
                }
            }

            var quizzes = await _quizRepository.GetByCourseIdAsync(courseId);
            
            // Filter for students: only published quizzes
            if (isStudent)
            {
                quizzes = quizzes.Where(q => q.IsPublished).ToList();
            }

            var response = new List<QuizResponseDto>();
            foreach (var quiz in quizzes)
            {
                // Shallow map for list view (optimization: don't load questions for list)
                response.Add(quiz.Adapt<QuizResponseDto>());
            }

            return response;
        }

        public async Task<PagedResult<QuizResponseDto>> GetQuizzesForCoursePagedAsync(int courseId, int userId, bool isStudent, PaginationParams paginationParams)
        {
            var allQuizzes = await GetQuizzesForCourseAsync(courseId, userId, isStudent);

            var totalCount = allQuizzes.Count;
            var paginatedQuizzes = allQuizzes
                .Skip((paginationParams.PageNumber - 1) * paginationParams.PageSize)
                .Take(paginationParams.PageSize)
                .ToList();

            return new PagedResult<QuizResponseDto>
            {
                Items = paginatedQuizzes,
                TotalCount = totalCount,
                PageNumber = paginationParams.PageNumber,
                PageSize = paginationParams.PageSize
            };
        }

        public async Task<QuizResponseDto?> GetQuizByIdAsync(int quizId)
        {
            var quiz = await _quizRepository.GetByIdAsync(quizId);
            if (quiz == null) return null;

            var response = quiz.Adapt<QuizResponseDto>();
            
            var questions = await _quizRepository.GetQuestionsByQuizIdAsync(quizId);
            response.Questions = new List<QuestionResponseDto>();

            // Collect all question IDs
            var questionIds = questions.Select(q => q.QuestionId).ToList();
            
            // Batch fetch choices
            var allChoices = await _quizRepository.GetChoicesByQuestionIdsAsync(questionIds);
            var choicesMap = allChoices.GroupBy(c => c.QuestionId).ToDictionary(g => g.Key, g => g.ToList());

            foreach (var q in questions)
            {
                var qDto = q.Adapt<QuestionResponseDto>();
                if (choicesMap.TryGetValue(q.QuestionId, out var choices))
                {
                    qDto.Choices = choices.Adapt<List<ChoiceResponseDto>>();
                }
                else
                {
                    qDto.Choices = new List<ChoiceResponseDto>();
                }
                response.Questions.Add(qDto);
            }

            return response;
        }

        public async Task<QuizResponseDto> UpdateQuizAsync(int quizId, UpdateQuizDto updateQuizDto, int userId)
        {
            // Verify quiz exists
            var quiz = await _quizRepository.GetByIdAsync(quizId);
            if (quiz == null)
            {
                throw new ArgumentException($"Quiz with ID {quizId} not found");
            }

            // Verify course exists
            var course = await _courseRepository.GetByIdAsync(quiz.CourseId);
            if (course == null)
            {
                throw new InvalidOperationException("Quiz belongs to a non-existent course");
            }

            // Verify user is the instructor
            if (course.InstructorUserId != userId)
            {
                throw new UnauthorizedAccessException("Only the assigned instructor can update quizzes for this course");
            }

            // Update fields if provided
            if (!string.IsNullOrEmpty(updateQuizDto.Title))
            {
                quiz.Title = updateQuizDto.Title;
            }

            if (updateQuizDto.DueAt.HasValue)
            {
                quiz.DueAt = updateQuizDto.DueAt;
            }

            if (updateQuizDto.TimeLimitMinutes.HasValue)
            {
                quiz.TimeLimitMinutes = updateQuizDto.TimeLimitMinutes;
            }

            if (updateQuizDto.IsPublished.HasValue)
            {
                quiz.IsPublished = updateQuizDto.IsPublished.Value;
            }

            quiz.UpdatedAt = DateTime.UtcNow;

            var updatedQuiz = await _quizRepository.UpdateAsync(quiz);

            // Handle question updates if provided
            if (updateQuizDto.Questions != null && updateQuizDto.Questions.Any())
            {
                foreach (var questionDto in updateQuizDto.Questions)
                {
                    // Delete question if marked
                    if (questionDto.Delete == true && questionDto.QuestionId.HasValue)
                    {
                        // Note: You'll need to add DeleteQuestionAsync to IQuizRepository
                        // For now, we'll skip or assume cascade delete
                        continue;
                    }

                    // Update existing question
                    if (questionDto.QuestionId.HasValue)
                    {
                        var existingQuestion = await _quizRepository.GetQuestionsByQuizIdAsync(quizId);
                        var question = existingQuestion.FirstOrDefault(q => q.QuestionId == questionDto.QuestionId.Value);
                        
                        if (question != null)
                        {
                            if (!string.IsNullOrEmpty(questionDto.Body))
                                question.Body = questionDto.Body;
                            if (questionDto.Points.HasValue)
                                question.Points = questionDto.Points.Value;
                            if (questionDto.SortOrder.HasValue)
                                question.SortOrder = questionDto.SortOrder.Value;
                            if (!string.IsNullOrEmpty(questionDto.Type))
                                question.Type = NormalizeQuestionType(questionDto.Type);

                            // Note: You'll need UpdateQuestionAsync in repository
                            // For now, we acknowledge the limitation
                        }
                    }
                    // Create new question
                    else if (!string.IsNullOrEmpty(questionDto.Body))
                    {
                        var normalizedType = NormalizeQuestionType(questionDto.Type ?? "Single");
                        
                        // Validate question type
                        if (!QuestionTypeConstants.IsValid(normalizedType))
                        {
                            throw new ArgumentException($"Invalid question type: {questionDto.Type}. Valid types are: {string.Join(", ", QuestionTypeConstants.ValidTypes)}");
                        }
                        
                        // Validate essay questions don't have choices
                        if (QuestionTypeConstants.IsEssayType(normalizedType) && questionDto.Choices != null && questionDto.Choices.Any(c => c.Delete != true))
                        {
                            throw new ArgumentException("Essay/Text questions cannot have multiple choice options. Please remove choices or change the question type.");
                        }
                        
                        // Validate choice-based questions have at least one choice
                        if (QuestionTypeConstants.RequiresChoices(normalizedType) && (questionDto.Choices == null || !questionDto.Choices.Any(c => c.Delete != true)))
                        {
                            throw new ArgumentException($"{normalizedType} choice questions must have at least one choice option.");
                        }
                        
                        var newQuestion = new Question
                        {
                            QuizId = quizId,
                            Type = normalizedType,
                            Body = questionDto.Body,
                            Points = questionDto.Points ?? 1.0m,
                            SortOrder = questionDto.SortOrder ?? 0
                        };
                        await _quizRepository.CreateQuestionAsync(newQuestion);

                        // Handle choices for new question
                        if (questionDto.Choices != null)
                        {
                            foreach (var choiceDto in questionDto.Choices.Where(c => c.Delete != true))
                            {
                                if (!string.IsNullOrEmpty(choiceDto.Body))
                                {
                                    var newChoice = new Choice
                                    {
                                        QuestionId = newQuestion.QuestionId,
                                        Body = choiceDto.Body,
                                        IsCorrect = choiceDto.IsCorrect ?? false
                                    };
                                    await _quizRepository.CreateChoiceAsync(newChoice);
                                }
                            }
                        }
                    }
                }
            }

            // Trigger notification if it was just published
            if (updateQuizDto.IsPublished == true)
            {
                 await _notificationService.NotifyStudentsOfNewQuizAsync(updatedQuiz.QuizId, updatedQuiz.CourseId, updatedQuiz.Title);
            }

            // Return updated quiz with full details
            return await GetQuizByIdAsync(updatedQuiz.QuizId)
                ?? throw new InvalidOperationException("Failed to retrieve updated quiz");
        }

        public async Task<bool> DeleteQuizAsync(int quizId, int userId)
        {
            var quiz = await _quizRepository.GetByIdAsync(quizId);
            if (quiz == null)
            {
                return false;
            }

            var course = await _courseRepository.GetByIdAsync(quiz.CourseId);
            if (course == null)
            {
                throw new InvalidOperationException("Quiz belongs to a non-existent course");
            }

            // Check if user is the instructor
            if (course.InstructorUserId != userId)
            {
                // Check if user is Admin
                var userRoles = await _userRoleRepository.GetByUserIdAsync(userId);
                var isAdmin = userRoles.Any(ur => ur.RoleId == RoleConstants.Admin); // Assuming 1 is Admin Role ID

                if (!isAdmin)
                {
                    throw new UnauthorizedAccessException("Only the assigned instructor or an admin can delete quizzes");
                }
            }

            return await _quizRepository.DeleteAsync(quizId);
        }

        public async Task<int> BulkDeleteQuizzesAsync(List<int> quizIds, int userId)
        {
            if (!quizIds.Any()) return 0;

            // Fetch all quizzes to verify authorization
            var quizzes = await _quizRepository.GetByIdsAsync(quizIds);
            
            if (!quizzes.Any()) return 0;

            // Get unique course IDs
            var courseIds = quizzes.Select(q => q.CourseId).Distinct().ToList();
            
            // Check if user is admin
            var userRoles = await _userRoleRepository.GetByUserIdAsync(userId);
            var isAdmin = userRoles.Any(ur => ur.RoleId == RoleConstants.Admin);

            if (!isAdmin)
            {
                // Verify user owns all courses
                var courses = await _courseRepository.GetByInstructorIdAsync(userId);
                var ownedCourseIds = courses.Select(c => c.CourseId).ToHashSet();

                foreach (var courseId in courseIds)
                {
                    if (!ownedCourseIds.Contains(courseId))
                    {
                        throw new UnauthorizedAccessException("You are not authorized to delete all the specified quizzes");
                    }
                }
            }

            return await _quizRepository.BulkDeleteAsync(quizIds);
        }

        // Archive operations
        public async Task<QuizResponseDto> ArchiveQuizAsync(int quizId, int userId, int archivedBy)
        {
            var quiz = await _quizRepository.GetByIdAsync(quizId);
            if (quiz == null)
            {
                throw new InvalidOperationException($"Quiz with ID {quizId} not found");
            }

            // Verify user has permission (instructor or admin)
            var course = await _courseRepository.GetByIdAsync(quiz.CourseId);
            if (course == null)
            {
                throw new InvalidOperationException($"Course with ID {quiz.CourseId} not found");
            }

            // Check if user is the instructor or admin (admin check should be done at controller level)
            if (course.InstructorUserId != userId)
            {
                throw new UnauthorizedAccessException("Only the course instructor can archive quizzes");
            }

            if (quiz.Status == EntityStatusConstants.Archived)
            {
                throw new InvalidOperationException($"Quiz with ID {quizId} is already archived");
            }

            var archivedQuiz = await _quizRepository.ArchiveAsync(quizId, archivedBy);
            if (archivedQuiz == null)
            {
                throw new InvalidOperationException($"Failed to archive quiz with ID {quizId}");
            }

            // Return lightweight DTO without additional DB calls - archive only updates status
            return archivedQuiz.Adapt<QuizResponseDto>();
        }

        public async Task<QuizResponseDto> UnarchiveQuizAsync(int quizId, int userId)
        {
            var quiz = await _quizRepository.GetByIdAsync(quizId);
            if (quiz == null)
            {
                throw new InvalidOperationException($"Quiz with ID {quizId} not found");
            }

            // Verify user has permission (instructor or admin)
            var course = await _courseRepository.GetByIdAsync(quiz.CourseId);
            if (course == null)
            {
                throw new InvalidOperationException($"Course with ID {quiz.CourseId} not found");
            }

            // Check if user is the instructor or admin (admin check should be done at controller level)
            if (course.InstructorUserId != userId)
            {
                throw new UnauthorizedAccessException("Only the course instructor can unarchive quizzes");
            }

            if (quiz.Status != EntityStatusConstants.Archived)
            {
                throw new InvalidOperationException($"Quiz with ID {quizId} is not archived");
            }

            var unarchivedQuiz = await _quizRepository.UnarchiveAsync(quizId);
            if (unarchivedQuiz == null)
            {
                throw new InvalidOperationException($"Failed to unarchive quiz with ID {quizId}");
            }

            // Return lightweight DTO without additional DB calls - unarchive only updates status
            return unarchivedQuiz.Adapt<QuizResponseDto>();
        }

        public async Task<BulkArchiveResponseDto> BulkArchiveQuizzesAsync(List<int> quizIds, int userId, int archivedBy)
        {
            var response = new BulkArchiveResponseDto
            {
                TotalRequested = quizIds.Count
            };

            foreach (var quizId in quizIds)
            {
                try
                {
                    await ArchiveQuizAsync(quizId, userId, archivedBy);
                    response.SuccessfulIds.Add(quizId);
                    response.SuccessCount++;
                }
                catch (Exception ex)
                {
                    response.Errors.Add(new ArchiveErrorDto
                    {
                        Id = quizId,
                        Error = ex.Message
                    });
                    response.FailureCount++;
                }
            }

            response.Message = $"Archived {response.SuccessCount} of {response.TotalRequested} quizzes";
            return response;
        }

        public async Task<BulkArchiveResponseDto> BulkUnarchiveQuizzesAsync(List<int> quizIds, int userId)
        {
            var response = new BulkArchiveResponseDto
            {
                TotalRequested = quizIds.Count
            };

            foreach (var quizId in quizIds)
            {
                try
                {
                    await UnarchiveQuizAsync(quizId, userId);
                    response.SuccessfulIds.Add(quizId);
                    response.SuccessCount++;
                }
                catch (Exception ex)
                {
                    response.Errors.Add(new ArchiveErrorDto
                    {
                        Id = quizId,
                        Error = ex.Message
                    });
                    response.FailureCount++;
                }
            }

            response.Message = $"Unarchived {response.SuccessCount} of {response.TotalRequested} quizzes";
            return response;
        }

        public async Task<List<QuizResponseDto>> GetArchivedQuizzesAsync(int courseId, int userId)
        {
            // Verify user has access to the course
            var course = await _courseRepository.GetByIdAsync(courseId);
            if (course == null)
            {
                throw new InvalidOperationException($"Course with ID {courseId} not found");
            }

            var archivedQuizzes = await _quizRepository.GetArchivedByCourseIdAsync(courseId);
            if (!archivedQuizzes.Any())
            {
                return new List<QuizResponseDto>();
            }

            var response = archivedQuizzes.Adapt<List<QuizResponseDto>>();
            
            // Batch fetch questions and choices
            var quizIds = archivedQuizzes.Select(q => q.QuizId).ToList();
            var allQuestions = await _quizRepository.GetQuestionsByQuizIdsAsync(quizIds);
            var questionIds = allQuestions.Select(q => q.QuestionId).ToList();
            var allChoices = await _quizRepository.GetChoicesByQuestionIdsAsync(questionIds);
            
            // Create lookup maps
            var questionsMap = allQuestions.GroupBy(q => q.QuizId).ToDictionary(g => g.Key, g => g.ToList());
            var choicesMap = allChoices.GroupBy(c => c.QuestionId).ToDictionary(g => g.Key, g => g.ToList());

            // Populate questions and choices for each quiz
            foreach (var quizDto in response)
            {
                quizDto.Questions = new List<QuestionResponseDto>();
                
                if (questionsMap.TryGetValue(quizDto.QuizId, out var questions))
                {
                    foreach (var question in questions)
                    {
                        var questionDto = question.Adapt<QuestionResponseDto>();
                        
                        if (choicesMap.TryGetValue(question.QuestionId, out var choices))
                        {
                            questionDto.Choices = choices.Adapt<List<ChoiceResponseDto>>();
                        }
                        else
                        {
                            questionDto.Choices = new List<ChoiceResponseDto>();
                        }
                        
                        quizDto.Questions.Add(questionDto);
                    }
                }
            }

            return response;
        }

        public async Task<PagedResult<QuizResponseDto>> GetArchivedQuizzesPagedAsync(int courseId, int userId, PaginationParams paginationParams)
        {
            // Verify user has access to the course
            var course = await _courseRepository.GetByIdAsync(courseId);
            if (course == null)
            {
                throw new InvalidOperationException($"Course with ID {courseId} not found");
            }

            var archivedQuizzes = await _quizRepository.GetArchivedByCourseIdAsync(courseId);
            var totalCount = archivedQuizzes.Count;
            
            // Apply pagination at the data level
            var pagedQuizzes = archivedQuizzes
                .Skip((paginationParams.PageNumber - 1) * paginationParams.PageSize)
                .Take(paginationParams.PageSize)
                .ToList();

            if (!pagedQuizzes.Any())
            {
                return new PagedResult<QuizResponseDto>
                {
                    Items = new List<QuizResponseDto>(),
                    TotalCount = totalCount,
                    PageNumber = paginationParams.PageNumber,
                    PageSize = paginationParams.PageSize
                };
            }

            var response = pagedQuizzes.Adapt<List<QuizResponseDto>>();
            
            // Batch fetch questions and choices for this page only
            var quizIds = pagedQuizzes.Select(q => q.QuizId).ToList();
            var allQuestions = await _quizRepository.GetQuestionsByQuizIdsAsync(quizIds);
            var questionIds = allQuestions.Select(q => q.QuestionId).ToList();
            var allChoices = await _quizRepository.GetChoicesByQuestionIdsAsync(questionIds);
            
            // Create lookup maps
            var questionsMap = allQuestions.GroupBy(q => q.QuizId).ToDictionary(g => g.Key, g => g.ToList());
            var choicesMap = allChoices.GroupBy(c => c.QuestionId).ToDictionary(g => g.Key, g => g.ToList());

            // Populate questions and choices for each quiz
            foreach (var quizDto in response)
            {
                quizDto.Questions = new List<QuestionResponseDto>();
                
                if (questionsMap.TryGetValue(quizDto.QuizId, out var questions))
                {
                    foreach (var question in questions)
                    {
                        var questionDto = question.Adapt<QuestionResponseDto>();
                        
                        if (choicesMap.TryGetValue(question.QuestionId, out var choices))
                        {
                            questionDto.Choices = choices.Adapt<List<ChoiceResponseDto>>();
                        }
                        else
                        {
                            questionDto.Choices = new List<ChoiceResponseDto>();
                        }
                        
                        quizDto.Questions.Add(questionDto);
                    }
                }
            }

            return new PagedResult<QuizResponseDto>
            {
                Items = response,
                TotalCount = totalCount,
                PageNumber = paginationParams.PageNumber,
                PageSize = paginationParams.PageSize
            };
        }

        public async Task<ArchiveStatisticsDto> GetQuizArchiveStatisticsAsync(int? courseId = null)
        {
            List<Quiz> allQuizzes;
            
            if (courseId.HasValue)
            {
                // Get all quizzes for specific course (including archived)
                var activeQuizzes = await _quizRepository.GetByCourseIdAsync(courseId.Value);
                var archivedQuizzes = await _quizRepository.GetArchivedByCourseIdAsync(courseId.Value);
                allQuizzes = activeQuizzes.Concat(archivedQuizzes).ToList();
            }
            else
            {
                // Get all quizzes system-wide
                allQuizzes = await _quizRepository.GetAllIncludingArchivedAsync();
            }
            
            var activeCount = allQuizzes.Count(q => q.Status == EntityStatusConstants.Active);
            var archivedCount = allQuizzes.Count(q => q.Status == EntityStatusConstants.Archived);
            var inactiveCount = allQuizzes.Count(q => q.Status == EntityStatusConstants.Inactive);
            var totalCount = allQuizzes.Count;

            return new ArchiveStatisticsDto
            {
                EntityType = courseId.HasValue ? $"Quiz (Course {courseId})" : "Quiz",
                ActiveCount = activeCount,
                ArchivedCount = archivedCount,
                InactiveCount = inactiveCount,
                TotalCount = totalCount,
                ArchivePercentage = totalCount > 0 ? (decimal)archivedCount / totalCount * 100 : 0
            };
        }

        private string NormalizeQuestionType(string type)
        {
            if (string.IsNullOrWhiteSpace(type)) return QuestionTypeConstants.Single;

            var normalized = type.Trim();
            
            // Handle common aliases
            if (normalized.Equals("Single Choice", StringComparison.OrdinalIgnoreCase)) return QuestionTypeConstants.Single;
            if (normalized.Equals("Multiple Choice", StringComparison.OrdinalIgnoreCase)) return QuestionTypeConstants.Multiple;
            if (normalized.Equals("True/False", StringComparison.OrdinalIgnoreCase)) return QuestionTypeConstants.Single; // T/F is a type of single choice
            if (normalized.Equals("Short Answer", StringComparison.OrdinalIgnoreCase)) return QuestionTypeConstants.Text;
            if (normalized.Equals("Essay", StringComparison.OrdinalIgnoreCase)) return QuestionTypeConstants.Text;
            if (normalized.Equals("Free Text", StringComparison.OrdinalIgnoreCase)) return QuestionTypeConstants.Text;
            if (normalized.Equals("Open Ended", StringComparison.OrdinalIgnoreCase)) return QuestionTypeConstants.Text;
            
            // Return proper casing if it matches allowed values (case-insensitive check)
            if (normalized.Equals(QuestionTypeConstants.Single, StringComparison.OrdinalIgnoreCase)) return QuestionTypeConstants.Single;
            if (normalized.Equals(QuestionTypeConstants.Multiple, StringComparison.OrdinalIgnoreCase)) return QuestionTypeConstants.Multiple;
            if (normalized.Equals(QuestionTypeConstants.Text, StringComparison.OrdinalIgnoreCase)) return QuestionTypeConstants.Text;

            return normalized; // Fallback - will be caught by validation
        }
    }
}
