// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using ClassicUO.Utility.Logging;

namespace ClassicUO.Utility
{
    public static class FileSystemHelper
    {
        public static string CreateFolderIfNotExists(string path, params string[] parts)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            char[] invalid = Path.GetInvalidFileNameChars();

            for (int i = 0; i < parts.Length; i++)
            {
                for (int j = 0; j < invalid.Length; j++)
                {
                    parts[i] = parts[i].Replace(invalid[j].ToString(), "");
                }
            }

            var sb = new StringBuilder();

            foreach (string part in parts)
            {
                sb.Append(Path.Combine(path, part));

                string r = sb.ToString();

                if (!Directory.Exists(r))
                {
                    Directory.CreateDirectory(r);
                }

                path = r;
                sb.Clear();
            }

            return path;
        }

        public static string RemoveInvalidChars(string text)
        {
            char[] invalid = Path.GetInvalidFileNameChars();

            for (int j = 0; j < invalid.Length; j++)
            {
                text = text.Replace(invalid[j].ToString(), "");
            }

            return text;
        }

        public static void EnsureFileExists(string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(path);
            }
        }


        public static void CopyAllTo(this DirectoryInfo source, DirectoryInfo target)
        {
            Directory.CreateDirectory(target.FullName);

            // Copy each file into the new directory.
            foreach (FileInfo fi in source.GetFiles())
            {
                Console.WriteLine(@"Copying {0}\{1}", target.FullName, fi.Name);
                fi.CopyTo(Path.Combine(target.FullName, fi.Name), true);
            }

            // Copy each subdirectory using recursion.
            foreach (DirectoryInfo diSourceSubDir in source.GetDirectories())
            {
                DirectoryInfo nextTargetSubDir = target.CreateSubdirectory(diSourceSubDir.Name);

                diSourceSubDir.CopyAllTo(nextTargetSubDir);
            }
        }

        public static void OpenFileWithDefaultApp(string filePath)
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    ProcessStartInfo p = new() { FileName = "xdg-open", ArgumentList = { filePath }};
                    Process.Start(p);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    ProcessStartInfo p = new() { FileName = "open", ArgumentList = { filePath }};
                    Process.Start(p);
                }
            }
            catch (Exception ex)
            {
                Log.Error("Error opening file: " + ex.Message);
            }
        }
    }
}
