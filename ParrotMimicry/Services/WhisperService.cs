using System.Diagnostics;
using ParrotMimicry.Models;
using Whisper.net;
using Whisper.net.Ggml;
using System.Text;
using ParrotMimicry.Utilities;

namespace ParrotMimicry.Services
{
    public enum WhisperModelType
    {
        Tiny,
        Base,
        Small,
        Medium,
        LargeV1,
        LargeV3
    }

    public class WhisperService
    {
        private readonly DatabaseService _databaseService;
        private readonly DictionaryService _dictionaryService;
        private WhisperModelType _currentModel = WhisperModelType.LargeV3;
        private readonly Dictionary<WhisperModelType, (string Name, GgmlType Type)> _modelMap = new()
        {
            { WhisperModelType.Tiny, ("whisper-tiny", GgmlType.Tiny) },
            { WhisperModelType.Base, ("whisper-base", GgmlType.Base) },
            { WhisperModelType.Small, ("whisper-small", GgmlType.Small) },
            { WhisperModelType.Medium, ("whisper-medium", GgmlType.Medium) },
            { WhisperModelType.LargeV1, ("whisper-large", GgmlType.LargeV1) },
            { WhisperModelType.LargeV3, ("whisper-large-v3", GgmlType.LargeV3) }
        };

        public WhisperService(DatabaseService databaseService, DictionaryService dictionaryService)
        {
            _databaseService = databaseService;
            _dictionaryService = dictionaryService;
        }

        public async Task<List<Subtitle>> ProcessAudioFileAsync(string audioFilePath)
        {
            try
            {
                var subtitles = new List<Subtitle>();
                var modelInfo = _modelMap[_currentModel];
                var modelPath = CustomModelFilePath ?? Path.Combine(FileSystem.AppDataDirectory, $"{modelInfo.Name}.bin");

                // 检查模型文件是否存在，不存在则下载
                if (!File.Exists(modelPath))
                {
                    await DownloadModelAsync(modelPath);
                    Debug.WriteLine("下载模型完成！");
                }
                Debug.WriteLine("开始识别语音！");
                // 使用Whisper进行语音识别
                using var whisperFactory = WhisperFactory.FromPath(modelPath);
                using var processor = whisperFactory.CreateBuilder()
                    .WithLanguage("auto")
                    .Build();

                using var audioStream = File.OpenRead(audioFilePath);
                var audioDirectory = Path.GetDirectoryName(audioFilePath);
                var audioFileName = Path.GetFileNameWithoutExtension(audioFilePath);
                var srtFilePath = Path.Combine(audioDirectory, $"{audioFileName}.srt");
                var srtBuilder = new StringBuilder();
                var subtitleNumber = 1;

                await foreach (var segment in processor.ProcessAsync(audioStream))
                {
                    var subtitle = new Subtitle
                    {
                        Text = segment.Text,
                        StartTime = segment.Start.ToString(@"hh\:mm\:ss\,fff"),
                        EndTime = segment.End.ToString(@"hh\:mm\:ss\,fff"),
                        SrtPath = srtFilePath,
                    };

                    // 构建SRT格式的字幕内容
                    srtBuilder.AppendLine(subtitleNumber.ToString());
                    srtBuilder.AppendLine($"{subtitle.StartTime} --> {subtitle.EndTime}");
                    srtBuilder.AppendLine(subtitle.Text);
                    srtBuilder.AppendLine();
                    subtitleNumber++;

                    Debug.WriteLine($"处理每个字幕{subtitle.Text}，提取单词");
                    // 处理每个字幕，提取单词
                    var words = subtitle.Text.ExtractWords();
                    // 统计每个单词在字幕中出现的次数
                    var wordOccurrences = words.GroupBy(w => w.ToLower())
                                              .ToDictionary(g => g.Key, g => g.Count());

                    foreach (var (wordText, occurrenceCount) in wordOccurrences)
                    {
                        Debug.WriteLine($"创建或获取单词{wordText}记录");
                        // 创建或获取单词记录
                        var word = await GetOrCreateWordAsync(wordText);

                        // 创建WordSubtitle关联
                        var wordSubtitle = new WordSubtitle
                        {
                            WordId = word.Id,
                            SubtitleId = subtitle.Id,
                            OccurrenceNumber = occurrenceCount
                        };

                        await _databaseService.SaveWordSubtitleAsync(wordSubtitle);
                    }
                    Debug.WriteLine($"保存字幕{subtitle.Text}");
                    // 保存字幕
                    await _databaseService.SaveSubtitleAsync(subtitle);
                    subtitles.Add(subtitle);
                }
                Debug.WriteLine("AI完成生成整个字幕");
                // 保存SRT文件到音频文件同目录

                await File.WriteAllTextAsync(srtFilePath, srtBuilder.ToString());
                Debug.WriteLine($"字幕文件已保存到：{srtFilePath}");
                return subtitles;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"处理音频文件时出错：{ex.Message}");
                throw;
            }
        }

        public WhisperModelType CurrentModel
        {
            get => _currentModel;
            set => _currentModel = value;
        }
        public string CustomModelFilePath { get; set; }

        private async Task DownloadModelAsync(string modelPath)
        {
            Debug.WriteLine("准备下载模型文件");
            using var modelStream = await WhisperGgmlDownloader.GetGgmlModelAsync(_modelMap[_currentModel].Type);
            using var fileStream = File.Create(modelPath);
            await modelStream.CopyToAsync(fileStream);
        }

        private async Task<Word> GetOrCreateWordAsync(string wordText)
        {
            // 查找现有单词
            var existingWords = await _databaseService.GetWordsAsync();
            var word = existingWords.FirstOrDefault(w => string.Equals(w.Text, wordText, StringComparison.OrdinalIgnoreCase));

            if (word != null)
                return word;

            // 创建新单词
            word = new Word
            {
                Text = wordText,
                // 使用DictionaryService获取音标和释义
                IPAPhonetic =await _dictionaryService.GetIPAPhoneticAsync(wordText),
                KKPhonetic =await _dictionaryService.GetKKPhoneticAsync(wordText),
                Definition =await _dictionaryService.GetDefinitionAsync(wordText),
                // 初始化学习进度相关属性
                Familiarity = 0,
                LastReviewTime = null
            };

            await _databaseService.SaveWordAsync(word);
            return word;
        }
    }
}