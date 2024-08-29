using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using OtzariaIndexer;

namespace OtzriaIndexerTextFilesOnly
{
    public class TermToIndexEntry
    {
        public int Id { get; set; }

        [JsonIgnore]
        public SizeLimitedStringBuilder stringBuilder;

        public TermToIndexEntry(int id)
        {
            Id = id;
            stringBuilder = new SizeLimitedStringBuilder(id);
        }
    }

    public class ConcurrenTermToIndexMap : ConcurrentDictionary<string, TermToIndexEntry> { }
    //public class ConcurrenTermInvertedIndex : ConcurrentDictionary<int, string> { }
    public class IndexerBase
    {
        public string invertedIndexPath;
        public string termsFilePath;
        protected ConcurrenTermToIndexMap termToIndexMap = new ConcurrenTermToIndexMap();
        //protected ConcurrenTermInvertedIndex invertedIndex = new ConcurrenTermInvertedIndex();

        public IndexerBase()
        {
            string indexPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Index");
            if (!Directory.Exists(indexPath)) { Directory.CreateDirectory(indexPath); }
            invertedIndexPath = Path.Combine(indexPath, "InvertedIndex");
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
                termToIndexMap = JsonSerializer.Deserialize<ConcurrenTermToIndexMap>(json, new JsonSerializerOptions() { IncludeFields = true }) ?? new ConcurrenTermToIndexMap();
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
        bool isFlushingInProgress = false;
        bool isMemoryExceedsLimits = false;
        public void IndexDocuments(string[] documentFilePaths)
        {
            try
            {
                using (var memoryCleanerTimer = new Timer(state =>
                {
                    isMemoryExceedsLimits = MemoryManager.MemoryExceedsLimit();
                    if (isMemoryExceedsLimits)
                        FlushIndex();
                    //Console.WriteLine("Memory cleaned!");
                }, null, TimeSpan.Zero, TimeSpan.FromSeconds(3))) // Run every 5 seconds
                {
                    for (int i = 0; i < documentFilePaths.Count(); i++)
                    {
                        while (isMemoryExceedsLimits) Task.Delay(100).Wait();
                        string filePath = documentFilePaths[i];
                        Console.WriteLine(filePath);
                        Console.WriteLine(i + "\\" + documentFilePaths.Count());

                        try
                        {
                            IndexDocument(filePath);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.Message);
                        }

                    }

                    SaveTermsToJson();
                    FlushIndex();
                    Console.WriteLine("Indexing Complete!");
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        void IndexDocument(string filePath)
        {
            if (!filePath.ToLower().EndsWith(".txt")) return;

            Console.WriteLine($"Reading Text...");
            string text = File.ReadAllText(filePath);

            Console.WriteLine($"Tokenizing...");
            var tokens = Tokenizer_2.Tokenize(text, filePath);


            //StoreTokensInMemory(tokens);
            StoreTokens(tokens);
        }

        public void StoreTokens(List<Token> tokens)
        {
            Console.WriteLine($"Sorting Tokens...");
            var tokenGroups = tokens.GroupBy(t => t.Text);
            int termCount = termToIndexMap.Count;

            Console.WriteLine($"Storing Tokens...");
            using (var progress = new ConsoleProgressBar())
            {
                int progressCount = 1;
                int maxProgress = tokenGroups.Count();
                Parallel.ForEach(tokenGroups, group =>
                {
                    while (isMemoryExceedsLimits) Task.Delay(100).Wait();
                    progress.Report((double)progressCount++ / maxProgress);
                    termToIndexMap.TryAdd(group.Key, new TermToIndexEntry(++termCount));
                    if (group.Count() > 1)
                    {
                        foreach (var token in group)
                        {
                            termToIndexMap[group.Key].stringBuilder.Append(JsonSerializer.Serialize(group.First()) + "|");
                        }
                    }
                    else
                    {
                        termToIndexMap[group.Key].stringBuilder.Append(JsonSerializer.Serialize(group.First()) + "|");
                    }
                });
            }
        }

        public void FlushIndex()
        {
            if (isFlushingInProgress) return;
            isFlushingInProgress = true;

            using (var memoryCleanerTimer = new Timer(state =>
            {
                MemoryManager.CleanAsync();
            }, null, TimeSpan.Zero, TimeSpan.FromSeconds(10)))
            {
                Console.WriteLine("Flushing Inverted Index...");
                //int maxProgress = termToIndexMap.Count() + 3;
                //int progressCount = 1;

                var termsToFlush = termToIndexMap.Where(t =>
                        t.Value.stringBuilder.StringBuilder != null &&
                        t.Value.stringBuilder.StringBuilder.Length > 0)
                        .ToHashSet();

                //using (var progress = new ConsoleContinuousProgressBar())
                //{
                    Parallel.ForEach(termsToFlush, (entry, ct) =>
                    {
                        entry.Value.stringBuilder.Flush();
                        //Console.Write(".");
                        //progress.Report((double)progressCount++ / maxProgress);
                    });

                    SaveTermsToJson();
                    //progress.Report((double)progressCount++ / maxProgress);
                    //termToIndexMap = new ConcurrenTermToIndexMap();
                    //progress.Report((double)progressCount++ / maxProgress);
                    //LoadTermsFromJson();
                    //progress.Report((double)progressCount++ / maxProgress);
                //}
            }
            MemoryManager.Clean();

            Console.WriteLine("Flushing Complete!");
            isFlushingInProgress = false;
        }

    }
}


