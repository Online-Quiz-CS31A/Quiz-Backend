using System.ComponentModel.DataAnnotations;

namespace OnlineQuiz.DTOs
{
    public class CreateQuizDto
    {
        [Required]
        public int CourseId { get; set; }

        [Required]
        public string Title { get; set; } = string.Empty;

        public DateTime? DueAt { get; set; }

        public int? TimeLimitMinutes { get; set; }

        public bool IsPublished { get; set; } = false;

        [Required]
        public int CreatedBy { get; set; }
        
        public List<CreateQuestionDto> Questions { get; set; } = new();
    }

    public class UpdateQuizDto
    {
        [StringLength(255, MinimumLength = 3)]
        public string? Title { get; set; }

        public DateTime? DueAt { get; set; }

        [Range(1, 1440, ErrorMessage = "Time limit must be between 1 minute and 24 hours")]
        public int? TimeLimitMinutes { get; set; }

        public bool? IsPublished { get; set; }
        public List<UpdateQuestionDto>? Questions { get; set; }
    }

    public class UpdateQuestionDto
    {
        public int? QuestionId { get; set; } // Null for new questions
        public string? Type { get; set; }
        public string? Body { get; set; }
        public decimal? Points { get; set; }
        public int? SortOrder { get; set; }
        public List<UpdateChoiceDto>? Choices { get; set; }
        public bool? Delete { get; set; } // Flag to mark for deletion
    }

    public class UpdateChoiceDto
    {
        public int? ChoiceId { get; set; } // Null for new choices
        public string? Body { get; set; }
        public bool? IsCorrect { get; set; }
        public bool? Delete { get; set; } // Flag to mark for deletion
    }

    public class QuizResponseDto
    {
        public int QuizId { get; set; }
        public int CourseId { get; set; }
        public string Title { get; set; } = string.Empty;
        public DateTime? DueAt { get; set; }
        public int? TimeLimitMinutes { get; set; }
        public bool IsPublished { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<QuestionResponseDto> Questions { get; set; } = new();
    }

    public class CreateQuestionDto
    {
        [Required]
        public int QuizId { get; set; }

        [Required]
        public string Type { get; set; } = "Single"; // Valid types: Single, Multiple, Text (Essay)

        [Required]
        public string Body { get; set; } = string.Empty;

        public decimal Points { get; set; } = 1.0m;
        
        public int SortOrder { get; set; }

        public List<CreateChoiceDto> Choices { get; set; } = new();
    }

    public class QuestionResponseDto
    {
        public int QuestionId { get; set; }
        public int QuizId { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public decimal Points { get; set; }
        public int SortOrder { get; set; }
        public List<ChoiceResponseDto> Choices { get; set; } = new();
    }

    public class CreateChoiceDto
    {
        public int QuestionId { get; set; }

        [Required]
        public string Body { get; set; } = string.Empty;

        public bool IsCorrect { get; set; } = false;
    }

    public class ChoiceResponseDto
    {
        public int ChoiceId { get; set; }
        public int QuestionId { get; set; }
        public string Body { get; set; } = string.Empty;
        public bool IsCorrect { get; set; }
    }
}
