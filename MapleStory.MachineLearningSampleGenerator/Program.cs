using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using MapleStory.Avatar;
using MapleStory.Common;
using MapleStory.MachineLearningSampleGenerator.Configuration;
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

        [STAThread]
        private static int Main(string[] args)
        {
            int ret = -1;
            try
            {
                BootstrapArguments bootstrapArguments = BootstrapArgumentsParser.Parse(args);
                if (bootstrapArguments.ShowHelp)
                {
                    PrintBanner();
                    Console.WriteLine(GetHelpText());
                    ret = 0;
                    return ret;
                }

                if (bootstrapArguments.ShowVersion)
                {
                    PrintBanner();
                    ret = 0;
                    return ret;
                }

                PrintBanner();
                ConfigurationLoader loader = new ConfigurationLoader();
                ConfigurationResolver resolver = new ConfigurationResolver();
                GeneratorConfig config = loader.Load(bootstrapArguments.ConfigPath);
                ResolvedRunConfig runConfig = resolver.Resolve(config, bootstrapArguments.ConfigPath);

                Console.WriteLine("Configuration: {0}", runConfig.ConfigPath);
                Console.WriteLine("MapleStory Location: {0}", runConfig.MapleStoryPath);
                ret = Run(runConfig);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
            }
            finally
            {
                Console.WriteLine("MapleStoryDetectionSampleGenerator exited with code= {0}", ret);
                FreeConsole();
            }

            return ret;
        }

        /// <summary>
        /// Main logic here
        /// </summary>
        private static int Run(ResolvedRunConfig config)
        {
            switch (config.GenerationMode)
            {
                case GenerationMode.Character:
                    return RunCharacter(config);
                case GenerationMode.Rune:
                    return RunRune(config);
                default:
                    throw new ArgumentOutOfRangeException(nameof(config), config.GenerationMode, null);
            }
        }

        private static int RunCharacter(ResolvedRunConfig config)
        {
            using AvatarGenerator avatarGenerator = new AvatarGenerator(config.MapleStoryPath, config.TextEncoding, false);
            AvatarPlayerFrameSource playerFrameSource = new AvatarPlayerFrameSource(avatarGenerator);
            PlayerPostProcessorValidator.Validate(config.Maps, playerFrameSource);
            return RunSampler(
                config,
                map => PostProcessorFactory.Create(map.PostProcessors, playerFrameSource));
        }

        private static int RunRune(ResolvedRunConfig config)
        {
            using RuneAssetSet runeAssets = RuneAssetLoader.Load(config.MapleStoryPath, config.TextEncoding);
            return RunSampler(
                config,
                _ => new IPostProcessor[] { new RuneProcessor(runeAssets) });
        }

        private static int RunSampler(
            ResolvedRunConfig config,
            Func<ResolvedMapConfig, IReadOnlyList<IPostProcessor>> createPostProcessors)
        {
            MapRenderInvoker renderInvoker = new MapRenderInvoker(config.MapleStoryPath, config.TextEncoding, false);
            Queue<ResolvedMapConfig> maps = new Queue<ResolvedMapConfig>(config.Maps);
            ResolvedMapConfig firstMap = maps.Dequeue();
            renderInvoker.LoadMap(firstMap.Id);
            renderInvoker.Launch(config.RenderWidth, config.RenderHeight);

            IDatasetWriter writer = GetDatasetWriter(config);
            Sampler.Sampler sampler = new Sampler.Sampler(renderInvoker);
            while (true)
            {
                IReadOnlyList<IPostProcessor> postProcessors = createPostProcessors(firstMap);
                try
                {
                    sampler.SampleAll(firstMap.XStep, firstMap.YStep, writer, firstMap.IntervalMs, postProcessors);
                }
                finally
                {
                    DisposePostProcessors(postProcessors);
                }

                if (maps.Count == 0)
                {
                    break;
                }

                firstMap = maps.Dequeue();
                renderInvoker.SwitchMap(firstMap.Id);
            }
            writer.Finish();
            return 0;
        }

        private static void DisposePostProcessors(
            IReadOnlyList<IPostProcessor> postProcessors)
        {
            if (postProcessors == null)
            {
                return;
            }

            foreach (IPostProcessor postProcessor in postProcessors)
            {
                if (postProcessor is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
        }

        private static IDatasetWriter GetDatasetWriter(ResolvedRunConfig config)
        {
            switch (config.OutputFormat)
            {
                case OutputFormat.TfRecord:
                    return new TfRecordWriter(Path.Combine(config.OutputPath, config.OutputName));
                case OutputFormat.Darknet:
                    return new DarknetWriter(config.OutputPath);
                case OutputFormat.Coco:
                    return new CocoWriter(config.OutputPath, config.OutputName);
                default:
                    throw new ArgumentOutOfRangeException(nameof(config), config, null);
            }
        }

        private static void PrintBanner()
        {
            AssemblyName assemblyName = Assembly.GetExecutingAssembly().GetName();
            Console.WriteLine("{0} {1}", assemblyName.Name, assemblyName.Version);
            Console.WriteLine(FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).LegalCopyright);
        }

        private static string GetHelpText()
        {
            return
@"Usage:
  MapleStory.MachineLearningSampleGenerator --config <path>
  MapleStory.MachineLearningSampleGenerator --help
  MapleStory.MachineLearningSampleGenerator --version

Options:
  -c, --config <path>   Path to the YAML configuration file.
  -h, --help            Display this help screen.
  -v, --version         Display version information.";
        }
    }
}
