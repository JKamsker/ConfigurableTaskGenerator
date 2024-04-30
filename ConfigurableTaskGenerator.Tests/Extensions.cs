using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConfigurableTaskGenerator.Tests;
internal static class Extensions
{
    public static int CountStringOccurrences(this string text, string pattern)
    {
        int count = 0;
        int i = 0;
        while ((i = text.IndexOf(pattern, i, StringComparison.Ordinal)) != -1)
        {
            i += pattern.Length;
            count++;
        }
        return count;
    }
}
