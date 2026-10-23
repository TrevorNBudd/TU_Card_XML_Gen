// See https://aka.ms/new-console-template for more information

using System.Xml.Linq;
using System.Xml.XPath;
using TU_Card_XML_Gen;
using TUComparatorLibrary;

// config.xml is read from the current folder, or else from the exe's folder.  That folder is the "main folder":
// relative paths in config.xml (data, results, logs, the update file, the card XMLs) start there.
string configPath = File.Exists("config.xml") ? Path.GetFullPath("config.xml") : Path.Combine(AppContext.BaseDirectory, "config.xml");
if (!File.Exists(configPath))
{
    Console.WriteLine($"Can't find config.xml in {Environment.CurrentDirectory} or {AppContext.BaseDirectory}.");
    Pause();
    return;
}

string mainFolder = Path.GetDirectoryName(configPath)!;
Environment.CurrentDirectory = mainFolder;

XDocument config;
RunFolders folders;
try
{
    config = XDocument.Parse(File.ReadAllText(configPath));
    folders = RunFolders.FromConfig(config.Root!, mainFolder);
    folders.Create();
}
catch (Exception ex)
{
    Console.WriteLine($"Can't read config.xml: {ex.Message}");
    Pause();
    return;
}

using (RunLog log = RunLog.Start(folders.Logs, Updater.Version))
{
    try
    {
        ConfigStore store = new ConfigStore(config.Root!);
        string cardXmls = RunFolders.Resolve(mainFolder, config.XPathSelectElement("//PathToCardXMLs")?.Value, "data");
        string updateFile = RunFolders.Resolve(mainFolder, config.XPathSelectElement("//StandardOperationConfig/PathToUpdateFile")?.Value, Path.Combine("data", "updatefile.txt"));

        Cleanup.Run(folders, cardXmls, new[] { updateFile, log.FilePath });

        new Updater().Run(cardXmls, updateFile, store, folders.Results);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Stopped: {ex.Message}");
        Console.WriteLine(ex.StackTrace);
    }

    Console.WriteLine($"This run is logged in {log.FilePath}");
}

Console.WriteLine("End Results.  Press any key to end.");
Pause();

static void Pause()
{
    if (!Console.IsInputRedirected)
    {
        Console.ReadKey(true);
    }
}
