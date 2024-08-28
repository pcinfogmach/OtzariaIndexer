using OtzariaIndexer;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OtzriaIndexerTextFilesOnly
{
    internal class Tokenizer_2
    {
        public static List<Token> Tokenize(string text, string filePath)
        {
            text = text.Replace("''", "\"").ToLower();
            bool inWord = false;
            bool doubleQuotesDetected = false;

            var tokens = new List<Token>();
            int position = 1;
            int currentIndex = 0;

            StringBuilder stringBuilder = new StringBuilder();
            foreach (char c in text)
            {
                if (char.IsLetterOrDigit(c))
                {
                    if (doubleQuotesDetected)
                    {
                        stringBuilder.Append('"');
                        doubleQuotesDetected = false;
                    }
                    stringBuilder.Append(c);
                    inWord = true;
                }
                else if (inWord && c == '\'')
                {
                    stringBuilder.Append(c);
                }
                else if (inWord && c == '"')
                {
                    doubleQuotesDetected = true;
                }
                else if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                {
                    if (stringBuilder.Length > 0)
                    {
                        tokens.Add(new Token
                        {
                            DocumentPath = filePath,
                            Text = stringBuilder.ToString(),
                            Position = position++,
                            StartIndex = currentIndex
                        });
                        stringBuilder.Clear();
                    }
                    doubleQuotesDetected = false;
                    inWord = false;
                }

                currentIndex++;
            }

            if (stringBuilder.Length > 0)
            {
                tokens.Add(new Token
                {
                    DocumentPath = filePath,
                    Text = stringBuilder.ToString(),
                    Position = position,
                    StartIndex = currentIndex
                });
            }

            return tokens;
        }
    }
}