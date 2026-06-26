using System;
using System.IO;
using MapleStory.MachineLearningSampleGenerator;
using Xunit;

namespace MapleStory.MachineLearningSampleGenerator.Tests
{
    public class ProgramOutputTargetTests
    {
        [Fact]
        public void EnsureTargetDirectoryIsEmptyOrCreate_MissingDirectory_CreatesDirectory()
        {
            using TestWorkspace workspace = new TestWorkspace();
            string targetDirectory = Path.Combine(workspace.RootPath, "new-output");

            Program.EnsureTargetDirectoryIsEmptyOrCreate(targetDirectory);

            Assert.True(Directory.Exists(targetDirectory));
            Assert.Empty(Directory.EnumerateFileSystemEntries(targetDirectory));
        }

        [Fact]
        public void EnsureTargetDirectoryIsEmptyOrCreate_EmptyDirectory_Succeeds()
        {
            using TestWorkspace workspace = new TestWorkspace();
            string targetDirectory = workspace.CreateDirectory("output");

            Program.EnsureTargetDirectoryIsEmptyOrCreate(targetDirectory);

            Assert.True(Directory.Exists(targetDirectory));
            Assert.Empty(Directory.EnumerateFileSystemEntries(targetDirectory));
        }

        [Fact]
        public void EnsureTargetDirectoryIsEmptyOrCreate_NonEmptyDirectory_Throws()
        {
            using TestWorkspace workspace = new TestWorkspace();
            string targetDirectory = workspace.CreateDirectory("output");
            workspace.CreateFile(Path.Combine("output", "existing.txt"), "data");

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => Program.EnsureTargetDirectoryIsEmptyOrCreate(targetDirectory));

            Assert.Contains("not empty", exception.Message);
            Assert.True(File.Exists(Path.Combine(targetDirectory, "existing.txt")));
        }
    }
}
