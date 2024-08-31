using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;

namespace OtzriaIndexerTextFilesOnly
{
    public class SizeLimitedStringBuilder
    {
        public StringBuilder StringBuilder;
        public bool stringBuilderBusy = false;
        //private readonly int _maxSizeInBytes = 1048576 * 10;
        //private int _currentSizeInBytes;
        public string _filePath;
        public int Id;

        public SizeLimitedStringBuilder(int id)
        {
            Id = id;
            //_currentSizeInBytes = 0;
            _filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Index", "InvertedIndex", id.ToString());
        }

        public void Append(string text)
        {
            while (stringBuilderBusy) Task.Delay(100).Wait();
            stringBuilderBusy = true;

            if (StringBuilder == null) StringBuilder = new StringBuilder();
            // Calculate the byte size of the new text and add it to the current size
            //int textSizeInBytes = Encoding.UTF8.GetByteCount(text);
            //_currentSizeInBytes += textSizeInBytes;

            // Append the text to the StringBuilder
            StringBuilder.Append(text);

            // Check if the accumulated size exceeds the maximum size
            //CheckSizeAndFlush();
            stringBuilderBusy = false;
        }

        //private void CheckSizeAndFlush()
        //{
        //    if (_currentSizeInBytes >= _maxSizeInBytes)
        //    {
        //        Flush();
        //        if (MemoryManager.MemoryExceedsLimit()) { MemoryManager.CleanAsync(); }
        //    }
        //}

        public void Flush(RocksDbManager db)
        {
            if (StringBuilder == null || StringBuilder.Length == 0)
                return;

            while (stringBuilderBusy) Task.Delay(100).Wait();
            stringBuilderBusy = true;

            string text = StringBuilder.ToString();
            StringBuilder.Clear();
            StringBuilder = null;
            //_currentSizeInBytes = 0;

            stringBuilderBusy = false;

            try
            {
                 db.AppendEntry(Id, text);
                

                //using (FileStream fileStream = new FileStream(_filePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None))
                //{
                //    // Seek to the end of the file if you want to append
                //    fileStream.Seek(0, SeekOrigin.End);

                //    using (DeflateStream deflateStream = new DeflateStream(fileStream, CompressionMode.Compress, leaveOpen: true))
                //    {
                //        using (StreamWriter writer = new StreamWriter(deflateStream, Encoding.UTF8, bufferSize: 1024, leaveOpen: true))
                //        {
                //            writer.Write(text);
                //            writer.Flush();
                //        }
                //    }
                //}
            }
            catch (OutOfMemoryException)
            {
                Task.Delay(1000).Wait();
                Flush(db);
            }
            catch (IOException ex) when (ex.Message.Contains("because it is being used by another process"))
            {
                Task.Delay(1000).Wait();
                Flush(db);
            }
        }

    }
}
