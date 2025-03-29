using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParrotMimicry.Utilities
{
    // 替换为跨平台的自然排序比较器
    public class NaturalStringComparer : IComparer<string>
    {
        public int Compare(string x, string y)
        {
            if (x == null || y == null) return 0;

            var segmentsX = GetSegments(x);
            var segmentsY = GetSegments(y);

            int minLength = Math.Min(segmentsX.Count, segmentsY.Count);
            for (int i = 0; i < minLength; i++)
            {
                int compareResult = CompareSegments(segmentsX[i], segmentsY[i]);
                if (compareResult != 0)
                    return compareResult;
            }
            return segmentsX.Count.CompareTo(segmentsY.Count);
        }

        private List<object> GetSegments(string input)
        {
            var segments = new List<object>();
            var currentNumber = new StringBuilder();
            var currentText = new StringBuilder();

            foreach (char c in input)
            {
                if (char.IsDigit(c))
                {
                    if (currentText.Length > 0)
                    {
                        segments.Add(currentText.ToString());
                        currentText.Clear();
                    }
                    currentNumber.Append(c);
                }
                else
                {
                    if (currentNumber.Length > 0)
                    {
                        segments.Add(int.Parse(currentNumber.ToString()));
                        currentNumber.Clear();
                    }
                    currentText.Append(c);
                }
            }

            if (currentNumber.Length > 0)
                segments.Add(int.Parse(currentNumber.ToString()));
            if (currentText.Length > 0)
                segments.Add(currentText.ToString());

            return segments;
        }

        private int CompareSegments(object x, object y)
        {
            if (x is int xNum && y is int yNum)
                return xNum.CompareTo(yNum);

            return string.Compare(
                x?.ToString() ?? "",
                y?.ToString() ?? "",
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
