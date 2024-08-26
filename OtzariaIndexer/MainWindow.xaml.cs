﻿using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.IO;
using System.Windows;

namespace OtzariaIndexer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private IndexSearcher searchApp = new IndexSearcher();
        public MainWindow()
        {
            InitializeComponent();
        }

        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            string query = SearchBox.Text;
            //int snippetLength = 100; // Adjust the snippet length as needed
            var results = searchApp.Search(query, 3);
            ResultsListBox.ItemsSource = results;
        }

        private void IndexFilesButton_Click(object sender, RoutedEventArgs e)
        {
            CommonOpenFileDialog dialog = new CommonOpenFileDialog { IsFolderPicker = true };
            if (dialog.ShowDialog() != CommonFileDialogResult.Ok) return;
            string[] selectedFiles = Directory.GetFiles(dialog.FileName, "*.*", SearchOption.AllDirectories);
            searchApp.IndexDocuments(selectedFiles);
            Console.WriteLine("Files indexed successfully!");
        }
    }
}
