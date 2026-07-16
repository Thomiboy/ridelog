namespace RideLog.Application.Import;

/// <summary>One uploaded file: its original name and raw bytes.</summary>
public sealed record ActivityFile(string FileName, byte[] Content);
