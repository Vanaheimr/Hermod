using System.Text;

using org.GraphDefined.Vanaheimr.Hermod;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Illias;

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP;

/// <summary>
/// Tests for the /hashimage/{fingerprint} endpoint of the HTTPExtAPI, which
/// renders the OpenSSH "drunken bishop" randomart of a fingerprint as SVG.
/// </summary>
[TestFixture]
public sealed class HashImageTests
{

    #region HashImage_Must_Render_Fingerprint_As_SVG()

    [Test]
    public async Task HashImage_Must_Render_Fingerprint_As_SVG()
    {

        var server = new HTTPServer(
                         IPAddress: IPv4Address.Localhost,
                         TCPPort:   IPPort.Zero,
                         AutoStart: false
                     );

        try
        {

            _ = new HTTPExtAPI(
                    server,
                    SkipURLTemplates:     false,
                    DisableLogging:       true,
                    DisableNotifications: true
                );

            var response = await Invoke(server, "/hashimage/fc:94:b0:c1:e5:b0:98:7c:58:43:99:76:97:ee:9f:b7");
            var svg      = Encoding.UTF8.GetString(response.HTTPBody!);

            Assert.Multiple(() =>
            {
                Assert.That(response.HTTPStatusCode.Code,       Is.EqualTo(200));
                Assert.That(response.ContentType,               Is.EqualTo(HTTPContentType.Image.SVG));
                Assert.That(svg,                                Does.StartWith("<svg "));
                Assert.That(svg,                                Does.EndWith("</svg>"));
                Assert.That(svg,                                Does.Contain("viewBox=\"0 0 130 130\""));
                Assert.That(svg.Split("<rect ").Length - 1,     Is.EqualTo(13 * 13));
            });

        }
        finally
        {
            await server.Stop();
        }

    }

    #endregion

    #region HashImage_With_URLEncoded_Separators_Must_Render_As_SVG()

    [Test]
    public async Task HashImage_With_URLEncoded_Separators_Must_Render_As_SVG()
    {

        var server = new HTTPServer(
                         IPAddress: IPv4Address.Localhost,
                         TCPPort:   IPPort.Zero,
                         AutoStart: false
                     );

        try
        {

            _ = new HTTPExtAPI(
                    server,
                    SkipURLTemplates:     false,
                    DisableLogging:       true,
                    DisableNotifications: true
                );

            var response = await Invoke(server, "/hashimage/AE0D%205C5C%204EB5%20C3F0");
            var svg      = Encoding.UTF8.GetString(response.HTTPBody!);

            Assert.Multiple(() =>
            {
                Assert.That(response.HTTPStatusCode.Code,  Is.EqualTo(200));
                Assert.That(response.ContentType,          Is.EqualTo(HTTPContentType.Image.SVG));
                Assert.That(svg,                           Does.StartWith("<svg "));
            });

        }
        finally
        {
            await server.Stop();
        }

    }

    #endregion

    #region HashImage_With_Different_Fingerprints_Must_Render_Different_Images()

    [Test]
    public async Task HashImage_With_Different_Fingerprints_Must_Render_Different_Images()
    {

        var server = new HTTPServer(
                         IPAddress: IPv4Address.Localhost,
                         TCPPort:   IPPort.Zero,
                         AutoStart: false
                     );

        try
        {

            _ = new HTTPExtAPI(
                    server,
                    SkipURLTemplates:     false,
                    DisableLogging:       true,
                    DisableNotifications: true
                );

            var response1  = await Invoke(server, "/hashimage/fc:94:b0:c1:e5:b0:98:7c:58:43:99:76:97:ee:9f:b7");
            var response2  = await Invoke(server, "/hashimage/AE0D-5C5C-4EB5-C3F0-683E-2173-B1EA-6EEA-A89A-2896");

            var svg1       = Encoding.UTF8.GetString(response1.HTTPBody!);
            var svg2       = Encoding.UTF8.GetString(response2.HTTPBody!);

            Assert.Multiple(() =>
            {
                Assert.That(response1.HTTPStatusCode.Code,  Is.EqualTo(200));
                Assert.That(response2.HTTPStatusCode.Code,  Is.EqualTo(200));
                Assert.That(svg1,                           Is.Not.EqualTo(svg2));
            });

        }
        finally
        {
            await server.Stop();
        }

    }

    #endregion

    #region HashImage_With_Invalid_Fingerprint_Must_Return_400()

    [Test]
    public async Task HashImage_With_Invalid_Fingerprint_Must_Return_400()
    {

        var server = new HTTPServer(
                         IPAddress: IPv4Address.Localhost,
                         TCPPort:   IPPort.Zero,
                         AutoStart: false
                     );

        try
        {

            _ = new HTTPExtAPI(
                    server,
                    SkipURLTemplates:     false,
                    DisableLogging:       true,
                    DisableNotifications: true
                );

            var notHex   = await Invoke(server, "/hashimage/not-a-fingerprint");
            var tooLong  = await Invoke(server, "/hashimage/" + new String('a', 130));
            var odd      = await Invoke(server, "/hashimage/abc");

            Assert.Multiple(() =>
            {
                Assert.That(notHex. HTTPStatusCode.Code,  Is.EqualTo(400));
                Assert.That(tooLong.HTTPStatusCode.Code,  Is.EqualTo(400));
                Assert.That(odd.    HTTPStatusCode.Code,  Is.EqualTo(400));
            });

        }
        finally
        {
            await server.Stop();
        }

    }

    #endregion


    #region (private) Invoke(server, path)

    private static async Task<HTTPResponse> Invoke(HTTPServer  server,
                                                   String      path)
    {

        var request        = ParseGETRequest(server, path);
        var parsedRequest  = server.GetRequestHandle(request);

        Assert.That(parsedRequest.RequestHandlers?.RequestHandler, Is.Not.Null);

        // Mirror the request pipeline: hand the parsed URL parameters to the request.
        request.ParsedURLParametersX = parsedRequest.Parameters;

        return await parsedRequest.RequestHandlers!.RequestHandler!(request);

    }

    #endregion

    #region (private) ParseGETRequest(server, path)

    private static HTTPRequest ParseGETRequest(HTTPServer  server,
                                               String      path)
    {

        var localSocket   = IPSocket.LocalhostV4(IPPort.Parse(6801));
        var remoteSocket  = IPSocket.LocalhostV4(IPPort.Parse(43123));

        var parsed = HTTPRequest.TryParse(
                         Timestamp.Now,
                         new HTTPSource(remoteSocket),
                         localSocket,
                         remoteSocket,
                         "GET " + path + " HTTP/1.1\r\n" +
                         "Host: example.test\r\n" +
                         "Accept: image/svg+xml\r\n" +
                         "Connection: close\r\n",
                         out var request,
                         out var errorResponse,
                         HTTPServer:        server,
                         CancellationToken: CancellationToken.None
                     );

        Assert.That(parsed, Is.True, errorResponse?.ToString());
        Assert.That(request, Is.Not.Null);

        return request!;

    }

    #endregion

}
