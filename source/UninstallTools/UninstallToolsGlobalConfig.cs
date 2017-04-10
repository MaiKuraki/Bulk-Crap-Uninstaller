﻿/*
    Copyright (c) 2017 Marcin Szeniak (https://github.com/Klocman/)
    Apache License Version 2.0
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using Klocman.Extensions;
using Klocman.Native;
using Klocman.Tools;

namespace UninstallTools
{
    public static class UninstallToolsGlobalConfig
    {
        internal static readonly IEnumerable<string> DirectoryBlacklist = new[]
        {
            "Microsoft", "Microsoft Games", "Temp", "Programs", "Common", "Common Files", "Clients",
            "Desktop", "Internet Explorer", "Windows NT", "Windows Photo Viewer", "Windows Mail",
            "Windows Defender", "Windows Media Player", "Uninstall Information", "Reference Assemblies",
            "InstallShield Installation Information"
        };

        internal static readonly IEnumerable<string> QuestionableDirectoryNames = new[]
        {
            "install", "settings", "config", "configuration",
            "users", "data"
        };

        /// <summary>
        ///     Custom "Program Files" directories. Use with dirs that get used to install applications to.
        /// </summary>
        public static string[] CustomProgramFiles { get; set; }

        /// <summary>
        ///     Directiories containing programs, both built in "Program Files" and user-defined ones.
        /// </summary>
        internal static IEnumerable<string> AllProgramFiles
            => StockProgramFiles.Concat(CustomProgramFiles ?? Enumerable.Empty<string>());

        private static IEnumerable<string> _junkSearchDirs;
        internal static IEnumerable<string> JunkSearchDirs
        {
            get
            {
                if(_junkSearchDirs == null)
                {
                    var localData = WindowsTools.GetEnvironmentPath(CSIDL.CSIDL_LOCAL_APPDATA);
                    var paths = new List<string>
                    {
                        WindowsTools.GetEnvironmentPath(CSIDL.CSIDL_PROGRAMS),
                        WindowsTools.GetEnvironmentPath(CSIDL.CSIDL_COMMON_PROGRAMS),
                        WindowsTools.GetEnvironmentPath(CSIDL.CSIDL_APPDATA),
                        WindowsTools.GetEnvironmentPath(CSIDL.CSIDL_COMMON_APPDATA),
                        localData
                        //Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) danger?
                    };

                    var vsPath = Path.Combine(localData, "VirtualStore");
                    if(Directory.Exists(vsPath))
                        paths.AddRange(Directory.GetDirectories(vsPath));

                    _junkSearchDirs = paths.Distinct().ToList();
                }
                return _junkSearchDirs;
            }
        }

        internal static IEnumerable<string> StockProgramFiles => new[]
        {
            WindowsTools.GetEnvironmentPath(CSIDL.CSIDL_PROGRAM_FILES),
            WindowsTools.GetProgramFilesX86Path()
        }.Distinct();

        public static bool QuietAutomatization { get; set; }
        public static bool QuietAutomatizationKillStuck { get; set; }

        internal static bool IsSystemDirectory(DirectoryInfo dir)
        {
            return //dir.Name.StartsWith("Windows ") //Probably overkill
                DirectoryBlacklist.Any(y => y.Equals(dir.Name, StringComparison.InvariantCultureIgnoreCase))
                || (dir.Attributes & FileAttributes.System) == FileAttributes.System;
        }

        /// <summary>
        ///     Get a list of directiories containing programs. Optionally user-defined directories are added.
        ///     The boolean value is true if the directory is confirmed to contain 64bit applications.
        /// </summary>
        /// <param name="includeUserDirectories">Add user-defined directories.</param>
        /// <returns></returns>
        public static IEnumerable<KeyValuePair<DirectoryInfo, bool?>> GetProgramFilesDirectories(
            bool includeUserDirectories)
        {
            var pfDirectories = new List<KeyValuePair<string, bool?>>(2);

            var pf64 = WindowsTools.GetEnvironmentPath(CSIDL.CSIDL_PROGRAM_FILES);
            var pf32 = WindowsTools.GetProgramFilesX86Path();
            pfDirectories.Add(new KeyValuePair<string, bool?>(pf32, false));
            if (!PathTools.PathsEqual(pf32, pf64))
                pfDirectories.Add(new KeyValuePair<string, bool?>(pf64, true));

            if (includeUserDirectories)
                pfDirectories.AddRange(CustomProgramFiles.Where(x => !pfDirectories.Any(y => PathTools.PathsEqual(x, y.Key)))
                    .Select(x => new KeyValuePair<string, bool?>(x, null)));

            var output = new List<KeyValuePair<DirectoryInfo, bool?>>();
            foreach (var directory in pfDirectories.ToList())
            {
                // Ignore missing or inaccessible directories
                try
                {
                    var di = new DirectoryInfo(directory.Key);
                    if (di.Exists)
                        output.Add(new KeyValuePair<DirectoryInfo, bool?>(di, directory.Value));
                }
                catch (Exception ex)
                {
                    Debug.Fail("Failed to open dir", ex.Message);
                }
            }

            return output;
        }

        private static string _assemblyLocation;

        public static string AssemblyLocation
        {
            get
            {
                if (_assemblyLocation == null)
                {
                    _assemblyLocation = Assembly.GetExecutingAssembly().Location;
                    if (_assemblyLocation.ContainsAny(new[] { ".dll", ".exe" }, StringComparison.OrdinalIgnoreCase))
                        _assemblyLocation = PathTools.GetDirectory(_assemblyLocation);
                }
                return _assemblyLocation;
            }
        }

        public static Icon TryExtractAssociatedIcon(string path)
        {
            if(path != null && File.Exists(path))
            {
                try
                {
                    return Icon.ExtractAssociatedIcon(path);
                }
                catch (Exception ex)
                {
                    Debug.Assert(ex == null, ex.Message);
                }
            }
            return null;
        }
    }
}