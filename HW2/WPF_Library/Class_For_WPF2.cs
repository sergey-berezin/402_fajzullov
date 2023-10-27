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

namespace ViewModel
{
    public interface IUIServices
    {
        List<string> GetFiles(string folderName, string format);
        string? FindFOlder();
        void ReportError(string message);
    }

    public class MainCommand : ICommand
    {
        private readonly Action<object> execute;
        private readonly Func<object, bool> canExecute;

        public MainCommand(Action<object> execute, Func<object, bool> canExecute = null)
        {
            this.execute = execute;
            this.canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object parameter) => canExecute == null ? true : canExecute(parameter);

        public void Execute(object parameter) => execute?.Invoke(parameter);
    }

    public abstract class ViewModelBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void RaisePropertyChanged([CallerMemberName] String propertyName = "") =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public class ImageDetect
    {

        private ObjectBox Box { get; set; }
        private Image<Rgb24> firstImage { get; }
        private WeakReference ImageRefSe { get; set; }

        public ImageDetect(DetectedItem segmentedObject)
        {
            Box = segmentedObject.BBoxInfo;
            firstImage = segmentedObject.OriginalImage;
            ImageRefSe = new WeakReference(null);
            Image = ImageToBitmapSource(segmentedObject.BoundingBox);
            Class = segmentedObject.Label;
            Percentage = segmentedObject.Confidence;
        }

        public BitmapSource SelectedImage
        {
            get
            {
                var selectedImage = ImageRefSe.Target;
                if (selectedImage == null)
                {
                    var mainImage = ImageToBitmapSource(ImageProcessing.AnnotateBoundingBox(firstImage, Box));
                    ImageRefSe = new WeakReference(mainImage);
                    return mainImage;
                }
                else
                {
                    return (BitmapSource)selectedImage;
                }
            }
        }
        public BitmapSource Image { get; }
        public string Class { get; set; }
        public double Percentage { get; set; }

        private BitmapSource ImageToBitmapSource(Image<Rgb24> image)
        {
            byte[] pixels = new byte[image.Width * image.Height * Unsafe.SizeOf<Rgb24>()];
            image.CopyPixelDataTo(pixels);

            return BitmapFrame.Create(image.Width, image.Height, 96, 96, PixelFormats.Rgb24, null, pixels, 3 * image.Width);
        }
    }

    public class MainViewModel : ViewModelBase
    {
        private string SelFolder { get; set; } = string.Empty;
        private bool CheckActi { get; set; } = false;
        private CancellationTokenSource cts { get; set; }
        public string SelectedFolder
        {
            get => SelFolder;
            set
            {
                if (value != null && value != SelFolder)
                {
                    SelFolder = value;
                    RaisePropertyChanged(nameof(SelectedFolder));
                }
            }
        }
        public List<ImageDetect> DetectedImages { get; private set; }
      

        private readonly IUIServices Serv;

        #region COMMANDS
        private void OnSelectFolder(object arg)
        {
            string? folderName = Serv.FindFOlder();
            if (folderName == null) { return; }
            SelectedFolder = folderName;
        }
        public async Task OnRunModel(object arg)
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
                    DetectedImages = DetectedImages.Concat(
                        detectedObjects.Select(x => new ImageDetect(x))
                    ).ToList();
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
        }
        public void OnRequestCancellation(object arg)
        {
            cts.Cancel();
        }
        public ICommand SelectFolderCommand { get; private set; }
        public ICommand RunModelCommand { get; private set; }
        public ICommand RequestCancellationCommand { get; private set; }
        #endregion

        public MainViewModel(IUIServices uiServices)
        {
            SelectedFolder = string.Empty;
            DetectedImages = new List<ImageDetect>();

            this.Serv = uiServices;

            SelectFolderCommand = new MainCommand(OnSelectFolder, x => !CheckActi);
            RunModelCommand = new AsyncRelayCommand(OnRunModel, x => SelectedFolder != string.Empty && !CheckActi);
            RequestCancellationCommand = new MainCommand(OnRequestCancellation, x => CheckActi);
        }
    }
}
