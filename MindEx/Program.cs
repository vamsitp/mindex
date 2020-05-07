namespace MindEx
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Web;
    using System.Xml;

    using CsvHelper;
    using CsvHelper.Configuration;
    using CsvHelper.Configuration.Attributes;

    using HtmlAgilityPack;

    using Newtonsoft.Json;

    using ServiceStack;
    using ServiceStack.Html;

    using Formatting = Newtonsoft.Json.Formatting;

    class Program
    {
        private static JsonSerializerSettings SerializerSettings = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore };

        static void Main(string[] args)
        {
            try
            {
                var file = args.FirstOrDefault();
                Console.WriteLine("input: " + file);
                Console.WriteLine("--------------------------------------------------");

                Node root = null;
                if (Path.GetExtension(file).EndsWith("json", StringComparison.OrdinalIgnoreCase))
                {
                    root = ReadJson(file);
                }
                else if (Path.GetExtension(file).EndsWith("csv", StringComparison.OrdinalIgnoreCase))
                {
                    root = ReadCsv(file);
                }
                else if (Path.GetExtension(file).EndsWith("xml", StringComparison.OrdinalIgnoreCase))
                {
                    root = ReadXml(file);
                }
                else if (Path.GetExtension(file).EndsWith("html", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(file).EndsWith("htm", StringComparison.OrdinalIgnoreCase))
                {
                    var headerRowsCount = 1;
                    if (args.Length > 1 && int.TryParse(args[1], out var headersCount))
                    {
                        headerRowsCount = headersCount;
                    }

                    root = ReadHtml(file, headerRowsCount);
                }

                if (root == null)
                {
                    Console.WriteLine("\noutput: empty!");
                    return;
                }

                ProcessOutput(root, file);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private static Node ReadHtml(string file, int headerRowsCount)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(File.ReadAllText(file));

            var result = new Node { name = Path.GetFileNameWithoutExtension(file) };
            for (var i = 1; i <= headerRowsCount; i++)
            {
                var headerCols = doc.DocumentNode.SelectNodes($"//table/tr[{i}]/td").Select((node, index) => (node, index: index + 1)).ToList(); //.Select(x => (text: HttpUtility.HtmlDecode(x.SelectSingleNode("./descendant-or-self::*/text()[normalize-space()]")?.InnerText?.Trim())?.Replace("\r\n ", string.Empty), url: x.SelectSingleNode("./a")?.Attributes["href"]?.Value?.Trim() ?? string.Empty))
                var colLength = 0;
                headerCols.ForEach(h =>
                {
                    var nodeDetails = (text: HttpUtility.HtmlDecode(h.node.SelectSingleNode("./descendant-or-self::*/text()[normalize-space()]")?.InnerText?.Trim()), url: h.node.SelectSingleNode("./a")?.Attributes["href"]?.Value?.Trim());
                    var text = nodeDetails.text;
                    var url = nodeDetails.url;
                    var row = i;
                    var col = h.index;
                    var colspan = 1;
                    if (int.TryParse(h.node.Attributes[nameof(colspan)]?.Value, out var cs))
                    {
                        colspan = cs;
                    }

                    var cols = new List<int>();
                    for (var i = colLength + 1; i <= colLength + colspan; i++)
                    {
                        cols.Add(i);
                    }

                    Debug.WriteLine($"{row}.{col} {text} ({string.Join(',', cols).TrimEnd(new[] { ',', ' ' })} = {colspan})");
                    colLength += colspan;

                    if (row == 1)
                    {
                        var node = new Node { name = text, parent = result.name, url = url, row = row, col = col, colspan = colspan, cols = cols };
                        result.children.Add(node);
                    }
                    else
                    {
                        var parents = SelectManyRecursive<Node>(result.children, r => r.children).Where(r => r.row == row - 1 && r.cols.Any(c => cols.Contains(c) && (r.parentCols == null || r.parentCols.Any(c => cols.Contains(c)))));
                        foreach (var parent in parents)
                        {
                            var node = new Node { name = text, parent = parent.name, url = url, row = row, col = col, colspan = colspan, cols = cols, parentCols = parent.cols };
                            parent.children.Add(node);
                        }
                    }
                });
            }

            Debug.WriteLine(JsonConvert.SerializeObject(result, SerializerSettings));
            var parents = SelectManyRecursive<Node>(result.children, r => r.children).Where(r => r.row == headerRowsCount);
            foreach (var parent in parents)
            {
                var colTexts = new List<string> { };
                var colUrls = new List<string> { };

                foreach (var col in parent.cols)
                {
                    var records = doc.DocumentNode.SelectNodes($"//table/tr[position()>{headerRowsCount}]/td[{col}]").Select(x => (text: HttpUtility.HtmlDecode(x.SelectSingleNode("./descendant-or-self::*/text()[normalize-space()]")?.InnerText?.Trim())?.Replace("\r\n ", string.Empty), url: x.SelectSingleNode("./a")?.Attributes["href"]?.Value?.Trim())).ToList();
                    foreach (var record in records.Select((x, i) => (colText: x.text, url: x.url, index: i)))
                    {
                        if (!string.IsNullOrWhiteSpace(record.colText))
                        {
                            if (record.index < colTexts.Count)
                            {
                                colTexts[record.index] = colTexts[record.index] + " | " + record.colText;
                                colUrls[record.index] = record.url; // TODO: Handle multiple Urls?
                            }
                            else
                            {
                                colTexts.Add(record.colText ?? string.Empty);
                                colUrls.Add(record.url);
                            }
                        }
                    }
                }

                for (var i = 0; i < colTexts.Count; i++)
                {
                    var colText = colTexts[i];
                    var url = colUrls[i];
                    var node = new Node { name = colText, parent = parent.name };
                    parent.children.Add(node);
                    if (!string.IsNullOrWhiteSpace(url))
                    {
                        node.url = url;
                    }
                }
            }

            Debug.WriteLine(JsonConvert.SerializeObject(result, SerializerSettings));
            return result;
        }

        private static Node ReadJson(string file)
        {
            // Debug.WriteLine(JsonConvert.DeserializeXmlNode(File.ReadAllText(file), "root", true, true).OuterXml);
            var result = JsonConvert.DeserializeObject<Node>(File.ReadAllText(file));
            return result;
        }

        private static Node ReadXml(string file)
        {
            var doc = new XmlDocument();
            doc.LoadXml(File.ReadAllText(file));
            var json = JsonConvert.SerializeXmlNode(doc, Formatting.Indented, true);
            var result = JsonConvert.DeserializeObject<Node>(json);
            return result;
        }

        private static Node ReadCsv(string file)
        {
            var result = new Node { name = Path.GetFileNameWithoutExtension(file) };
            var textReader = new StreamReader(file);
            using (var csvReader = new CsvReader(textReader, new CsvConfiguration(CultureInfo.InvariantCulture) { ShouldSkipRecord = record => record.All(string.IsNullOrEmpty) }))
            {
                using (var dr = new CsvDataReader(csvReader))
                {
                    var dt = new DataTable();
                    dt.Load(dr);

                    foreach (DataColumn col in dt.Columns)
                    {
                        var colName = col.ColumnName?.Trim();
                        if (!string.IsNullOrEmpty(colName))
                        {
                            var node = new Node { name = colName, parent = result.name };
                            result.children.Add(node);
                            foreach (DataRow row in dt.Rows)
                            {
                                var rowVal = row[colName].ToString()?.Trim();
                                if (!string.IsNullOrEmpty(rowVal))
                                {
                                    node.children.Add(new Node { name = rowVal, parent = colName });
                                }
                            }
                        }
                    }
                }

                return result;
            }
        }

        private static void ProcessOutput(Node root, string file)
        {
            var outputFile = Path.Combine(Path.GetDirectoryName(file), $"{nameof(MindEx)}_{Path.GetFileNameWithoutExtension(file)}.html");
            var json = JsonConvert.SerializeObject(root, Formatting.Indented, SerializerSettings);
            Console.WriteLine(json);
            Console.WriteLine("--------------------------------------------------");
            File.WriteAllText(outputFile.Replace(".html", ".json"), json);

            var template = Path.Combine(AppContext.BaseDirectory, $"{nameof(MindEx)}");
            var js = File.ReadAllText($"{template}.js").Replace("/*placeholder*/", json);
            var css = File.ReadAllText($"{template}.css");
            var html = File.ReadAllText($"{template}.html").Replace("/*MindExCss*/", css).Replace("/*MindExJs*/", js);

            File.WriteAllText(outputFile, Minifiers.HtmlAdvanced.Compress(html));
            Console.WriteLine("\noutput: " + outputFile);
            if (Debugger.IsAttached)
            {
                Console.ReadLine();
                Process.Start(new ProcessStartInfo(outputFile) { UseShellExecute = true });
            }
        }

        // Credit: https://stackoverflow.com/a/42680957
        public static IEnumerable<T> SelectManyRecursive<T>(IEnumerable<T> source, Func<T, IEnumerable<T>> selector)
        {
            if (!source.Any())
            {
                return source;
            }

            return source.Concat(SelectManyRecursive<T>(source.SelectMany(selector), selector));
        }
    }

    public class Node
    {
        public Node()
        {
            this.children = new List<Node>();
        }

        public string name { get; set; }

        public string parent { get; set; }

        public string url { get; set; }

        public List<Node> children { get; set; }

        [Ignore, JsonIgnore]
        internal int row { get; set; }

        [Ignore, JsonIgnore]
        internal int col { get; set; }

        [Ignore, JsonIgnore]
        internal List<int> cols { get; set; }

        [Ignore, JsonIgnore]
        internal List<int> parentCols { get; set; }

        [Ignore, JsonIgnore]
        internal int colspan { get; set; }

        // https://www.newtonsoft.com/json/help/html/ConditionalProperties.htm
        // small-case children (should match prop-name)
        public bool ShouldSerializechildren()
        {
            return this.children?.Count > 0;
        }
    }
}
