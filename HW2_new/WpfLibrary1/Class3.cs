using System;
using System.Collections.Generic;
using System.Windows.Input;
using ImgProcLib;
using Yolo_Sharp;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Threading.Tasks;
using AsyncCommand;
using System.Threading;
using System.Linq;
using System.Windows.Media.Imaging;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.ComponentModel;
using System.Windows;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Microsoft.Windows.Themes;

namespace ViewModel
{
    public interface IUIServices
    {
        List<string> GetFiles(string folderName, string format);
        string? FindFOlder();
        void ReportError(string message);
    }

    public class MainCommand : ICommand   // для команд интерфейса
    {
        private readonly Action<object> execute;           // предоставляем метод
        private readonly Func<object, bool> canExecute;    // предоставляем условие, при котором команда будет выполнена 

        public MainCommand(Action<object> execute, Func<object, bool> canExecute = null)
        {
            this.execute = execute;
            this.canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged   // событие уведомляет систему WPF о том, что результат вызова CanExecute может измениться
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object parameter)   // проверка на возомдность выполнения
        {
            if (canExecute != null)
            {
                return canExecute(parameter);
            }
            return true;
        }

        public void Execute(object parameter)  //  выполнение команды
        {
            if (execute != null)
            {
                execute(parameter);
            }
        }
    }

