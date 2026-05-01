using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using MapRender.Invoker;
using OpenCvSharp;
using CvPoint2f = OpenCvSharp.Point2f;
using CvSize = OpenCvSharp.Size;

namespace MapleStory.Sampler.PostProcessor
{
    /// <summary>
    /// Post-processor that replaces a map screenshot with a center-square rune prompt.
    /// </summary>
    public sealed class RuneProcessor : IPostProcessor
    {
        private const double PromptCenterY = 0.50;
        private const double PromptJitterX = 0.025;
        private const double PromptJitterY = 0.045;
        private const double OptionalTransformProbability = 0.50;
        private const double MinOpacity = 0.55;
        private const double MaxOpacity = 1.00;

        private readonly RuneAssetSet _assets;
        private readonly Random _random;
        private readonly RuneProcessorOptions _options;
        private readonly object _sync = new object();

        public RuneProcessor(RuneAssetSet assets, Random random = null, RuneProcessorOptions options = null)
        {
            _assets = assets ?? throw new ArgumentNullException(nameof(assets));
            if (_assets.Arrows.Count == 0)
            {
                throw new ArgumentException("Rune assets must contain at least one arrow.", nameof(assets));
            }

            _random = random ?? new Random();
            _options = options ?? new RuneProcessorOptions();
            if (_options.ArrowCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(options), "Rune arrow count must be greater than 0.");
            }
        }

        public Sample Process(Sample sample)
        {
            if (sample == null)
            {
                throw new ArgumentNullException(nameof(sample));
            }

            lock (_sync)
            {
                sample.ImageStream = DrawRunes(sample.ImageStream, sample);
            }

            return sample;
        }

        private MemoryStream DrawRunes(Stream source, Sample sample)
        {
            source.Position = 0;
            using Bitmap sourceBitmap = new Bitmap(source);
            int side = Math.Min(sourceBitmap.Width, sourceBitmap.Height);
            int cropX = (sourceBitmap.Width - side) / 2;
            int cropY = (sourceBitmap.Height - side) / 2;

            using Bitmap canvas = new Bitmap(side, side, PixelFormat.Format32bppArgb);
            using (Graphics graphics = Graphics.FromImage(canvas))
            {
                graphics.CompositingMode = CompositingMode.SourceOver;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.DrawImage(
                    sourceBitmap,
                    new Rectangle(0, 0, side, side),
                    new Rectangle(cropX, cropY, side, side),
                    GraphicsUnit.Pixel);
            }

            sample.Items.Clear();
            sample.Width = side;
            sample.Height = side;

            PromptLayout promptLayout = CreatePromptLayout(canvas.Width);
            DrawSharedBase(canvas, promptLayout);

            for (int i = 0; i < _options.ArrowCount; i++)
            {
                DrawArrow(canvas, sample, i, promptLayout);
            }

            MemoryStream output = new MemoryStream();
            canvas.Save(output, ImageFormat.Png);
            output.Position = 0;
            return output;
        }

        private void DrawArrow(Bitmap canvas, Sample sample, int index, PromptLayout promptLayout)
        {
            RuneArrowAsset asset = _assets.Arrows[_random.Next(0, _assets.Arrows.Count)];
            using Bitmap arrow = PrepareArrowBitmap(asset.Arrow);
            TransformSpec transform = CreateArrowTransform(sample.Width, arrow.Width, arrow.Height, index, promptLayout);
            RectangleF fitBounds = promptLayout.HasBase
                ? GetBaseArrowFitBounds(promptLayout.BaseBounds)
                : new RectangleF(1, 1, sample.Width - 2, sample.Height - 2);
            transform = ScaleTransformToFit(transform, fitBounds);
            PointF[] arrowQuad = CreateFittedQuad(transform, fitBounds, out PointF fittedCenter);
            transform = transform.WithCenter(fittedCenter);

            using Mat arrowMatrix = CreatePerspectiveTransform(arrow.Width, arrow.Height, arrowQuad);
            using Bitmap warpedArrow = WarpBitmap(arrow, arrowMatrix, sample.Width, sample.Height);
            DrawLayer(canvas, warpedArrow);

            DrawOptionalNoise(canvas, transform);

            TargetItem annotation = CreateAnnotation(asset.Direction, arrow.Width, arrow.Height, arrowMatrix, arrowQuad, sample.Width);
            sample.Items.Add(annotation);
        }

