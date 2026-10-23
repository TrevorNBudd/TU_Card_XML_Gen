using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Xml.Linq;

namespace TU_Card_XML_Gen.Review
{
    /// <summary>
    /// Writes review.html after a run: the Card XML Viewer (Review/index.html, built into the exe) with this
    /// run's changed and new cards, their versions from before the run, and the fusion recipes that link them.
    /// The viewer's rule checks then flag anything out of place, and opening it needs no files.
    /// </summary>
    internal static class ReviewPage
    {
        private const string Marker = "<!-- REVIEW-DATA -->";

        /// <param name="changed">Each changed card after the run, with the same card before it (null if unknown).</param>
        /// <param name="created">New cards built this run.</param>
        /// <param name="recipes">All fusion recipes; the ones touching these cards are included.</param>
        /// <returns>The path of the page written.</returns>
        public static string Write(string outputDirectory, string title,
            IEnumerable<(XElement After, XElement? Before)> changed, IEnumerable<XElement> created, IEnumerable<XElement> recipes)
        {
            XElement after = new XElement("after");
            XElement before = new XElement("before");
            HashSet<string> ids = new HashSet<string>();

            foreach ((XElement a, XElement? b) in changed)
            {
                after.Add(Tagged(a, "changed"));
                if (b != null)
                {
                    before.Add(new XElement(b));
                }

                AddIds(a, ids);
            }

            foreach (XElement unit in created)
            {
                after.Add(Tagged(unit, "new"));
                AddIds(unit, ids);
            }

            // recipes that make one of these cards, or use one of them
            XElement recipeList = new XElement("recipes", recipes
                .Where(r => ids.Contains(r.Element("card_id")?.Value.Trim() ?? string.Empty)
                    || r.Elements("resource").Any(res => ids.Contains(res.Attribute("card_id")?.Value ?? string.Empty)))
                .Select(r => new XElement(r)));

            XElement review = new XElement("review",
                new XAttribute("title", title),
                new XAttribute("generated", DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)),
                after, before, recipeList);

            string html = LoadViewer();
            string data = "<script type=\"text/xml\" id=\"review-data\">\n" + review.ToString().Replace("</script", "<\\/script") + "\n</script>";
            html = html.Contains(Marker) ? html.Replace(Marker, data) : html.Replace("</body>", data + "\n</body>");

            string path = Path.GetFullPath(Path.Combine(outputDirectory, "review.html"));
            File.WriteAllText(path, html);
            return path;
        }

        /// <summary>Opens the page in the default browser.  Never fails the run.</summary>
        public static void Open(string path)
        {
            try
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"(Couldn't open the review page automatically: {ex.Message}. Open it yourself: {path})");
            }
        }

        private static XElement Tagged(XElement unit, string status)
        {
            XElement copy = new XElement(unit);
            copy.SetAttributeValue("review-status", status);
            return copy;
        }

        private static void AddIds(XElement unit, HashSet<string> ids)
        {
            ids.Add(unit.Element("id")?.Value.Trim() ?? string.Empty);
            foreach (XElement up in unit.Elements("upgrade"))
            {
                ids.Add(up.Element("card_id")?.Value.Trim() ?? string.Empty);
            }
        }

        private static string LoadViewer()
        {
            Assembly assembly = typeof(ReviewPage).Assembly;
            string? name = assembly.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith("index.html", StringComparison.OrdinalIgnoreCase));
            if (name == null)
            {
                throw new InvalidOperationException("The review page template (Review/index.html) isn't built into this exe.");
            }

            using Stream stream = assembly.GetManifestResourceStream(name)!;
            using StreamReader reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }
}
