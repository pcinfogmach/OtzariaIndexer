using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using OtzariaIndexer;

namespace OtzriaIndexerTextFilesOnly
{
    public class ConcurrenTermToIndexMap : ConcurrentDictionary<string, int> { }
    public class ConcurrenTermInvertedIndex : ConcurrentDictionary<int, string> { }
    public class IndexerBase : IDisposable
    {
        public string invertedIndexPath;
        public string termsFilePath;
        protected ConcurrenTermToIndexMap termToIndexMap = new ConcurrenTermToIndexMap();
        protected ConcurrenTermInvertedIndex invertedIndex = new ConcurrenTermInvertedIndex();
        //public ZipArchive zipArchive;

        public void Dispose()
        {
            //if (zipArchive != null) zipArchive.Dispose();
        }

        public IndexerBase()
        {
            string indexPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Index");
            if (!Directory.Exists(indexPath)) { Directory.CreateDirectory(indexPath); }
            invertedIndexPath = Path.Combine(indexPath, "InvertedIndex");
            //zipArchive = ZipFile.Open(invertedIndexPath, ZipArchiveMode.Update);
            //if (zipArchive == null)
            //{
            //    Console.WriteLine("zipArchive is null.");
            //}
            //else
            //{
            //    Console.WriteLine("zipArchive is initialized.");
            //}

            if (!Directory.Exists(invertedIndexPath)) { Directory.CreateDirectory(invertedIndexPath); }
            termsFilePath = Path.Combine(indexPath, "TermsIndex.json");

            LoadTermsFromJson();

            System.Windows.Application.Current.Exit += (s, e) => { SaveTermsToJson(); };
        }

        protected void LoadTermsFromJson()
        {
            if (File.Exists(termsFilePath))
            {
                var json = File.ReadAllText(termsFilePath);
                termToIndexMap = JsonSerializer.Deserialize<ConcurrenTermToIndexMap>(json) ?? new ConcurrenTermToIndexMap();
            }
        }

        public void SaveTermsToJson()
        {
            var json = JsonSerializer.Serialize(termToIndexMap);
            File.WriteAllText(termsFilePath, json);
        }
    }

    public class Indexer : IndexerBase
    {
        public async void IndexDocuments(string[] documentFilePaths)
        {
            for (int i = 0; i < documentFilePaths.Count(); i++)
            {
                string filePath = documentFilePaths[i];
                Console.WriteLine(filePath);
                Console.WriteLine(i + "\\" + documentFilePaths.Count());

                try
                {
                    IndexDocument(filePath);
                    //if (MemoryExceedsLimit())
                    //{
                    //    Console.WriteLine("Flushing Index From Memory....");
                    //    zipArchive.Dispose();
                    //    Console.WriteLine("Reloading Index....");
                    //    zipArchive = ZipFile.Open(invertedIndexPath, ZipArchiveMode.Update);
                    //}
                    //SaveInvertedIndex();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }

            }

            SaveTermsToJson();  // Save the updated term dictionary to JSON

            //Console.WriteLine("Flushing Inverted Index...");
            //SaveInvertedIndex();
            Console.WriteLine("Indexing Complete!");
        }

        void IndexDocument(string filePath)
        {
            if (!filePath.ToLower().EndsWith(".txt")) return;

            Console.WriteLine($"Reading Text...");
            string text = File.ReadAllText(filePath);

            Console.WriteLine($"Tokenizing...");
            var tokens = Tokenizer_2.Tokenize(text, filePath);

            Console.WriteLine($"Storing Tokens...");
            //StoreTokensInMemory(tokens);
            StoreTokensInDisk(tokens);
        }

        public async void StoreTokensInDisk(List<Token> tokens)
        {
            var tokenGroups = tokens.GroupBy(t => t.Text);
            int termCount = termToIndexMap.Count;

            using (var progress = new ConsoleProgressBar())
            {
                int progressCount = 1;
                int maxProgress = tokenGroups.Count();
                Parallel.ForEach(tokenGroups, group =>
                {
                    progress.Report((double)progressCount++ / maxProgress);
                    string newData = "";
                    if (group.Count() > 1)
                    {
                        var stb = new StringBuilder();
                        foreach (var token in group)
                        {
                            stb.Append(JsonSerializer.Serialize(token) + "|");
                        }
                        newData = stb.ToString();
                    }
                    else
                    {
                        newData = JsonSerializer.Serialize(group.First()) + "|";
                    }

                    int termId = termToIndexMap.GetOrAdd(group.Key, _ => Interlocked.Increment(ref termCount));

                    //using (ZipArchive zipArchive = ZipFile.Open(invertedIndexPath, ZipArchiveMode.Update))
                    //{
                    //    var entry = zipArchive.GetEntry(termId + ".txt") ?? zipArchive.CreateEntry(termId + ".txt");
                    //    using (StreamWriter writer = new StreamWriter(entry.Open()))
                    //    {
                    //        writer.BaseStream.Seek(0, SeekOrigin.End);
                    //        writer.Write(newData);
                    //        writer.Flush();
                    //    }
                    //}

                    string entryPath = Path.Combine(invertedIndexPath, termId.ToString() + ".txt");
                    File.AppendAllText(entryPath, newData);
                });
            }               
        }

        public void StoreTokensInMemory(List<Token> tokens)
        {
            int termCount = termToIndexMap.Count;

            Parallel.ForEach(tokens, token =>
            {
                int termId = termToIndexMap.GetOrAdd(token.Text, _ => Interlocked.Increment(ref termCount));
                string newData = JsonSerializer.Serialize(token);
                invertedIndex.AddOrUpdate(termId, newData + "|", (key, oldValue) => oldValue + newData + "|");
            });
        }

        bool MemoryExceedsLimit()
        {
            const long oneGB = 1L * 1024 * 1024 * 1024;

            // Get the current memory usage of the application
            Process currentProcess = Process.GetCurrentProcess();
            long memoryUsed = currentProcess.WorkingSet64;

            return memoryUsed > oneGB;
        }

        public void SaveInvertedIndex()
        {
            Parallel.ForEach(invertedIndex, entry =>
            {
                string entryPath = Path.Combine(invertedIndexPath, entry.Key.ToString() + ".txt");

                lock (entryPath) // Ensure that file operations are thread-safe
                {
                    if (File.Exists(entryPath))
                    {
                        File.AppendAllText(entryPath, entry.Value);
                    }
                    else
                    {
                        File.WriteAllText(entryPath, entry.Value);
                    }
                }
            });

            invertedIndex.Clear(); // Clear the inverted index after saving
        }

    }

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
                matchedTokens.Add(GetSerializedResults(term.Value));
            }
            if (matchedTokens.Count < terms.Length) return results;

            // Group by document and check proximity
            var groupedByDocument = matchedTokens
                .SelectMany(token => token)
                .GroupBy(token => token.DocumentPath);

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
            }
            return results;
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
            //var entry = zipArchive.GetEntry(termId + ".txt") ?? zipArchive.CreateEntry(termId + ".txt");
            //using (StreamReader reader = new StreamReader(entry.Open()))
            //{
            //    return reader.ReadToEnd();
            //}

            string entryPath = Path.Combine(invertedIndexPath, termId.ToString() + ".txt");
            if (File.Exists(entryPath))
            {
                return File.ReadAllText(entryPath);
            }
            return string.Empty;
        }
    }
}


