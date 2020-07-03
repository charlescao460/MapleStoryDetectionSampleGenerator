using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using CommandLine.Text;
using MapleStory.Common;
using MapRender.Invoker;

namespace MapleStory.TFRecordPreparer
{
    internal static class Program
    {
        [DllImport("kernel32.dll")]
        static extern bool SetDllDirectory(string path);

        static Program()
        {
            Console.OutputEncoding = Encoding.UTF8; // Correctly show non-English characters
            string libPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Lib",
                Environment.Is64BitProcess ? "x64" : "x86");
            SetDllDirectory(libPath); // Add dll search path for WzComparerR2
        }

        private class Options
        {
            [Option('m', "map", Required = true, HelpText = "Space-separated Wz image ID of map(s) used for generating TFRecord.")]
            public IEnumerable<string> Maps { get; set; }

            [Option('x', "width", Required = false, Default = 1366, HelpText = "Width of sample image.")]
            public int RenderWidth { get; set; }

            [Option('y', "height", Required = false, Default = 768, HelpText = "Height of sample image.")]
            public int RenderHeight { get; set; }

            [Option('p', "path", Required = false, Default = "", HelpText = "MapleStory Installed Path")]
            public string MapleStoryPath { get; set; }

            [Option('e', "encoding", Required = false, HelpText = "Encoding used to decode Wz strings. Using system default if not specified.")]
            public string Encoding { get; set; } = "";
        }

        [STAThread]
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

        /// <summary>
        /// Main logic here
        /// </summary>
        private static int RunAndReturn(Options options)
        {
            // Print program info
            Console.WriteLine(HeadingInfo.Default);
            Console.WriteLine(FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).LegalCopyright);
            // Check arguments
            PreRunTest(options);
            Console.WriteLine("MapleStory Location: {0}", options.MapleStoryPath);
            // Initialize render
            MapRenderInvoker renderInvoker = new MapRenderInvoker(options.MapleStoryPath,
                options.Encoding == string.Empty ? Encoding.Default : Encoding.GetEncoding(options.Encoding),
                false);
            // Iterate each map
            var map = options.Maps.First();
            string imgText = map.EndsWith(".img") ? map : (map + ".img");
            renderInvoker.LoadMap(imgText);
            renderInvoker.Launch(options.RenderWidth, options.RenderHeight);

            int lastX;
            bool reverse = false;
            long timer = 0;
            const int step = 5;
            bool took = false;
            while (true) // block
            {
                Thread.Sleep(step);
                lastX = renderInvoker.CurrentCameraX;
                renderInvoker.MoveCamera(renderInvoker.CurrentCameraX + (reverse ? 1 : -1), renderInvoker.CurrentCameraY);
                if (renderInvoker.CurrentCameraX == lastX)
                {
                    reverse = !reverse;
                }

                timer += step;
                if (timer >= 2000 && !took)
                {
                    Sampler.Sampler sampler = new Sampler.Sampler(renderInvoker);
                    var tfExample = sampler.SampleSingle();
                    FileStream file = new FileStream(tfExample.Guid.ToString()+".example",FileMode.CreateNew);
                    tfExample.SerializeToStream().WriteTo(file);
                    file.Dispose();
                    took = true;
                }
            }

            return 0;
        }


        /// <summary>
        /// Throw if condition not meet.
        /// </summary>
        private static void PreRunTest(Options options)
        {
            // Check resolution
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

            // Check file path
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

            // Check map id format
            foreach (var map in options.Maps)
            {
                string id = map.Replace(".img", string.Empty);
                if (!id.All(char.IsDigit))
                {
                    throw new ArgumentException("Supplied Map Id is not in correct format." +
                                                " --map parameter should be space-separated list of IDs. " +
                                                "E.g. --map 450007010 450007060");
                }
            }

            // Check encoding
            if ((options.Encoding != string.Empty) &&
                Encoding.GetEncodings()
                .Any(e => e.Name.Equals(options.Encoding, StringComparison.OrdinalIgnoreCase)))
            {
                throw new ArgumentException($"{options.Encoding} is not an available Encoding in your system.");
            }
        }

    }
}
