using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MapRender.Invoker;
using Encoder = System.Drawing.Imaging.Encoder;

namespace MapleStory.Sampler
{
    public class Sampler
    {
        private const long JPEG_RATIO = 90L;
        private const double ITEM_PARTIAL_AREA_THRESHOLD = 0.70;
        private readonly MapRenderInvoker _renderInvoker;

        public Sampler(MapRenderInvoker renderInvoker)
        {
            _renderInvoker = renderInvoker;
            if (!renderInvoker.IsRunning)
            {
                throw new ArgumentException("Sampler must have a lunched render!");
            }
        }

        public Sample SampleSingle()
        {
            MemoryStream stream = new MemoryStream();
            var screenShotData = _renderInvoker.TakeScreenShot(stream);
            stream = EncodeScreenShot(stream);
            var items = FilterTargetsInCamera(screenShotData);
            int width = screenShotData.CameraRectangle.Width;
            int height = screenShotData.CameraRectangle.Height;
            return new Sample(stream, items, width, height);
        }

        /// <summary>
        /// Sample all based on provided step
        /// </summary>
        /// <param name="xStep">step in X to sample</param>
        /// <param name="yStep">step in Y to sample</param>
        /// <param name="writer">Writer to save result</param>
        /// <param name="interval">Sampling time interval, in ms.</param>
        public void SampleAll(int xStep, int yStep, IDatasetWriter writer, int interval = 0)
        {
            xStep = Math.Abs(xStep);
            yStep = Math.Abs(yStep);
            int initX = _renderInvoker.WorldOriginX + _renderInvoker.ScreenWidth / 2;
            int initY = _renderInvoker.WorldOriginY + _renderInvoker.ScreenHeight / 2;
            int endX = _renderInvoker.WorldOriginX + _renderInvoker.WorldWidth - _renderInvoker.ScreenWidth / 2;
            int endY = _renderInvoker.WorldOriginY + _renderInvoker.WorldHeight - _renderInvoker.ScreenHeight / 2;

            for (int x = initX; x < endX; x += xStep)
            {
                for (int y = initY; y < endY; y += yStep)
                {
                    Console.WriteLine($"Sampling at center x={x},y={y}....");
                    _renderInvoker.MoveCamera(x, y);
                    Sample sample = SampleSingle();
                    Console.WriteLine($"Writing {sample.Guid.ToString()} to TfRecord...");
                    writer.Write(sample);
                    Console.WriteLine("Done writing.");
                    Thread.Sleep(interval);
                }
            }
            return;
        }

        private MemoryStream EncodeScreenShot(Stream screenShotStream)
        {
            Bitmap source = new Bitmap(screenShotStream);
            Bitmap result = new Bitmap(_renderInvoker.ScreenWidth, _renderInvoker.ScreenHeight);
            Rectangle rectangle = new Rectangle(Point.Empty, source.Size);
            using (Graphics graphics = Graphics.FromImage(result))
            {
                graphics.Clear(Color.Black);
                graphics.DrawImageUnscaledAndClipped(source, rectangle);
            }
            MemoryStream ret = new MemoryStream();
            ImageCodecInfo jpegCodecInfo = ImageCodecInfo.GetImageEncoders().First(i => i.MimeType == "image/jpeg");
            using (EncoderParameters parameters = new EncoderParameters(1))
            {
                EncoderParameter parameter = new EncoderParameter(Encoder.Quality, JPEG_RATIO);
                parameters.Param[0] = parameter;
                result.Save(ret, jpegCodecInfo, parameters);
            }

            return ret;
        }

        private static List<TargetItem> FilterTargetsInCamera(ScreenShotData data)
        {
            List<TargetItem> ret = new List<TargetItem>();
            List<TargetItem> source = data.Items;
            Rectangle camRectangle = data.CameraRectangle;
            source.ForEach(i =>
            {
                i.X -= camRectangle.X;
                i.Y -= camRectangle.Y;

                double itemArea = i.Height * i.Width;
                int inCameraWidth = (i.X < 0 ? i.X + i.Width : i.Width) % camRectangle.Width;
                int inCameraHeight = (i.Y < 0 ? i.Y + i.Height : i.Height) % camRectangle.Height;
                inCameraWidth = (inCameraWidth + i.X) > camRectangle.Width ? (camRectangle.Width - i.X) : inCameraWidth;
                inCameraHeight = (inCameraHeight + i.Y) > camRectangle.Height ? (camRectangle.Height - i.Y) : inCameraHeight;
                double inCameraArea = inCameraHeight * inCameraWidth;

                if (inCameraArea / itemArea >= ITEM_PARTIAL_AREA_THRESHOLD
                    && i.X < camRectangle.Width
                    && i.Y < camRectangle.Height)
                {
                    i.Height = inCameraHeight;
                    i.Width = inCameraWidth;
                    i.X = i.X < 0 ? 0 : i.X;
                    i.Y = i.Y < 0 ? 0 : i.Y;
                    ret.Add(i);
                }
            });
            return ret;
        }


    }
}
