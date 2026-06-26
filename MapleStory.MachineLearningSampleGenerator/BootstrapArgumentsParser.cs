using System;
using System.Collections.Generic;

namespace MapleStory.MachineLearningSampleGenerator
{
    internal static class BootstrapArgumentsParser
    {
        public static BootstrapArguments Parse(IReadOnlyList<string> args)
        {
            if (args == null || args.Count == 0)
            {
                throw new ConfigurationException("Missing arguments. Use --config <path> to run, or --help for usage.");
            }

            bool showHelp = false;
            bool showVersion = false;
            string configPath = null;

            for (int i = 0; i < args.Count; i++)
            {
                string arg = args[i];
                switch (arg)
                {
                    case "-h":
                    case "--help":
                        showHelp = true;
                        continue;
                    case "-v":
                    case "--version":
                        showVersion = true;
                        continue;
                    case "-c":
                    case "--config":
                        if (configPath != null)
                        {
                            throw new ConfigurationException("Configuration path was supplied more than once.");
                        }
                        if (i + 1 >= args.Count)
                        {
                            throw new ConfigurationException("Missing value for --config.");
                        }
                        configPath = args[++i];
                        continue;
                    default:
                        if (arg.StartsWith("--config=", StringComparison.Ordinal))
                        {
                            if (configPath != null)
                            {
                                throw new ConfigurationException("Configuration path was supplied more than once.");
                            }
                            configPath = arg.Substring("--config=".Length);
                            continue;
                        }
                        throw new ConfigurationException($"Unknown argument '{arg}'.");
                }
            }

            if (showHelp && (showVersion || configPath != null))
            {
                throw new ConfigurationException("--help cannot be combined with other arguments.");
            }

            if (showVersion && configPath != null)
            {
                throw new ConfigurationException("--version cannot be combined with --config.");
            }

            if (showHelp)
            {
                return BootstrapArguments.Help();
            }

            if (showVersion)
            {
                return BootstrapArguments.Version();
            }

            if (string.IsNullOrWhiteSpace(configPath))
            {
                throw new ConfigurationException("Missing --config <path> argument.");
            }

            return BootstrapArguments.Config(configPath);
        }
    }
}
