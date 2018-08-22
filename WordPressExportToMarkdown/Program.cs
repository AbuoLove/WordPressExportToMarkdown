/*
 * TODO:
 * - 
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ReverseMarkdown;

namespace WordPressExportToMarkdown
{
    class Program
    {
        const string imagesDirectory =
#if DEBUG
            "";
#else
            "images";
#endif
        const string outputPath = "/Users/marcos/Repositorios/marcoscobena/items";
        const string importMessage = "*(This post was imported, please [contact](#/contact) me if there's anything wrong with it. Thanks in advance)*";

        static readonly string[] postTypes = { "page", "post" };
        static readonly string[] postsToBypass = { "about", "about-2", "blog", "contact" };
        static readonly string[] sourcecodeTags = { "code", "sourcecode" };
        static readonly string[] sourcecodeLanguages = { "c-sharp", "csharp", "xml" };

        static void Main(string[] args)
        {
            var xmlDocument = XDocument.Load("marcoscobeamorin.wordpress.2018-08-01.001.xml");
            var items = xmlDocument.Descendants("item");
            XNamespace wpNamespace = "http://wordpress.org/export/1.2/";
            var filteredItems = items.Where(item => postTypes.Contains(item.Element(wpNamespace + "post_type")?.Value));
            XNamespace contentNamespace = "http://purl.org/rss/1.0/modules/content/";
            //var htmlTags = new List<string>();
            var wordPressTags = new List<string>();
            var markdownConverter = new Converter();

            foreach (var item in filteredItems)
            {
                var title = item.Element("title").Value;
                var date = DateTime.Parse(item.Element("pubDate").Value);
                var filename = item.Element(wpNamespace + "post_name").Value;

                if (postsToBypass.Contains(filename))
                {
                    continue;
                }

                var content = item.Element(contentNamespace + "encoded")?.Value;

                //var tags = Regex.Matches(content, @"<\w*?>")
                //     .Select(match => match.Value)
                //     .Distinct();
                //htmlTags.AddRange(tags);

                var tags = Regex.Matches(content, @"\[.*\](.+)")
                                .Select(match => match.Value)
                                .Distinct();
                wordPressTags.AddRange(tags);

                content = TranslateCaptionsIntoMarkdown(content);
                content = TranslateYoutubeIntoMarkdown(content);
                content = TranslateSourceCodeIntoMarkdown(content);
                content = DownloadImagesAndReplaceUrls(content);
                // FIXME it removes CRs I guess because expects <h*> or <br />
                //content = markdownConverter.Convert(content);
                //content = content.Replace("\n\n", "\n");
                content = content.Insert(0, $"{importMessage}\n\n");

                File.WriteAllText($"{outputPath}{Path.DirectorySeparatorChar}{filename}.md", content);

                Console.WriteLine($"addPost(\"{title}\", \"{filename}\", \"{date.ToShortDateString()}\");");
            }

            //htmlTags = htmlTags.Distinct()
            //                   .ToList();
            wordPressTags = wordPressTags.Distinct()
            .ToList();
        }

        private static string TranslateSourceCodeIntoMarkdown(string content)
        {
            // [sourcecode language="c-sharp"]
            // ...
            // [/sourcecode]

            //var tuples = Regex.Matches(content, @"\[sourcecode language=(.+)\](.+)\[/sourcecode\]")
            //    .Select(match => (original: match.Value, language: match.Groups[0].Value, body: match.Groups[1].Value));

            //foreach (var tuple in tuples)
            //{
            //    var markdown = $"```{tuple.body}```";
            //    content = content.Replace(tuple.original, markdown);
            //}

            var translation = content;

            foreach (var tag in sourcecodeTags)
            {
                foreach (var language in sourcecodeLanguages)
                {
                    translation = translation.Replace($"[{tag} language=\"{language}\"]", "```c-sharp");
                }

                translation = translation.Replace($"[/{tag}]", "```");
            }

            return translation;
        }

        private static string TranslateYoutubeIntoMarkdown(string content)
        {
            const string videoAttribute = "v=";
            // [youtube https://www.youtube.com/watch?v=W9LJDwZOprY&amp;w=560h=315]
            var tuples = Regex.Matches(content, @"\[youtube (.+)\]")
                .Select(match => (original: match.Value, translation: match.Groups[1].Value));

            foreach (var tuple in tuples)
            {
                var url = tuple.translation;
                var index = url.IndexOf("&amp;", StringComparison.InvariantCultureIgnoreCase);

                if (index > 0)
                {
                    url = url.Substring(0, index);
                }

                // [![IMAGE ALT TEXT HERE](https://img.youtube.com/vi/YOUTUBE_VIDEO_ID_HERE/0.jpg)]
                // (https://www.youtube.com/watch?v=YOUTUBE_VIDEO_ID_HERE)
                var videoId = url.Substring(
                    url.IndexOf(videoAttribute, StringComparison.InvariantCultureIgnoreCase) + videoAttribute.Length);
                var markdown = $"[![](https://img.youtube.com/vi/{videoId}/0.jpg)]" +
                    $"(https://www.youtube.com/watch?v={videoId})";
                content = content.Replace(tuple.original, markdown);
            }

            return content;
        }

        private static string TranslateCaptionsIntoMarkdown(string content)
        {
            var tuples = Regex.Matches(content, @"\[caption .*\](.+)\[/caption\]")
                .Select(match => (original: match.Value, translation: match.Groups[1].Value));

            foreach (var tuple in tuples)
            {
                var index = tuple.translation.IndexOf('>') + 1;
                var translation = tuple.translation
                                       .Remove(index, 1)
                                       .Insert(index, Environment.NewLine)
                                       .Insert(index + 1, "*")
                                       + "*";
                content = content.Replace(tuple.original, translation);
            }

            return content;
        }

        private static string DownloadImagesAndReplaceUrls(string content)
        {
            var urls = Regex.Matches(content, "src\\s*=\\s*\"(.+?)\"")
                            .Select(match => match.Groups[1].Value)
                            .Where(url => url.Contains("files.wordpress.com"));
            var webClient = new WebClient();

            foreach (var url in urls)
            {
                var fileName = Path.GetFileName(url);

                if (fileName.Contains('?'))
                {
                    fileName = fileName.Substring(0, fileName.IndexOf('?'));
                }

                webClient.DownloadFile(
                    new Uri(url), $"{outputPath}{Path.DirectorySeparatorChar}{imagesDirectory}" +
                        $"{Path.DirectorySeparatorChar}{fileName}");

                content = content.Replace(url, $"items/{imagesDirectory}/{fileName}");
            }

            return content;
        }
    }
}
