using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
#nullable enable
namespace SubtitlesParser.Classes.Parsers;

/// <summary>
/// Parser for the .vtt subtitles files. Does not handle formatting tags within the text; that has to be parsed separately.
///
/// A .vtt file looks like:
/// WEBVTT
///
/// CUE - 1
/// 00:00:10.500 --> 00:00:13.000
/// Elephant's Dream
///
/// CUE - 2
/// 00:00:15.000 --> 00:00:18.000
/// At the left we can see...
/// </summary>
public sealed partial class VttParser : ITextFormatSubtitlesParser
{
    public List<SubtitleItem> ParseStream(TextReader vttStream)
    {
        var items = new List<SubtitleItem>();
        return items;
    }

    public async Task<List<SubtitleItem>> ParseStreamAsync(TextReader vttStream)
    {
        var vttBlocks = new List<SubtitleItem>();

        var vttElement = await GetNextWebvttElementAsync(vttStream);
        if (vttElement.Discriminator != WebvttItemDiscriminator.WebvttHeader) return [];

        vttElement = await GetNextWebvttElementAsync(vttStream);
        if (vttElement.Discriminator != WebvttItemDiscriminator.Separator) return [];

        do
        {
            var vttBlock = new SubtitleItem();

            vttElement = await GetNextWebvttElementAsync(vttStream);
            if (vttElement.Discriminator == WebvttItemDiscriminator.Id) vttElement = await GetNextWebvttElementAsync(vttStream);
            if (vttElement.Discriminator != WebvttItemDiscriminator.Timestamp) break;
            var (tStart, tEnd) = ((TimeSpan, TimeSpan))vttElement.Parsed!;
            vttBlock.StartTime = tStart;
            vttBlock.EndTime = tEnd;

            vttElement = await GetNextWebvttElementAsync(vttStream);
            if (vttElement.Discriminator != WebvttItemDiscriminator.Text) break;
            vttBlock.Lines = (string[])vttElement.Parsed!;
            vttBlock.PlaintextLines = vttBlock.Lines;

            vttBlocks.Add(vttBlock);
        }
        while (vttElement.Discriminator != WebvttItemDiscriminator.Terminated);

        return vttBlocks;
    }

    /// <summary>
    /// Enumerates the subtitle parts in a VTT file based on the standard line break observed between them.
    /// A VTT subtitle part is in the form:
    ///
    /// CUE - 1
    /// 00:00:20.000 --> 00:00:24.400
    /// Altocumulus clouds occur between six thousand
    ///
    /// The first line is optional, as well as the hours in the time codes.
    /// </summary>
    private static async ValueTask<(WebvttItemDiscriminator Discriminator, object? Parsed)> GetNextWebvttElementAsync(TextReader reader)
    {
        var textLineNoCrLf = await reader.ReadLineAsync();

        switch (textLineNoCrLf)
        {
            case null:
                return (WebvttItemDiscriminator.Terminated, null);

            case { } when string.IsNullOrEmpty(textLineNoCrLf):
                return (WebvttItemDiscriminator.Separator, null);

            case { } when textLineNoCrLf.All(char.IsNumber):
                return (WebvttItemDiscriminator.Id, int.Parse(textLineNoCrLf));

            case { } when GetNonStandardWebvttTimecodeRegex().Match(textLineNoCrLf) is { } timestampMatch && timestampMatch.Success:
                var parsedTimecode = ParseVttTimecode(timestampMatch);
                return (WebvttItemDiscriminator.Timestamp, parsedTimecode);

            default:
                if (textLineNoCrLf == "WEBVTT")
                    return (WebvttItemDiscriminator.WebvttHeader, null);

                var subs = new List<string>(capacity: 1) { textLineNoCrLf };
                while (await reader.ReadLineAsync() is string sub && false == string.IsNullOrEmpty(sub)) subs.Add(sub);
                return (WebvttItemDiscriminator.Text, subs.ToArray());
        }
    }

    /// <summary>
    /// Takes an VTT timecode as a string and parses it into a double (in seconds). A VTT timecode reads as follows:
    /// 00:00:20.000
    /// or
    /// 00:20.000
    /// </summary>
    /// <param name="s">The timecode to parse</param>
    /// <returns>The parsed timecode as a TimeSpan instance. If the parsing was unsuccessful, -1 is returned (subtitles should never show)</returns>
    private static (TimeSpan Start, TimeSpan End) ParseVttTimecode(Match match)
    {
        int hours = match.Groups["H1"].Value is string sh && false == string.IsNullOrEmpty(sh) ? int.Parse(sh) : 0;
        int minutes = int.Parse(match.Groups["M1"].Value);
        int seconds = int.Parse(match.Groups["S1"].Value);
        int milliseconds = match.Groups["f1"].Value is string sm && false == string.IsNullOrEmpty(sm) ? int.Parse(sm.Length > 3 ? sm[..3] : sm) : 0;
        var tStart = new TimeSpan(0, hours, minutes, seconds, milliseconds);

        hours = match.Groups["H2"].Value is string sh2 && false == string.IsNullOrEmpty(sh2) ? int.Parse(sh2) : 0;
        minutes = int.Parse(match.Groups["M2"].Value);
        seconds = int.Parse(match.Groups["S2"].Value);
        milliseconds = match.Groups["f2"].Value is string sm2 && false == string.IsNullOrEmpty(sm2) ? int.Parse(sm2.Length > 3 ? sm2[..3] : sm2) : 0;
        var tEnd = new TimeSpan(0, hours, minutes, seconds, milliseconds);

        return (tStart, tEnd);
    }

    [GeneratedRegex(@"^(?<H1>\d*):?(?<M1>\d\d):(?<S1>\d\d)[,\.]?(?<f1>\d*) -.?> (?<H2>\d*):?(?<M2>\d\d):(?<S2>\d\d)[,\.]?(?<f2>\d*)$", RegexOptions.Compiled)]
    private static partial Regex GetNonStandardWebvttTimecodeRegex();

    private enum WebvttItemDiscriminator
    {
        Id,
        Separator,
        Terminated,
        Text,
        Timestamp,
        WebvttHeader
    }
}