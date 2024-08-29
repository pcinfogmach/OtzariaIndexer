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
        public void IndexDocuments(string[] documentFilePaths)
        {
            for (int i = 0; i < documentFilePaths.Count(); i++)
            {
                if (MemoryExceedsLimit())
                    FlushIndex();

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

        void IndexDocument(string filePath)
        {
            if (!filePath.ToLower().EndsWith(".txt")) return;

            Console.WriteLine($"Reading Text...");
            string text = File.ReadAllText(filePath);

            Console.WriteLine($"Tokenizing...");
            var tokens = Tokenizer_2.Tokenize(text, filePath);

            Console.WriteLine($"Storing Tokens...");
            //StoreTokensInMemory(tokens);
            StoreTokens(tokens);
        }

        public void StoreTokens(List<Token> tokens)
        {
            var tokenGroups = tokens.GroupBy(t => t.Text);
            int termCount = termToIndexMap.Count;

            using (var progress = new ConsoleProgressBar())
            {
                int progressCount = 1;
                int maxProgress = tokenGroups.Count();
                Parallel.ForEach(tokenGroups, group =>
                //foreach (var group in tokenGroups)
                {
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

        bool MemoryExceedsLimit()
        {
            const long oneGB = 1L * 1024 * 1024 * 1024;

            // Get the current memory usage of the application
            Process currentProcess = Process.GetCurrentProcess();
            long memoryUsed = currentProcess.WorkingSet64;

            return memoryUsed > oneGB;
        }


        [DllImport("kernel32.dll")]
        static extern bool SetProcessWorkingSetSize(IntPtr proc, int min, int max);
        public void FlushIndex()
        {
            Timer memoryCleanerTimer = null;
            memoryCleanerTimer = new Timer(state =>
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                SetProcessWorkingSetSize(Process.GetCurrentProcess().Handle, -1, -1);

                //Console.WriteLine("Alert: Memory cleaned!");
            }, null, TimeSpan.Zero, TimeSpan.FromSeconds(5)); // Run every 5 seconds


            Console.WriteLine("Flushing Inverted Index...");
            using (var progress = new ConsoleProgressBar())
            {
                int progressCount = 1;
                int maxProgress = termToIndexMap.Count();
                Parallel.ForEach(termToIndexMap, entry =>
                //foreach (var entry in termToIndexMap)
                {
                    entry.Value.stringBuilder.Flush();
                    progress.Report((double)progressCount++ / maxProgress);
                });

                SaveTermsToJson();
                termToIndexMap = new ConcurrenTermToIndexMap();
                LoadTermsFromJson();
            }

            memoryCleanerTimer.Dispose();
        }
    }
}


