namespace MapleStory.MachineLearningSampleGenerator
{
    internal sealed class BootstrapArguments
    {
        private BootstrapArguments(string configPath, bool showHelp, bool showVersion)
        {
            ConfigPath = configPath;
            ShowHelp = showHelp;
            ShowVersion = showVersion;
        }

        public string ConfigPath { get; }

        public bool ShowHelp { get; }

        public bool ShowVersion { get; }

        public static BootstrapArguments Help()
        {
            return new BootstrapArguments(string.Empty, true, false);
        }

        public static BootstrapArguments Version()
        {
            return new BootstrapArguments(string.Empty, false, true);
        }

        public static BootstrapArguments Config(string configPath)
        {
            return new BootstrapArguments(configPath, false, false);
        }
    }
}
