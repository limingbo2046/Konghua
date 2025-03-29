using SQLite;
using System.Diagnostics;
using ParrotMimicry.Models;

namespace ParrotMimicry.Services
{
    public class DatabaseService
    {
        private SQLiteAsyncConnection _database;
        private bool _isInitialized;

        private static string ConnectionString
        {
            get
            {
                var dbPath = Path.Combine(FileSystem.AppDataDirectory, "whispersubtitle.db");
                return dbPath;
            }
        }

        public DatabaseService()
        {
            _database = new SQLiteAsyncConnection(ConnectionString);
        }

        public async Task InitializeAsync()
        {
            if (!_isInitialized)
            {
                // 删除现有表以确保新字段能够被正确创建
                await _database.DropTableAsync<Word>();
                await _database.DropTableAsync<Subtitle>();
                await _database.DropTableAsync<WordSubtitle>();

                // 重新创建表
                await _database.CreateTableAsync<Word>();
                await _database.CreateTableAsync<Subtitle>();
                await _database.CreateTableAsync<WordSubtitle>();

                _isInitialized = true;
            }
        }

        public async Task<List<WordSubtitle>> GetWordSubtitlesByWordIdAsync(int wordId)
        {
            return await _database.Table<WordSubtitle>().Where(ws => ws.WordId == wordId).ToListAsync();
        }

        public async Task<List<WordSubtitle>> GetWordSubtitlesBySubtitleIdAsync(int subtitleId)
        {
            return await _database.Table<WordSubtitle>().Where(ws => ws.SubtitleId == subtitleId).ToListAsync();
        }

        public async Task<int> DeleteWordSubtitleAsync(WordSubtitle wordSubtitle)
        {
            return await _database.DeleteAsync(wordSubtitle);
        }

        public async Task<List<Word>> GetWordsAsync()
        {
            return await _database.Table<Word>().ToListAsync();
        }

        public async Task<Word> GetWordAsync(int id)
        {
            return await _database.Table<Word>().Where(w => w.Id == id).FirstOrDefaultAsync();
        }

        public async Task<Word> GetWordByTextAsync(string text)
        {
            return await _database.Table<Word>().Where(w => w.Text.ToLower() == text.ToLower()).FirstOrDefaultAsync();
        }

        public async Task<int> SaveWordAsync(Word word)
        {
            if (word.Id != 0)
            {
                return await _database.UpdateAsync(word);
            }
            return await _database.InsertAsync(word);
        }

        public async Task<int> SaveWordSubtitleAsync(WordSubtitle wordSubtitle)
        {
            if (wordSubtitle.Id != 0)
            {
                return await _database.UpdateAsync(wordSubtitle);
            }
            return await _database.InsertAsync(wordSubtitle);
        }

        public async Task<List<Subtitle>> GetSubtitlesAsync()
        {
            return await _database.Table<Subtitle>().ToListAsync();
        }

        public async Task<int> SaveSubtitleAsync(Subtitle subtitle)
        {
            if (subtitle.Id != 0)
            {
                return await _database.UpdateAsync(subtitle);
            }
            return await _database.InsertAsync(subtitle);
        }

        public async Task<List<Subtitle>> GetSubtitlesByWordIdAsync(int wordId)
        {
            var wordSubtitles = await GetWordSubtitlesByWordIdAsync(wordId);
            var subtitleIds = wordSubtitles.Select(ws => ws.SubtitleId).ToList();
            return await _database.Table<Subtitle>().Where(s => subtitleIds.Contains(s.Id)).ToListAsync();
        }

        public async Task<int> DeleteWordAsync(Word word)
        {
            return await _database.DeleteAsync(word);
        }

        public async Task<int> DeleteSubtitleAsync(Subtitle subtitle)
        {
            return await _database.DeleteAsync(subtitle);
        }
    }
}