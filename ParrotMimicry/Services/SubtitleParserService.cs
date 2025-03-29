using System.Text.RegularExpressions;
using System.Globalization;
using ParrotMimicry.Models;
using System;

namespace ParrotMimicry.Services;

public class SubtitleParserService
{
    private readonly DatabaseService _databaseService;
    private readonly DictionaryService _dictionaryService;

    public SubtitleParserService(DatabaseService databaseService, DictionaryService dictionaryService)
    {
        _databaseService = databaseService;
        _dictionaryService = dictionaryService;
    }
    public event Action<int,int> BatchParseSubtitles_ProgressChanged;
    // 进度信息类，用于报告进度
    private class ProgressInfo
    {
        public int ProcessedCount { get; set; }
        public int TotalFiles { get; set; }
    }

    public async Task BatchParseSubtitlesAsync(List<string> subtitlePaths)
    {
        int totalFiles = subtitlePaths.Count;
        int processedCount = 0;

        // 创建进度回调，确保在UI线程上执行
        var progress = new Progress<ProgressInfo>(value =>
        {
            // 使用MainThread.InvokeOnMainThreadAsync确保在UI线程上更新进度
            MainThread.BeginInvokeOnMainThread(() =>
            {
                BatchParseSubtitles_ProgressChanged?.Invoke(value.ProcessedCount, value.TotalFiles);
            });
        });

        // 使用Task.Run将整个批处理操作放在后台线程执行，避免阻塞UI线程
        await Task.Run(async () =>
        {
            // 使用SemaphoreSlim限制并发任务数量，避免过多并发导致资源竞争
            using var semaphore = new SemaphoreSlim(5); // 限制最多5个并发任务
            var tasks = new List<Task>();

            foreach (var subtitlePath in subtitlePaths)
            {
                await semaphore.WaitAsync(); // 等待信号量
                
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        await ParseSubtitleFileAsync(subtitlePath);
                        
                        // 线程安全地增加计数并报告进度
                        int currentCount = Interlocked.Increment(ref processedCount);
                        ((IProgress<ProgressInfo>)progress).Report(new ProgressInfo 
                        { 
                            ProcessedCount = currentCount, 
                            TotalFiles = totalFiles
                        });
                    }
                    catch (Exception ex)
                    {
                        // 确保UI线程执行UI操作
                        await MainThread.InvokeOnMainThreadAsync(async () =>
                        {
                            await Application.Current.MainPage.DisplayAlert("错误", $"解析字幕文件失败 ({subtitlePath})：{ex.Message}", "确定");
                        });
                    }
                    finally
                    {
                        semaphore.Release(); // 释放信号量
                    }
                }));
            }

            // 等待所有任务完成
            await Task.WhenAll(tasks);
            
            // 解析完毕，通知用户
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await Application.Current.MainPage.DisplayAlert("完成", "所有字幕解析完成！", "确定");
            });
        });
    }

    public async Task ParseSubtitleFileAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("字幕文件不存在", filePath);
        }

        var subtitleContent = await File.ReadAllTextAsync(filePath);
        var subtitleItems = ParseSrtContent(subtitleContent, filePath);
        var words = ExtractWords(subtitleContent);

        // 保存字幕内容到数据库
        foreach (var subtitleItem in subtitleItems)
        {
            var subtitle = new Subtitle
            {
                Text = subtitleItem.Text,
                StartTime = subtitleItem.Start.ToString(@"hh\:mm\:ss\.fff"),
                EndTime = subtitleItem.End.ToString(@"hh\:mm\:ss\.fff"),
                SrtPath = filePath,
                CreatedAt = DateTime.UtcNow
            };
            await _databaseService.SaveSubtitleAsync(subtitle);

            // 提取该字幕中的单词并建立关联
            var subtitleWords = ExtractWords(subtitleItem.Text);
            foreach (var wordText in subtitleWords)
            {
                // 保存单词（如果不存在）
                var existingWord = await _databaseService.GetWordByTextAsync(wordText);
                int wordId;

                if (existingWord == null)
                {
                    var wordInfo = await _dictionaryService.GetWordInfoAsync(wordText);
                    if (wordInfo != null)
                    {
                        await _databaseService.SaveWordAsync(wordInfo);
                        wordId = wordInfo.Id;
                    }
                    else
                    {
                        continue; // 如果无法获取单词信息，跳过
                    }
                }
                else
                {
                    wordId = existingWord.Id;
                }

                // 创建单词与字幕的关联
                var wordSubtitle = new WordSubtitle
                {
                    WordId = wordId,
                    SubtitleId = subtitle.Id,
                    OccurrenceNumber = 1, // 可以根据需要计算出现次数
                    CreatedAt = DateTime.UtcNow
                };

                await _databaseService.SaveWordSubtitleAsync(wordSubtitle);
            }
        }
    }

    private HashSet<string> ExtractWords(string content)
    {
        var words = new HashSet<string>();
        var wordPattern = @"\b[a-zA-Z]+\b";
        var matches = Regex.Matches(content, wordPattern);

        foreach (Match match in matches)
        {
            var word = match.Value.ToLower();
            if (word.Length > 1) // 忽略单字母单词
            {
                words.Add(word);
            }
        }

        return words;
    }

    private List<SubtitleItem> ParseSrtContent(string content, string filePath)
    {
        var subtitles = new List<SubtitleItem>();
        using (StringReader reader = new StringReader(content))
        {
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                // 解析字幕索引
                if (!int.TryParse(line, out int index)) continue;

                // 解析时间戳
                string? timeLine = reader.ReadLine();
                if (timeLine == null) continue;

                string[] times = timeLine.Split(" --> ");
                if (times.Length != 2) continue;

                TimeSpan start = TimeSpan.ParseExact(times[0].Replace(',', '.'), @"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture);
                TimeSpan end = TimeSpan.ParseExact(times[1].Replace(',', '.'), @"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture);

                // 解析字幕内容（可能是多行）
                string text = "";
                while (!string.IsNullOrWhiteSpace(line = reader.ReadLine()))
                {
                    if (line == null) break;
                    text += " " + line;
                }

                subtitles.Add(new SubtitleItem
                {
                    Index = index,
                    Start = start,
                    End = end,
                    Text = text.Trim()
                });
            }
        }
        return subtitles;
    }

    // 内部类，用于解析SRT文件
    private class SubtitleItem
    {
        public int Index { get; set; }
        public TimeSpan Start { get; set; }
        public TimeSpan End { get; set; }
        public string Text { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public string? Note { get; set; }
    }
}