        private Bitmap PrepareArrowBitmap(Bitmap source)
        {
            Rectangle bounds = GetAlphaBounds(source);
            Bitmap cropped = CropBitmap(source, bounds);
            double opacity = ShouldApplyOptionalTransform()
                ? NextDouble(MinOpacity, MaxOpacity)
                : 1.0;
            Bitmap remapped = _options.EnableColorRemap && ShouldApplyOptionalTransform()
                ? RemapColor(cropped, opacity)
                : ApplyOpacity(cropped, opacity);
            cropped.Dispose();
            return remapped;
        }

        private PromptLayout CreatePromptLayout(int side)
        {
            Bitmap[] bases = GetBaseAssets();
            bool hasBase = _options.EnableBases && bases.Length > 0 && ShouldApplyOptionalOverlay();
            if (!hasBase)
            {
                return new PromptLayout();
            }

            Bitmap source = bases[_random.Next(0, bases.Length)];
            Rectangle sourceBounds = GetAlphaBounds(source);
            RectangleF baseBounds = CreateBaseBounds(side, sourceBounds.Size);
            return new PromptLayout(source, sourceBounds, baseBounds);
        }

        private static RectangleF CreateBaseBounds(int side, System.Drawing.Size baseSize)
        {
            float width = baseSize.Width;
            float height = baseSize.Height;
            float x = (side - width) / 2f;
            float y = (float)(side * (PromptCenterY + 0.02) - height / 2f);
            return new RectangleF(x, y, width, height);
        }

        private Bitmap[] GetBaseAssets()
        {
            return _assets.Arrows
                .SelectMany(asset => asset.Bases)
                .ToArray();
        }

        private void DrawSharedBase(Bitmap canvas, PromptLayout promptLayout)
        {
            if (!promptLayout.HasBase)
            {
                return;
            }

            DrawPromptBaseBar(canvas, promptLayout.BaseBounds);

            using Bitmap croppedBase = CropBitmap(promptLayout.BaseSource, promptLayout.BaseSourceBounds);
            using Bitmap baseBitmap = ApplyOpacity(croppedBase, NextDouble(0.18, 0.40));
            using Graphics graphics = Graphics.FromImage(canvas);
            graphics.CompositingMode = CompositingMode.SourceOver;
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.DrawImageUnscaled(
                baseBitmap,
                (int)Math.Round(promptLayout.BaseBounds.Left),
                (int)Math.Round(promptLayout.BaseBounds.Top));
        }

        private void DrawPromptBaseBar(Bitmap canvas, RectangleF bounds)
        {
            float radius = bounds.Height * 0.42f;

            using Graphics graphics = Graphics.FromImage(canvas);
            graphics.CompositingMode = CompositingMode.SourceOver;
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using GraphicsPath path = CreateRoundedRectanglePath(bounds, radius);
            using SolidBrush brush = new SolidBrush(Color.FromArgb(_random.Next(95, 151), 0, 0, 0));
            graphics.FillPath(brush, path);
        }

        private void DrawOptionalNoise(Bitmap canvas, TransformSpec arrowTransform)
        {
            if (!_options.EnableNoise || _assets.Noises.Count == 0 || !ShouldApplyOptionalOverlay())
            {
                return;
            }

            Bitmap source = _assets.Noises[_random.Next(0, _assets.Noises.Count)];
            using Bitmap noise = MakeGrayscale(source, NextDouble(0.12, 0.42));
            float width = arrowTransform.Width;
            float height = arrowTransform.Height;
            if (ShouldApplyOptionalTransform())
            {
                width *= (float)NextDouble(1.00, 1.25);
            }

            if (ShouldApplyOptionalTransform())
            {
                height *= (float)NextDouble(1.00, 1.25);
            }

            TransformSpec noiseTransform = arrowTransform.WithSize(
                width,
                height);
            PointF[] noiseQuad = CreateQuad(noiseTransform);
            using Mat matrix = CreatePerspectiveTransform(noise.Width, noise.Height, noiseQuad);
            using Bitmap warped = WarpBitmap(noise, matrix, canvas.Width, canvas.Height);
            DrawLayer(canvas, warped);
        }

