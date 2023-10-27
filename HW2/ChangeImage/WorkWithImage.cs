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

        private static Image<Rgb24> CropBoundingImage(Image<Rgb24> firstimg, ObjectBox boundingBox)
        {
            int w = (int)(boundingBox.XMax - boundingBox.XMin);
            int x = (int)boundingBox.XMin, y = (int)boundingBox.YMin;
            int h = (int)(boundingBox.YMax - boundingBox.YMin);
            if (x < 0) { w += x; x = 0; }
            if (y < 0) { h += y; y = 0; }
            if (x + w > outp) w = outp - x;
            if (y + h > outp) h = outp - y;
            if (x > outp || y > outp) return firstimg.Clone(i => i.Resize(ChangeSize));

            return firstimg.Clone(
                i => i.Resize(ChangeSize).Crop(new SixLabors.ImageSharp.Rectangle(x, y, w, h)));
        }

        public static async Task<IEnumerable<DetectedItem>> ProcessImageDetection(string filename, CancellationToken token)
        {
            var inpimg = SixLabors.ImageSharp.Image.Load<Rgb24>(filename);
            var Tasks = Program_Yolo.WorkImageAsync(inpimg, token);
            List<DetectedItem> detectedItems = new();

            await Tasks;
            var boundingBoxes = Tasks.Result.BoxesInfo;
            var croppedImages = await Task.WhenAll(
                boundingBoxes.Select(boundingBox => Task.Run(
                    () => CropBoundingImage(inpimg, boundingBox), token)));

            return croppedImages.Zip(boundingBoxes).Select(
                pair => new DetectedItem(
                    Categories[pair.Second.Class],
                    inpimg,
                    pair.Second.Confidence,
                    pair.First,
                    pair.Second
                )
           );
        }
        public static Image<Rgb24> AnnotateBoundingBox(Image<Rgb24> target, ObjectBox boundingBox)
        {
            int maxDimension = Math.Max(target.Width, target.Height);
            float scale = (float)maxDimension / outp;
            return target.Clone(context =>
            {
                context.Resize(new ResizeOptions { Size = new SixLabors.ImageSharp.Size(maxDimension, maxDimension), Mode = ResizeMode.Pad }).DrawPolygon(
                    Pens.Solid(SixLabors.ImageSharp.Color.Red, 1 + maxDimension / outp),
                    new SixLabors.ImageSharp.PointF[] {
                        new SixLabors.ImageSharp.PointF((float)boundingBox.XMin * scale, (float) boundingBox.YMin * scale),
                        new SixLabors.ImageSharp.PointF((float)boundingBox.XMin * scale, (float) boundingBox.YMax * scale),
                        new SixLabors.ImageSharp.PointF((float)boundingBox.XMax * scale, (float) boundingBox.YMax * scale),
                        new SixLabors.ImageSharp.PointF((float) boundingBox.XMax * scale, (float) boundingBox.YMin * scale)
                    });
            });
        }
    }

    public record DetectedItem(string Label, Image<Rgb24> OriginalImage, double Confidence, Image<Rgb24> BoundingBox, ObjectBox BBoxInfo);

    public record BoundingBoxInfo(string ImageName, int ClassNumber, int X, int Y, int Width, int Height) { }
}
