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
using MapleStory.Sampler;
using MapleStory.Sampler.PostProcessor;
using MapRender.Invoker;

namespace MapleStory.MachineLearningSampleGenerator
{
    internal static class Program
    {
        [DllImport("kernel32.dll")]
        static extern bool SetDllDirectory(string path);

        [DllImport("kernel32")]
        static extern bool AllocConsole();

        [DllImport("kernel32.dll")]
        static extern bool AttachConsole(int dwProcessId);

        [DllImport("kernel32.dll")]
        static extern bool FreeConsole();

        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        static Program()
        {
            AttachConsole(-1); // Try to attach to parent process's console
            if (GetConsoleWindow() == IntPtr.Zero)
            {
                AllocConsole();
            }

            Console.OutputEncoding = Encoding.UTF8; // Correctly show non-English characters
            string libPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Lib",
                Environment.Is64BitProcess ? "x64" : "x86");
            SetDllDirectory(libPath); // Add dll search path for WzComparerR2
        }

        private class Options
        {
            [Option('m', "map", Required = true, HelpText = "Space-separated Wz image ID of map(s) used for generating TFRecord.")]
            public IEnumerable<string> Maps { get; set; }

            [Option('x', "xStep", Required = true, HelpText = "Step in X.")]
            public int StepX { get; set; }

            [Option('y', "yStep", Required = true, HelpText = "Step in Y.")]
            public int StepY { get; set; }

            [Option('f', "format", Required = true, HelpText = "Output format, must be one of: [tfrecord, darknet, coco]")]
            public string Format { get; set; }

            [Option('o', "output", Required = false, Default = ".", HelpText = "Data set output location")]
            public string OutputPath { get; set; }

            [Option('w', "width", Required = false, Default = 1366, HelpText = "Width of sample image.")]
            public int RenderWidth { get; set; }

            [Option('h', "height", Required = false, Default = 768, HelpText = "Height of sample image.")]
            public int RenderHeight { get; set; }

            [Option('i', "interval", Required = false, Default = 0, HelpText = "Time interval in ms between each sample")]
            public int SampleInterval { get; set; }

            [Option('p', "path", Required = false, Default = "", HelpText = "MapleStory Installed Path")]
            public string MapleStoryPath { get; set; }

            [Option("post", Required = false, Default = false, HelpText = "Indicate whether to enable post-processing.")]
            public bool PostProcessingEnable { get; set; }

            [Option("players", Required = false, Default = "", HelpText = "Directory where the post-processing player images stored.")]
            public string PlayerImageDirectory { get; set; }

            [Option('e', "encoding", Required = false, HelpText = "Encoding used to decode Wz strings. Using system default if not specified.")]
            public string Encoding { get; set; } = "";
        }

        [STAThread]
        private static int Main(string[] args)
        {
            int ret = CommandLine.Parser.Default.ParseArguments<Options>(args).MapResult(RunAndReturn, OnParseError);
            Console.WriteLine("MapleStoryDetectionSampleGenerator exited with code= {0}", ret);
            FreeConsole();
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
            Queue<string> maps = new Queue<string>(options.Maps);
            var first = maps.Dequeue();
            renderInvoker.LoadMap(first);
            renderInvoker.Launch(options.RenderWidth, options.RenderHeight);
            Thread.Sleep(10000);
            // Initialize sampler
            IDatasetWriter writer = GetDatasetWriter(options, first);
            Sampler.Sampler sampler = new Sampler.Sampler(renderInvoker);
            if (options.PostProcessingEnable && options.PlayerImageDirectory != "")
            {
                IPostProcessor postProcessor = new PlayerProcessor(options.PlayerImageDirectory);
                sampler.OnSampleCaptured += (s, e) => postProcessor.Process(s);
            }

            while (true)
            {
                sampler.SampleAll(options.StepX, options.StepY, writer, options.SampleInterval);
                if (maps.Count == 0)
                {
                    break;
                }
                renderInvoker.SwitchMap(maps.Dequeue());
            }
            writer.Finish();
            return 0;
        }


        /// <summary>
        /// Throw if condition not meet.
        /// </summary>
        private static void PreRunTest(Options options)
        {
            // Check resolution
            if (options.RenderHeight <= 0)
            {
                throw new ArgumentException("Render size cannot exceed screen size. Height illegal.",
                    nameof(options.RenderHeight));
            }
            if (options.RenderWidth <= 0)
            {
                throw new ArgumentException("Render size cannot exceed screen size. Width illegal.",
                    nameof(options.RenderWidth));
            }

            // Check format
            Enum.Parse(typeof(OutputFormat), options.Format, true);

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

            // Check output path
            if (!Directory.Exists(options.OutputPath))
            {
                throw new ArgumentException($"OutputPath {options.OutputPath} does not exist.");
            }

            // Check interval
            if (options.SampleInterval < 0)
            {
                throw new ArgumentException("SampleInterval cannot be negative!");
            }

            // Check for post-processing
            if (options.PostProcessingEnable)
            {
                if (options.PlayerImageDirectory != "")
                {
                    if (!Directory.Exists(options.PlayerImageDirectory))
                    {
                        throw new ArgumentException($"PlayerImageDirectory {options.PlayerImageDirectory} cannot be found!");
                    }
                }
            }
        }

        private static IDatasetWriter GetDatasetWriter(Options options, string map)
        {
            switch (Enum.Parse(typeof(OutputFormat), options.Format, true))
            {
                case OutputFormat.TfRecord:
                    return new TfRecordWriter(Path.Combine(options.OutputPath, map));
                case OutputFormat.Darknet:
                    return new DarknetWriter(options.OutputPath);
                case OutputFormat.Coco:
                    return new CocoWriter(options.OutputPath, $"MapleStory {map}.img Object Detection Samples");
                default:
                    throw new ArgumentOutOfRangeException(nameof(options), options, null);
            }
        }

    }
}