        private TransformSpec CreateArrowTransform(int side, int sourceWidth, int sourceHeight, int index, PromptLayout promptLayout)
        {
            double centerX;
            double centerY;
            double targetLongSide = side * 0.058;

            if (promptLayout.HasBase)
            {
                float padding = promptLayout.BaseBounds.Width * 0.12f;
                double usableLeft = promptLayout.BaseBounds.Left + padding;
                double usableRight = promptLayout.BaseBounds.Right - padding;
                double slotWidth = (usableRight - usableLeft) / Math.Max(_options.ArrowCount - 1, 1);
                centerX = _options.ArrowCount == 1
                    ? (usableLeft + usableRight) / 2.0
                    : usableLeft + slotWidth * index;
                centerY = promptLayout.BaseBounds.Top + promptLayout.BaseBounds.Height / 2.0;
                targetLongSide = Math.Min(targetLongSide, promptLayout.BaseBounds.Height * 0.58);
            }
            else
            {
                double slotWidth = side / (double)(_options.ArrowCount + 1);
                centerX = slotWidth * (index + 1);
                centerY = side * PromptCenterY;
            }

            if (_options.EnableRandomTransforms)
            {
                if (ShouldApplyOptionalTransform())
                {
                    centerX += NextDouble(-side * PromptJitterX, side * PromptJitterX);
                    centerY += NextDouble(-side * PromptJitterY, side * PromptJitterY);
                }

                if (ShouldApplyOptionalTransform())
                {
                    targetLongSide *= NextDouble(0.78, 1.18);
                }

                double scale = targetLongSide / Math.Max(sourceWidth, sourceHeight);
                float width = (float)(sourceWidth * scale);
                float height = (float)(sourceHeight * scale);

                if (ShouldApplyOptionalTransform())
                {
                    width *= (float)NextDouble(0.82, 1.14);
                    height *= (float)NextDouble(0.82, 1.14);
                }

                float angle = ShouldApplyOptionalTransform()
                    ? (float)NextDouble(-42.0, 42.0)
                    : 0;
                float twist = ShouldApplyOptionalTransform()
                    ? (float)(Math.Min(width, height) * NextDouble(0.02, 0.10))
                    : 0;

                return new TransformSpec(new PointF((float)centerX, (float)centerY), width, height, angle, twist);
            }

            return new TransformSpec(
                new PointF((float)centerX, (float)centerY),
                sourceWidth,
                sourceHeight,
                0,
                0);
        }

        private PointF[] CreateFittedQuad(TransformSpec transform, RectangleF fitBounds, out PointF fittedCenter)
        {
            PointF[] quad = CreateQuad(transform);
            RectangleF bounds = BoundsOf(quad);
            float shiftX = 0;
            float shiftY = 0;

            if (bounds.Left < fitBounds.Left)
            {
                shiftX = fitBounds.Left - bounds.Left;
            }
            else if (bounds.Right > fitBounds.Right)
            {
                shiftX = fitBounds.Right - bounds.Right;
            }

            if (bounds.Top < fitBounds.Top)
            {
                shiftY = fitBounds.Top - bounds.Top;
            }
            else if (bounds.Bottom > fitBounds.Bottom)
            {
                shiftY = fitBounds.Bottom - bounds.Bottom;
            }

            if (shiftX != 0 || shiftY != 0)
            {
                for (int i = 0; i < quad.Length; i++)
                {
                    quad[i] = new PointF(quad[i].X + shiftX, quad[i].Y + shiftY);
                }
            }

            fittedCenter = new PointF(transform.Center.X + shiftX, transform.Center.Y + shiftY);
            return quad;
        }

        private static RectangleF GetBaseArrowFitBounds(RectangleF baseBounds)
        {
            float horizontalInset = Math.Max(1, baseBounds.Width * 0.04f);
            float verticalInset = Math.Max(1, baseBounds.Height * 0.08f);
            return RectangleF.FromLTRB(
                baseBounds.Left + horizontalInset,
                baseBounds.Top + verticalInset,
                baseBounds.Right - horizontalInset,
                baseBounds.Bottom - verticalInset);
        }

        private TransformSpec ScaleTransformToFit(TransformSpec transform, RectangleF fitBounds)
        {
            TransformSpec scaled = transform;
            for (int i = 0; i < 4; i++)
            {
                RectangleF bounds = BoundsOf(CreateQuad(scaled));
                float scale = Math.Min(
                    fitBounds.Width / bounds.Width,
                    fitBounds.Height / bounds.Height);
                if (scale >= 1)
                {
                    return scaled;
                }

                scale = Math.Max(0.01f, scale * 0.98f);
                scaled = scaled.WithScaledSize(scale);
            }

            return scaled;
        }

