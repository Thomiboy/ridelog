namespace RideLog.Application.Import;

/// <summary>Parses one activity file format (GPX, TCX, …) into a <see cref="ParsedActivity"/>.</summary>
public interface IActivityFileParser
{
    /// <summary>Whether this parser handles the given file (matched on extension).</summary>
    bool CanParse(string fileName);

    /// <summary>Parses the file content. Throws when the payload is malformed.</summary>
    ParsedActivity Parse(Stream content, string fileName);
}
