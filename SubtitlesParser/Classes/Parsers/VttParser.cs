using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
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
        var vttBlocks = new List<SubtitleItem>();

        var vttElement = GetNextWebvttElement(vttStream);
        if (vttElement.Discriminator != WebvttItemDiscriminator.WebvttHeader) return [];

        vttElement = GetNextWebvttElement(vttStream);
        if (vttElement.Discriminator != WebvttItemDiscriminator.Separator) return [];

        var resync = false;

        do
        {
            if (resync) TryFindBlockStart(vttStream);
            (resync, var vttBlock) = ParseVttBlock(vttStream);
            if (resync) continue;
            if (vttBlock == null) break;

            vttBlocks.Add(vttBlock);
        }
        while (vttElement.Discriminator != WebvttItemDiscriminator.Terminated);

        return vttBlocks;
    }

    public async Task<List<SubtitleItem>> ParseStreamAsync(TextReader vttStream, CancellationToken cancellation)
    {
        var vttBlocks = new List<SubtitleItem>();

        var vttElement = await GetNextWebvttElementAsync(vttStream, cancellation);
        if (vttElement.Discriminator != WebvttItemDiscriminator.WebvttHeader) return [];

        vttElement = await GetNextWebvttElementAsync(vttStream, cancellation);
        if (vttElement.Discriminator != WebvttItemDiscriminator.Separator) return [];

        var resync = false;

        do
        {
            if (resync) await TryFindBlockStartAsync(vttStream, cancellation);
            (resync, var vttBlock) = await ParseVttBlockAsync(vttStream, cancellation);
            if (resync) continue;
            if (vttBlock == null) break;

            vttBlocks.Add(vttBlock);
        }
        while (vttElement.Discriminator != WebvttItemDiscriminator.Terminated);

        return vttBlocks;
    }

    public async IAsyncEnumerable<SubtitleItem> ParseStreamAsyncEnum(TextReader vttStream, [EnumeratorCancellation] CancellationToken cancellation)
    {
        var vttElement = await GetNextWebvttElementAsync(vttStream, cancellation);
        if (vttElement.Discriminator != WebvttItemDiscriminator.WebvttHeader) yield break;

        vttElement = await GetNextWebvttElementAsync(vttStream, cancellation);
        if (vttElement.Discriminator != WebvttItemDiscriminator.Separator) yield break;

        var resync = false;

        do
        {
            if (resync) await TryFindBlockStartAsync(vttStream, cancellation);
            (resync, var vttBlock) = await ParseVttBlockAsync(vttStream, cancellation);
            if (resync) continue;
            if (vttBlock == null) break;

            yield return vttBlock;
        }
        while (vttElement.Discriminator != WebvttItemDiscriminator.Terminated);
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
    private static (WebvttItemDiscriminator Discriminator, object? Parsed) GetNextWebvttElement(TextReader reader)
    {
        var textLineNoCrLf = reader.ReadLine();

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
                while (reader.ReadLine() is string sub && false == string.IsNullOrEmpty(sub)) subs.Add(sub);
                return (WebvttItemDiscriminator.Text, subs.ToArray());
        }
    }

    private static async ValueTask<(WebvttItemDiscriminator Discriminator, object? Parsed)> GetNextWebvttElementAsync(TextReader reader, CancellationToken cancellation)
    {
        var textLineNoCrLf = await reader.ReadLineAsync(cancellation);

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
                while (await reader.ReadLineAsync(cancellation) is string sub && false == string.IsNullOrEmpty(sub)) subs.Add(sub);
                return (WebvttItemDiscriminator.Text, subs.ToArray());
        }
    }

    [GeneratedRegex(@"^(?<H1>\d*):?(?<M1>\d\d):(?<S1>\d\d)[,\.]?(?<f1>\d*) -.?> (?<H2>\d*):?(?<M2>\d\d):(?<S2>\d\d)[,\.]?(?<f2>\d*)$", RegexOptions.Compiled)]
    private static partial Regex GetNonStandardWebvttTimecodeRegex();

    private static (bool Resync, SubtitleItem? VttBlock) ParseVttBlock(TextReader vttStream)
    {
        var vttBlock = new SubtitleItem();

        var vttElement = GetNextWebvttElement(vttStream);
        if (vttElement.Discriminator == WebvttItemDiscriminator.Terminated) return (Resync: false, VttBlock: null);

        if (vttElement.Discriminator == WebvttItemDiscriminator.Id)
            vttElement = GetNextWebvttElement(vttStream);

        if (vttElement.Discriminator == WebvttItemDiscriminator.Terminated) return (Resync: false, VttBlock: null);
        if (vttElement.Discriminator != WebvttItemDiscriminator.Timestamp) return (Resync: true, VttBlock: null);
        var (tStart, tEnd) = ((TimeSpan, TimeSpan))vttElement.Parsed!;
        vttBlock.StartTime = tStart;
        vttBlock.EndTime = tEnd;

        vttElement = GetNextWebvttElement(vttStream);
        if (vttElement.Discriminator == WebvttItemDiscriminator.Terminated) return (Resync: false, VttBlock: null);
        if (vttElement.Discriminator != WebvttItemDiscriminator.Text) return (Resync: true, VttBlock: null);
        vttBlock.Lines = (string[])vttElement.Parsed!;
        vttBlock.PlaintextLines = vttBlock.Lines;

        return (Resync: false, vttBlock);
    }

    private static async ValueTask<(bool Resync, SubtitleItem? VttBlock)> ParseVttBlockAsync(TextReader vttStream, CancellationToken cancellation)
    {
        var vttBlock = new SubtitleItem();

        var vttElement = await GetNextWebvttElementAsync(vttStream, cancellation);
        if (vttElement.Discriminator == WebvttItemDiscriminator.Terminated) return (Resync: false, VttBlock: null);

        if (vttElement.Discriminator == WebvttItemDiscriminator.Id)
            vttElement = await GetNextWebvttElementAsync(vttStream, cancellation);

        if (vttElement.Discriminator == WebvttItemDiscriminator.Terminated) return (Resync: false, VttBlock: null);
        if (vttElement.Discriminator != WebvttItemDiscriminator.Timestamp) return (Resync: true, VttBlock: null);
        var (tStart, tEnd) = ((TimeSpan, TimeSpan))vttElement.Parsed!;
        vttBlock.StartTime = tStart;
        vttBlock.EndTime = tEnd;

        vttElement = await GetNextWebvttElementAsync(vttStream, cancellation);
        if (vttElement.Discriminator == WebvttItemDiscriminator.Terminated) return (Resync: false, VttBlock: null);
        if (vttElement.Discriminator != WebvttItemDiscriminator.Text) return (Resync: true, VttBlock: null);
        vttBlock.Lines = (string[])vttElement.Parsed!;
        vttBlock.PlaintextLines = vttBlock.Lines;

        return (Resync: false, vttBlock);
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

    private static void TryFindBlockStart(TextReader vttStream)
    {
        for (;
            GetNextWebvttElement(vttStream).Discriminator
                is not (WebvttItemDiscriminator.Separator or WebvttItemDiscriminator.Text or WebvttItemDiscriminator.Terminated)
            ;)
            ;
    }

    private static async ValueTask TryFindBlockStartAsync(TextReader vttStream, CancellationToken cancellation)
    {
        for (;
            (await GetNextWebvttElementAsync(vttStream, cancellation)).Discriminator
                is not (WebvttItemDiscriminator.Separator or WebvttItemDiscriminator.Text or WebvttItemDiscriminator.Terminated)
            ;)
            ;
    }

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