using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace OtzariaIndexer
{
    public static class SnippetGenerator
    {
        public static string CreateSnippet(string text, List<Token> tokens, int snippetLength = 150)
        {
            var sortedTokens = tokens.OrderBy(t => t.StartIndex).ToList();
            int snippetStart = Math.Max(0, sortedTokens.First().StartIndex - snippetLength / 2);
            int snippetEnd = Math.Min(text.Length, sortedTokens.Last().StartIndex + sortedTokens.Last().Text.Length + snippetLength / 2);

            // Adjust snippetStart to not start in the middle of a word
            if (snippetStart > 0 && !char.IsWhiteSpace(text[snippetStart - 1]))
            {
                while (snippetStart > 0 && !char.IsWhiteSpace(text[snippetStart - 1]))
                {
                    snippetStart--;
                }
            }

            // Adjust snippetEnd to not end in the middle of a word
            if (snippetEnd < text.Length && !char.IsWhiteSpace(text[snippetEnd]))
            {
                while (snippetEnd < text.Length && !char.IsWhiteSpace(text[snippetEnd]))
                {
                    snippetEnd++;
                }
            }

            string snippet = text.Substring(snippetStart, snippetEnd - snippetStart);

            // Build the regex pattern
            StringBuilder patternBuilder = new StringBuilder();
            patternBuilder.Append("(");
            for (int i = 0; i < sortedTokens.Count; i++)
            {
                if (i > 0)
                    patternBuilder.Append("|");

                patternBuilder.Append(Regex.Escape(sortedTokens[i].Text));
            }
            patternBuilder.Append(")");

            string pattern = patternBuilder.ToString();

            // Use Regex to replace the token text with highlighted versions
            snippet = Regex.Replace(snippet, pattern, "<$&>", RegexOptions.IgnoreCase);

            return snippet;
        }
    }
}
