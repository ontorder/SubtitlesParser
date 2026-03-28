using System;

namespace SubtitlesParser.Classes;

public sealed class SubtitleItem
{
    public TimeSpan StartTime { get; set; }

    public TimeSpan EndTime { get; set; }

    /// <summary>
    /// The raw subtitle string from the file
    /// May include formatting
    /// </summary>
    public string[] Lines { get; set; }

    /// <summary>
    /// The plain-text string from the file
    /// Does not include formatting
    /// </summary>
    public string[] PlaintextLines { get; set; }


    public SubtitleItem()
    {
        Lines = [];
        PlaintextLines = [];
    }

    public override string ToString()
    {
        var res = string.Format("{0} --> {1}: {2}", StartTime.ToString("G"), EndTime.ToString("G"), string.Join(Environment.NewLine, Lines));
        return res;
    }
}