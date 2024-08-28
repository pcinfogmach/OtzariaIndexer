using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;

namespace OtzriaIndexerTextFilesOnly
{
    public class SizeLimitedStringBuilder
    {
        private readonly StringBuilder _stringBuilder;
        private readonly int _maxSizeInBytes = 1048576 * 50;
        private readonly string _filePath;
        private int _currentSizeInBytes;
        int Id;

        public SizeLimitedStringBuilder(int id)
        {
            Id = id;
            _stringBuilder = new StringBuilder();
            _currentSizeInBytes = 0;
            _filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Index", "InvertedIndex", id + ".zip");
        }

        public void Append(string text)
        {
            // Calculate the byte size of the new text and add it to the current size
            int textSizeInBytes = Encoding.UTF8.GetByteCount(text);
            _currentSizeInBytes += textSizeInBytes;

            // Append the text to the StringBuilder
            _stringBuilder.Append(text);

            // Check if the accumulated size exceeds the maximum size
            CheckSizeAndFlush();
        }

        private void CheckSizeAndFlush()
        {
            if (_currentSizeInBytes >= _maxSizeInBytes)
            {
                Flush();
            }
        }

        public void Flush()
        {
            if (_stringBuilder.Length < 0) return;
            try
            {
                using (ZipArchive zipArchive = ZipFile.Open(_filePath, ZipArchiveMode.Update))
                {
                    var entry = zipArchive.GetEntry($"{Id}.txt") ?? zipArchive.CreateEntry($"{Id}.txt");

                    using (Stream stream = entry.Open())
                    {
                        // Seek to the end of the stream if you want to append
                        stream.Seek(0, SeekOrigin.End);

                        using (StreamWriter writer = new StreamWriter(stream, Encoding.UTF8, bufferSize: 1024, leaveOpen: true))
                        {
                            writer.Write(_stringBuilder.ToString());
                            writer.Flush();
                        }
                    }
                }
                // Clear the StringBuilder after flushing and reset the current size
                _stringBuilder.Clear();
                _currentSizeInBytes = 0;
            }
            catch (IOException ex) when (ex.Message.Contains("because it is being used by another process"))
            {
                Task.Delay(1000);
                Flush();
            }
        }
    }
}
