using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Collections;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;
using org.GraphDefined.Vanaheimr.Hermod.Mail;
using org.GraphDefined.Vanaheimr.Illias;

using HermodIPAddress = org.GraphDefined.Vanaheimr.Hermod.IPAddress;

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP;

/// <summary>
/// Safe regression specifications for the Hermod HTTP/1.1 audit.
/// Confirmed fixes remain active in the ordinary suite; the tests use only local
/// parsers, handlers and loopback connections.
/// </summary>
[TestFixture]
public sealed class HTTP11AuditRegressionTests
{

    [Test]
    public void Canonical_HTTP11_Request_Must_Parse()
    {
        var parsed = TryParseRequest(
                         "GET / HTTP/1.1\r\n" +
                         "Host: example.test\r\n" +
                         "Connection: close\r\n",
                         out var request
                     );

        Assert.That(parsed, Is.True);
        Assert.That(request, Is.Not.Null);
        Assert.That(request!.Host.ToString(), Is.EqualTo("example.test"));
    }

    [Test]
    public async Task Internal_Server_Error_Must_Hide_Exception_Details_By_Default()
    {
        const String sensitiveExceptionMessage = "C012-sensitive-exception-marker";

        var server = new HTTPServer(
                         IPAddress: IPv4Address.Localhost,
                         TCPPort:   IPPort.Zero,
                         AutoStart: false
                     );

        try
        {
            var api = new HTTPAPI(server);
            api.AddHandler(
                HTTPPath.Root,
                HTTPMethod: HTTPMethod.GET,
                HTTPDelegate: _ => Task.FromException<HTTPResponse>(
                                       new InvalidOperationException(sensitiveExceptionMessage)
                                   )
            );

            await server.Start();

            using var client = new TcpClient(AddressFamily.InterNetwork);
            await client.ConnectAsync(System.Net.IPAddress.Loopback, server.TCPPort.ToInt32());

            await using var stream = client.GetStream();
            using var reader = new StreamReader(
                                   stream,
                                   Encoding.ASCII,
                                   detectEncodingFromByteOrderMarks: false,
                                   leaveOpen: true
                               );
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(3));

            await stream.WriteAsync(
                Encoding.ASCII.GetBytes(
                    "GET / HTTP/1.1\r\n" +
                    "Host: example.test\r\n" +
                    "Connection: close\r\n\r\n"
                ),
                cancellation.Token
            );

            var response = await ReadSingleHTTPResponse(reader, cancellation.Token);

            Assert.Multiple(() =>
            {
                Assert.That(response.StatusLine, Does.Contain("500 Internal Server Error"));
                Assert.That(response.Body,       Does.Contain("An internal server error occurred."));
                Assert.That(response.Body,       Does.Contain("eventTrackingId"));
                Assert.That(response.Body,       Does.Not.Contain(sensitiveExceptionMessage));
                Assert.That(response.Body,       Does.Not.Contain("stackTrace"));
                Assert.That(response.Body,       Does.Not.Contain("\"source\""));
                Assert.That(response.Body,       Does.Not.Contain("\"type\""));
            });
        }
        finally
        {
            await server.Stop();
        }
    }

    [Test]
    public async Task Internal_Server_Error_May_Include_Exception_Details_When_Explicitly_Enabled()
    {
        const String sensitiveExceptionMessage = "C012-explicit-diagnostics-marker";

        var server = new HTTPServer(
                         IPAddress:                            IPv4Address.Localhost,
                         TCPPort:                              IPPort.Zero,
                         AutoStart:                            false,
                         IncludeStackTracesInErrorResponses:  true
                     );

        try
        {
            Assert.That(server.IncludeStackTracesInErrorResponses, Is.True);

            var api = new HTTPAPI(server);
            api.AddHandler(
                HTTPPath.Root,
                HTTPMethod: HTTPMethod.GET,
                HTTPDelegate: _ => Task.FromException<HTTPResponse>(
                                       new InvalidOperationException(sensitiveExceptionMessage)
                                   )
            );

            await server.Start();

            using var client = new TcpClient(AddressFamily.InterNetwork);
            await client.ConnectAsync(System.Net.IPAddress.Loopback, server.TCPPort.ToInt32());

            await using var stream = client.GetStream();
            using var reader = new StreamReader(
                                   stream,
                                   Encoding.ASCII,
                                   detectEncodingFromByteOrderMarks: false,
                                   leaveOpen: true
                               );
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(3));

            await stream.WriteAsync(
                Encoding.ASCII.GetBytes(
                    "GET / HTTP/1.1\r\n" +
                    "Host: example.test\r\n" +
                    "Connection: close\r\n\r\n"
                ),
                cancellation.Token
            );

            var response = await ReadSingleHTTPResponse(reader, cancellation.Token);

            Assert.Multiple(() =>
            {
                Assert.That(response.StatusLine, Does.Contain("500 Internal Server Error"));
                Assert.That(response.Body,       Does.Contain(sensitiveExceptionMessage));
                Assert.That(response.Body,       Does.Contain("stackTrace"));
                Assert.That(response.Body,       Does.Contain("\"source\""));
                Assert.That(response.Body,       Does.Contain("\"type\""));
            });

            server.IncludeStackTracesInErrorResponses = false;
            Assert.That(server.IncludeStackTracesInErrorResponses, Is.False);
        }
        finally
        {
            await server.Stop();
        }
    }

    [Test]
    public async Task Login_Must_Bind_Session_To_The_PasswordVerified_User()
    {
        var server = new HTTPServer(
                         IPAddress: HermodIPAddress.Localhost,
                         TCPPort:   IPPort.Zero,
                         AutoStart: false
                     );

        try
        {
            var api = new HTTPExtAPI(
                          server,
                          SkipURLTemplates:     false,
                          DisableLogging:       false,
                          DisableNotifications: true
                      );

            var duplicateEMail = SimpleEMailAddress.Parse("duplicate-login@example.test");
            var firstUser      = new User(
                                     User_Id.Parse("first-login-user"),
                                     I18NString.Create("First Login User"),
                                     duplicateEMail
                                 );
            var verifiedUser   = new User(
                                     User_Id.Parse("verified-login-user"),
                                     I18NString.Create("Verified Login User"),
                                     duplicateEMail
                                 );
            var organization   = new Organization(
                                     Organization_Id.Parse("login-test-org"),
                                     I18NString.Create("Login Test Organization")
                                 );

            verifiedUser.AddOutgoingEdge(User2OrganizationEdgeLabel.IsMember, organization);

            AddPrivateDictionaryEntry(api, "users", firstUser.Id, firstUser);
            AddPrivateDictionaryEntry(api, "users", verifiedUser.Id, verifiedUser);
            AddPrivateDictionaryEntry(
                api,
                "loginPasswords",
                firstUser.Id,
                new LoginPassword(firstUser.Id, Password.Parse("FirstPassword-123!"))
            );
            AddPrivateDictionaryEntry(
                api,
                "loginPasswords",
                verifiedUser.Id,
                new LoginPassword(verifiedUser.Id, Password.Parse("VerifiedPassword-123!"))
            );

            var request = ParseFormRequest(
                              server,
                              "/login",
                              "login=duplicate-login%40example.test&password=VerifiedPassword-123!&redirect=%2F",
                              CancellationToken.None
                          );
            var handler = GetRegisteredHandler(server, request);
            var response = await handler(request);

            Assert.That(response.HTTPStatusCode.Code, Is.EqualTo(201));

            var cookiesField = typeof(HTTPExtAPI).GetField(
                                   "httpCookies",
                                   BindingFlags.Instance | BindingFlags.NonPublic
                               );
            Assert.That(cookiesField, Is.Not.Null);

            var cookieStore = cookiesField!.GetValue(api)!;
            var tokens = ((IEnumerable) cookieStore.GetType().GetProperty("Values")!.GetValue(cookieStore)!).
                             OfType<SecurityToken>().
                             ToArray();

            Assert.That(tokens, Has.Some.Matches<SecurityToken>(token => token.UserId == verifiedUser.Id));
            Assert.That(tokens, Has.None.Matches<SecurityToken>(token => token.UserId == firstUser.Id));
        }
        finally
        {
            await server.Stop();
        }
    }

    [Test]
    public async Task Login_Redirect_Must_Be_Local_And_HTML_Encoded()
    {
        var server = new HTTPServer(
                         IPAddress: HermodIPAddress.Localhost,
                         TCPPort:   IPPort.Zero,
                         AutoStart: false
                     );

        try
        {
            _ = CreateLoginFixture(server, out _);
            var request = ParseFormRequest(
                              server,
                              "/login",
                              "login=redirect-login%40example.test&password=RedirectPassword-123!&redirect=https%3A%2F%2Fevil.example%2Fsteal",
                              CancellationToken.None
                          );
            var handler = GetRegisteredHandler(server, request);
            var response = await handler(request);
            var responseBody = response.GetResponseBodyAsUTF8String(HTTPContentType.Text.HTML_UTF8);

            Assert.That(response.HTTPStatusCode.Code, Is.EqualTo(201));
            Assert.That(responseBody, Does.Contain("url=/\""));
            Assert.That(responseBody, Does.Not.Contain("evil.example"));

            request = ParseFormRequest(
                          server,
                          "/login",
                          "login=redirect-login%40example.test&password=RedirectPassword-123!&redirect=%2Fdashboard%3Fnext%3D%22%3E%3Cscript%3Ealert(1)%3C%2Fscript%3E",
                          CancellationToken.None
                      );
            handler = GetRegisteredHandler(server, request);
            response = await handler(request);
            responseBody = response.GetResponseBodyAsUTF8String(HTTPContentType.Text.HTML_UTF8);

            Assert.That(response.HTTPStatusCode.Code, Is.EqualTo(201));
            Assert.That(responseBody, Does.Contain("/dashboard?next=&quot;&gt;&lt;script&gt;alert(1)&lt;/script&gt;"));
            Assert.That(responseBody, Does.Not.Contain("<script>"));
        }
        finally
        {
            await server.Stop();
        }
    }

    [TestCase(null,                                                        "/")]
    [TestCase("",                                                          "/")]
    [TestCase("https%3A%2F%2Fevil.example%2Fsteal",                       "/")]
    [TestCase("%2F%2Fevil.example%2Fsteal",                               "/")]
    [TestCase("%2F%5Cevil.example%2Fsteal",                               "/")]
    [TestCase("%2Fsafe%0D%0ALocation%3A%20https%3A%2F%2Fevil.example",    "/")]
    [TestCase("%252F%252Fevil.example%252Fsteal",                         "/")]
    [TestCase("%2Fdashboard%3Fnext%3D%22%3E%3Cscript%3Ealert(1)%3C%2Fscript%3E",
              "/dashboard?next=&quot;&gt;&lt;script&gt;alert(1)&lt;/script&gt;")]
    public void Login_Redirect_Normalization_Must_Allow_Only_Encoded_Local_Paths(String? redirect,
                                                                                String  expected)
    {
        Assert.That(InvokeNormalizeLoginRedirect(redirect), Is.EqualTo(expected));
    }

    [Test]
    public void Login_Redirect_Normalization_Must_Reject_Overlong_Local_Path()
    {
        Assert.That(
            InvokeNormalizeLoginRedirect("/" + new String('a', 2048)),
            Is.EqualTo("/")
        );
    }

    private static String InvokeNormalizeLoginRedirect(String? redirect)
    {
        var normalizeLoginRedirect = typeof(HTTPExtAPI).GetMethod(
                                         "NormalizeLoginRedirect",
                                         BindingFlags.Static | BindingFlags.NonPublic
                                     );

        Assert.That(normalizeLoginRedirect, Is.Not.Null);
        return (String) normalizeLoginRedirect!.Invoke(null, [ redirect ])!;
    }

    [Test]
    public async Task Content_Length_Above_Server_Limit_Must_Return_413_Before_Body_Read()
    {
        var server = new HTTPServer(
                         IPAddress:       IPv4Address.Localhost,
                         TCPPort:         IPPort.Zero,
                         MaxHTTPBodySize: 4,
                         AutoStart:       true
                     );

        try
        {
            using var client = new TcpClient(AddressFamily.InterNetwork);
            await client.ConnectAsync(System.Net.IPAddress.Loopback, server.TCPPort.ToInt32());

            await using var stream = client.GetStream();
            await stream.WriteAsync(
                Encoding.ASCII.GetBytes(
                     "POST / HTTP/1.1\r\n" +
                     "Host: example.test\r\n" +
                     "Content-Length: 5\r\n" +
                     "Expect: 100-continue\r\n" +
                     "Connection: close\r\n\r\n"
                 )
            );

            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            using var responseBuffer = new MemoryStream();
            var buffer = new Byte[1024];

            while (true)
            {
                var read = await stream.ReadAsync(buffer, cancellation.Token);
                if (read == 0)
                    break;

                responseBuffer.Write(buffer, 0, read);
            }

            var response = Encoding.ASCII.GetString(responseBuffer.ToArray());
            Assert.That(response, Does.Contain("413"));
            Assert.That(response, Does.Not.Contain("100 Continue"));
        }
        finally
        {
            await server.Stop();
        }
    }

    [Test]
    public async Task Expect_100_Continue_Must_Be_Sent_Before_Reading_A_Valid_Body()
    {
        var server = new HTTPServer(
                         IPAddress: IPv4Address.Localhost,
                         TCPPort:   IPPort.Zero,
                         AutoStart: false
                     );

        try
        {
            var api = new HTTPAPI(server);
            api.AddHandler(
                HTTPPath.Root,
                HTTPMethod: HTTPMethod.POST,
                HTTPDelegate: async request =>
                {
                    var bodyRead = await request.TryReadHTTPBodyStreamAsync();

                    return new HTTPResponse.Builder(request) {
                               HTTPStatusCode = bodyRead
                                                    ? HTTPStatusCode.OK
                                                    : HTTPStatusCode.BadRequest,
                               ContentType    = HTTPContentType.Text.PLAIN,
                               Content        = bodyRead
                                                    ? "received".ToUTF8Bytes()
                                                    : "missing body".ToUTF8Bytes()
                           }.AsImmutable;
                }
            );
            await server.Start();

            using var client = new TcpClient(AddressFamily.InterNetwork);
            await client.ConnectAsync(System.Net.IPAddress.Loopback, server.TCPPort.ToInt32());

            await using var stream = client.GetStream();
            using var reader = new StreamReader(
                                   stream,
                                   Encoding.ASCII,
                                   detectEncodingFromByteOrderMarks: false,
                                   leaveOpen: true
                               );
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(3));

            await stream.WriteAsync(
                Encoding.ASCII.GetBytes(
                    "POST / HTTP/1.1\r\n" +
                    "Host: example.test\r\n" +
                    "Content-Length: 4\r\n" +
                    "Expect: 100-CONTINUE\r\n" +
                    "Expect: 100-continue\r\n" +
                    "Connection: close\r\n\r\n"
                ),
                cancellation.Token
            );

            var continueResponse = await ReadSingleHTTPResponse(reader, cancellation.Token);

            await stream.WriteAsync(
                Encoding.ASCII.GetBytes("ping"),
                cancellation.Token
            );

            var finalResponse = await ReadSingleHTTPResponse(reader, cancellation.Token);

            Assert.Multiple(() =>
            {
                Assert.That(continueResponse.StatusLine, Does.Contain("100 Continue"));
                Assert.That(continueResponse.Body,       Is.Empty);
                Assert.That(finalResponse.StatusLine,    Does.Contain("200 OK"));
                Assert.That(finalResponse.Body,          Is.EqualTo("received"));
            });
        }
        finally
        {
            await server.Stop();
        }
    }

    [Test]
    public async Task Unsupported_Expectations_Must_Be_Rejected_With_417_Before_Body_Read()
    {
        var server = new HTTPServer(
                         IPAddress: IPv4Address.Localhost,
                         TCPPort:   IPPort.Zero,
                         AutoStart: true
                     );

        try
        {
            using var client = new TcpClient(AddressFamily.InterNetwork);
            await client.ConnectAsync(System.Net.IPAddress.Loopback, server.TCPPort.ToInt32());

            await using var stream = client.GetStream();
            using var reader = new StreamReader(
                                   stream,
                                   Encoding.ASCII,
                                   detectEncodingFromByteOrderMarks: false,
                                   leaveOpen: true
                               );
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(3));

            await stream.WriteAsync(
                Encoding.ASCII.GetBytes(
                    "POST / HTTP/1.1\r\n" +
                    "Host: example.test\r\n" +
                    "Content-Length: 1\r\n" +
                    "Expect: 100-continue, unsupported-feature\r\n" +
                    "Connection: close\r\n\r\n"
                ),
                cancellation.Token
            );

            var response = await ReadSingleHTTPResponse(reader, cancellation.Token);

            Assert.That(response.StatusLine, Does.Contain("417 Expectation Failed"));
        }
        finally
        {
            await server.Stop();
        }
    }

    [Test]
    public async Task Request_Target_Forms_Must_Be_Handled_Before_Application_Routing()
    {
        var routedRequests = 0;
        var server = new HTTPServer(
                         IPAddress: IPv4Address.Localhost,
                         TCPPort:   IPPort.Zero,
                         AutoStart: false
                     );

        try
        {
            var api = new HTTPAPI(server);
            api.AddHandler(
                HTTPPath.Root,
                HTTPMethod: HTTPMethod.GET,
                HTTPDelegate: request =>
                {
                    Interlocked.Increment(ref routedRequests);

                    return Task.FromResult(
                               new HTTPResponse.Builder(request) {
                                   HTTPStatusCode = HTTPStatusCode.OK,
                                   ContentType    = HTTPContentType.Text.PLAIN,
                                   Content        = "origin-form".ToUTF8Bytes()
                               }.AsImmutable
                           );
                }
            );
            api.AddHandler(
                HTTPPath.Root,
                HTTPMethod: HTTPMethod.OPTIONS,
                HTTPDelegate: request =>
                {
                    Interlocked.Increment(ref routedRequests);

                    return Task.FromResult(
                               new HTTPResponse.Builder(request) {
                                   HTTPStatusCode = HTTPStatusCode.OK
                               }.AsImmutable
                           );
                }
            );

            await server.Start();

            var originFormResponse = await SendRequest(
                                         "GET / HTTP/1.1\r\n" +
                                         "Host: example.test\r\n" +
                                         "Connection: close\r\n\r\n"
                                     );
            var absoluteFormResponse = await SendRequest(
                                           "GET http://example.test/ HTTP/1.1\r\n" +
                                           "Host: example.test\r\n" +
                                           "Connection: close\r\n\r\n"
                                       );
            var authorityFormResponse = await SendRequest(
                                            "CONNECT example.test:443 HTTP/1.1\r\n" +
                                            "Host: example.test:443\r\n" +
                                            "Connection: close\r\n\r\n"
                                        );
            var asteriskFormResponse = await SendRequest(
                                           "OPTIONS * HTTP/1.1\r\n" +
                                           "Host: example.test\r\n" +
                                           "Connection: close\r\n\r\n"
                                       );
            var invalidAsteriskResponse = await SendRequest(
                                              "GET * HTTP/1.1\r\n" +
                                              "Host: example.test\r\n" +
                                              "Connection: close\r\n\r\n"
                                          );

            Assert.Multiple(() =>
            {
                Assert.That(originFormResponse.StatusLine,     Does.Contain("200 OK"));
                Assert.That(originFormResponse.Body,           Is.EqualTo("origin-form"));
                Assert.That(absoluteFormResponse.StatusLine,   Does.Contain("400 Bad Request"));
                Assert.That(authorityFormResponse.StatusLine,  Does.Contain("501 Not Implemented"));
                Assert.That(asteriskFormResponse.StatusLine,   Does.Contain("204 No Content"));
                Assert.That(invalidAsteriskResponse.StatusLine, Does.Contain("400 Bad Request"));
                Assert.That(routedRequests,                    Is.EqualTo(1));
            });

            async Task<(String StatusLine, String Body)> SendRequest(String request)
            {
                using var client = new TcpClient(AddressFamily.InterNetwork);
                await client.ConnectAsync(System.Net.IPAddress.Loopback, server.TCPPort.ToInt32());

                await using var stream = client.GetStream();
                using var reader = new StreamReader(
                                       stream,
                                       Encoding.ASCII,
                                       detectEncodingFromByteOrderMarks: false,
                                       leaveOpen: true
                                   );
                using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(3));

                await stream.WriteAsync(
                    Encoding.ASCII.GetBytes(request),
                    cancellation.Token
                );

                return await ReadSingleHTTPResponse(reader, cancellation.Token);
            }
        }
        finally
        {
            await server.Stop();
        }
    }

    [Test]
    public async Task Strict_Header_And_Ambiguous_Path_Must_Be_Rejected_Before_Routing()
    {
        var routedRequests        = 0;
        String? normalizedUnicode = null;
        var server = new HTTPServer(
                         IPAddress: IPv4Address.Localhost,
                         TCPPort:   IPPort.Zero,
                         AutoStart: false
                     );

        try
        {
            var api = new HTTPAPI(server);
            api.AddHandler(
                HTTPPath.Parse("/strict"),
                HTTPMethod: HTTPMethod.GET,
                HTTPDelegate: request =>
                {
                    Interlocked.Increment(ref routedRequests);

                    return Task.FromResult(
                               new HTTPResponse.Builder(request) {
                                   HTTPStatusCode = HTTPStatusCode.OK,
                                   ContentType    = HTTPContentType.Text.PLAIN,
                                   Content        = "strict-route".ToUTF8Bytes()
                               }.AsImmutable
                           );
                }
            );
            api.AddHandler(
                HTTPPath.Parse("/item/{value}"),
                HTTPMethod: HTTPMethod.GET,
                HTTPDelegate: request =>
                {
                    Interlocked.Increment(ref routedRequests);
                    var parameterValue = request.ParsedURLParametersX["value"];

                    if (parameterValue.Any(character => character > '\u007F'))
                        normalizedUnicode = parameterValue;

                    return Task.FromResult(
                               new HTTPResponse.Builder(request) {
                                   HTTPStatusCode = HTTPStatusCode.OK,
                                   ContentType    = HTTPContentType.Text.PLAIN,
                                   Content        = parameterValue.ToUTF8Bytes()
                               }.AsImmutable
                           );
                }
            );
            api.AddHandler(
                HTTPPath.Parse("/item/static"),
                HTTPMethod: HTTPMethod.GET,
                HTTPDelegate: request =>
                {
                    Interlocked.Increment(ref routedRequests);

                    return Task.FromResult(
                               new HTTPResponse.Builder(request) {
                                   HTTPStatusCode = HTTPStatusCode.OK,
                                   ContentType    = HTTPContentType.Text.PLAIN,
                                   Content        = "static-route".ToUTF8Bytes()
                               }.AsImmutable
                           );
                }
            );

            await server.Start();

            var validResponse = await SendRawRequest(
                                    server,
                                    "GET /strict HTTP/1.1\r\n" +
                                    "Host: example.test\r\n" +
                                    "Connection: close\r\n\r\n"
                                );
            var invalidHeaderNameResponse = await SendRawRequest(
                                                server,
                                                "GET /strict HTTP/1.1\r\n" +
                                                "Host: example.test\r\n" +
                                                "Bad(Name): value\r\n" +
                                                "Connection: close\r\n\r\n"
                                            );
            var validTokenHeaderResponse = await SendRawRequest(
                                               server,
                                               "GET /strict HTTP/1.1\r\n" +
                                               "Host: example.test\r\n" +
                                               "X-!#$%&'*+-.^_`|~: value\r\n" +
                                               "Connection: close\r\n\r\n"
                                           );
            var invalidHeaderValueResponse = await SendRawRequest(
                                                 server,
                                                 "GET /strict HTTP/1.1\r\n" +
                                                 "Host: example.test\r\n" +
                                                 "X-Test: before\0after\r\n" +
                                                 "Connection: close\r\n\r\n"
                                             );
            var bareLFResponse = await SendRawRequest(
                                     server,
                                     "GET /strict HTTP/1.1\n" +
                                     "Host: example.test\r\n" +
                                     "Connection: close\r\n\r\n"
                                 );
            var obsFoldResponse = await SendRawRequest(
                                      server,
                                      "GET /strict HTTP/1.1\r\n" +
                                      "Host: example.test\r\n" +
                                      "X-Test: first\r\n" +
                                      " second\r\n" +
                                      "Connection: close\r\n\r\n"
                                  );
            var looseRequestLineResponse = await SendRawRequest(
                                               server,
                                               "GET  /strict HTTP/1.1\r\n" +
                                               "Host: example.test\r\n" +
                                               "Connection: close\r\n\r\n"
                                           );
            var duplicateAuthorizationResponse = await SendRawRequest(
                                                     server,
                                                     "GET /strict HTTP/1.1\r\n" +
                                                     "Host: example.test\r\n" +
                                                     "Authorization: first\r\n" +
                                                     "Authorization: second\r\n" +
                                                     "Connection: close\r\n\r\n"
                                                 );
            var duplicateUnknownHeaderResponse = await SendRawRequest(
                                                     server,
                                                     "GET /strict HTTP/1.1\r\n" +
                                                     "Host: example.test\r\n" +
                                                     "X-Unknown: first\r\n" +
                                                     "X-Unknown: second\r\n" +
                                                     "Connection: close\r\n\r\n"
                                                 );
            var repeatedListHeaderResponse = await SendRawRequest(
                                                 server,
                                                 "GET /strict HTTP/1.1\r\n" +
                                                 "Host: example.test\r\n" +
                                                 "Accept: text/plain\r\n" +
                                                 "Accept: application/json\r\n" +
                                                 "Connection: close\r\n\r\n"
                                             );
            var ipv6HostResponse = await SendRawRequest(
                                       server,
                                       "GET /strict HTTP/1.1\r\n" +
                                       "Host: [::1]:8080\r\n" +
                                       "Connection: close\r\n\r\n"
                                   );
            var malformedHostResponse = await SendRawRequest(
                                            server,
                                            "GET /strict HTTP/1.1\r\n" +
                                            "Host: example.test:80:evil\r\n" +
                                            "Connection: close\r\n\r\n"
                                        );
            var repeatedSlashResponse = await SendRawRequest(
                                            server,
                                            "GET //strict HTTP/1.1\r\n" +
                                            "Host: example.test\r\n" +
                                            "Connection: close\r\n\r\n"
                                        );
            var dotSegmentResponse = await SendRawRequest(
                                         server,
                                         "GET /strict/../item HTTP/1.1\r\n" +
                                         "Host: example.test\r\n" +
                                         "Connection: close\r\n\r\n"
                                     );
            var encodedDotSegmentResponse = await SendRawRequest(
                                                server,
                                                "GET /%2e%2e/strict HTTP/1.1\r\n" +
                                                "Host: example.test\r\n" +
                                                "Connection: close\r\n\r\n"
                                            );
            var encodedSlashResponse = await SendRawRequest(
                                           server,
                                           "GET /item/a%2Fb HTTP/1.1\r\n" +
                                           "Host: example.test\r\n" +
                                           "Connection: close\r\n\r\n"
                                       );
            var encodedBackslashResponse = await SendRawRequest(
                                               server,
                                               "GET /item/a%5Cb HTTP/1.1\r\n" +
                                               "Host: example.test\r\n" +
                                               "Connection: close\r\n\r\n"
                                           );
            var doubleEncodedResponse = await SendRawRequest(
                                            server,
                                            "GET /item/a%252Fb HTTP/1.1\r\n" +
                                            "Host: example.test\r\n" +
                                            "Connection: close\r\n\r\n"
                                        );
            var invalidPercentResponse = await SendRawRequest(
                                             server,
                                             "GET /item/a%ZZb HTTP/1.1\r\n" +
                                             "Host: example.test\r\n" +
                                             "Connection: close\r\n\r\n"
                                         );
            var encodedValueResponse = await SendRawRequest(
                                           server,
                                           "GET /item/hello%20world HTTP/1.1\r\n" +
                                           "Host: example.test\r\n" +
                                           "Connection: close\r\n\r\n"
                                       );
            var normalizedUnicodeResponse = await SendRawRequest(
                                                server,
                                                "GET /item/cafe%CC%81 HTTP/1.1\r\n" +
                                                "Host: example.test\r\n" +
                                                "Connection: close\r\n\r\n"
                                            );
            var plusValueResponse = await SendRawRequest(
                                        server,
                                        "GET /item/a+b HTTP/1.1\r\n" +
                                        "Host: example.test\r\n" +
                                        "Connection: close\r\n\r\n"
                                    );
            var exclamationValueResponse = await SendRawRequest(
                                               server,
                                               "GET /item/wow!yes HTTP/1.1\r\n" +
                                               "Host: example.test\r\n" +
                                               "Connection: close\r\n\r\n"
                                           );
            var staticRouteResponse = await SendRawRequest(
                                          server,
                                          "GET /item/static HTTP/1.1\r\n" +
                                          "Host: example.test\r\n" +
                                          "Connection: close\r\n\r\n"
                                      );
            var trailingSlashResponse = await SendRawRequest(
                                            server,
                                            "GET /strict/ HTTP/1.1\r\n" +
                                            "Host: example.test\r\n" +
                                            "Connection: close\r\n\r\n"
                                        );

            Assert.Multiple(() =>
            {
                Assert.That(validResponse.StatusLine,             Does.Contain("200 OK"));
                Assert.That(validResponse.Body,                   Is.EqualTo("strict-route"));
                Assert.That(validTokenHeaderResponse.StatusLine,  Does.Contain("200 OK"));
                Assert.That(invalidHeaderNameResponse.StatusLine, Does.Contain("400 Bad Request"));
                Assert.That(invalidHeaderValueResponse.StatusLine, Does.Contain("400 Bad Request"));
                Assert.That(bareLFResponse.StatusLine,             Does.Contain("400 Bad Request"));
                Assert.That(obsFoldResponse.StatusLine,            Does.Contain("400 Bad Request"));
                Assert.That(looseRequestLineResponse.StatusLine,   Does.Contain("400 Bad Request"));
                Assert.That(duplicateAuthorizationResponse.StatusLine, Does.Contain("400 Bad Request"));
                Assert.That(duplicateUnknownHeaderResponse.StatusLine, Does.Contain("400 Bad Request"));
                Assert.That(repeatedListHeaderResponse.StatusLine, Does.Contain("200 OK"));
                Assert.That(ipv6HostResponse.StatusLine,          Does.Contain("200 OK"));
                Assert.That(malformedHostResponse.StatusLine,     Does.Contain("400 Bad Request"));
                Assert.That(repeatedSlashResponse.StatusLine,     Does.Contain("400 Bad Request"));
                Assert.That(dotSegmentResponse.StatusLine,        Does.Contain("400 Bad Request"));
                Assert.That(encodedDotSegmentResponse.StatusLine, Does.Contain("400 Bad Request"));
                Assert.That(encodedSlashResponse.StatusLine,      Does.Contain("400 Bad Request"));
                Assert.That(encodedBackslashResponse.StatusLine,  Does.Contain("400 Bad Request"));
                Assert.That(doubleEncodedResponse.StatusLine,     Does.Contain("400 Bad Request"));
                Assert.That(invalidPercentResponse.StatusLine,    Does.Contain("400 Bad Request"));
                Assert.That(encodedValueResponse.Body,            Is.EqualTo("hello world"));
                Assert.That(normalizedUnicodeResponse.StatusLine, Does.Contain("200 OK"));
                Assert.That(normalizedUnicode,                     Is.EqualTo("café"));
                Assert.That(plusValueResponse.Body,               Is.EqualTo("a+b"));
                Assert.That(exclamationValueResponse.Body,        Is.EqualTo("wow!yes"));
                Assert.That(staticRouteResponse.Body,             Is.EqualTo("static-route"));
                Assert.That(trailingSlashResponse.Body,           Is.EqualTo("strict-route"));
                Assert.That(routedRequests,                       Is.EqualTo(10));
            });
        }
        finally
        {
            await server.Stop();
        }
    }

    [Test]
    public async Task Header_And_Request_Target_Limits_Must_Return_414_Or_431()
    {
        var routedRequests = 0;
        var server = new HTTPServer(
                         IPAddress:                    IPv4Address.Localhost,
                         TCPPort:                      IPPort.Zero,
                         MaxHTTPHeaderSize:            512,
                         MaxHTTPHeaderLineLength:      128,
                         MaxHTTPRequestTargetLength:   32,
                         MaxHTTPHeaderCount:           10,
                         AutoStart:                    false
                     );

        try
        {
            var api = new HTTPAPI(server);
            api.AddHandler(
                HTTPPath.Parse("/strict"),
                HTTPMethod: HTTPMethod.GET,
                HTTPDelegate: request =>
                {
                    Interlocked.Increment(ref routedRequests);
                    return Task.FromResult(
                               new HTTPResponse.Builder(request) {
                                   HTTPStatusCode = HTTPStatusCode.OK
                               }.AsImmutable
                           );
                }
            );

            await server.Start();

            var validResponse = await SendRawRequest(
                                    server,
                                    "GET /strict HTTP/1.1\r\nHost: example.test\r\nConnection: close\r\n\r\n"
                                );
            var targetTooLongResponse = await SendRawRequest(
                                            server,
                                            $"GET /{new String('a', 40)} HTTP/1.1\r\nHost: example.test\r\nConnection: close\r\n\r\n"
                                        );
            var fieldTooLongResponse = await SendRawRequest(
                                           server,
                                           $"GET /strict HTTP/1.1\r\nHost: example.test\r\nX-Large: {new String('a', 130)}\r\nConnection: close\r\n\r\n"
                                       );
            var tooManyFields = String.Join(
                                    String.Empty,
                                    Enumerable.Range(0, 11).Select(index => $"X-{index}: value\r\n")
                                );
            var tooManyFieldsResponse = await SendRawRequest(
                                            server,
                                            $"GET /strict HTTP/1.1\r\nHost: example.test\r\n{tooManyFields}Connection: close\r\n\r\n"
                                        );
            var largeFields = String.Join(
                                  String.Empty,
                                  Enumerable.Range(0, 5).Select(index => $"X-{index}: {new String('a', 100)}\r\n")
                              );
            var headerTooLargeResponse = await SendRawRequest(
                                             server,
                                             $"GET /strict HTTP/1.1\r\nHost: example.test\r\n{largeFields}Connection: close\r\n\r\n"
                                         );

            Assert.Multiple(() =>
            {
                Assert.That(validResponse.StatusLine,          Does.Contain("200 OK"));
                Assert.That(targetTooLongResponse.StatusLine,  Does.Contain("414"));
                Assert.That(fieldTooLongResponse.StatusLine,   Does.Contain("431 Request Header Fields Too Large"));
                Assert.That(tooManyFieldsResponse.StatusLine,  Does.Contain("431 Request Header Fields Too Large"));
                Assert.That(headerTooLargeResponse.StatusLine, Does.Contain("431 Request Header Fields Too Large"));
                Assert.That(routedRequests,                    Is.EqualTo(1));
            });
        }
        finally
        {
            await server.Stop();
        }
    }

    [Test]
    public void Semantically_Equivalent_Parameter_Routes_Must_Be_Rejected()
    {
        var server = new HTTPServer(
                         IPAddress: IPv4Address.Localhost,
                         TCPPort:   IPPort.Zero,
                         AutoStart: false
                     );
        var api = new HTTPAPI(server);

        api.AddHandler(
            HTTPPath.Parse("/users/{userId}"),
            HTTPMethod: HTTPMethod.GET,
            HTTPDelegate: request => Task.FromResult(
                                         new HTTPResponse.Builder(request) {
                                             HTTPStatusCode = HTTPStatusCode.OK
                                         }.AsImmutable
                                     )
        );

        Assert.That(
            () => api.AddHandler(
                      HTTPPath.Parse("/users/{name}"),
                      HTTPMethod: HTTPMethod.GET,
                      HTTPDelegate: request => Task.FromResult(
                                                   new HTTPResponse.Builder(request) {
                                                       HTTPStatusCode = HTTPStatusCode.OK
                                                   }.AsImmutable
                                               )
                  ),
            Throws.TypeOf<InvalidOperationException>()
        );
    }

    [Test]
    public async Task Maximum_Length_Stream_Must_Reject_Chunked_Data_Above_Limit()
    {
        await using var source = new MemoryStream(Encoding.ASCII.GetBytes("ABCDE"));
        await using var limited = new MaximumLengthStream(source, 4);
        var buffer = new Byte[8];

        Assert.That(
            async () => await limited.ReadAsync(buffer),
            Throws.TypeOf<HTTPBodyTooLargeException>()
        );

        Assert.That(
            async () => await limited.ReadAsync(buffer),
            Throws.TypeOf<HTTPBodyTooLargeException>()
        );
    }

    [Test]
    public void Content_Length_Above_Default_Materialization_Limit_Must_Not_Read_Or_Allocate()
    {
        using var source = new MemoryStream();

        var parsed = HTTPRequest.TryParse(
                         Timestamp.Now,
                         new HTTPSource(IPSocket.LocalhostV4(IPPort.Parse(43123))),
                         IPSocket.LocalhostV4(IPPort.Parse(6801)),
                         IPSocket.LocalhostV4(IPPort.Parse(43123)),
                         "POST / HTTP/1.1\r\n" +
                         "Host: example.test\r\n" +
                         "Content-Length: 8388609\r\n",
                         out var request,
                         out _,
                         HTTPBodyStream: source
                     );

        Assert.That(parsed, Is.True);
        Assert.That(request, Is.Not.Null);
        Assert.That(
            () => request!.TryReadHTTPBodyStream(),
            Throws.TypeOf<HTTPBodyTooLargeException>()
        );
        Assert.That(source.Position, Is.EqualTo(0));
    }

    [Test]
    public async Task Partial_Header_Must_Be_Closed_After_Header_Deadline()
    {
        var server = new HTTPServer(
                         IPAddress:         IPv4Address.Localhost,
                         TCPPort:           IPPort.Zero,
                         HeaderReadTimeout: TimeSpan.FromMilliseconds(100),
                         AutoStart:         true
                     );

        try
        {
            using var client = new TcpClient(AddressFamily.InterNetwork);
            await client.ConnectAsync(System.Net.IPAddress.Loopback, server.TCPPort.ToInt32());

            await using var stream = client.GetStream();
            await stream.WriteAsync(
                Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\nHost:")
            );

            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var buffer = new Byte[1];
            var closed = false;

            try
            {
                closed = await stream.ReadAsync(buffer, cancellation.Token) == 0;
            }
            catch (IOException)
            {
                closed = true;
            }

            Assert.That(closed, Is.True);
        }
        finally
        {
            await server.Stop();
        }
    }

    [Test]
    public async Task Partial_Body_Must_Return_408_After_Body_Deadline()
    {
        var server = new HTTPServer(
                         IPAddress:       IPv4Address.Localhost,
                         TCPPort:         IPPort.Zero,
                         BodyReadTimeout: TimeSpan.FromMilliseconds(100),
                         AutoStart:       true
                     );

        try
        {
            using var client = new TcpClient(AddressFamily.InterNetwork);
            await client.ConnectAsync(System.Net.IPAddress.Loopback, server.TCPPort.ToInt32());

            await using var stream = client.GetStream();
            await stream.WriteAsync(
                Encoding.ASCII.GetBytes(
                    "POST / HTTP/1.1\r\n" +
                    "Host: example.test\r\n" +
                    "Content-Length: 1\r\n" +
                    "Connection: close\r\n\r\n"
                )
            );

            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            using var responseBuffer = new MemoryStream();
            var buffer = new Byte[1024];

            while (true)
            {
                var read = await stream.ReadAsync(buffer, cancellation.Token);
                if (read == 0)
                    break;

                responseBuffer.Write(buffer, 0, read);
            }

            Assert.That(
                Encoding.ASCII.GetString(responseBuffer.ToArray()),
                Does.Contain("408")
            );
        }
        finally
        {
            await server.Stop();
        }
    }

    [Test]
    public async Task ResetPassword_RateLimit_Must_Return_429_After_Burst()
    {
        var server = new HTTPServer(
                         IPAddress: HermodIPAddress.Localhost,
                         TCPPort:   IPPort.Zero,
                         AutoStart: false
                     );

        try
        {
            _ = new HTTPExtAPI(
                    server,
                    SkipURLTemplates:   false,
                    DisableLogging:     true,
                    DisableNotifications: true
                );

            var request = ParseJSONRequest(
                              server,
                              "RESET",
                              "/resetPassword",
                              new MemoryStream(Encoding.UTF8.GetBytes("{}")),
                              CancellationToken.None
                          );
            var handler = GetRegisteredHandler(server, request);
            var responses = new List<HTTPResponse>();

            for (var i = 0; i < 6; i++)
                responses.Add(
                    await InvokeJSONHandler(
                        server,
                        handler,
                        "RESET",
                        "/resetPassword",
                        "{}"
                    )
                );

            Assert.That(responses.Take(5).All(response => response.HTTPStatusCode.Code == 400), Is.True);
            Assert.That(responses[5].HTTPStatusCode.Code, Is.EqualTo(429));
            Assert.That(responses[5].RetryAfter, Is.Not.Null.And.Not.Empty);
        }
        finally
        {
            await server.Stop();
        }
    }

    [Test]
    public async Task SetPassword_RateLimit_Must_Return_429_After_Burst()
    {
        var server = new HTTPServer(
                         IPAddress: HermodIPAddress.Localhost,
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

            var request = ParseJSONRequest(
                              server,
                              "SET",
                              "/setPassword",
                              new MemoryStream(Encoding.UTF8.GetBytes("{]")),
                              CancellationToken.None
                          );
            var handler = GetRegisteredHandler(server, request);
            var responses = new List<HTTPResponse>();

            for (var i = 0; i < 11; i++)
                responses.Add(
                    await InvokeJSONHandler(
                        server,
                        handler,
                        "SET",
                        "/setPassword",
                        "{]"
                    )
                );

            Assert.That(responses.Take(10).All(response => response.HTTPStatusCode.Code == 400), Is.True);
            Assert.That(responses[10].HTTPStatusCode.Code, Is.EqualTo(429));
            Assert.That(responses[10].RetryAfter, Is.Not.Null.And.Not.Empty);
        }
        finally
        {
            await server.Stop();
        }
    }

    [Test]
    public async Task SetPassword_InvalidTokenLength_RateLimit_Must_Return_429()
    {
        var server = new HTTPServer(
                         IPAddress: HermodIPAddress.Localhost,
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

            var request = ParseJSONRequest(
                              server,
                              "SET",
                              "/setPassword",
                              new MemoryStream(
                                  Encoding.UTF8.GetBytes(
                                      "{\"securityToken1\":\"short\",\"newPassword\":\"Abcdef1!\"}"
                                  )
                              ),
                              CancellationToken.None
                          );
            var handler = GetRegisteredHandler(server, request);
            var responses = new List<HTTPResponse>();

            for (var i = 0; i < 4; i++)
                responses.Add(
                    await InvokeJSONHandler(
                        server,
                        handler,
                        "SET",
                        "/setPassword",
                        "{\"securityToken1\":\"short\",\"newPassword\":\"Abcdef1!\"}"
                    )
                );

            Assert.That(responses.Take(3).All(response => response.HTTPStatusCode.Code == 400), Is.True);
            Assert.That(responses[3].HTTPStatusCode.Code, Is.EqualTo(429));
            Assert.That(responses[3].RetryAfter, Is.Not.Null.And.Not.Empty);
        }
        finally
        {
            await server.Stop();
        }
    }

    [Test]
    public void InMemoryTokenBucket_Must_Refill_And_Evict_Bounded_Keys()
    {
        var limiter = new InMemoryTokenBucketRateLimiter(
                           Capacity:        2,
                           RefillPeriod:    TimeSpan.FromSeconds(10),
                           MaximumBuckets:  2,
                           BucketLifetime:  TimeSpan.FromSeconds(5)
                       );
        var timestamp = DateTimeOffset.UtcNow;

        Assert.That(limiter.TryAcquire("one", timestamp).Allowed, Is.True);
        Assert.That(limiter.TryAcquire("one", timestamp).Allowed, Is.True);
        Assert.That(limiter.TryAcquire("one", timestamp).Allowed, Is.False);
        Assert.That(limiter.TryAcquire("two", timestamp).Allowed, Is.True);
        Assert.That(limiter.TryAcquire("three", timestamp).Allowed, Is.False);
        Assert.That(limiter.BucketCount, Is.EqualTo(2));

        Assert.That(limiter.TryAcquire("one", timestamp.AddSeconds(6)).Allowed, Is.True);
        Assert.That(limiter.BucketCount, Is.EqualTo(1));
    }

    [Test]
    public async Task Fixed_Port_Any_Listener_Must_Be_Constructible()
    {
        var port = ReserveFreePort();
        HTTPServer? server = null;

        Assert.That(
            () => server = new HTTPServer(
                               IPAddress:  HermodIPAddress.Any,
                               TCPPort:    IPPort.Parse(port),
                               AutoStart:  false
                           ),
            Throws.Nothing
        );

        Assert.That(server, Is.Not.Null);

        try
        {
            await server!.Start();
            Assert.That(server.IsRunning, Is.True);
        }
        finally
        {
            if (server is not null)
                await server.Stop();
        }
    }

    [Test]
    public void HTTP11_Default_Connection_Must_Be_Persistent()
    {
        var parsed = TryParseRequest(
                         "GET / HTTP/1.1\r\n" +
                         "Host: example.test\r\n",
                         out var request
                     );

        Assert.Multiple(() =>
        {
            Assert.That(parsed, Is.True);
            Assert.That(request, Is.Not.Null);
            Assert.That(request!.IsKeepAlive, Is.True);
        });
    }

    [Test]
    public void HTTP11_Connection_Close_Must_Disable_Persistence()
    {
        var parsed = TryParseRequest(
                         "GET / HTTP/1.1\r\n" +
                         "Host: example.test\r\n" +
                         "Connection: close\r\n",
                         out var request
                     );

        var parsedList = TryParseRequest(
                             "GET / HTTP/1.1\r\n" +
                             "Host: example.test\r\n" +
                             "Connection: upgrade\r\n" +
                             "Connection: keep-alive, close\r\n",
                             out var requestWithOptionList
                         );

        Assert.Multiple(() =>
        {
            Assert.That(parsed,                         Is.True);
            Assert.That(request,                        Is.Not.Null);
            Assert.That(request!.IsKeepAlive,           Is.False);
            Assert.That(parsedList,                     Is.True);
            Assert.That(requestWithOptionList,          Is.Not.Null);
            Assert.That(requestWithOptionList!.IsKeepAlive, Is.False);
        });
    }

    [Test]
    public void HTTP10_Default_Connection_Must_Not_Be_Persistent()
    {
        var parsed = TryParseRequest(
                         "GET / HTTP/1.0\r\n" +
                         "Host: example.test\r\n",
                         out var request
                     );

        Assert.That(parsed, Is.True);
        Assert.That(request, Is.Not.Null);
        Assert.That(request!.IsKeepAlive, Is.False);
    }

    [Test]
    public void HTTP10_Explicit_KeepAlive_Must_Enable_Persistence()
    {
        var parsed = TryParseRequest(
                         "GET / HTTP/1.0\r\n" +
                         "Host: example.test\r\n" +
                         "Connection: upgrade, keep-alive\r\n",
                         out var request
                     );

        Assert.That(parsed, Is.True);
        Assert.That(request, Is.Not.Null);
        Assert.That(request!.IsKeepAlive, Is.True);
    }

    [Test]
    public void Response_Persistence_Must_Not_Override_Request_Or_HTTP10_Negotiation()
    {
        Assert.That(
            TryParseRequest(
                "GET / HTTP/1.1\r\n" +
                "Host: example.test\r\n" +
                "Connection: close\r\n",
                out var closingHTTP11Request
            ),
            Is.True
        );
        Assert.That(
            TryParseRequest(
                "GET / HTTP/1.0\r\n" +
                "Host: example.test\r\n" +
                "Connection: keep-alive\r\n",
                out var persistentHTTP10Request
            ),
            Is.True
        );

        var overridingHTTP11Response = new HTTPResponse.Builder(closingHTTP11Request!) {
                                           HTTPStatusCode = HTTPStatusCode.OK,
                                           Connection     = ConnectionType.KeepAlive
                                       }.AsImmutable;
        var implicitHTTP10Response   = new HTTPResponse.Builder(persistentHTTP10Request!) {
                                           HTTPStatusCode = HTTPStatusCode.OK
                                       }.AsImmutable;
        var explicitHTTP10Response   = new HTTPResponse.Builder(persistentHTTP10Request!) {
                                           HTTPStatusCode = HTTPStatusCode.OK,
                                           Connection     = ConnectionType.KeepAlive
                                       }.AsImmutable;

        Assert.Multiple(() =>
        {
            Assert.That(overridingHTTP11Response.IsKeepAlive, Is.False);
            Assert.That(implicitHTTP10Response.  IsKeepAlive, Is.False);
            Assert.That(explicitHTTP10Response.  IsKeepAlive, Is.True);
        });
    }

    [Test]
    public async Task HTTP11_Default_Persistence_Must_Allow_A_Second_Request_On_The_Same_Connection()
    {
        var server = new HTTPServer(
                         IPAddress: IPv4Address.Localhost,
                         TCPPort:   IPPort.Zero,
                         AutoStart: false
                     );

        try
        {
            var api = new HTTPAPI(server);
            api.AddHandler(
                HTTPPath.Root,
                HTTPMethod: HTTPMethod.GET,
                HTTPDelegate: request => Task.FromResult(
                                             new HTTPResponse.Builder(request) {
                                                 HTTPStatusCode = HTTPStatusCode.OK,
                                                 ContentType    = HTTPContentType.Text.PLAIN,
                                                 Content        = "persistent".ToUTF8Bytes()
                                             }.AsImmutable
                                         )
            );

            await server.Start();

            using var client = new TcpClient(AddressFamily.InterNetwork);
            await client.ConnectAsync(System.Net.IPAddress.Loopback, server.TCPPort.ToInt32());

            await using var stream = client.GetStream();
            using var reader = new StreamReader(
                                   stream,
                                   Encoding.ASCII,
                                   detectEncodingFromByteOrderMarks: false,
                                   leaveOpen: true
                               );
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(3));

            await stream.WriteAsync(
                Encoding.ASCII.GetBytes(
                    "GET / HTTP/1.1\r\n" +
                    "Host: example.test\r\n\r\n"
                ),
                cancellation.Token
            );

            var firstResponse = await ReadSingleHTTPResponse(reader, cancellation.Token);

            await stream.WriteAsync(
                Encoding.ASCII.GetBytes(
                    "GET / HTTP/1.1\r\n" +
                    "Host: example.test\r\n" +
                    "Connection: close\r\n\r\n"
                ),
                cancellation.Token
            );

            var secondResponse = await ReadSingleHTTPResponse(reader, cancellation.Token);

            Assert.Multiple(() =>
            {
                Assert.That(firstResponse.StatusLine,  Does.Contain("200 OK"));
                Assert.That(firstResponse.Body,        Is.EqualTo("persistent"));
                Assert.That(secondResponse.StatusLine, Does.Contain("200 OK"));
                Assert.That(secondResponse.Body,       Is.EqualTo("persistent"));
            });
        }
        finally
        {
            await server.Stop();
        }
    }

    [Test]
    public void Duplicate_Content_Length_Must_Be_Rejected()
    {
        var parsed = TryParseRequest(
                         "POST /chargy/issues HTTP/1.1\r\n" +
                         "Host: example.test\r\n" +
                         "Content-Length: 5\r\n" +
                         "Content-Length: 5\r\n",
                         out _
                     );

        Assert.That(parsed, Is.False);
    }

    [Test]
    public void Conflicting_Content_Length_Values_Must_Be_Rejected()
    {
        var parsed = TryParseRequest(
                         "POST /chargy/issues HTTP/1.1\r\n" +
                         "Host: example.test\r\n" +
                         "Content-Length: 5\r\n" +
                         "content-length: 6\r\n",
                         out _
                     );

        Assert.That(parsed, Is.False);
    }

    [Test]
    public void Transfer_Encoding_And_Content_Length_Must_Be_Rejected()
    {
        var parsed = TryParseRequest(
                         "POST /chargy/issues HTTP/1.1\r\n" +
                         "Host: example.test\r\n" +
                         "Transfer-Encoding: chunked\r\n" +
                         "Content-Length: 5\r\n",
                         out _
                     );

        Assert.That(parsed, Is.False);
    }

    [Test]
    public void Whitespace_Before_Header_Colon_Must_Be_Rejected()
    {
        var parsed = TryParseRequest(
                         "POST /chargy/issues HTTP/1.1\r\n" +
                         "Host: example.test\r\n" +
                         "Content-Length : 5\r\n",
                         out _
                     );

        Assert.That(parsed, Is.False);
    }

    [Test]
    public async Task Whitespace_Only_Trailer_Line_Must_Be_Rejected()
    {
        await using var source = new MemoryStream(
                                  Encoding.ASCII.GetBytes("0\r\n \r\n")
                              );
        await using var chunked = new ChunkedTransferEncodingStream(source);
        var buffer = new Byte[1];

        Assert.That(
            async () => await chunked.ReadAsync(buffer),
            Throws.TypeOf<HTTPInvalidChunkException>()
        );
    }

    [Test]
    public async Task Oversized_Chunk_Size_Line_Must_Stop_At_The_Default_Limit()
    {
        await using var source = new MemoryStream(
                                     Encoding.ASCII.GetBytes(
                                         new String('A', 8193) + "\r\n"
                                     )
                                 );
        await using var chunked = new ChunkedTransferEncodingStream(source);
        var buffer = new Byte[1];

        Assert.That(
            async () => await chunked.ReadAsync(buffer),
            Throws.TypeOf<HTTPChunkMetadataTooLargeException>()
        );
        Assert.That(source.Position, Is.LessThanOrEqualTo(8193));
    }

    [Test]
    public void Synchronous_Chunk_Read_Must_Use_The_Same_Metadata_Limit()
    {
        using var source = new MemoryStream(
                               Encoding.ASCII.GetBytes(
                                   new String('A', 8193) + "\r\n"
                               )
                           );
        using var chunked = new ChunkedTransferEncodingStream(source);
        var buffer = new Byte[1];

        Assert.That(
            () => chunked.Read(buffer, 0, buffer.Length),
            Throws.TypeOf<HTTPChunkMetadataTooLargeException>()
        );
        Assert.That(source.Position, Is.LessThanOrEqualTo(8193));
    }

    [Test]
    public async Task Excessive_Chunk_Trailers_Must_Be_Rejected()
    {
        var trailers = String.Join(
                           String.Empty,
                           Enumerable.Range(0, 101).Select(index => $"X-{index}: value\r\n")
                       );
        await using var source = new MemoryStream(
                                     Encoding.ASCII.GetBytes($"0\r\n{trailers}\r\n")
                                 );
        await using var chunked = new ChunkedTransferEncodingStream(source);
        var buffer = new Byte[1];

        Assert.That(
            async () => await chunked.ReadAsync(buffer),
            Throws.TypeOf<HTTPChunkMetadataTooLargeException>()
        );
    }

    [Test]
    public async Task Chunk_Metadata_Limits_Must_Cover_Lines_And_Aggregates()
    {
        static async Task<HTTPChunkMetadataTooLargeException> ReadRejected(
            String wireData,
            UInt32 maxChunkSizeLineLength = 64,
            UInt32 maxTrailerLineLength   = 64,
            UInt32 maxTrailerSize         = 256,
            UInt32 maxMetadataSize        = 512)
        {
            await using var source = new MemoryStream(Encoding.ASCII.GetBytes(wireData));
            await using var chunked = new ChunkedTransferEncodingStream(
                                          source,
                                          MaxChunkSizeLineLength: maxChunkSizeLineLength,
                                          MaxTrailerLineLength:   maxTrailerLineLength,
                                          MaxTrailerSize:         maxTrailerSize,
                                          MaxChunkMetadataSize:   maxMetadataSize
                                      );
            var buffer = new Byte[8];

            try
            {
                while (await chunked.ReadAsync(buffer) > 0)
                { }
            }
            catch (HTTPChunkMetadataTooLargeException exception)
            {
                return exception;
            }

            Assert.Fail("The oversized HTTP chunk metadata was accepted.");
            throw new InvalidOperationException();
        }

        var sizeLineException = await ReadRejected(
                                    $"1;{new String('a', 32)}\r\nx\r\n0\r\n\r\n",
                                    maxChunkSizeLineLength: 16
                                );
        var trailerLineException = await ReadRejected(
                                       $"0\r\nX-Long: {new String('a', 32)}\r\n\r\n",
                                       maxTrailerLineLength: 16
                                   );
        var trailerSizeException = await ReadRejected(
                                       "0\r\nA: 1\r\nB: 2\r\n\r\n",
                                       maxTrailerSize: 10
                                   );
        var metadataSizeException = await ReadRejected(
                                        "1\r\na\r\n1\r\nb\r\n0\r\n\r\n",
                                        maxMetadataSize: 8
                                    );

        Assert.Multiple(() =>
        {
            Assert.That(sizeLineException.Component,     Is.EqualTo("chunk-size line"));
            Assert.That(trailerLineException.Component,  Is.EqualTo("trailer line"));
            Assert.That(trailerSizeException.Component,  Is.EqualTo("trailer section"));
            Assert.That(metadataSizeException.Component, Is.EqualTo("metadata section"));
        });
    }

    [Test]
    public async Task Valid_Chunk_Extensions_And_Trailers_Within_Limits_Must_Remain_Supported()
    {
        await using var source = new MemoryStream(
                                     Encoding.ASCII.GetBytes(
                                         "4;name=value\r\nWiki\r\n" +
                                         "0\r\n" +
                                         "ETag: abc123\r\n" +
                                         "X-Trace: test\r\n\r\n"
                                     )
                                 );
        await using var chunked = new ChunkedTransferEncodingStream(
                                      source,
                                      MaxChunkSizeLineLength: 32,
                                      MaxTrailerLineLength:   32,
                                      MaxTrailerCount:         2,
                                      MaxTrailerSize:         64,
                                      MaxChunkMetadataSize:  128
                                  );
        var chunks = new List<String>();

        var trailers = (await chunked.ReadAllChunks(
                                         (_, _, _, data) => chunks.Add(Encoding.ASCII.GetString(data))
                                     )).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(chunks, Is.EqualTo(new[] { "Wiki" }));
            Assert.That(trailers, Is.EqualTo(new[] {
                ("ETag", "abc123"),
                ("X-Trace", "test")
            }));
            Assert.That(source.Position, Is.EqualTo(source.Length));
        });
    }

    [Test]
    public async Task Chunked_Response_Content_And_Stream_Must_Be_Sent()
    {
        var server = new HTTPServer(
                         IPAddress: IPv4Address.Localhost,
                         TCPPort:   IPPort.Zero,
                         AutoStart: false
                     );

        try
        {
            var api     = new HTTPAPI(server);
            var framing = "4\r\nWiki\r\n0\r\n\r\n";

            api.AddHandler(
                HTTPPath.Parse("/chunked-bytes"),
                HTTPMethod: HTTPMethod.GET,
                HTTPDelegate: request => Task.FromResult(
                                             new HTTPResponse.Builder(request) {
                                                 HTTPStatusCode   = HTTPStatusCode.OK,
                                                 TransferEncoding = "chunked",
                                                 Content          = Encoding.ASCII.GetBytes(framing),
                                                 Connection       = ConnectionType.Close
                                             }.AsImmutable
                                         )
            );
            api.AddHandler(
                HTTPPath.Parse("/chunked-stream"),
                HTTPMethod: HTTPMethod.GET,
                HTTPDelegate: request => Task.FromResult(
                                             new HTTPResponse.Builder(request) {
                                                 HTTPStatusCode    = HTTPStatusCode.OK,
                                                 TransferEncoding = "chunked",
                                                 ContentStream     = new MemoryStream(Encoding.ASCII.GetBytes(framing)),
                                                 Connection        = ConnectionType.Close
                                             }.AsImmutable
                                         )
            );

            await server.Start();

            static async Task<String> ReadRawResponse(HTTPServer server, String path)
            {
                using var client = new TcpClient(AddressFamily.InterNetwork);
                await client.ConnectAsync(System.Net.IPAddress.Loopback, server.TCPPort.ToInt32());

                await using var stream = client.GetStream();
                using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                await stream.WriteAsync(
                    Encoding.ASCII.GetBytes(
                        $"GET {path} HTTP/1.1\r\n" +
                        "Host: example.test\r\n" +
                        "Connection: close\r\n\r\n"
                    ),
                    cancellation.Token
                );

                using var response = new MemoryStream();
                await stream.CopyToAsync(response, cancellation.Token);
                return Encoding.ASCII.GetString(response.ToArray());
            }

            var byteResponse   = await ReadRawResponse(server, "/chunked-bytes");
            var streamResponse = await ReadRawResponse(server, "/chunked-stream");

            Assert.Multiple(() =>
            {
                Assert.That(byteResponse,   Does.EndWith(framing));
                Assert.That(streamResponse, Does.EndWith(framing));
            });
        }
        finally
        {
            await server.Stop();
        }
    }

    [Test]
    public async Task Oversized_Chunk_Metadata_Must_Return_400()
    {
        var server = new HTTPServer(
                         IPAddress:                    IPv4Address.Localhost,
                         TCPPort:                      IPPort.Zero,
                         MaxHTTPChunkSizeLineLength:   16,
                         MaxHTTPChunkMetadataSize:     64,
                         AutoStart:                    true
                     );

        try
        {
            using var client = new TcpClient(AddressFamily.InterNetwork);
            await client.ConnectAsync(System.Net.IPAddress.Loopback, server.TCPPort.ToInt32());

            await using var stream = client.GetStream();
            using var reader = new StreamReader(
                                   stream,
                                   Encoding.ASCII,
                                   detectEncodingFromByteOrderMarks: false,
                                   leaveOpen: true
                               );
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(3));

            await stream.WriteAsync(
                Encoding.ASCII.GetBytes(
                    "POST / HTTP/1.1\r\n" +
                    "Host: example.test\r\n" +
                    "Transfer-Encoding: chunked\r\n\r\n" +
                    $"1;{new String('a', 32)}\r\nx\r\n0\r\n\r\n"
                ),
                cancellation.Token
            );

            var statusLine       = await reader.ReadLineAsync(cancellation.Token);
            var connectionHeader = String.Empty;

            while (true)
            {
                var headerLine = await reader.ReadLineAsync(cancellation.Token);
                Assert.That(headerLine, Is.Not.Null);

                if (headerLine!.Length == 0)
                    break;

                if (headerLine.StartsWith("Connection:", StringComparison.OrdinalIgnoreCase))
                    connectionHeader = headerLine;
            }

            Assert.Multiple(() =>
            {
                Assert.That(statusLine,       Does.Contain("400 Bad Request"));
                Assert.That(connectionHeader, Does.Contain("close").IgnoreCase);
            });
        }
        finally
        {
            await server.Stop();
        }
    }

    [Test]
    public async Task Partial_Chunk_Metadata_Must_Return_408_After_Body_Deadline()
    {
        var server = new HTTPServer(
                         IPAddress:       IPv4Address.Localhost,
                         TCPPort:         IPPort.Zero,
                         BodyReadTimeout: TimeSpan.FromMilliseconds(100),
                         AutoStart:       true
                     );

        try
        {
            using var client = new TcpClient(AddressFamily.InterNetwork);
            await client.ConnectAsync(System.Net.IPAddress.Loopback, server.TCPPort.ToInt32());

            await using var stream = client.GetStream();
            using var reader = new StreamReader(
                                   stream,
                                   Encoding.ASCII,
                                   detectEncodingFromByteOrderMarks: false,
                                   leaveOpen: true
                               );
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(3));

            await stream.WriteAsync(
                Encoding.ASCII.GetBytes(
                    "POST / HTTP/1.1\r\n" +
                    "Host: example.test\r\n" +
                    "Transfer-Encoding: chunked\r\n" +
                    "Connection: close\r\n\r\n" +
                    "1;unterminated"
                ),
                cancellation.Token
            );

            var response = await ReadSingleHTTPResponse(reader, cancellation.Token);

            Assert.That(response.StatusLine, Does.Contain("408 Request Timeout"));
        }
        finally
        {
            await server.Stop();
        }
    }

    [Test]
    public void Chunked_Final_Coding_In_List_Must_Be_Recognized()
    {
        var parsed = TryParseRequest(
                         "POST /chargy/issues HTTP/1.1\r\n" +
                         "Host: example.test\r\n" +
                         "Transfer-Encoding: gzip, chunked\r\n",
                         out var request
                     );

        Assert.Multiple(() =>
        {
            Assert.That(parsed, Is.True);
            Assert.That(request, Is.Not.Null);
            Assert.That(request!.IsChunkedTransferEncoding, Is.True);
        });
    }

    [Test]
    public void Header_Names_Must_Be_Case_Insensitive()
    {
        var parsed = TryParseRequest(
                         "GET / HTTP/1.1\r\n" +
                         "host: example.test\r\n" +
                         "Connection: close\r\n",
                         out var request
                     );

        Assert.That(parsed, Is.True);
        Assert.That(request, Is.Not.Null);
        Assert.That(request!.Host.ToString(), Is.EqualTo("example.test"));
    }

    private static Boolean TryParseRequest(String requestHeader,
                                           out HTTPRequest? request)
    {
        var localSocket  = IPSocket.LocalhostV4(IPPort.Parse(6801));
        var remoteSocket = IPSocket.LocalhostV4(IPPort.Parse(43123));

        return HTTPRequest.TryParse(
                   Timestamp.Now,
                   new HTTPSource(remoteSocket),
                   localSocket,
                   remoteSocket,
                   requestHeader,
                   out request,
                   out _
               );
    }

    private static HTTPRequest ParseJSONRequest(HTTPServer        server,
                                                String            method,
                                                String            path,
                                                Stream            body,
                                                CancellationToken cancellationToken)
    {
        var localSocket  = IPSocket.LocalhostV4(IPPort.Parse(6801));
        var remoteSocket = IPSocket.LocalhostV4(IPPort.Parse(43123));

        var parsed = HTTPRequest.TryParse(
                         Timestamp.Now,
                         new HTTPSource(remoteSocket),
                         localSocket,
                         remoteSocket,
                         method + " " + path + " HTTP/1.1\r\n" +
                         "Host: example.test\r\n" +
                         "Content-Type: application/json; charset=UTF-8\r\n" +
                         "Content-Length: " + body.Length + "\r\n" +
                         "Connection: close\r\n",
                         out var request,
                         out var errorResponse,
                         HTTPBodyStream:    body,
                         HTTPServer:         server,
                         CancellationToken: cancellationToken
                     );

        Assert.That(parsed, Is.True, errorResponse?.ToString());
        Assert.That(request, Is.Not.Null);

        return request!;
    }

    private static HTTPExtAPI CreateLoginFixture(HTTPServer server,
                                                 out User   user)
    {
        var api = new HTTPExtAPI(
                      server,
                      SkipURLTemplates:     false,
                      DisableLogging:       false,
                      DisableNotifications: true
                  );

        user = new User(
                   User_Id.Parse("redirect-login-user"),
                   I18NString.Create("Redirect Login User"),
                   SimpleEMailAddress.Parse("redirect-login@example.test")
               );

        var organization = new Organization(
                               Organization_Id.Parse("redirect-login-org"),
                               I18NString.Create("Redirect Login Organization")
                           );

        user.AddOutgoingEdge(User2OrganizationEdgeLabel.IsMember, organization);

        AddPrivateDictionaryEntry(api, "users", user.Id, user);
        AddPrivateDictionaryEntry(
            api,
            "loginPasswords",
            user.Id,
            new LoginPassword(user.Id, Password.Parse("RedirectPassword-123!"))
        );

        return api;
    }

    private static HTTPRequest ParseFormRequest(HTTPServer        server,
                                                String            path,
                                                String            bodyText,
                                                CancellationToken cancellationToken)
    {
        var body = new MemoryStream(Encoding.UTF8.GetBytes(bodyText));
        var localSocket  = IPSocket.LocalhostV4(IPPort.Parse(6801));
        var remoteSocket = IPSocket.LocalhostV4(IPPort.Parse(43123));

        var parsed = HTTPRequest.TryParse(
                         Timestamp.Now,
                         new HTTPSource(remoteSocket),
                         localSocket,
                         remoteSocket,
                         "POST " + path + " HTTP/1.1\r\n" +
                         "Host: example.test\r\n" +
                         "Content-Type: application/x-www-form-urlencoded\r\n" +
                         "Content-Length: " + body.Length + "\r\n" +
                         "Connection: close\r\n",
                         out var request,
                         out var errorResponse,
                         HTTPBodyStream:    body,
                         HTTPServer:         server,
                         CancellationToken: cancellationToken
                     );

        Assert.That(parsed, Is.True, errorResponse?.ToString());
        Assert.That(request, Is.Not.Null);

        return request!;
    }

    private static void AddPrivateDictionaryEntry<TKey, TValue>(HTTPExtAPI api,
                                                                String     fieldName,
                                                                TKey       key,
                                                                TValue     value)
    {
        var field = typeof(HTTPExtAPI).GetField(
                        fieldName,
                        BindingFlags.Instance | BindingFlags.NonPublic
                    );
        Assert.That(field, Is.Not.Null);

        var dictionary = field!.GetValue(api);
        var tryAdd = dictionary!.GetType().GetMethod("TryAdd");
        Assert.That(tryAdd, Is.Not.Null);
        Assert.That(
            (Boolean) tryAdd!.Invoke(dictionary, [ key, value ])!,
            Is.True
        );
    }

    private static async Task<HTTPResponse> InvokeJSONHandler(HTTPServer   server,
                                                              HTTPDelegate handler,
                                                              String       method,
                                                              String       path,
                                                              String       bodyText)
    {
        await using var body = new MemoryStream(Encoding.UTF8.GetBytes(bodyText));
        var request = ParseJSONRequest(
                          server,
                          method,
                          path,
                          body,
                          CancellationToken.None
                      );

        return await handler(request);
    }

    private static HTTPDelegate GetRegisteredHandler(HTTPServer server,
                                                      HTTPRequest request)
    {
        var getRequestHandle = typeof(HTTPServer).GetMethod(
                                   "GetRequestHandle",
                                   BindingFlags.Instance | BindingFlags.NonPublic,
                                   binder:      null,
                                   types:       [ typeof(HTTPRequest) ],
                                   modifiers:   null
                               );

        Assert.That(getRequestHandle, Is.Not.Null);

        var parsedRequest = getRequestHandle!.Invoke(server, [ request ]) as ParsedRequest;
        Assert.That(parsedRequest?.RequestHandlers?.RequestHandler, Is.Not.Null);

        return parsedRequest!.RequestHandlers!.RequestHandler!;
    }

    private static async Task<(String StatusLine, String Body)> ReadSingleHTTPResponse(StreamReader       reader,
                                                                                       CancellationToken cancellationToken)
    {
        var statusLine = await reader.ReadLineAsync(cancellationToken);
        Assert.That(statusLine, Is.Not.Null.And.Not.Empty);

        var contentLength = 0;

        while (true)
        {
            var headerLine = await reader.ReadLineAsync(cancellationToken);
            Assert.That(headerLine, Is.Not.Null);

            if (headerLine!.Length == 0)
                break;

            if (headerLine.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
            {
                Assert.That(
                    Int32.TryParse(headerLine["Content-Length:".Length..].Trim(), out contentLength),
                    Is.True
                );
            }
        }

        var body       = new Char[contentLength];
        var bodyOffset = 0;

        while (bodyOffset < body.Length)
        {
            var read = await reader.ReadAsync(
                           body.AsMemory(bodyOffset),
                           cancellationToken
                       );

            if (read == 0)
                throw new EndOfStreamException("The HTTP response body ended before Content-Length was reached.");

            bodyOffset += read;
        }

        return (statusLine!, new String(body));
    }

    private static async Task<(String StatusLine, String Body)> SendRawRequest(HTTPServer server,
                                                                               String     request)
    {
        using var client = new TcpClient(AddressFamily.InterNetwork);
        await client.ConnectAsync(System.Net.IPAddress.Loopback, server.TCPPort.ToInt32());

        await using var stream = client.GetStream();
        using var reader = new StreamReader(
                               stream,
                               Encoding.ASCII,
                               detectEncodingFromByteOrderMarks: false,
                               leaveOpen: true
                           );
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(3));

        await stream.WriteAsync(Encoding.ASCII.GetBytes(request), cancellation.Token);

        return await ReadSingleHTTPResponse(reader, cancellation.Token);
    }

    private static Int32 ReserveFreePort()
    {
        var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();

        try
        {
            return ((System.Net.IPEndPoint) listener.LocalEndpoint).Port;
        }
        finally
        {
            listener.Stop();
        }
    }

}
