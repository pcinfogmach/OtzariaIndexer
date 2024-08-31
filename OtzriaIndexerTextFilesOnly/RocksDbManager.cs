using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using RocksDbSharp;

public class RocksDbManager : IDisposable
{
    private readonly RocksDb _db;

    public RocksDbManager(string dbPath)
    {
        _db = RocksDb.Open(new DbOptions().SetCreateIfMissing(true), dbPath);
    }

    public void AppendEntry(int key, string value)
    {
        // Convert the int key to byte array
        byte[] keyBytes = BitConverter.GetBytes(key);

        // Retrieve existing value if it exists
        byte[] existingCompressedValue = _db.Get(keyBytes);
        string existingValue = string.Empty;

        if (existingCompressedValue != null)
        {
            // Decompress the existing value
            using (var memoryStream = new MemoryStream(existingCompressedValue))
            using (var deflateStream = new DeflateStream(memoryStream, CompressionMode.Decompress))
            using (var reader = new StreamReader(deflateStream, Encoding.UTF8))
            {
                existingValue = reader.ReadToEnd();
            }
        }

        // Concatenate the existing value with the new value
        string newValue = existingValue + value;

        // Compress the new concatenated value
        byte[] compressedValue;
        using (var memoryStream = new MemoryStream())
        {
            using (var deflateStream = new DeflateStream(memoryStream, CompressionMode.Compress, true))
            using (var writer = new StreamWriter(deflateStream, Encoding.UTF8))
            {
                writer.Write(newValue);
            }
            compressedValue = memoryStream.ToArray();
        }

        // Store the compressed data
        _db.Put(keyBytes, compressedValue);
    }

    public string GetEntry(int key)
    {
        // Convert the int key to byte array
        byte[] keyBytes = BitConverter.GetBytes(key);

        // Retrieve the compressed data
        byte[] compressedValue = _db.Get(keyBytes);
        if (compressedValue == null)
            return null;

        // Decompress the value
        using (var memoryStream = new MemoryStream(compressedValue))
        using (var deflateStream = new DeflateStream(memoryStream, CompressionMode.Decompress))
        using (var reader = new StreamReader(deflateStream, Encoding.UTF8))
        {
            return reader.ReadToEnd();
        }
    }

    public void Dispose()
    {
        _db?.Dispose();
    }
}
