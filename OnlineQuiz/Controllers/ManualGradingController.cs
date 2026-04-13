using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OnlineQuiz.DTOs;
using OnlineQuiz.IServices;
using System.Security.Claims;

namespace OnlineQuiz.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ManualGradingController : ControllerBase
    {
        private readonly IManualGradingService _gradingService;
        private readonly IActivityLogService _activityLogService;

        public ManualGradingController(IManualGradingService gradingService, IActivityLogService activityLogService)
        {
            _gradingService = gradingService;
            _activityLogService = activityLogService;
        }

        /// <summary>
        /// Grade a single essay answer
        /// </summary>
        [HttpPost("answers/{attemptAnswerId}/grade")]
        public async Task<IActionResult> GradeEssayAnswer(int attemptAnswerId, [FromBody] GradeEssayAnswerDto gradeDto)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int teacherId))
                {
                    return Unauthorized(new { message = "Invalid user token" });
                }

                gradeDto.AttemptAnswerId = attemptAnswerId;
                var result = await _gradingService.GradeEssayAnswerAsync(attemptAnswerId, gradeDto, teacherId);

                // Log activity
                await _activityLogService.LogActivityAsync(new CreateActivityLogDto
                {
                    UserId = teacherId,
                    Action = "Grade Essay Answer",
                    Entity = "AttemptAnswer",
                    EntityId = attemptAnswerId,
                    Description = $"Graded essay answer. IsCorrect: {gradeDto.IsCorrect}"
                });

                return Ok(new { message = "Essay answer graded successfully", data = result });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new { message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while grading the essay answer", error = ex.Message });
            }
        }

        /// <summary>
        /// Grade multiple essay answers for an attempt
        /// </summary>
        [HttpPost("attempts/{attemptId}/grade-bulk")]
        public async Task<IActionResult> BulkGradeEssayAnswers(int attemptId, [FromBody] BulkGradeEssayDto bulkGradeDto)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int teacherId))
                {
                    return Unauthorized(new { message = "Invalid user token" });
                }

                bulkGradeDto.AttemptId = attemptId;
                var results = await _gradingService.BulkGradeEssayAnswersAsync(bulkGradeDto, teacherId);

                // Log activity
                await _activityLogService.LogActivityAsync(new CreateActivityLogDto
                {
                    UserId = teacherId,
                    Action = "Bulk Grade Essay Answers",
                    Entity = "Attempt",
                    EntityId = attemptId,
                    Description = $"Graded {bulkGradeDto.Grades.Count} essay answers"
                });

                return Ok(new { message = $"Successfully graded {results.Count} essay answers", data = results });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new { message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while bulk grading essay answers", error = ex.Message });
            }
        }

        /// <summary>
        /// Get all pending essay answers for a specific quiz
        /// </summary>
        [HttpGet("quiz/{quizId}/pending")]
        public async Task<IActionResult> GetPendingEssayAnswersForQuiz(int quizId)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int teacherId))
                {
                    return Unauthorized(new { message = "Invalid user token" });
                }

                var pendingAnswers = await _gradingService.GetPendingEssayAnswersForQuizAsync(quizId, teacherId);

                return Ok(new
                {
                    message = $"Found {pendingAnswers.Count} pending essay answers",
                    quizId = quizId,
                    pendingCount = pendingAnswers.Count,
                    data = pendingAnswers
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new { message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while fetching pending essay answers", error = ex.Message });
            }
        }

        /// <summary>
        /// Get all pending essay answers across all courses taught by the teacher
        /// </summary>
        [HttpGet("pending")]
        public async Task<IActionResult> GetAllPendingEssayAnswers()
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int teacherId))
                {
                    return Unauthorized(new { message = "Invalid user token" });
                }

                var pendingGrading = await _gradingService.GetAllPendingEssayAnswersForTeacherAsync(teacherId);

                var totalPending = pendingGrading.Sum(p => p.PendingCount);

                return Ok(new
                {
                    message = $"Found {totalPending} pending essay answers across {pendingGrading.Count} quizzes",
                    totalPending = totalPending,
                    quizCount = pendingGrading.Count,
                    data = pendingGrading
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while fetching pending essay answers", error = ex.Message });
            }
        }

        /// <summary>
        /// Recalculate attempt score after grading essay answers
        /// </summary>
        [HttpPost("attempts/{attemptId}/recalculate-score")]
        public async Task<IActionResult> RecalculateAttemptScore(int attemptId)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int teacherId))
                {
                    return Unauthorized(new { message = "Invalid user token" });
                }

                var result = await _gradingService.RecalculateAttemptScoreAsync(attemptId, teacherId);

                // Log activity
                await _activityLogService.LogActivityAsync(new CreateActivityLogDto
                {
                    UserId = teacherId,
                    Action = "Recalculate Attempt Score",
                    Entity = "Attempt",
                    EntityId = attemptId,
                    Description = $"Recalculated score: {result.Score}"
                });

                return Ok(new { message = "Attempt score recalculated successfully", data = result });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new { message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while recalculating attempt score", error = ex.Message });
            }
        }
    }
}
