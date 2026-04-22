using System;
using System.IO;
using System.Linq;
using MapleStory.Common;

namespace MapleStory.Avatar.DebugCli
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            try
            {
                AvatarDebugCliOptions options = AvatarDebugCliOptions.Parse(args);
                string mapleStoryPath = ResolveMapleStoryPath(options.MapleStoryPath);
                using (AvatarGenerator generator = new AvatarGenerator(mapleStoryPath, options.Encoding))
                {
                    AvatarAppearance appearance = new AvatarAppearance(options.PartIds);

                    if (options.ListActions)
                    {
                        foreach (string action in generator.GetAvailableActions())
                        {
                            Console.WriteLine(action);
                        }

                        return 0;
                    }

                    if (options.ListEmotions)
                    {
                        foreach (string emotion in generator.GetAvailableEmotions(appearance))
                        {
                            Console.WriteLine(emotion);
                        }

                        return 0;
                    }

                    if (options.AllBodyFrames)
                    {
                        Directory.CreateDirectory(options.OutputDirectory);
                        AvatarFrameResult[] frames = generator
                            .RenderBodyAnimationFrames(appearance, options.ToPose(), options.AlignFrames)
                            .ToArray();
                        for (int i = 0; i < frames.Length; i++)
                        {
                            string outputPath = Path.Combine(
                                options.OutputDirectory,
                                $"{SanitizeFileName(options.Action)}_{i:D3}.png");
                            frames[i].Save(outputPath);
                            Console.WriteLine(outputPath);
                        }

                        return 0;
                    }

                    AvatarFrameResult result = generator.SaveFrame(appearance, options.ToPose(), options.OutputPath);
                    Console.WriteLine(
                        $"{options.OutputPath} ({result.Width}x{result.Height}, origin {result.Origin.X},{result.Origin.Y}, " +
                        $"body {result.BodyWidth}x{result.BodyHeight}, body origin {result.BodyOrigin.X},{result.BodyOrigin.Y})");
                    return 0;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        }

        private static string SanitizeFileName(string value)
        {
            char[] invalidChars = Path.GetInvalidFileNameChars();
            char[] chars = value.Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray();
            return new string(chars);
        }

        private static string ResolveMapleStoryPath(string configuredPath)
        {
            if (!string.IsNullOrWhiteSpace(configuredPath))
            {
                return configuredPath;
            }

            if (!MapleStoryPathHelper.FoundMapleStoryInstalled)
            {
                throw new InvalidOperationException(
                    "MapleStory install path was not provided and registry lookup did not find an installation. Pass --path explicitly.");
            }

            return MapleStoryPathHelper.MapleStoryInstallDirectory;
        }
    }
}
