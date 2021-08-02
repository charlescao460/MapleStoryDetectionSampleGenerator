using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MapRender.Invoker;

namespace MapleStory.Sampler.PostProcessor
{
    /// <summary>
    /// Post-processor by adding player images and coordinates.
    /// </summary>
    public class PlayerProcessor : IPostProcessor
    {
        private const double VerticalRange = 0.85;
        private const double HorizontalRange = 0.85;

        private IEnumerable<Image> _playerImages;
        private int _numImages;
        private Random _random;

        public PlayerProcessor(string directory)
        {
            if (!Directory.Exists(directory))
            {
                throw new FileNotFoundException($"{directory} is not a valid directory!");
            }
            _random = new Random();
            _playerImages = Directory.GetFiles(directory, "*.bmp").Select(Image.FromFile);
            _numImages = _playerImages.Count();
            if (_numImages == 0)
            {
                throw new FileNotFoundException($"{directory} does not contain any .bmp images!");
            }
        }

        public Sample Process(Sample sample)
        {
            int drawX = _random.Next((int)(sample.Width * (1 - HorizontalRange)), (int)(sample.Width * HorizontalRange));
            int drawY = _random.Next((int)(sample.Height * (1 - VerticalRange)), (int)(sample.Height * VerticalRange));
            Image player = GetNextPlayer();
            // Draw player & replace stream
            sample.ImageStream = DrawPlayer(sample.ImageStream, player, drawX, drawY, sample.Width, sample.Height);
            // Add coordinate information 
            sample.Items.Add(new TargetItem() { Height = player.Height, Width = player.Width, X = drawX, Y = drawY, Type = ObjectClass.Player });
            return sample;
        }

        private Image GetNextPlayer()
        {
            return _playerImages.ElementAt(_random.Next(0, _numImages));
        }

        private MemoryStream DrawPlayer(MemoryStream source, Image player, int x, int y, int width, int height)
        {
            Bitmap result = new Bitmap(source);
            Rectangle drawRegion = new Rectangle(x, y, player.Width, player.Height);
            if (_random.NextDouble() > 0.5)
            {
                player.RotateFlip(RotateFlipType.RotateNoneFlipX);
            }
            using (Graphics graphics = Graphics.FromImage(result))
            {
                graphics.DrawImageUnscaledAndClipped(player, drawRegion);
            }
            MemoryStream ret = new MemoryStream();
            result.Save(ret, ImageFormat.Png);
            return ret;
        }

    }
}
