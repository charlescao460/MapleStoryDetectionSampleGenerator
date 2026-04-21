using Xunit;

namespace MapleStory.MachineLearningSampleGenerator.Tests
{
    public class BootstrapArgumentsParserTests
    {
        [Fact]
        public void Parse_Help_ReturnsHelpMode()
        {
            BootstrapArguments arguments = BootstrapArgumentsParser.Parse(new[] { "--help" });

            Assert.True(arguments.ShowHelp);
            Assert.False(arguments.ShowVersion);
            Assert.Equal(string.Empty, arguments.ConfigPath);
        }

        [Fact]
        public void Parse_ConfigPath_ReturnsConfigMode()
        {
            BootstrapArguments arguments = BootstrapArgumentsParser.Parse(new[] { "--config", "sample-generator.yml" });

            Assert.False(arguments.ShowHelp);
            Assert.False(arguments.ShowVersion);
            Assert.Equal("sample-generator.yml", arguments.ConfigPath);
        }
    }
}
