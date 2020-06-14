using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommandLine;

namespace MapleStory_TFRecord_Preparer
{
    internal static class MainEntryPoint
    {
        private class Options
        {
            [Option('m', "map", Required = true, HelpText = "Space-separated Wz image ID of map(s) used for generating TFRecord.")]
            public IEnumerable<int> Maps { get; set; }

            [Option('x', "width", Required = false, Default = 1366, HelpText = "Width of sample image.")]
            public int RenderWidth { get; set; }

            [Option('y', "height", Required = false, Default = 768, HelpText = "Height of sample image.")]
            public int RenderHeight { get; set; }

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
            return 0;
        }

    }
}
