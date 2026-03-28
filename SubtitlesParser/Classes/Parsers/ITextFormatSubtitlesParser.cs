using System.Collections.Generic;
using System.IO;

namespace SubtitlesParser.Classes.Parsers;

/// <summary>
/// Interface specifying the required method for a SubParser.
/// </summary>
public interface ITextFormatSubtitlesParser
{
    /// <summary>
    /// Parses a subtitles file stream in a list of SubtitleItem
    /// </summary>
    /// <param name="stream">The subtitles file stream to parse</param>
    /// <returns>The corresponding list of SubtitleItems</returns>
    List<SubtitleItem> ParseStream(TextReader stream);

    // /// <summary>
    // /// Parses a subtitles file stream in a list of SubtitleItem
    // /// </summary>
    // /// <param name="stream">The subtitles file stream to parse</param>
    // /// <param name="encoding">The stream encoding (if known)</param>
    // /// <returns>The corresponding list of SubtitleItems</returns>
    //System.Threading.Tasks.Task<List<SubtitleItem>> ParseStreamAsync(TextReader stream, Encoding encoding);
    // to implement in all parsers
}
