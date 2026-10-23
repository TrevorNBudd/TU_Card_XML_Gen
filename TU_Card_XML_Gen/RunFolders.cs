using System.Globalization;
using System.Text;
using System.Xml.Linq;
using System.Xml.XPath;

namespace TU_Card_XML_Gen
{
    /// <summary>
    /// The main folder (where config.xml is) and its data, results and logs subfolders.  Set in config.xml under
    /// <Folders>; relative paths there, and in PathToCardXMLs / PathToUpdateFile, are relative to the main folder.
    /// </summary>
    internal class RunFolders
    {
        public string Main = string.Empty;
        public string Data = string.Empty;
        public string Results = string.Empty;
        public string Logs = string.Empty;

        /// <summary>Data, results and logs older than this many months are deleted at the start of a run.  0 = never.</summary>
        public int CleanupMonths = 3;

        public static RunFolders FromConfig(XElement root, string mainFolder)
        {
            XElement? folders = root.XPathSelectElement("Folders");
            RunFolders f = new RunFolders { Main = mainFolder };
            f.Data = Resolve(mainFolder, folders?.Element("Data")?.Value, "data");
            f.Results = Resolve(mainFolder, folders?.Element("Results")?.Value, "results");
            f.Logs = Resolve(mainFolder, folders?.Element("Logs")?.Value, "logs");

            string? months = folders?.Element("CleanupMonths")?.Value.Trim();
            if (!string.IsNullOrEmpty(months))
            {
                if (!int.TryParse(months, out int m) || m < 0)
                {
                    throw new Exception($"Folders/CleanupMonths must be a whole number of months (0 = never clean up), not \"{months}\".");
                }

                f.CleanupMonths = m;
            }

            return f;
        }

        public static string Resolve(string mainFolder, string? path, string fallback)
        {
            string p = string.IsNullOrWhiteSpace(path) ? fallback : path.Trim();
            if (Path.DirectorySeparatorChar == '/')
            {
                p = p.Replace('\\', '/');   // config.xml is written with Windows paths; let it work elsewhere too
            }

            return Path.GetFullPath(Path.IsPathRooted(p) ? p : Path.Combine(mainFolder, p));
        }

        public void Create()
        {
            Directory.CreateDirectory(Data);
            Directory.CreateDirectory(Results);
            Directory.CreateDirectory(Logs);
        }
    }

    /// <summary>
    /// Everything written to the console also goes to logs\yyyy-MM-dd.log.  Runs on the same day are appended to
    /// the same file, each starting with a header line.
    /// </summary>
    internal sealed class RunLog : TextWriter
    {
        private readonly TextWriter console;
        private readonly StreamWriter file;

        public string FilePath { get; }

        private RunLog(TextWriter console, string filePath)
        {
            this.console = console;
            FilePath = filePath;
            file = new StreamWriter(new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.Read), new UTF8Encoding(false)) { AutoFlush = true };
        }

        public static RunLog Start(string logsFolder, string version)
        {
            string path = Path.Combine(logsFolder, DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".log");
            RunLog log = new RunLog(Console.Out, path);
            log.file.WriteLine();
            log.file.WriteLine($"===== {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}  TU_Card_XML_Gen {version} =====");
            Console.SetOut(log);
            return log;
        }

        public override Encoding Encoding => console.Encoding;

        public override void Write(char value) { console.Write(value); file.Write(value); }

        public override void Write(string? value) { console.Write(value); file.Write(value); }

        public override void WriteLine(string? value) { console.WriteLine(value); file.WriteLine(value); }

        public override void Flush() { console.Flush(); file.Flush(); }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Console.SetOut(console);
                file.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Deletes files and folders in the data, results and logs folders that haven't changed for CleanupMonths months.
    /// Only those three folders, only what's directly inside them, and never a folder that holds game data.
    /// </summary>
    internal static class Cleanup
    {
        /// <param name="keep">Full paths that are never deleted (the update file in use, today's log).</param>
        public static void Run(RunFolders folders, string cardXmlsPath, IEnumerable<string> keep)
        {
            if (folders.CleanupMonths == 0)
            {
                return;
            }

            DateTime cutoff = DateTime.Now.AddMonths(-folders.CleanupMonths);
            HashSet<string> keepPaths = new HashSet<string>(keep.Select(p => Path.GetFullPath(p)), StringComparer.OrdinalIgnoreCase);
            string gameData = Path.GetFullPath(cardXmlsPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            List<string> removed = new List<string>();

            foreach ((string label, string folder) in new[] { ("data", folders.Data), ("results", folders.Results), ("logs", folders.Logs) })
            {
                string? whyNot = UnsafeReason(folder, folders.Main, gameData);
                if (whyNot != null)
                {
                    Console.WriteLine($"Cleanup: skipped the {label} folder ({folder}) - {whyNot}");
                    continue;
                }

                if (!Directory.Exists(folder))
                {
                    continue;
                }

                foreach (string entry in Directory.EnumerateFileSystemEntries(folder))
                {
                    string full = Path.GetFullPath(entry);
                    if (keepPaths.Contains(full) || keepPaths.Any(k => k.StartsWith(full + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    try
                    {
                        if (LastChanged(full) >= cutoff)
                        {
                            continue;
                        }

                        if (Directory.Exists(full))
                        {
                            Directory.Delete(full, recursive: true);
                        }
                        else
                        {
                            File.Delete(full);
                        }

                        removed.Add(Path.Combine(label, Path.GetFileName(full)));
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Cleanup: couldn't remove {full}: {ex.Message}");
                    }
                }
            }

            if (removed.Count > 0)
            {
                Console.WriteLine($"Cleanup: removed {removed.Count} item(s) not changed for {folders.CleanupMonths} month(s) (before {cutoff:yyyy-MM-dd}):");
                foreach (string r in removed)
                {
                    Console.WriteLine($"  {r}");
                }
            }
        }

        /// <summary>Why a folder must not be cleaned, or null if it's fine.</summary>
        private static string? UnsafeReason(string folder, string mainFolder, string gameData)
        {
            string f = folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string main = mainFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            if (Path.GetPathRoot(f)?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) == f)
            {
                return "it's a whole drive.";
            }

            if (f.Equals(main, StringComparison.OrdinalIgnoreCase))
            {
                return "it's the main folder itself (the exe and config.xml are there).";
            }

            if (main.StartsWith(f + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                return "it holds the main folder, so it isn't one of the tool's own subfolders.";
            }

            if (f.Equals(gameData, StringComparison.OrdinalIgnoreCase)
                || gameData.StartsWith(f + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                return "it is, or holds, the game data folder (PathToCardXMLs).";
            }

            if (Directory.Exists(f) && Directory.EnumerateFiles(f, "cards_section_*.xml").Any())
            {
                return "it holds cards_section files, so it looks like game data.";
            }

            return null;
        }

        /// <summary>A file's last change, or the newest change of anything inside a folder.</summary>
        private static DateTime LastChanged(string path)
        {
            if (!Directory.Exists(path))
            {
                return File.GetLastWriteTime(path);
            }

            DateTime newest = Directory.GetLastWriteTime(path);
            foreach (string inner in Directory.EnumerateFileSystemEntries(path, "*", SearchOption.AllDirectories))
            {
                DateTime t = File.GetLastWriteTime(inner);
                if (t > newest)
                {
                    newest = t;
                }
            }

            return newest;
        }
    }
}
