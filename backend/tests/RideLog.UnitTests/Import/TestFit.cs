using Dynastream.Fit;
using File = Dynastream.Fit.File;

namespace RideLog.UnitTests.Import;

/// <summary>Builds valid FIT byte payloads with the SDK encoder for importer/parser tests.</summary>
internal static class TestFit
{
    /// <param name="unpositionedPrefix">
    /// How many leading records carry no lat/lon — what a device logs while it is switched on but
    /// still waiting for a GPS fix.
    /// </param>
    public static byte[] Build(
        (System.DateTime Time, sbyte Temp)[] samples, double lat = 47.5, double lon = 19.0, int unpositionedPrefix = 0)
    {
        using var stream = new MemoryStream();
        var encoder = new Encode(ProtocolVersion.V20);
        encoder.Open(stream);

        var fileId = new FileIdMesg();
        fileId.SetType(File.Activity);
        fileId.SetTimeCreated(new Dynastream.Fit.DateTime(samples[0].Time));
        encoder.Write(fileId);

        for (var i = 0; i < samples.Length; i++)
        {
            var (time, temp) = samples[i];
            var record = new RecordMesg();
            record.SetTimestamp(new Dynastream.Fit.DateTime(time));
            if (i >= unpositionedPrefix)
            {
                record.SetPositionLat((int)(lat / 180.0 * int.MaxValue));
                record.SetPositionLong((int)(lon / 180.0 * int.MaxValue));
            }
            record.SetAltitude(100f);
            record.SetTemperature(temp);
            encoder.Write(record);
        }

        var session = new SessionMesg();
        session.SetStartTime(new Dynastream.Fit.DateTime(samples[0].Time));
        session.SetSport(Sport.Cycling);
        encoder.Write(session);

        encoder.Close();
        return stream.ToArray();
    }
}
