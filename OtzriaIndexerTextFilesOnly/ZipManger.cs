using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OtzriaIndexerTextFilesOnly
{
    public static class ZipManger
    {
        public static void AppendOrCreateTextFileInZip(ZipArchive archive, int id, string content)
        {
            var entry = archive.GetEntry(id + ".txt") ?? archive.CreateEntry(id + ".txt");
            using (StreamWriter writer = new StreamWriter(entry.Open()))
            {
                writer.BaseStream.Seek(0,SeekOrigin.End);
                writer.WriteLine("Appending new text to the file.");
            }
        }
    }
}