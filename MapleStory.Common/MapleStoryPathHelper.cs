using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace MapleStory.Common
{
    /// <summary>
    /// Helper class for finding MapleStory's path. 
    /// </summary>
    public static class MapleStoryPathHelper
    {
        private static readonly string REG_KEY_PATH =
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\MapleStory"; // Tested for CMS

        private static readonly string REG_VALUE_NAME = @"UninstallString";

        public static readonly string MapleStoryExecutableName = @"MapleStory.exe";

        public static readonly string MapleStoryBaseWzName = @"Base.wz";

        private static readonly Lazy<string> _mapleStoryInstallDirectory = new Lazy<string>(() =>
        {
            try
            {
                RegistryKey key = Registry.LocalMachine.OpenSubKey(REG_KEY_PATH); // Possible null
                string uninstallPath = key.GetValue(REG_VALUE_NAME) as string; // Possible invalid cast
                return Path.GetDirectoryName(uninstallPath); // Possible corrupted path
            }
            catch (Exception ex) when (ex is NullReferenceException || ex is InvalidCastException || ex is ArgumentException)
            {
                return string.Empty;
            }
        });

        /// <summary>
        /// Indicate if MapleStory installed location was found.
        /// </summary>
        public static bool FoundMapleStoryInstalled => Directory.Exists(_mapleStoryInstallDirectory.Value);

        /// <summary>
        /// Get MapleStory installed folder based on registry, might fail if no elevate privilege, in which case it will return empty string.
        /// </summary>
        public static string MapleStoryInstallDirectory => _mapleStoryInstallDirectory.Value;

    }
}
