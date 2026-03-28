using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace SubtitlesParser.Classes.Parsers;

public sealed class TtmlParser : IXmlFormatSubtitlesParser
{
    private const int NetflixTicksToMilliseconds = 10_000;

    public List<SubtitleItem> ParseStream(Stream xmlStream, Encoding encoding)
    {
        var items = new List<SubtitleItem>();

        // parse xml stream
        var xElement = XElement.Load(xmlStream);
        var tt = xElement.GetNamespaceOfPrefix("tt") ?? xElement.GetDefaultNamespace();

        var nodeList = xElement.Descendants(tt + "p").ToList();
        foreach (var node in nodeList)
        {
            try
            {
                var reader = node.CreateReader();
                reader.MoveToContent();
                var beginString = node.Attribute("begin").Value.Replace("t", "");
                var startTicks = ParseTimecode(beginString);
                var endString = node.Attribute("end").Value.Replace("t", "");
                var endTicks = ParseTimecode(endString);
                var text = reader.ReadInnerXml()
                    .Replace("<tt:", "<")
                    .Replace("</tt:", "</")
                    .Replace(string.Format(@" xmlns:tt=""{0}""", tt), "")
                    .Replace(string.Format(@" xmlns=""{0}""", tt), "");

                items.Add(new SubtitleItem()
                {
                    StartTime = startTicks,
                    EndTime = endTicks,
                    Lines = [text]
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception raised when parsing xml node {0}: {1}", node, ex);
            }
        }

        if (items.Any())
        {
            return items;
        }
        throw new ArgumentException("Stream is not in a valid TTML format, or represents empty subtitles");
    }

    /// <summary>
    /// Takes an SRT timecode as a string and parses it into a double (in seconds). A SRT timecode reads as follows:
    /// 00:00:20,000
    /// </summary>
    /// <param name="s">The timecode to parse</param>
    /// <returns>The parsed timecode as a TimeSpan instance. If the parsing was unsuccessful, -1 is returned (subtitles should never show)</returns>
    private static TimeSpan ParseTimecode(string s)
    {
        if (TimeSpan.TryParse(s, out TimeSpan result))
        {
            return result;
        }
        // Netflix subtitles have a weird format: timecodes are specified as ticks. Ex: begin="79249170t"
        if (long.TryParse(s.TrimEnd('t'), out long ticks))
        {
            return TimeSpan.FromMilliseconds(ticks / NetflixTicksToMilliseconds);
        }
        return TimeSpan.MaxValue;
    }
}