    public abstract class ViewModelBase : INotifyPropertyChanged    // уведомление об изменении свойств объектов
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void RaisePropertyChanged([CallerMemberName] String propertyName = "") =>      // вызывается при измении объектов, для уведомления об этом
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }


    public class ClassCount
    {
        public string ClassName { get; set; }
        public int Count { get; set; }
    }

    public class ImageDetect       // предоставляет инфу об обноруженном объекте на изображении
    {

        public ImageDetect(DetectedItem segmentedObject)   // конструктор, принимающий на вход информацию об объекте
        {
            Image = ImageToBitmapSource(segmentedObject.OriginalImage);
            Class = segmentedObject.Label;
        }

        
        public BitmapSource Image { get; }   // Изображение объекта в формате BitmapSource
        public string Class { get; set; }   //  Класс обнаруженного объекта
       

        private BitmapSource ImageToBitmapSource(Image<Rgb24> image)
        {
            int width = image.Width;
            int height = image.Height;
            byte[] pixels = new byte[width * height * Unsafe.SizeOf<Rgb24>()];  
            image.CopyPixelDataTo(pixels);   // копируем в массив пикселей

            WriteableBitmap bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Rgb24, null);
            bitmap.WritePixels(new Int32Rect(0, 0, width, height), pixels, 3 * width, 0);

            return bitmap;
        }
    }

    public class ObservableCollectionEx<T> : ObservableCollection<T>
    {
        public ObservableCollectionEx()
        {
        }
        public void AddRange(IEnumerable<T> items)
        {
            foreach (var item in items.ToList())
            {
                Add(item);
            }
        }
    }

    public class MainViewModel : ViewModelBase    // Основная модельЮ для главного окна WPF
    {
        private ObservableCollection<ClassCount> classCounts;
        public ObservableCollection<ClassCount> ClassCounts
        {
            get { return classCounts; }
            set
            {
                classCounts = value;
                RaisePropertyChanged(nameof(ClassCounts));
            }
        }

        private string SelFolder { get; set; } = string.Empty;  // Свойства выбранной папки
        private bool CheckActi { get; set; } = false;   // Флаг активности
        private CancellationTokenSource cts { get; set; }   // Отмена операции


        public string SelectedFolder        
        {
            get => SelFolder;
            set
            {
                if (value != null && value != SelFolder)
                {
                    SelFolder = value;
                }
            }
        }
        public ObservableCollectionEx<ImageDetect> DetectedImages { get; private set; } = new ObservableCollectionEx<ImageDetect>();


        public ObservableCollection<ImageDetect> OriginalDetectedImages { get; private set; } = new ObservableCollection<ImageDetect>();



        public Dictionary<string, int> classCountsDict = new Dictionary<string, int>();


        private readonly IUIServices Serv;   // методы для взаимодействия с пользовательским интерфейсом

        private void OnSelectFolder(object arg)       // Открывает диалоговое окно для выбора папки
        {
            string? folderName = Serv.FindFOlder();
            if (folderName == null) { return; }
            SelectedFolder = folderName;
        }
        public async Task OnRunModel(object arg)    // Запускает модель обработки изображений асинхронн
        {
            DetectedImages.Clear();
            RaisePropertyChanged(nameof(DetectedImages));
            try
            {
                CheckActi = true;
                cts = new CancellationTokenSource();

                List<string> fileNames = Serv.GetFiles(SelectedFolder, ".jpg");
                if (fileNames.Count == 0)
                {
                    Serv.ReportError("ERROR");
                    return;
                }
                var tmp_work = fileNames.Select(arg =>
                    Task.Run(() => ImageProcessing.ProcessImageDetection(arg, cts.Token))).ToList();

                while (tmp_work.Any())
                {
                    var work_one = await Task.WhenAny(tmp_work);
                    var detectedObjects = work_one.Result.ToList();
                    tmp_work.Remove(work_one);
                    DetectedImages.AddRange(detectedObjects.Select(x => new ImageDetect(x)));
                    RaisePropertyChanged(nameof(DetectedImages));
                }
            }
            catch (Exception)
            {
                Serv.ReportError("ERROR");
            }
            finally
            {
                CheckActi = false;
            }
            
            classCountsDict.Clear();

            try
            {
                foreach (var imageDetect in DetectedImages)
                {
                    string className = imageDetect.Class;

                    if (classCountsDict.ContainsKey(className))
                    {
                        classCountsDict[className]++;
                    }
                    else
                    {
                        classCountsDict[className] = 1;
                    }
                }

                // Преобразовать результат в список ClassCount
                List<ClassCount> classCountsList = new List<ClassCount>();
                foreach (var kvp in classCountsDict)
                {
                    classCountsList.Add(new ClassCount { ClassName = kvp.Key, Count = kvp.Value });
                }

                // Добавить "Все объекты"
                int totalObjectsCount = DetectedImages.Count;
                classCountsList.Add(new ClassCount { ClassName = "Все объекты", Count = totalObjectsCount });

                ClassCounts = new ObservableCollection<ClassCount>(classCountsList);
                RaisePropertyChanged(nameof(ClassCounts));

            }
            catch (Exception)
            {
                Serv.ReportError("ERROR");
            }
            finally
            {
                CheckActi = false;
                OriginalDetectedImages = new ObservableCollection<ImageDetect>(DetectedImages.ToList());
            }


        }

        private ClassCount selectedClass;
        public ClassCount SelectedClass
        {
            get { return selectedClass; }
            set
            {
                if (selectedClass != value)
                {
                    selectedClass = value;
                    RaisePropertyChanged(nameof(SelectedClass));
                    UpdateImages();
                }
            }
        }

        public ObservableCollectionEx<ImageDetect> SelectedImg
        {
            get { return DetectedImages; }
            set
            {
                if (DetectedImages != value)
                {
                    DetectedImages = value;
                    RaisePropertyChanged(nameof(SelectedImg));
                }
            }
        }

        private void UpdateImages()
        {

            DetectedImages.Clear();
            ObservableCollectionEx<ImageDetect> filteredImages;

            if (string.IsNullOrEmpty(SelectedClass.ClassName) || SelectedClass.ClassName == "Все объекты")
            {
                DetectedImages.AddRange(OriginalDetectedImages);

            }
            else
            {
                DetectedImages.AddRange(OriginalDetectedImages.Where(img => img.Class == SelectedClass.ClassName));
            }
            RaisePropertyChanged(nameof(DetectedImages));
        }

        public void OnRequestCancellation(object arg)      // Запрашивает отмену операции обработки 
        {
            cts.Cancel();
        }
        public ICommand SelectFolderCommand { get; private set; }
        public ICommand RunModelCommand { get; private set; }
        public ICommand RequestCancellationCommand { get; private set; }

        public MainViewModel(IUIServices uiServices)
        {
            SelectedFolder = string.Empty;
            
            this.Serv = uiServices;

            SelectFolderCommand = new MainCommand(OnSelectFolder, x => !CheckActi);
            RunModelCommand = new AsyncRelayCommand(OnRunModel, x => SelectedFolder != string.Empty && !CheckActi);
            RequestCancellationCommand = new MainCommand(OnRequestCancellation, x => CheckActi);
        }
    }
}
