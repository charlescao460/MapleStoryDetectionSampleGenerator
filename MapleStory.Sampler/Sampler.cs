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

        public delegate void SamplePostProcessor(Sample sample, EventArgs e);

        public event SamplePostProcessor OnSampleCaptured;

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
            var items = FilterTargetsInCamera(screenShotData);
            int width = screenShotData.CameraRectangle.Width;
            int height = screenShotData.CameraRectangle.Height;
            Sample ret = new Sample(stream, items, width, height);
            OnSampleCaptured?.Invoke(ret, null);
            ret.ImageStream = EncodeScreenShot(ret.ImageStream);
            return ret;
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

            int count = 0;
            int total = (int)(Math.Round((double)(endX - initX) / xStep, MidpointRounding.ToPositiveInfinity) *
                         Math.Round((double)(endY - initY) / yStep, MidpointRounding.ToPositiveInfinity));

            for (int x = initX; x < endX; x += xStep)
            {
                for (int y = initY; y < endY; y += yStep)
                {
                    Console.WriteLine($"Sampling at center x={x},y={y}....");
                    _renderInvoker.MoveCamera(x, y);
                    Sample sample = SampleSingle();
                    Console.WriteLine($"Writing {sample.Guid.ToString()} to dataset...");
                    writer.Write(sample);
                    count++;
                    Console.WriteLine($"Done writing. Progress: {count}/{total}, {(double)count / total * 100}%\n");
                    Thread.Sleep(interval);
                }
            }
            Console.WriteLine("############## All Samples Captured ##############");
            return;
        }

        private MemoryStream EncodeScreenShot(Stream screenShotStream)
        {
            Bitmap source = new Bitmap(screenShotStream);
            Bitmap result = new Bitmap(_renderInvoker.ScreenWidth, _renderInvoker.ScreenHeight);
            Rectangle rectangle = new Rectangle(Point.Empty, source.Size);
            using (Graphics graphics = Graphics.FromImage(result))
            {
                // Dark background, to back alpha channel.
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
            int imgWidth = camRectangle.Width;
            int imgHeight = camRectangle.Height;
            source.ForEach(i =>
            {
                i.X -= camRectangle.X;
                i.Y -= camRectangle.Y;

                // Validation
                if (i.Height < 0 || i.Width < 0)
                {
                    throw new InvalidDataException("Items Height or Width is negative!!");
                }

                // Not show in screenshots at all
                if (i.X > imgWidth || i.Y > imgHeight)
                {
                    return;
                }
                int inCameraWidth = i.Width;
                int inCameraHeight = i.Height;
                double itemArea = i.Width * i.Height;

                // Partial in X - left
                if (i.X < 0)
                {
                    inCameraWidth = i.X + i.Width;
                    if (inCameraWidth < 0)
                    {
                        return;
                    }
                    i.X = 0;
                }

                // Partial in X - right
                if (i.X + i.Width > imgWidth)
                {
                    inCameraWidth = imgWidth - i.X;
                }

                // Partial in Y - up
                if (i.Y < 0)
                {
                    inCameraHeight = i.Y + i.Height;
                    if (inCameraHeight < 0)
                    {
                        return;
                    }
                    i.Y = 0;
                }

                // Partial in Y - bottom
                if (i.Y + i.Height > imgHeight)
                {
                    inCameraHeight = imgHeight - i.Y;
                }

                double inCameraArea = inCameraHeight * imgWidth;
                if (inCameraArea / itemArea >= ITEM_PARTIAL_AREA_THRESHOLD)
                {
                    i.Width = inCameraWidth;
                    i.Height = inCameraHeight;
                    ret.Add(i);
                }
            });
            return ret;
        }


    }
}
