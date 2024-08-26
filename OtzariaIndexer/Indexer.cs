using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace OtzariaIndexer
{
    public class TermToIndexMap : Dictionary<string, int> { }

    public class IndexerBase : IDisposable
    {
        public string databasePath;
        public string termsFilePath;
        protected SQLiteConnection sqliteConnection;
        protected TermToIndexMap termToIndexMap = new TermToIndexMap();

        public void Dispose()
        {
            sqliteConnection?.Dispose();
        }

        public IndexerBase()
        {
            string indexPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Index");
            if (!Directory.Exists(indexPath)) { Directory.CreateDirectory(indexPath); }
            databasePath = Path.Combine(indexPath, "InvertedIndex.sqlite");
            termsFilePath = Path.Combine(indexPath, "TermsIndex.json");

            sqliteConnection = new SQLiteConnection($"Data Source={databasePath}");
            sqliteConnection.Open();

            LoadTermsFromJson();

            // Create tables
            using (var cmd = sqliteConnection.CreateCommand())
            {
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS InvertedIndex (
                        TermId INTEGER NOT NULL,
                        DocumentData TEXT NOT NULL,
                        PRIMARY KEY (TermId)
                    );
                    
                    CREATE TABLE IF NOT EXISTS DocumentPaths (
                        DocumentId INTEGER NOT NULL,
                        DocumentPath TEXT NOT NULL,
                        PRIMARY KEY (DocumentId)
                    );

                    CREATE TABLE IF NOT EXISTS DocumentTexts (
                        DocumentId INTEGER NOT NULL,
                        DocumentText TEXT NOT NULL,
                        PRIMARY KEY (DocumentId)
                    );
                ";
                cmd.ExecuteNonQuery();
            }

            System.Windows.Application.Current.Exit += (s, e) => { SaveTermsToJson(); };
        }

        protected void LoadTermsFromJson()
        {
            if (File.Exists(termsFilePath))
            {
                var json = File.ReadAllText(termsFilePath);
                termToIndexMap = JsonSerializer.Deserialize<TermToIndexMap>(json) ?? new TermToIndexMap();
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
                string filePath = documentFilePaths[i];
                Console.WriteLine(filePath);
                Console.WriteLine(i + "\\" + documentFilePaths.Count());

                // Wrap the operations in a transaction to optimize performance
                using (var transaction = sqliteConnection.BeginTransaction())
                {
                    try
                    {
                        IndexDocument(filePath);
                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        // Rollback the transaction in case of an error
                        transaction.Rollback();
                        Console.WriteLine(ex.Message);
                    }
                }
            }

            SaveTermsToJson();  // Save the updated term dictionary to JSON
        }

        void IndexDocument(string documentFilePath)
        {
            if (!documentFilePath.ToLower().EndsWith(".txt")) return;

            int documentId = GetOrCreateDocumentId(documentFilePath);

            string text = File.ReadAllText(documentFilePath);
            StoreDocumentText(documentId, text);

            var tokens = Tokenizer.Tokenize(text, documentId);

            IndexTokens(tokens);
        }

        public int GetOrCreateDocumentId(string documentPath)
        {
            using (var cmd = sqliteConnection.CreateCommand())
            {
                // Check if the document path already exists
                cmd.CommandText = @"
                SELECT DocumentId FROM DocumentPaths WHERE DocumentPath = @documentPath;
            ";
                cmd.Parameters.AddWithValue("@documentPath", documentPath);

                var result = cmd.ExecuteScalar();
                if (result != null) return Convert.ToInt32(result); // Return the existing document ID
                else return CreateNewDocumentId(documentPath);
            }
        }

        int CreateNewDocumentId(string documentPath)
        {
            using (var idcmd = sqliteConnection.CreateCommand())
            using (var cmd = sqliteConnection.CreateCommand())
            {
                idcmd.CommandText = "SELECT IFNULL(MAX(DocumentId), -1) + 1 FROM DocumentPaths";
                int documentId = Convert.ToInt32(idcmd.ExecuteScalar()); // Corrected command object

                cmd.CommandText = @"
        INSERT OR REPLACE INTO DocumentPaths (DocumentId, DocumentPath)
        VALUES (@documentId, @documentPath);
        ";
                cmd.Parameters.AddWithValue("@documentId", documentId);
                cmd.Parameters.AddWithValue("@documentPath", documentPath);
                cmd.ExecuteNonQuery();
                return documentId;
            }
        }


        public void StoreDocumentText(int documentId, string documentText)
        {
            using (var cmd = sqliteConnection.CreateCommand())
            {
                cmd.CommandText = @"
                INSERT OR REPLACE INTO DocumentTexts (DocumentId, DocumentText)
                VALUES (@documentId, @documentText);
            ";
                cmd.Parameters.AddWithValue("@documentId", documentId);
                cmd.Parameters.AddWithValue("@documentText", documentText);
                cmd.ExecuteNonQuery();
            }
        }

        public void IndexTokens(List<Token> tokens)
        {
            foreach (var token in tokens)
            {
                string newData = JsonSerializer.Serialize(token);

                if (!termToIndexMap.TryGetValue(token.Text, out int termId))
                {
                    termId = termToIndexMap.Count;
                    termToIndexMap[token.Text] = termId;
                }

                using (var transaction = sqliteConnection.BeginTransaction())
                {
                    using (var updateCmd = sqliteConnection.CreateCommand())
                    {
                        updateCmd.CommandText = @"
        UPDATE InvertedIndex
        SET DocumentData = 
            COALESCE((SELECT DocumentData || '|' || @newData FROM InvertedIndex WHERE TermId = @termId), @newData)
        WHERE TermId = @termId;

        INSERT INTO InvertedIndex (TermId, DocumentData)
        SELECT @termId, @newData
        WHERE NOT EXISTS (SELECT 1 FROM InvertedIndex WHERE TermId = @termId);
    ";

                        updateCmd.Parameters.AddWithValue("@termId", termId);
                        updateCmd.Parameters.AddWithValue("@newData", newData);

                        updateCmd.ExecuteNonQuery();
                        updateCmd.Parameters.Clear();
                    }
                    transaction.Commit();
                }
            }
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
                .GroupBy(token => token.DocumentId);

            foreach (var group in groupedByDocument)
            {
                var documentId = group.Key;
                List<List<Token>> tokenLists = group.GroupBy(t => t.Text).Select(g => g.ToList()).ToList();
                if (tokenLists.Count < terms.Length) continue;
                
                var validResults = ProximityChecker.GetAllValidConsecutiveResults(tokenLists, 2);
                if (validResults.Count == 0) continue;

                string documentPath = GetDocumentPathById(documentId);
                string documentText = RetrieveDocumentText(documentId);
                for (int i = 0; i < validResults.Count; i++)
                {
                    results.Add(new KeyValuePair<string,string>( documentPath, SnippetGenerator.CreateSnippet(documentText, validResults[i])));
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
            using (var searchCmd = sqliteConnection.CreateCommand())
            {
                searchCmd.CommandText = @"
            SELECT DocumentData FROM InvertedIndex WHERE TermId = @termId;
        ";
                searchCmd.Parameters.AddWithValue("@termId", termId);

                using (var reader = searchCmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return reader.GetString(0);
                    }
                }
            }
            return string.Empty;
        }

        string RetrieveDocumentText(int documentId)
        {
            using (var cmd = sqliteConnection.CreateCommand())
            {
                cmd.CommandText = @"
        SELECT DocumentText 
        FROM DocumentTexts 
        WHERE DocumentId = @documentId;
        ";
                cmd.Parameters.AddWithValue("@documentId", documentId);

                var result = cmd.ExecuteScalar();
                return result != null ? result.ToString() : null;
            }
        }

        string GetDocumentPathById(int documentId)
        {
            using (var cmd = sqliteConnection.CreateCommand())
            {
                cmd.CommandText = "SELECT DocumentPath FROM DocumentPaths WHERE DocumentId = @documentId";
                cmd.Parameters.AddWithValue("@documentId", documentId);

                var result = cmd.ExecuteScalar();
                return result != null ? result.ToString() : null;
            }
        }

    }
}

