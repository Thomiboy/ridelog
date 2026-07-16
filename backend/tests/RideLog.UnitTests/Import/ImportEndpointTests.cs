using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using RideLog.UnitTests.Auth;

namespace RideLog.UnitTests.Import;

public class ImportEndpointTests(RideLogApiFactory factory) : IClassFixture<RideLogApiFactory>
{
    private sealed record LoginRequest(string Email, string Password);
    private sealed record LoginResponse(string Token, DateTimeOffset ExpiresAt);

    private static readonly byte[] Gpx = Encoding.UTF8.GetBytes("""
        <?xml version="1.0" encoding="UTF-8"?>
        <gpx version="1.1" creator="test" xmlns="http://www.topografix.com/GPX/1/1">
          <trk><type>cycling</type><trkseg>
            <trkpt lat="47.5" lon="19.0"><ele>100</ele><time>2026-06-01T08:00:00Z</time></trkpt>
            <trkpt lat="47.6" lon="19.1"><ele>150</ele><time>2026-06-01T09:00:00Z</time></trkpt>
          </trkseg></trk>
        </gpx>
        """);

    private async Task<HttpClient> AdminClientAsync()
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/auth/login", new LoginRequest(RideLogApiFactory.AdminEmail, RideLogApiFactory.AdminPassword));
        response.EnsureSuccessStatusCode();
        var token = (await response.Content.ReadFromJsonAsync<LoginResponse>())!.Token;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static MultipartFormDataContent Upload(params (string Name, byte[] Content)[] files)
    {
        var form = new MultipartFormDataContent();
        foreach (var (name, content) in files)
        {
            var part = new ByteArrayContent(content);
            part.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            form.Add(part, "files", name);
        }
        return form;
    }

    private static readonly byte[] Tcx = Encoding.UTF8.GetBytes("""
        <?xml version="1.0" encoding="UTF-8"?>
        <TrainingCenterDatabase xmlns="http://www.garmin.com/xmlschemas/TrainingCenterDatabase/v2">
          <Activities><Activity Sport="Biking">
            <Id>2026-06-05T07:00:00Z</Id>
            <Lap StartTime="2026-06-05T07:00:00Z"><DistanceMeters>25000</DistanceMeters><Track>
              <Trackpoint><Time>2026-06-05T07:00:00Z</Time>
                <Position><LatitudeDegrees>47.5</LatitudeDegrees><LongitudeDegrees>19.0</LongitudeDegrees></Position>
                <AltitudeMeters>100</AltitudeMeters><HeartRateBpm><Value>130</Value></HeartRateBpm></Trackpoint>
              <Trackpoint><Time>2026-06-05T08:00:00Z</Time>
                <Position><LatitudeDegrees>47.6</LatitudeDegrees><LongitudeDegrees>19.1</LongitudeDegrees></Position>
                <AltitudeMeters>140</AltitudeMeters><HeartRateBpm><Value>150</Value></HeartRateBpm></Trackpoint>
            </Track></Lap>
          </Activity></Activities>
        </TrainingCenterDatabase>
        """);

    private sealed record SummaryDto(int Imported, int Skipped, int Failed);

    [Fact]
    public async Task Anonymous_upload_is_rejected()
    {
        var response = await factory.CreateClient().PostAsync("/import", Upload(("ride.gpx", Gpx)));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Admin_uploads_gpx_and_tcx_and_re_upload_is_idempotent()
    {
        var client = await AdminClientAsync();

        var first = await client.PostAsync("/import", Upload(("ride.gpx", Gpx), ("ride.tcx", Tcx)));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var firstSummary = await first.Content.ReadFromJsonAsync<SummaryDto>();
        Assert.Equal(2, firstSummary!.Imported);

        // Verify the rides are queryable through the API pipeline.
        var second = await client.PostAsync("/import", Upload(("ride.gpx", Gpx), ("ride.tcx", Tcx)));
        var secondSummary = await second.Content.ReadFromJsonAsync<SummaryDto>();
        Assert.Equal(0, secondSummary!.Imported);
        Assert.Equal(2, secondSummary.Skipped);
    }
}
