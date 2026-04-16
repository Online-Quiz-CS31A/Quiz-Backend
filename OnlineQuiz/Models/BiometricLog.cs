using System.ComponentModel.DataAnnotations;
using Postgrest.Attributes;
using Postgrest.Models;

namespace OnlineQuiz.Models
{
    [Table("BiometricLog")]
    public class BiometricLog : BaseModel
    {
        [PrimaryKey("Id")]
        [Column("Id", ignoreOnInsert: true)]
        public int Id { get; set; }

        [Required]
        [Column("UserId")]
        public int UserId { get; set; }

        [Required]
        [MaxLength(50)]
        [Column("ActionType")]
        public string ActionType { get; set; } = string.Empty;

        [Required]
        [Column("Success")]
        public bool Success { get; set; }

        [Column("SlotId")]
        public int? SlotId { get; set; }

        [Column("ErrorMessage")]
        public string? ErrorMessage { get; set; }

        [MaxLength(45)]
        [Column("IpAddress")]
        public string? IpAddress { get; set; }

        [MaxLength(255)]
        [Column("UserAgent")]
        public string? UserAgent { get; set; }

        [Column("CreatedAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
