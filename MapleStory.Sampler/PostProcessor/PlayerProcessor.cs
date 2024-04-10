using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MapRender.Invoker;
using SharpDX.MediaFoundation;

namespace MapleStory.Sampler.PostProcessor
{
    /// <summary>
    /// Post-processor by adding player images and coordinates.
    /// </summary>
    public class PlayerProcessor : IPostProcessor
    {
        private const double VerticalRange = 0.85;
        private const double HorizontalRange = 0.85;
        private const int NumPlayers = 3;

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
            _playerImages = Directory.GetFiles(directory, "*.*").Where(f => f.EndsWith(".png") || f.EndsWith(".bmp")).Select(Image.FromFile);
            _numImages = _playerImages.Count();
            if (_numImages == 0)
            {
                throw new FileNotFoundException($"{directory} does not contain any .bmp images!");
            }
        }

        public Sample Process(Sample sample)
        {
            // Draw player & replace stream
            sample.ImageStream = DrawPlayer(sample.ImageStream, sample);
            return sample;
        }

        private Image GetNextPlayer()
        {
            return _playerImages.ElementAt(_random.Next(0, _numImages));
        }

        private MemoryStream DrawPlayer(MemoryStream source, Sample sample)
        {
            Bitmap result = new Bitmap(source);
            using (Graphics graphics = Graphics.FromImage(result))
            {
                for (int i = 0; i < NumPlayers; ++i)
                {
                    Image player = GetNextPlayer();
                    int drawX = _random.Next((int)(sample.Width * (1 - HorizontalRange)), (int)(sample.Width * HorizontalRange));
                    int drawY = _random.Next((int)(sample.Height * (1 - VerticalRange)), (int)(sample.Height * VerticalRange));
                    if (_random.NextDouble() > 0.5)
                    {
                        player.RotateFlip(RotateFlipType.RotateNoneFlipX);
                    }
                    Rectangle drawRegion = new Rectangle(drawX, drawY, player.Width, player.Height);
                    graphics.DrawImageUnscaled(player, drawRegion);
                    // Add coordinate information 
                    sample.Items.Add(new TargetItem() { Height = player.Height, Width = player.Width, X = drawX, Y = drawY, Type = ObjectClass.Player });
                }
            }
            MemoryStream ret = new MemoryStream();
            result.Save(ret, ImageFormat.Png);
            return ret;
        }

    }
}
