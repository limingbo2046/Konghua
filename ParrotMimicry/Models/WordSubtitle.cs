using SQLite;

namespace ParrotMimicry.Models
{
    public class WordSubtitle
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed]
        public int WordId { get; set; }

        [Indexed]
        public int SubtitleId { get; set; }

        public int OccurrenceNumber { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}