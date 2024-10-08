﻿using OtzariaIndexer;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Shapes;

namespace OtzriaIndexerTextFilesOnly
{
    public class IndexSearcher : Indexer
    {
        public List<KeyValuePair<string, string>> Search(string query, int distanceBetweenWords)
        {
            List<KeyValuePair<string, string>> results = new List<KeyValuePair<string, string>>();

            var terms = query.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (terms.Length == 0) return results;

            var validTermMap = terms.Where(term => termToIndexMap.TryGetValue(term, out _))
                .ToDictionary(term => term, term => termToIndexMap[term]);

            List<List<Token>> matchedTokens = new List<List<Token>>();
            foreach (var term in validTermMap)
            {
                matchedTokens.Add(GetSerializedResults(term.Value.Id));
            }
            if (matchedTokens.Count < terms.Length) return results;

            // Group by document and check proximity
            var groupedByDocument = matchedTokens
                .SelectMany(token => token)
                .GroupBy(token => token.DocumentPath);

            int maxProgress = groupedByDocument.Count();
            int progressCount = 1;
            using (var progress = new ConsoleProgressBar())
            {
                foreach (var group in groupedByDocument)
                {
                    var documentPath = group.Key;
                    List<List<Token>> tokenLists = group.GroupBy(t => t.Text).Select(g => g.ToList()).ToList();
                    if (tokenLists.Count < terms.Length) continue;

                    var validResults = ProximityChecker.GetAllValidConsecutiveResults(tokenLists, 2);
                    if (validResults.Count == 0) continue;

                    //string documentText = RetrieveDocumentText(documentId);
                    string documentText = File.ReadAllText(documentPath);
                    for (int i = 0; i < validResults.Count; i++)
                    {
                        results.Add(new KeyValuePair<string, string>(documentPath, SnippetGenerator.CreateSnippet(documentText, validResults[i])));
                    }

                    progress.Report((double)progressCount++ / maxProgress);
                }
                return results;
            }
        }

        List<Token> GetSerializedResults(int termId)
        {
            string termData = ReadTermData(termId);

            var termEntries = termData.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries);

            List<Token> serailizedEntries = new List<Token>();
            foreach (var entry in termEntries)
            {
                var serializedEntry = JsonSerializer.Deserialize<Token>(entry);
                if (serializedEntry != null) serailizedEntries.Add(serializedEntry);
            }
            return serailizedEntries;
        }

        string ReadTermData(int termId)
        {
            using (var manager = new RocksDbManager(invertedIndexPath))
            {
               return manager.GetEntry(termId);
            }

                //string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Index", "InvertedIndex", termId.ToString());
                //using (FileStream fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                //{
                //    using (DeflateStream deflateStream = new DeflateStream(fileStream, CompressionMode.Decompress))
                //    {
                //        using (StreamReader reader = new StreamReader(deflateStream, Encoding.UTF8))
                //        {
                //            return reader.ReadToEnd();
                //        }
                //    }
                //}

                //using (ZipArchive zipArchive = ZipFile.Open(Path.Combine(invertedIndexPath, $"{termId}.zip"), ZipArchiveMode.Update))
                //{
                //    var entry = zipArchive.GetEntry($"{termId}.txt");
                //    if (entry == null) return string.Empty;
                //    using (StreamReader reader = new StreamReader(entry.Open()))
                //    {
                //        return reader.ReadToEnd();
                //    }
                //}

                //string entryPath = Path.Combine(invertedIndexPath, termId.ToString() + ".txt");
                //if (File.Exists(entryPath))
                //{
                //    return File.ReadAllText(entryPath);
                //}
                //return string.Empty;
            }
    }
}