        private PointF[] CreateQuad(TransformSpec transform)
        {
            float halfWidth = transform.Width / 2f;
            float halfHeight = transform.Height / 2f;
            PointF[] points =
            {
                new PointF(-halfWidth, -halfHeight),
                new PointF(halfWidth, -halfHeight),
                new PointF(halfWidth, halfHeight),
                new PointF(-halfWidth, halfHeight),
            };

            if (transform.Twist != 0)
            {
                points[0] = Offset(points[0], -RandomTwist(transform.Twist), -RandomTwist(transform.Twist));
                points[1] = Offset(points[1], RandomTwist(transform.Twist), -RandomTwist(transform.Twist));
                points[2] = Offset(points[2], RandomTwist(transform.Twist), RandomTwist(transform.Twist));
                points[3] = Offset(points[3], -RandomTwist(transform.Twist), RandomTwist(transform.Twist));
            }

            double radians = transform.AngleDegrees * Math.PI / 180.0;
            double cos = Math.Cos(radians);
            double sin = Math.Sin(radians);
            for (int i = 0; i < points.Length; i++)
            {
                float x = points[i].X;
                float y = points[i].Y;
                points[i] = new PointF(
                    (float)(transform.Center.X + x * cos - y * sin),
                    (float)(transform.Center.Y + x * sin + y * cos));
            }

            return points;
        }

        private float RandomTwist(float amount)
        {
            return _options.EnableRandomTransforms && ShouldApplyOptionalTransform()
                ? (float)NextDouble(0, amount)
                : 0;
        }

        private TargetItem CreateAnnotation(
            RuneArrowDirection direction,
            int sourceWidth,
            int sourceHeight,
            Mat matrix,
            PointF[] quad,
            int side)
        {
            RectangleF bounds = BoundsOf(quad);
            int x = Clamp((int)Math.Floor(bounds.Left), 0, side - 1);
            int y = Clamp((int)Math.Floor(bounds.Top), 0, side - 1);
            int right = Clamp((int)Math.Ceiling(bounds.Right), x + 1, side);
            int bottom = Clamp((int)Math.Ceiling(bounds.Bottom), y + 1, side);
            PointF[] keypoints = GetDirectionalKeypoints(direction, sourceWidth, sourceHeight)
                .Select(point => ClampPoint(TransformPoint(matrix, point), side))
                .ToArray();

            return new TargetItem
            {
                X = x,
                Y = y,
                Width = right - x,
                Height = bottom - y,
                Type = ObjectClass.RuneArrow,
                Keypoints = keypoints
                    .Select(point => new TargetKeypoint(point.X, point.Y, 2))
                    .ToList(),
            };
        }

        private static PointF[] GetDirectionalKeypoints(RuneArrowDirection direction, int width, int height)
        {
            float centerX = width / 2f;
            float centerY = height / 2f;
            switch (direction)
            {
                case RuneArrowDirection.Up:
                    return new[] { new PointF(centerX, height), new PointF(centerX, 0) };
                case RuneArrowDirection.Down:
                    return new[] { new PointF(centerX, 0), new PointF(centerX, height) };
                case RuneArrowDirection.Left:
                    return new[] { new PointF(width, centerY), new PointF(0, centerY) };
                case RuneArrowDirection.Right:
                    return new[] { new PointF(0, centerY), new PointF(width, centerY) };
                default:
                    throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
            }
        }

        private static Mat CreatePerspectiveTransform(int sourceWidth, int sourceHeight, PointF[] destination)
        {
            CvPoint2f[] source =
            {
                new CvPoint2f(0, 0),
                new CvPoint2f(sourceWidth, 0),
                new CvPoint2f(sourceWidth, sourceHeight),
                new CvPoint2f(0, sourceHeight),
            };
            CvPoint2f[] dest = destination
                .Select(point => new CvPoint2f(point.X, point.Y))
                .ToArray();
            return Cv2.GetPerspectiveTransform(source, dest);
        }

