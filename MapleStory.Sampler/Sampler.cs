using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Encoder = System.Drawing.Imaging.Encoder;

namespace MapRender.Invoker
{
    public class Sampler
    {
        private const long JPEG_RATIO = 90L;
        private MapRenderInvoker _renderInvoker;

        public Sampler(MapRenderInvoker renderInvoker)
        {
            _renderInvoker = renderInvoker;
            if (!renderInvoker.IsRunning)
            {
                throw new NullReferenceException("Sampler must have a lunched render!");
            }
        }

        public string SampleSingle()
        {
            MemoryStream stream = new MemoryStream();
            var items = _renderInvoker.TakeScreenShot(stream);
            string ret = Guid.NewGuid().ToString() + ".png";
            SaveScreenShot(stream, ret);
            return ret;
        }


        public List<string> SampleAll(int xStep, int yStep)
        {
            //TODO
            return null;
        }

        private void SaveScreenShot(Stream screenShotStream, string path)
        {
            Bitmap source = new Bitmap(screenShotStream);
            Bitmap result = new Bitmap(_renderInvoker.ScreenWidth, _renderInvoker.ScreenHeight);
            Rectangle rectangle = new Rectangle(Point.Empty, source.Size);
            using (Graphics graphics = Graphics.FromImage(result))
            {
                graphics.Clear(Color.Black);
                graphics.DrawImageUnscaledAndClipped(source, rectangle);
            }

            ImageCodecInfo jpegCodecInfo = ImageCodecInfo.GetImageEncoders().First(i => i.MimeType == "image/jpeg");
            using (EncoderParameters parameters = new EncoderParameters(1))
            {
                EncoderParameter parameter = new EncoderParameter(Encoder.Quality, JPEG_RATIO);
                parameters.Param[0] = parameter;
                result.Save(path, jpegCodecInfo, parameters);
            }
        }


    }
}
