using ICSharpCode.SharpZipLib.Tar;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TarArchiveTest
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            // Prompt the user to choose the folder containing ZIP files
            string zipDirectory = ChooseFolder();
            if (string.IsNullOrEmpty(zipDirectory))
            {
                Console.WriteLine("No folder selected. Exiting...");
                return;
            }

            string tarFileName = Path.Combine(zipDirectory, "Archive.tar");

            using (FileStream tarFileStream = new FileStream(tarFileName, FileMode.Create, FileAccess.Write, FileShare.None, 8192, FileOptions.SequentialScan))
            using (TarOutputStream tarOutputStream = new TarOutputStream(tarFileStream, Encoding.UTF8))
            {
                tarOutputStream.IsStreamOwner = false; // Prevent closing the underlying stream

                foreach (string zipFilePath in Directory.GetFiles(zipDirectory, "*.zip"))
                {
                    TarEntry tarEntry = TarEntry.CreateEntryFromFile(zipFilePath);
                    tarOutputStream.PutNextEntry(tarEntry);

                    // Efficiently stream the file into the TAR archive
                    using (FileStream zipFileStream = File.OpenRead(zipFilePath))
                    {
                        zipFileStream.CopyTo(tarOutputStream, 8192); // Use a buffer to reduce I/O operations
                    }

                    tarOutputStream.CloseEntry();
                }
            }

            Console.WriteLine($"ZIP files from {zipDirectory} have been archived into {tarFileName}");
        }

        // Method to prompt the user to choose a folder
        static string ChooseFolder()
        {
            using (FolderBrowserDialog folderDialog = new FolderBrowserDialog())
            {
                folderDialog.Description = "Select the folder containing the ZIP files to archive";
                folderDialog.ShowNewFolderButton = false;

                DialogResult result = folderDialog.ShowDialog();
                if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(folderDialog.SelectedPath))
                {
                    return folderDialog.SelectedPath;
                }
            }
            return null;
        }
    }
}