        private static Bitmap WarpBitmap(Bitmap bitmap, Mat matrix, int width, int height)
        {
            using Mat source = BitmapToMat(bitmap);
            using Mat destination = new Mat(height, width, MatType.CV_8UC4, Scalar.All(0));
            Cv2.WarpPerspective(
                source,
                destination,
                matrix,
                new CvSize(width, height),
                InterpolationFlags.Linear,
                BorderTypes.Constant,
                Scalar.All(0));
            return MatToBitmap(destination);
        }

        private static PointF TransformPoint(Mat matrix, PointF point)
        {
            double m00 = matrix.At<double>(0, 0);
            double m01 = matrix.At<double>(0, 1);
            double m02 = matrix.At<double>(0, 2);
            double m10 = matrix.At<double>(1, 0);
            double m11 = matrix.At<double>(1, 1);
            double m12 = matrix.At<double>(1, 2);
            double m20 = matrix.At<double>(2, 0);
            double m21 = matrix.At<double>(2, 1);
            double m22 = matrix.At<double>(2, 2);

            double denominator = m20 * point.X + m21 * point.Y + m22;
            return new PointF(
                (float)((m00 * point.X + m01 * point.Y + m02) / denominator),
                (float)((m10 * point.X + m11 * point.Y + m12) / denominator));
        }

        private static Mat BitmapToMat(Bitmap bitmap)
        {
            using MemoryStream stream = new MemoryStream();
            bitmap.Save(stream, ImageFormat.Png);
            Mat mat = Cv2.ImDecode(stream.ToArray(), ImreadModes.Unchanged);
            if (mat.Channels() == 4)
            {
                return mat;
            }

            Mat converted = new Mat();
            if (mat.Channels() == 3)
            {
                Cv2.CvtColor(mat, converted, ColorConversionCodes.BGR2BGRA);
            }
            else
            {
                Cv2.CvtColor(mat, converted, ColorConversionCodes.GRAY2BGRA);
            }

            mat.Dispose();
            return converted;
        }

        private static Bitmap MatToBitmap(Mat mat)
        {
            Cv2.ImEncode(".png", mat, out byte[] buffer);
            using MemoryStream stream = new MemoryStream(buffer);
            using Bitmap decoded = new Bitmap(stream);
            return new Bitmap(decoded);
        }

        private static void DrawLayer(Bitmap canvas, Bitmap layer)
        {
            using Graphics graphics = Graphics.FromImage(canvas);
            graphics.CompositingMode = CompositingMode.SourceOver;
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.DrawImageUnscaled(layer, 0, 0);
        }

        private Bitmap RemapColor(Bitmap source, double opacity)
        {
            using Bitmap argb = CreateArgbCopy(source);
            Bitmap result = new Bitmap(argb.Width, argb.Height, PixelFormat.Format32bppArgb);
            double targetHue = PickSaturatedTargetHue();
            double saturationScale = NextDouble(0.92, 1.20);
            double valueScale = NextDouble(1.02, 1.18);

            for (int y = 0; y < argb.Height; y++)
            {
                for (int x = 0; x < argb.Width; x++)
                {
                    Color pixel = argb.GetPixel(x, y);
                    if (pixel.A == 0)
                    {
                        result.SetPixel(x, y, Color.Transparent);
                        continue;
                    }

                    RgbToHsv(pixel, out _, out double sourceSaturation, out double sourceValue);
                    double mappedSaturation = sourceSaturation < 0.15
                        ? 0.28 + sourceSaturation * 1.40
                        : Math.Max(0.68, sourceSaturation * saturationScale);
                    double mappedValue = Clamp(sourceValue * valueScale + 0.04, 0, 1);

                    if (sourceValue < 0.22)
                    {
                        mappedSaturation *= 0.55;
                        mappedValue = Clamp(sourceValue * 1.08, 0, 1);
                    }

                    Color mapped = ColorFromHsv(targetHue, Clamp(mappedSaturation, 0, 1), mappedValue);
                    result.SetPixel(
                        x,
                        y,
                        Color.FromArgb(
                            Clamp((int)Math.Round(pixel.A * opacity), 0, 255),
                            mapped.R,
                            mapped.G,
                            mapped.B));
                }
            }

            return result;
        }

        private double PickSaturatedTargetHue()
        {
            double[] hues =
            {
                0,
                28,
                52,
                112,
                178,
                218,
                270,
                315,
            };
            return NormalizeHue(hues[_random.Next(0, hues.Length)] + NextDouble(-9, 9));
        }

