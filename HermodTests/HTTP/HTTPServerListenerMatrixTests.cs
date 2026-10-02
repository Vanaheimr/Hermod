using System.Net.Sockets;

using org.GraphDefined.Vanaheimr.Hermod;
using org.GraphDefined.Vanaheimr.Hermod.HTTP;

using HermodIPAddress = org.GraphDefined.Vanaheimr.Hermod.IPAddress;

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.HTTP;

[TestFixture]
public sealed class HTTPServerListenerMatrixTests
{

    [Test]
    public async Task Fixed_Port_Composite_Any_Must_Accept_IPv4_And_IPv6()
    {
        using var port = new ClosedPort();
        var server = CreateServer(HermodIPAddress.Any, port);

        try
        {
            // [::] in dual mode, where Windows lets no other socket be bound
            // next to the server.
            port.HandOver(ForIPv6Any: true);

            await server.Start();
            await AssertAccepts(server, AddressFamily.InterNetwork);

            if (Socket.OSSupportsIPv6)
                await AssertAccepts(server, AddressFamily.InterNetworkV6);
        }
        finally
        {
            await StopIgnoringServerTaskFailure(server);
        }
    }

    [Test]
    public async Task Fixed_Port_Composite_Localhost_Must_Accept_IPv4_And_IPv6()
    {
        using var port = new ClosedPort();
        var server = CreateServer(HermodIPAddress.Localhost, port);

        try
        {
            port.HandOver();

            await server.Start();
            await AssertAccepts(server, AddressFamily.InterNetwork);

            if (Socket.OSSupportsIPv6)
                await AssertAccepts(server, AddressFamily.InterNetworkV6);
        }
        finally
        {
            await StopIgnoringServerTaskFailure(server);
        }
    }

    [Test]
    public async Task Fixed_Port_Pure_IPv6_Any_Must_Accept_IPv6()
    {
        if (!Socket.OSSupportsIPv6)
            Assert.Ignore("IPv6 is not available on this host.");

        using var port = new ClosedPort();
        var server = CreateServer(IPv6Address.Any, port);

        try
        {
            // [::] without dual mode, which Windows is taken to refuse next to
            // what holds the port as well - see ClosedPort.
            port.HandOver(ForIPv6Any: true);

            await server.Start();
            await AssertAccepts(server, AddressFamily.InterNetworkV6);
        }
        finally
        {
            await StopIgnoringServerTaskFailure(server);
        }
    }

    [Test]
    public async Task Fixed_Port_Pure_IPv4_Localhost_Must_Accept_IPv4()
    {
        using var port = new ClosedPort();
        var server = CreateServer(IPv4Address.Localhost, port);

        try
        {
            port.HandOver();

            await server.Start();
            await AssertAccepts(server, AddressFamily.InterNetwork);
        }
        finally
        {
            await StopIgnoringServerTaskFailure(server);
        }
    }

    [Test]
    public async Task Ephemeral_Port_Pure_IPv4_Any_Must_Accept_IPv4()
    {
        await using var server = new HTTPServer(
                                     IPAddress: IPv4Address.Any,
                                     TCPPort:   IPPort.Zero,
                                     AutoStart: false
                                 );

        try
        {
            await server.Start();
            await AssertAccepts(server, AddressFamily.InterNetwork);
        }
        finally
        {
            await StopIgnoringServerTaskFailure(server);
        }
    }

    [Test]
    public async Task Fixed_Port_Pure_IPv4_Any_Must_Accept_IPv4()
    {
        using var port = new ClosedPort();
        var server = CreateServer(IPv4Address.Any, port);

        try
        {
            port.HandOver();

            await server.Start();
            await AssertAccepts(server, AddressFamily.InterNetwork);
        }
        finally
        {
            await StopIgnoringServerTaskFailure(server);
        }
    }

    // A fixed port, as these tests are about, held by the test until the
    // server binds it in Start() - rather than one that was free a moment
    // before, and with other test runs on the same machine was now and then
    // given to one of them in between. See ClosedPort.
    private static HTTPServer CreateServer(IIPAddress ipAddress,
                                           ClosedPort port)

        => new (
               IPAddress: ipAddress,
               TCPPort:   port.Number,
               AutoStart: false
           );

    private static async Task AssertAccepts(HTTPServer    server,
                                            AddressFamily addressFamily)
    {
        using var client = new TcpClient(addressFamily);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(3));

        var address = addressFamily == AddressFamily.InterNetwork
                          ? System.Net.IPAddress.Loopback
                          : System.Net.IPAddress.IPv6Loopback;

        await client.ConnectAsync(address, server.TCPPort.ToInt32(), cancellation.Token);
        Assert.That(client.Connected, Is.True);
    }

    private static async Task StopIgnoringServerTaskFailure(HTTPServer server)
    {
        try
        {
            await server.DisposeAsync();
        }
        catch
        {
            // A matrix failure can fault the background accept task. The
            // connection assertion above is the diagnostic result we retain.
        }
    }

}
