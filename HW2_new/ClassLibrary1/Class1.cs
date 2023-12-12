using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.Drawing;
using Yolo_Sharp;
using static System.Net.Mime.MediaTypeNames;

namespace ImgProcLib
{

    public class ImageProcessing
    {
        public static readonly string[] Categories = new string[] {
            "aeroplane", "bicycle", "bird", "boat", "bottle",
                "bus", "car", "cat", "chair", "cow",
                "diningtable", "dog", "horse", "motorbike", "person",
                "pottedplant", "sheep", "sofa", "train", "tvmonitor"
        };

        private const int outp = 416;

        private static readonly ResizeOptions ChangeSize = new ResizeOptions
        {
            Size = new SixLabors.ImageSharp.Size(outp, outp),
            Mode = ResizeMode.Pad
        };
        public static async Task<IEnumerable<DetectedItem>> ProcessImageDetection(string filename, CancellationToken token)
        {
            var inpimg = SixLabors.ImageSharp.Image.Load<Rgb24>(filename);
            var Tasks = Program_Yolo.WorkImageAsync(inpimg, token);
            List<DetectedItem> detectedItems = new();

            await Tasks;
            var boundingBoxes = Tasks.Result.BoxesInfo;
            

            return boundingBoxes.Select(
                pair => new DetectedItem(
                    Categories[pair.Class],
                    inpimg
                )
           );
        }
    }

    public record DetectedItem(string Label, Image<Rgb24> OriginalImage);
    
    public record ClassCount(string ClassName, int Count);

    public record BoundingBoxInfo(string ImageName, int ClassNumber, int X, int Y, int Width, int Height) { }
}
