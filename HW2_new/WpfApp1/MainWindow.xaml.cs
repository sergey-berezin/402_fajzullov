using Microsoft.Win32;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Ookii.Dialogs.Wpf;
using ViewModel;

namespace MainUserInterface
{
    public partial class ViewWindow : Window, IUIServices
    {
        public ViewWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel(this);
        }

        public string? FindFOlder()
        {
            VistaFolderBrowserDialog dialog = new VistaFolderBrowserDialog();
            if (dialog.ShowDialog() == true)   // если выбирает папку, то возвращает путь
            {
                return dialog.SelectedPath;
            }
            return null;
        }
        public List<string> GetFiles(string tmpName, string sep = ".jpg")
        {
            var findedFiles = new List<string>();
            try
            {
                foreach (var tmp in Directory.EnumerateFiles(tmpName))   // перечисление файлов в директории
                {
                    if (tmp.EndsWith(sep))
                    {
                        findedFiles.Add(tmp);
                    }
                }
            }
            catch (Exception )
            {
                ReportError("ERROR");
            }
            return findedFiles;
        }
        public void ReportError(string message)
        {
            MessageBox.Show(message);
        }
    }
}
