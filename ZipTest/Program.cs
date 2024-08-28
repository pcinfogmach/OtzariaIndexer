using ICSharpCode.SharpZipLib.Zip;
using System;
using System.IO;
using System.Text;

namespace ZipTest
{
    class Program
    {
        static void Main()
        {
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string zipPath = Path.Combine(desktopPath, "example-sharpziplib.zip");

            // Create a ZIP file with some files
            using (FileStream fsOut = File.Create(zipPath))
            using (ZipOutputStream zipStream = new ZipOutputStream(fsOut))
            {
                zipStream.SetLevel(9); // Compression level 0-9 (0 = store only, 9 = best compression)

                AddFileToZip(zipStream, "file1.txt", "This is the content of file1.");
                AddFileToZip(zipStream, "file2.txt", "This is the content of file2.");
                AddFileToZip(zipStream, "file3.txt", "This is the content of file3.");
            }

            Console.WriteLine("ZIP file created on the desktop using SharpZipLib.");

            // Append a line to file1.txt within the ZIP archive
            AppendLineToFileInZip(zipPath, "file1.txt", "This is an appended line.");
            Console.WriteLine("Line appended to file1.txt in the ZIP archive.");
        }

        static void AddFileToZip(ZipOutputStream zipStream, string fileName, string content)
        {
            ZipEntry newEntry = new ZipEntry(fileName);
            zipStream.PutNextEntry(newEntry);

            byte[] contentBytes = Encoding.UTF8.GetBytes(content);
            zipStream.Write(contentBytes, 0, contentBytes.Length);

            zipStream.CloseEntry();
        }

        static void AppendLineToFileInZip(string zipPath, string fileName, string lineToAppend)
        {
            // Temporary directory to extract the files
            string tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempDir);

            try
            {
                // Extract all files from the ZIP archive
                using (ZipFile zipFile = new ZipFile(zipPath))
                {
                    foreach (ZipEntry entry in zipFile)
                    {
                        string entryFileName = Path.Combine(tempDir, entry.Name);
                        string entryDirectory = Path.GetDirectoryName(entryFileName);

                        if (!Directory.Exists(entryDirectory))
                        {
                            Directory.CreateDirectory(entryDirectory);
                        }

                        using (Stream zipStream = zipFile.GetInputStream(entry))
                        using (FileStream fileStream = File.Create(entryFileName))
                        {
                            zipStream.CopyTo(fileStream);
                        }
                    }
                }

                // Modify the specified file
                string filePathToModify = Path.Combine(tempDir, fileName);
                if (File.Exists(filePathToModify))
                {
                    File.AppendAllText(filePathToModify, Environment.NewLine + lineToAppend);
                }
                else
                {
                    throw new FileNotFoundException("The file to be modified was not found in the extracted files.");
                }

                // Create a new ZIP file with the modified files
                string tempZipPath = Path.Combine(tempDir, "temp.zip");
                using (FileStream fsOut = File.Create(tempZipPath))
                using (ZipOutputStream zipStream = new ZipOutputStream(fsOut))
                {
                    foreach (string file in Directory.GetFiles(tempDir, "*", SearchOption.AllDirectories))
                    {
                        string entryName = file.Substring(tempDir.Length + 1).Replace('\\', '/');
                        AddFileToZip(zipStream, entryName, File.ReadAllText(file));
                    }
                }

                // Replace the original ZIP file with the new one
                File.Delete(zipPath);
                File.Move(tempZipPath, zipPath);
            }
            finally
            {
                // Clean up temporary files
                Directory.Delete(tempDir, true);
            }
        }
    }
}
