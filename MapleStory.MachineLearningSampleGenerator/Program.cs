using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
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
                map => CreateCharacterPostProcessorPipeline(config, map));
        }

        private static int RunRune(ResolvedRunConfig config)
        {
            using RuneAssetSet runeAssets = RuneAssetLoader.Load(config.MapleStoryPath, config.TextEncoding);
            return RunSampler(
                config,
                _ => CreateRunePostProcessorPipeline(runeAssets));
        }

        private static int RunSampler(
            ResolvedRunConfig config,
            Func<ResolvedMapConfig, PostProcessorPipeline> createPostProcessors)
        {
            ValidateMaps(config);
            IDatasetWriter writer = GetDatasetWriter(config);
            try
            {
                Console.WriteLine("Concurrency: {0}", config.Concurrency);
                ConcurrentMapRunner.Run(
                    config.Maps,
                    config.Concurrency,
                    _ => new MapSamplerWorker(config, writer, createPostProcessors),
                    (worker, map) => worker.Sample(map),
                    (map, ex) => Console.Error.WriteLine($"Error sampling map {map.Id}: {ex}"));
                writer.Finish();
            }
            finally
            {
                if (writer is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            return 0;
        }

        private static void ValidateMaps(ResolvedRunConfig config)
        {
            using MapRenderInvoker renderInvoker = new MapRenderInvoker(config.MapleStoryPath, config.TextEncoding, false);
            foreach (ResolvedMapConfig map in config.Maps)
            {
                renderInvoker.LoadMap(map.Id);
            }
        }

        private static PostProcessorPipeline CreateCharacterPostProcessorPipeline(
            ResolvedRunConfig config,
            ResolvedMapConfig map)
        {
            if (map.PostProcessors.Count == 0)
            {
                return PostProcessorPipeline.Empty;
            }

            AvatarGenerator avatarGenerator = new AvatarGenerator(config.MapleStoryPath, config.TextEncoding, false);
            AvatarPlayerFrameSource playerFrameSource = new AvatarPlayerFrameSource(avatarGenerator);
            IReadOnlyList<IPostProcessor> postProcessors = PostProcessorFactory.Create(map.PostProcessors, playerFrameSource);
            return new PostProcessorPipeline(postProcessors, avatarGenerator);
        }

        private static PostProcessorPipeline CreateRunePostProcessorPipeline(RuneAssetSet source)
        {
            RuneAssetSet clonedAssets = CloneRuneAssets(source);
            return new PostProcessorPipeline(new IPostProcessor[] { new RuneProcessor(clonedAssets) }, clonedAssets);
        }

        private static RuneAssetSet CloneRuneAssets(RuneAssetSet source)
        {
            return new RuneAssetSet(
                source.Arrows.Select(arrow => new RuneArrowAsset(
                    arrow.Name,
                    arrow.Direction,
                    new Bitmap(arrow.Arrow),
                    arrow.Bases.Select(bitmap => new Bitmap(bitmap)))),
                source.Noises.Select(bitmap => new Bitmap(bitmap)));
        }

        internal sealed class PostProcessorPipeline : IDisposable
        {
            public static PostProcessorPipeline Empty => new PostProcessorPipeline(Array.Empty<IPostProcessor>(), null);

            private readonly IDisposable _owner;
            private bool _disposed;

            public PostProcessorPipeline(IReadOnlyList<IPostProcessor> processors, IDisposable owner)
            {
                Processors = processors ?? Array.Empty<IPostProcessor>();
                _owner = owner;
            }

            public IReadOnlyList<IPostProcessor> Processors { get; }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                foreach (IPostProcessor postProcessor in Processors)
                {
                    if (postProcessor is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }

                _owner?.Dispose();
                _disposed = true;
            }
        }

        private sealed class MapSamplerWorker : IDisposable
        {
            private readonly ResolvedRunConfig _config;
            private readonly IDatasetWriter _writer;
            private readonly Func<ResolvedMapConfig, PostProcessorPipeline> _createPostProcessors;
            private MapRenderInvoker _renderInvoker;
            private Sampler.Sampler _sampler;

            public MapSamplerWorker(
                ResolvedRunConfig config,
                IDatasetWriter writer,
                Func<ResolvedMapConfig, PostProcessorPipeline> createPostProcessors)
            {
                _config = config;
                _writer = writer;
                _createPostProcessors = createPostProcessors;
            }

            public void Sample(ResolvedMapConfig map)
            {
                EnsureRenderer(map);
                using PostProcessorPipeline postProcessors = _createPostProcessors(map);
                _sampler.SampleAll(map.Count, _writer, map.IntervalMs, postProcessors.Processors, map.Id);
            }

            public void Dispose()
            {
                _renderInvoker?.Dispose();
            }

            private void EnsureRenderer(ResolvedMapConfig map)
            {
                if (_renderInvoker == null || !_renderInvoker.IsRunning)
                {
                    LaunchRenderer(map);
                    return;
                }

                _renderInvoker.SwitchMap(map.Id);
            }

            private void LaunchRenderer(ResolvedMapConfig map)
            {
                _renderInvoker?.Dispose();
                _renderInvoker = new MapRenderInvoker(_config.MapleStoryPath, _config.TextEncoding, false);
                _renderInvoker.LoadMap(map.Id);
                _renderInvoker.Launch(_config.RenderWidth, _config.RenderHeight);
                _sampler = new Sampler.Sampler(_renderInvoker);
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
