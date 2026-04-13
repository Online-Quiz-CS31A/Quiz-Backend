using System.ComponentModel.DataAnnotations;
using Postgrest.Attributes;
using Postgrest.Models;

namespace OnlineQuiz.Models
{
    [Table("AttemptAnswer")]
    public class AttemptAnswer : BaseModel
    {
        [PrimaryKey("AttemptAnswerId")]
        [Column("AttemptAnswerId", ignoreOnInsert: true)]
        public int AttemptAnswerId { get; set; }

        [Required]
        [Column("AttemptId")]
        public int AttemptId { get; set; }

        [Required]
        [Column("QuestionId")]
        public int QuestionId { get; set; }

        [Column("ChoiceId")]
        public int? ChoiceId { get; set; }

        [Column("Free_Text")]
        public string? FreeText { get; set; }

        [Column("Is_Correct")]
        public bool? IsCorrect { get; set; }

        [Column("Points_Awarded")]
        public decimal? PointsAwarded { get; set; }

        [Column("Feedback")]
        public string? Feedback { get; set; }
    }
}
