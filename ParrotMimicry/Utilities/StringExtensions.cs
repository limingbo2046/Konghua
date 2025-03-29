using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParrotMimicry.Utilities
{
    public static class StringExtensions
    {
        private static readonly char[] Punctuation = { ' ', '\t', '\n', '\r', '.', ',', '!', '?' };

        public static IEnumerable<string> ExtractWords(this string text)
        {
            // 将文本分割成单词
            return text.Split(Punctuation, StringSplitOptions.RemoveEmptyEntries)
                       .Select(w => w.ToLower())
                       .Where(w => !string.IsNullOrWhiteSpace(w));
        }
    }

}
