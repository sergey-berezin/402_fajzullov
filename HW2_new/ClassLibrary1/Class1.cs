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
            "item1", "item2", "item3", "item4", "item5",
            "item6", "item7", "item8", "item9", "item10",
            "item11", "item12", "item13", "item14", "item15",
            "item16", "item17", "item18", "item19", "item20"
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
