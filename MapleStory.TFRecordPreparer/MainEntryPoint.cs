using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using CommandLine;
using CommandLine.Text;
using MapleStory.Common;

namespace MapleStory.TFRecordPreparer
{
    internal static class MainEntryPoint
    {
        static MainEntryPoint()
        {
            Console.OutputEncoding = Encoding.UTF8; // Correctly show non-English characters
        }

        private class Options
        {
            [Option('m', "map", Required = true, HelpText = "Space-separated Wz image ID of map(s) used for generating TFRecord.")]
            public IEnumerable<int> Maps { get; set; }

            [Option('x', "width", Required = false, Default = 1366, HelpText = "Width of sample image.")]
            public int RenderWidth { get; set; }

            [Option('y', "height", Required = false, Default = 768, HelpText = "Height of sample image.")]
            public int RenderHeight { get; set; }

            [Option('p', "path", Required = false, Default = "", HelpText = "MapleStory Installed Path")]
            public string MapleStoryPath { get; set; }
        }

        private static int Main(string[] args)
        {
            int ret = CommandLine.Parser.Default.ParseArguments<Options>(args).MapResult(RunAndReturn, OnParseError);
            Console.WriteLine("MapleStory_TFRecord_Preparer exited with code= {0}", ret);
            return ret;
        }

        private static int OnParseError(IEnumerable<Error> errors)
        {
            foreach (var error in errors)
            {
                Console.Error.WriteLine(Enum.GetName(typeof(ErrorType), error.Tag));
            }
            return -1;
        }

        private static int RunAndReturn(Options options)
        {
            Console.WriteLine(HeadingInfo.Default);
            Console.WriteLine(FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).LegalCopyright);
            PreRunTest(options);
            Console.WriteLine("MapleStory Location: {0}", options.MapleStoryPath);

            return 0;
        }


        /// <summary>
        /// Throw if condition not meet.
        /// </summary>
        private static void PreRunTest(Options options)
        {
            if (options.RenderHeight <= 0 || options.RenderHeight > System.Windows.SystemParameters.WorkArea.Height)
            {
                throw new ArgumentException("Render size cannot exceed screen size. Height illegal.",
                    nameof(options.RenderHeight));
            }
            if (options.RenderWidth <= 0 || options.RenderWidth > System.Windows.SystemParameters.WorkArea.Width)
            {
                throw new ArgumentException("Render size cannot exceed screen size. Width illegal.",
                    nameof(options.RenderWidth));
            }

            if (options.MapleStoryPath == string.Empty)
            {
                if (MapleStoryPathHelper.FoundMapleStoryInstalled)
                {
                    options.MapleStoryPath = MapleStoryPathHelper.MapleStoryInstallDirectory;
                }
                else
                {
                    throw new ArgumentException("Cannot find MapleStory installed location. Please specify it in commandline or retry as Administrator.");
                }
            }
            else if (!Directory.Exists(options.MapleStoryPath))
            {
                throw new ArgumentException("Supplied MapleStory directory does not exist.");
            }
        }

    }
}
