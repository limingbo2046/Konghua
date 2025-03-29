using SQLite;

namespace ParrotMimicry.Models
{
    public class Subtitle
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string Text { get; set; } = string.Empty;

        public string StartTime { get; set; } = string.Empty;

        public string EndTime { get; set; } = string.Empty;

        public string SrtPath { get; set; } = string.Empty;  // 存储生成的.srt字幕文件的保存路径

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    }
}