        private static void RgbToHsv(Color color, out double hue, out double saturation, out double value)
        {
            double red = color.R / 255.0;
            double green = color.G / 255.0;
            double blue = color.B / 255.0;
            double max = Math.Max(red, Math.Max(green, blue));
            double min = Math.Min(red, Math.Min(green, blue));
            double delta = max - min;

            value = max;
            saturation = max == 0 ? 0 : delta / max;
            if (delta == 0)
            {
                hue = 0;
                return;
            }

            if (max == red)
            {
                hue = 60 * (((green - blue) / delta) % 6);
            }
            else if (max == green)
            {
                hue = 60 * (((blue - red) / delta) + 2);
            }
            else
            {
                hue = 60 * (((red - green) / delta) + 4);
            }

            hue = NormalizeHue(hue);
        }

        private static Color ColorFromHsv(double hue, double saturation, double value)
        {
            hue = NormalizeHue(hue);
            double chroma = value * saturation;
            double x = chroma * (1 - Math.Abs((hue / 60.0) % 2 - 1));
            double match = value - chroma;
            double red;
            double green;
            double blue;

            if (hue < 60)
            {
                red = chroma;
                green = x;
                blue = 0;
            }
            else if (hue < 120)
            {
                red = x;
                green = chroma;
                blue = 0;
            }
            else if (hue < 180)
            {
                red = 0;
                green = chroma;
                blue = x;
            }
            else if (hue < 240)
            {
                red = 0;
                green = x;
                blue = chroma;
            }
            else if (hue < 300)
            {
                red = x;
                green = 0;
                blue = chroma;
            }
            else
            {
                red = chroma;
                green = 0;
                blue = x;
            }

            return Color.FromArgb(
                Clamp((int)Math.Round((red + match) * 255), 0, 255),
                Clamp((int)Math.Round((green + match) * 255), 0, 255),
                Clamp((int)Math.Round((blue + match) * 255), 0, 255));
        }

        private static double NormalizeHue(double hue)
        {
            hue %= 360;
            return hue < 0 ? hue + 360 : hue;
        }

        private static Bitmap MakeGrayscale(Bitmap source, double opacity)
        {
            using Bitmap argb = CreateArgbCopy(source);
            Bitmap result = new Bitmap(argb.Width, argb.Height, PixelFormat.Format32bppArgb);
            for (int y = 0; y < argb.Height; y++)
            {
                for (int x = 0; x < argb.Width; x++)
                {
                    Color pixel = argb.GetPixel(x, y);
                    if (pixel.A == 0)
                    {
                        result.SetPixel(x, y, Color.Transparent);
                        continue;
                    }

                    int gray = Clamp((int)Math.Round(pixel.R * 0.299 + pixel.G * 0.587 + pixel.B * 0.114), 0, 255);
                    result.SetPixel(
                        x,
                        y,
                        Color.FromArgb(Clamp((int)Math.Round(pixel.A * opacity), 0, 255), gray, gray, gray));
                }
            }

            return result;
        }

        private static Bitmap ApplyOpacity(Bitmap source, double opacity)
        {
            using Bitmap argb = CreateArgbCopy(source);
            Bitmap result = new Bitmap(argb.Width, argb.Height, PixelFormat.Format32bppArgb);
            for (int y = 0; y < argb.Height; y++)
            {
                for (int x = 0; x < argb.Width; x++)
                {
                    Color pixel = argb.GetPixel(x, y);
                    result.SetPixel(
                        x,
                        y,
                        Color.FromArgb(
                            Clamp((int)Math.Round(pixel.A * opacity), 0, 255),
                            pixel.R,
                            pixel.G,
                            pixel.B));
                }
            }

            return result;
        }

        private static Bitmap CropBitmap(Bitmap source, Rectangle bounds)
        {
            Bitmap result = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
            using Graphics graphics = Graphics.FromImage(result);
            graphics.DrawImage(
                source,
                new Rectangle(0, 0, result.Width, result.Height),
                bounds,
                GraphicsUnit.Pixel);
            return result;
        }

        private static Bitmap CreateArgbCopy(Bitmap source)
        {
            Bitmap copy = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
            using Graphics graphics = Graphics.FromImage(copy);
            graphics.DrawImageUnscaled(source, 0, 0);
            return copy;
        }

