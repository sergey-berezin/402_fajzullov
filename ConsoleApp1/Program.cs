using Yolo_Sharp;
using System.Collections.Generic;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace CnslApp
{
    public class Prog {
        public static async Task Main(string[] args)
        {
            
            List<OneLine> lines_table = new();
            
            SemaphoreSlim sem_check = new SemaphoreSlim(1, 1);
            //When all - wait for all works, Select is to use for every element
            var processingTasks = args.Select(async tmp_image =>
            {
                try
                {
                    var image = Image.Load<Rgb24>(tmp_image);

                    var result = await Program_Yolo.WorkImage(image);

                    Directory.CreateDirectory("results_test");
                    var path = $"results_test/{tmp_image}";
                    await result.Image.SaveAsJpegAsync(path);

                    
                    await sem_check.WaitAsync();

                    lines_table.AddRange(result.BoxesInfo.Select(tmp_obj => new OneLine(
                        tmp_image, Convert.ToInt32(tmp_obj.Class), Convert.ToInt32(tmp_obj.XMin), Convert.ToInt32(tmp_obj.YMin),
                        Convert.ToInt32(tmp_obj.XMax - tmp_obj.XMin), Convert.ToInt32(tmp_obj.YMax - tmp_obj.YMin)
                    )));

                    sem_check.Release();
                }
                catch (Exception)
                {
                    Console.WriteLine("ERR");
                }
            });

            await Task.WhenAll(processingTasks);
            
            using (StreamWriter sw = File.CreateText("outp_table.csv"))
            {
                sw.WriteLine("Imgname,Classnum,X,Y,W,H");
                foreach (var obj in lines_table)
                {
                    sw.WriteLine($"\"{obj.imgname}\",\"{obj.classnum}\",\"{obj.X}\",\"{obj.Y}\",\"{obj.W}\",\"{obj.H}\"");
                }
            }
        }
    }
    public record OneLine(string imgname, int classnum, int X, int Y, int W, int H) {}
}
