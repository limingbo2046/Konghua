using System.Text.RegularExpressions;
using ParrotMimicry.Models;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace ParrotMimicry.Services
{
    public class DictionaryService
    {
        private readonly Dictionary<string, string> _cmuToIpa = new()
        {
            { "AA", "ɑ" }, { "AE", "æ" }, { "AH", "ʌ" }, { "AO", "ɔ" }, { "AW", "aʊ" }, { "AY", "aɪ" },
            { "B", "b" }, { "CH", "tʃ" }, { "D", "d" }, { "DH", "ð" }, { "EH", "ɛ" }, { "ER", "ɝ" },
            { "EY", "eɪ" }, { "F", "f" }, { "G", "ɡ" }, { "HH", "h" }, { "IH", "ɪ" }, { "IY", "i" },
            { "JH", "dʒ" }, { "K", "k" }, { "L", "l" }, { "M", "m" }, { "N", "n" }, { "NG", "ŋ" },
            { "OW", "oʊ" }, { "OY", "ɔɪ" }, { "P", "p" }, { "R", "r" }, { "S", "s" }, { "SH", "ʃ" },
            { "T", "t" }, { "TH", "θ" }, { "UH", "ʊ" }, { "UW", "u" }, { "V", "v" }, { "W", "w" },
            { "Y", "j" }, { "Z", "z" }, { "ZH", "ʒ" }
        };

        private readonly Dictionary<string, string> _cmuToKk = new()
        {
            { "AA", "ɑ" }, { "AE", "æ" }, { "AH", "ʌ" }, { "AO", "ɔ" }, { "AW", "aʊ" }, { "AY", "aɪ" },
            { "B", "b" }, { "CH", "tʃ" }, { "D", "d" }, { "DH", "ð" }, { "EH", "ɛ" }, { "ER", "ɚ" },
            { "EY", "e" }, { "F", "f" }, { "G", "g" }, { "HH", "h" }, { "IH", "ɪ" }, { "IY", "i" },
            { "JH", "dʒ" }, { "K", "k" }, { "L", "l" }, { "M", "m" }, { "N", "n" }, { "NG", "ŋ" },
            { "OW", "o" }, { "OY", "ɔɪ" }, { "P", "p" }, { "R", "r" }, { "S", "s" }, { "SH", "ʃ" },
            { "T", "t" }, { "TH", "θ" }, { "UH", "ʊ" }, { "UW", "u" }, { "V", "v" }, { "W", "w" },
            { "Y", "j" }, { "Z", "z" }, { "ZH", "ʒ" }
        };

        private readonly Dictionary<string, string> _posMap = new()
        {
            { "n", "名词" },
            { "v", "动词" },
            { "a", "形容词" },
            { "s", "形容词" },
            { "r", "副词" }
        };

        private Dictionary<string, List<string>> _cmuDict = new();
        private Dictionary<string, List<string>> _wordnetDict = new();
        private bool _isInitialized;
        private readonly SemaphoreSlim _initializationLock = new(1, 1);

        public DictionaryService()
        {        
        }


        private async Task EnsureInitializedAsync()
        {
            if (_isInitialized) return;

            await _initializationLock.WaitAsync();
            try
            {
                if (_isInitialized) return;

                await Task.WhenAll(
                    LoadCmuDictionaryAsync(),
                    LoadWordNetDictionaryAsync()
                );

                _isInitialized = true;
            }
            finally
            {
                _initializationLock.Release();
            }
        }

        private async Task LoadCmuDictionaryAsync()
        {

            try
            {
                using var stream = await FileSystem.OpenAppPackageFileAsync("cmudict.dict");
                using var reader = new StreamReader(stream);

                // 读取文件内容到内存中
                string fileContent = await reader.ReadToEndAsync();

                var lines = fileContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

                foreach (var line in lines)
                {
                    if (line.StartsWith(";")) continue; // 跳过注释行

                    var parts = line.Split(" ", StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 2) continue; // 跳过无效行

                    var word = parts[0].ToLower();
                    word = CleanWord(word); // 处理同一单词的多个变体

                    var phonetic = CleanPhonetic(string.Join(" ", parts.Skip(1))); // 清理发音中的数字

                    if (!_cmuDict.ContainsKey(word))
                    {
                        _cmuDict[word] = new List<string>();
                    }
                    _cmuDict[word].Add(phonetic);
                }

                // 处理同一单词的多个变体（去除括号）
                string CleanWord(string word)
                {
                    if (word.Contains('('))
                    {
                        word = word.Substring(0, word.IndexOf('('));
                    }
                    return word;
                }

                // 清理发音中的数字（去除重音标记）
                string CleanPhonetic(string phonetic)
                {
                    return Regex.Replace(phonetic, @"[0-9]", ""); // 移除数字
                }

            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载CMU词典失败：{ex.Message}");
            }
        }

        private async Task LoadWordNetDictionaryAsync()
        {

            try
            {
                using var stream = await FileSystem.OpenAppPackageFileAsync("wordnet_data.txt");
            using var reader = new StreamReader(stream);

            string fileContent = await reader.ReadToEndAsync();  // 读取整个文件内容

            string? currentWord = null;
            string? currentDefinition = null;

            // 按行拆分文件内容
            var lines = fileContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            foreach (var line in lines)
            {
                if (line.StartsWith("Word: "))
                {
                    // 如果已经有一个完整的单词和释义，则添加到字典中
                    if (currentWord != null && currentDefinition != null)
                    {
                        if (!_wordnetDict.ContainsKey(currentWord))
                        {
                            _wordnetDict[currentWord] = new List<string>();
                        }
                        _wordnetDict[currentWord].Add(currentDefinition);
                    }

                    // 获取新的单词
                    currentWord = line.Substring(6).Trim().ToLower();
                    currentDefinition = null;
                }
                else if (line.StartsWith("Definition: ") && currentWord != null)
                {
                    // 获取释义
                    currentDefinition = line.Substring(12).Trim();
                }
            }

            // 最后一个词条也需要处理
            if (currentWord != null && currentDefinition != null)
            {
                if (!_wordnetDict.ContainsKey(currentWord))
                {
                    _wordnetDict[currentWord] = new List<string>();
                }
                _wordnetDict[currentWord].Add(currentDefinition);
            }

            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"加载WordNet词典失败：{ex.Message}");
            }
        }

        public async Task<Word> GetWordInfoAsync(string text)
        {
            await EnsureInitializedAsync();

            var word = new Word
            {
                Text = text.ToLower(),
                CreatedAt = DateTime.UtcNow
            };

            try
            {
                var cmuPhonetic = await GetCmuPhoneticAsync(text);
                if (!string.IsNullOrEmpty(cmuPhonetic))
                {
                    word.IPAPhonetic = ConvertCmuToIpa(cmuPhonetic);
                    word.KKPhonetic = ConvertCmuToKk(cmuPhonetic);
                }

                word.Definition = await GetDefinitionAsync(text);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取单词 {text} 信息失败：{ex.Message}");
            }

            return word;
        }

        private async Task<string?> GetCmuPhoneticAsync(string word)
        {
            await EnsureInitializedAsync();
            if (_cmuDict.TryGetValue(word.ToLower(), out var pronunciations))
            {
                return pronunciations.FirstOrDefault();
            }
            return string.Empty;
        }

        public async Task<string?> GetDefinitionAsync(string word)
        {
            await EnsureInitializedAsync();
            if (_wordnetDict.TryGetValue(word.ToLower(), out var definitions))
            {
                return string.Join("\n", definitions);
            }
            return null;
        }

        public async Task<string?> GetIPAPhoneticAsync(string word)
        {
            var cmuPhonetic = await GetCmuPhoneticAsync(word);
            return ConvertCmuToIpa(cmuPhonetic);
        }

        public async Task<string?> GetKKPhoneticAsync(string word)
        {
            var cmuPhonetic = await GetCmuPhoneticAsync(word);
            return ConvertCmuToKk(cmuPhonetic);
        }
        private string? ConvertCmuToIpa(string cmuPhonetic)
        {
            if (string.IsNullOrEmpty(cmuPhonetic)) return null;

            var phones = cmuPhonetic.Split(' ');
            var ipa = new List<string>();

            foreach (var phone in phones)
            {
                var basePhone = new string(phone.Where(char.IsLetter).ToArray());
                var stress = new string(phone.Where(char.IsDigit).ToArray());

                if (_cmuToIpa.TryGetValue(basePhone, out var ipaPhone))
                {
                    if (stress == "1")
                        ipaPhone = "ˈ" + ipaPhone;
                    else if (stress == "2")
                        ipaPhone = "ˌ" + ipaPhone;
                    ipa.Add(ipaPhone);
                }
            }

            return string.Join("", ipa);
        }

        private string? ConvertCmuToKk(string cmuPhonetic)
        {
            if (string.IsNullOrEmpty(cmuPhonetic)) return null;

            var phones = cmuPhonetic.Split(' ');
            var kk = new List<string>();

            foreach (var phone in phones)
            {
                var basePhone = new string(phone.Where(char.IsLetter).ToArray());
                var stress = new string(phone.Where(char.IsDigit).ToArray());

                if (_cmuToKk.TryGetValue(basePhone, out var kkPhone))
                {
                    if (stress == "1")
                        kkPhone = "ˋ" + kkPhone;
                    else if (stress == "2")
                        kkPhone = "ˌ" + kkPhone;
                    kk.Add(kkPhone);
                }
            }

            return string.Join("", kk);
        }
    }
}