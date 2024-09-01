using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using OtzariaIndexer;
using static System.Net.Mime.MediaTypeNames;

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
    //public class ConcurrenFileIds : ConcurrentDictionary<int, string> { }
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

        private readonly long _maxSizeInBytesHalfGB = 1024L * 1024 * 512;
        private readonly long _maxSizeInBytesFullGB = 1024L * 1024 * 1024;

        int currentSizeInBytes = 0;
        public void IndexDocuments(string[] documentFilePaths)
        {
            int documentCount = documentFilePaths.Count();
            using (var manager = new RocksDbManager(invertedIndexPath))
            using (var memoryCleanerTimer = new Timer(state =>
            {
                MemoryManager.CleanAsync();
                //if (MemoryManager.MemoryExceedsLimit())
                //{
                //    FlushIndex(manager);
                //}
            }, null, TimeSpan.Zero, TimeSpan.FromSeconds(5)))
            {

                for (int i = 0; i < documentFilePaths.Count(); i++)
                {
                    while (isMemoryExceedsLimits) Task.Delay(100).Wait();
                    string filePath = documentFilePaths[i];
                    Console.WriteLine($"{i}\\{documentCount}\t{filePath}");

                    try
                    {
                        IndexDocument(filePath, manager);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }

                }
                SaveTermsToJson();
                FlushIndex(manager);
                Console.WriteLine("Indexing Complete!");
            }     
        }

        void IndexDocument(string filePath, RocksDbManager manager)
        {
            if (!filePath.ToLower().EndsWith(".txt")) return;

            if (currentSizeInBytes > _maxSizeInBytesHalfGB)
            {
                FlushIndex(manager);
                currentSizeInBytes = 0;
            }

            Console.WriteLine($"Reading Text...");
            string text = File.ReadAllText(filePath);

            Console.WriteLine($"Tokenizing...");
            var tokens = Tokenizer_2.Tokenize(text, filePath);

            //StoreTokensInDataBase(tokens, manager);
            StoreTokens(tokens, manager);
        }

        public void StoreTokens(List<Token> tokens, RocksDbManager manager)
        {
            Console.WriteLine($"Sorting Tokens...");
            var tokenGroups = tokens.GroupBy(t => t.Text);
            int termCount = termToIndexMap.Count;

            Console.WriteLine($"Storing Tokens...");
            using (var progress = new ConsoleProgressBar())
            {
                int progressCount = 1;
                int maxProgress = tokenGroups.Count();
                ParallelOptions maxDegreeOfParallelism = new ParallelOptions() { MaxDegreeOfParallelism = 2 };
                Parallel.ForEach(tokenGroups, maxDegreeOfParallelism, group =>
                {
                    while (isFlushingInProgress) Task.Delay(100).Wait();
                    
                    if (currentSizeInBytes > _maxSizeInBytesFullGB) 
                    {
                        FlushIndex(manager);
                        currentSizeInBytes = 0;
                    }                 
                    
                    termToIndexMap.TryAdd(group.Key, new TermToIndexEntry(++termCount));
                    
                    if (group.Count() > 1)
                    {
                        foreach (var token in group)
                        {
                            string jsonText = JsonSerializer.Serialize(token) + "|";
                            currentSizeInBytes += Encoding.UTF8.GetByteCount(jsonText); 
                            termToIndexMap[group.Key].stringBuilder.Append(jsonText);
                        }
                    }
                    else
                    {
                        string jsonText = JsonSerializer.Serialize(group.First()) + "|";
                        currentSizeInBytes += Encoding.UTF8.GetByteCount(jsonText);
                        termToIndexMap[group.Key].stringBuilder.Append(jsonText);
                    }

                    progress.Report((double)progressCount++ / maxProgress);
                });
            }
        }

        public void StoreTokensInDataBase(List<Token> tokens, RocksDbManager manager)
        {
            Console.WriteLine($"Sorting Tokens...");
            var tokenGroups = tokens.GroupBy(t => t.Text);
            int termCount = termToIndexMap.Count;

            Console.WriteLine($"Storing Tokens...");
            using (var progress = new ConsoleProgressBar())
            {
                int progressCount = 1;
                int maxProgress = tokenGroups.Count();
                ParallelOptions maxDegreeOfParallelism = new ParallelOptions() { MaxDegreeOfParallelism = 2 };
                Parallel.ForEach(tokenGroups, maxDegreeOfParallelism, group =>
                {
                    progress.Report((double)progressCount++ / maxProgress);
                    termToIndexMap.TryAdd(group.Key, new TermToIndexEntry(++termCount));
                    if (group.Count() > 1)
                    {
                        StringBuilder stringBuilder = new StringBuilder();
                        foreach (var token in group)
                        {
                            stringBuilder.Append(JsonSerializer.Serialize(token) + "|");
                        }
                        manager.AppendEntry(termToIndexMap[group.Key].Id, stringBuilder.ToString());
                    }
                    else
                    {
                        manager.AppendEntry(termToIndexMap[group.Key].Id, JsonSerializer.Serialize(group.First()) + "|");
                    }
                });
            }
        }

        public void FlushIndex(RocksDbManager manager)
        {           
            if (isFlushingInProgress) return;
            isFlushingInProgress = true;
            using (var progress = new ConsoleProgressBar())
            //using (var memoryCleanerTimer = new Timer(state =>
            //{
            //    MemoryManager.CleanAsync();
            //}, null, TimeSpan.Zero, TimeSpan.FromSeconds(20)))
            {
                Console.WriteLine("Flushing inverted index...");

                int progressCount = 1;
                int maxProgress = termToIndexMap.Count();

                foreach (var entry in termToIndexMap)
                {
                    entry.Value.stringBuilder.Flush(manager);
                    progress.Report((double)progressCount++ / maxProgress);
                }

                SaveTermsToJson();

                MemoryManager.CleanAsync();

                Console.WriteLine("Flushing Complete!");
                Task.Delay(TimeSpan.FromSeconds(5)).Wait();
                isFlushingInProgress = false;
            }          
        }
    }
}