        private static GraphicsPath CreateRoundedRectanglePath(RectangleF rectangle, float radius)
        {
            float diameter = radius * 2f;
            GraphicsPath path = new GraphicsPath();
            if (diameter <= 0)
            {
                path.AddRectangle(rectangle);
                path.CloseFigure();
                return path;
            }

            RectangleF arc = new RectangleF(rectangle.X, rectangle.Y, diameter, diameter);
            path.AddArc(arc, 180, 90);
            arc.X = rectangle.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = rectangle.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = rectangle.X;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static Rectangle GetAlphaBounds(Bitmap bitmap)
        {
            using Bitmap argb = CreateArgbCopy(bitmap);
            int left = argb.Width;
            int top = argb.Height;
            int right = -1;
            int bottom = -1;

            for (int y = 0; y < argb.Height; y++)
            {
                for (int x = 0; x < argb.Width; x++)
                {
                    if (argb.GetPixel(x, y).A == 0)
                    {
                        continue;
                    }

                    left = Math.Min(left, x);
                    top = Math.Min(top, y);
                    right = Math.Max(right, x);
                    bottom = Math.Max(bottom, y);
                }
            }

            if (right < left || bottom < top)
            {
                return new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            }

            return Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
        }

        private double NextDouble(double minValue, double maxValue)
        {
            return minValue + _random.NextDouble() * (maxValue - minValue);
        }

        private bool ShouldApplyOptionalTransform()
        {
            return _options.EnableRandomTransforms && _random.NextDouble() < OptionalTransformProbability;
        }

        private bool ShouldApplyOptionalOverlay()
        {
            return !_options.EnableRandomTransforms || _random.NextDouble() < OptionalTransformProbability;
        }

        private static RectangleF BoundsOf(IReadOnlyList<PointF> points)
        {
            float left = points.Min(point => point.X);
            float top = points.Min(point => point.Y);
            float right = points.Max(point => point.X);
            float bottom = points.Max(point => point.Y);
            return RectangleF.FromLTRB(left, top, right, bottom);
        }

        private static PointF Offset(PointF point, float x, float y)
        {
            return new PointF(point.X + x, point.Y + y);
        }

        private static PointF ClampPoint(PointF point, int side)
        {
            return new PointF(
                Clamp(point.X, 0, side),
                Clamp(point.Y, 0, side));
        }

        private static int Clamp(int value, int minValue, int maxValue)
        {
            return Math.Min(Math.Max(value, minValue), maxValue);
        }

        private static float Clamp(float value, float minValue, float maxValue)
        {
            return Math.Min(Math.Max(value, minValue), maxValue);
        }

        private static double Clamp(double value, double minValue, double maxValue)
        {
            return Math.Min(Math.Max(value, minValue), maxValue);
        }

        private readonly struct TransformSpec
        {
            public TransformSpec(PointF center, float width, float height, float angleDegrees, float twist)
            {
                Center = center;
                Width = width;
                Height = height;
                AngleDegrees = angleDegrees;
                Twist = twist;
            }

            public PointF Center { get; }

            public float Width { get; }

            public float Height { get; }

            public float AngleDegrees { get; }

            public float Twist { get; }

            public TransformSpec WithCenter(PointF center)
            {
                return new TransformSpec(center, Width, Height, AngleDegrees, Twist);
            }

            public TransformSpec WithSize(float width, float height)
            {
                return new TransformSpec(Center, width, height, AngleDegrees, Twist);
            }

            public TransformSpec WithScaledSize(float scale)
            {
                return new TransformSpec(Center, Width * scale, Height * scale, AngleDegrees, Twist * scale);
            }
        }

        private readonly struct PromptLayout
        {
            public PromptLayout(Bitmap baseSource, Rectangle baseSourceBounds, RectangleF baseBounds)
            {
                BaseSource = baseSource;
                BaseSourceBounds = baseSourceBounds;
                BaseBounds = baseBounds;
                HasBase = true;
            }

            public Bitmap BaseSource { get; }

            public Rectangle BaseSourceBounds { get; }

            public RectangleF BaseBounds { get; }

            public bool HasBase { get; }
        }
    }

    public sealed class RuneProcessorOptions
    {
        public int ArrowCount { get; set; } = 4;

        public bool EnableRandomTransforms { get; set; } = true;

        public bool EnableColorRemap { get; set; } = true;

        public bool EnableBases { get; set; } = true;

        public bool EnableNoise { get; set; } = true;
    }
}
