using System.ComponentModel.DataAnnotations;

namespace OnlineQuiz.DTOs
{
    public class GradeEssayAnswerDto
    {
        [Required]
        public int AttemptAnswerId { get; set; }

        [Required]
        public bool IsCorrect { get; set; }

        /// <summary>
        /// Optional: Specific points awarded for partial credit.
        /// If not provided, IsCorrect determines full points (true) or zero (false).
        /// If provided, this value is used instead of binary grading.
        /// </summary>
        [Range(0, 1000, ErrorMessage = "Points awarded must be between 0 and 1000")]
        public decimal? PointsAwarded { get; set; }

        [MaxLength(1000)]
        public string? Feedback { get; set; }
    }

    public class BulkGradeEssayDto
    {
        [Required]
        public int AttemptId { get; set; }

        [Required]
        public List<GradeEssayAnswerDto> Grades { get; set; } = new();
    }

    public class EssayAnswerForGradingDto
    {
        public int AttemptAnswerId { get; set; }
        public int AttemptId { get; set; }
        public int QuestionId { get; set; }
        public string QuestionBody { get; set; } = string.Empty;
        public decimal QuestionPoints { get; set; }
        public string? StudentAnswer { get; set; }
        public bool? IsCorrect { get; set; }
        public decimal? PointsAwarded { get; set; }
        public string? Feedback { get; set; }
        public string StudentName { get; set; } = string.Empty;
        public DateTime AnsweredAt { get; set; }
    }

    public class PendingEssayGradingDto
    {
        public int QuizId { get; set; }
        public string QuizTitle { get; set; } = string.Empty;
        public int CourseId { get; set; }
        public string CourseName { get; set; } = string.Empty;
        public int PendingCount { get; set; }
        public List<EssayAnswerForGradingDto> PendingAnswers { get; set; } = new();
    }
}
