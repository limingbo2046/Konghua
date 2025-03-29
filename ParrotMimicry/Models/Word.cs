using SQLite;

namespace ParrotMimicry.Models
{
    public class Word
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed]
        public string Text { get; set; } = string.Empty;

        public string? IPAPhonetic { get; set; }

        public string? KKPhonetic { get; set; }

        public string? Definition { get; set; }

        public int Familiarity { get; set; } = 0;

        public DateTime? LastReviewTime { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}