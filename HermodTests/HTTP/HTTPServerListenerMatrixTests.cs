using System.Net.Sockets;

using NUnit.Framework;

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
        var server = CreateServer(HermodIPAddress.Any, ReserveDualStackPort());

        try
        {
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
        var server = CreateServer(HermodIPAddress.Localhost, ReserveDualStackPort());

        try
        {
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

        var server = CreateServer(IPv6Address.Any, ReserveDualStackPort());

        try
        {
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
        var server = CreateServer(IPv4Address.Localhost, ReserveIPv4Port());

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
    public async Task Ephemeral_Port_Pure_IPv4_Any_Must_Accept_IPv4()
    {
        var server = new HTTPServer(
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
        var server = CreateServer(IPv4Address.Any, ReserveIPv4Port());

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


    private static HTTPServer CreateServer(IIPAddress ipAddress,
                                           Int32      port)

        => new (
               IPAddress: ipAddress,
               TCPPort:   IPPort.Parse(port),
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


    private static Int32 ReserveIPv4Port()
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


    private static Int32 ReserveDualStackPort()
    {
        if (!Socket.OSSupportsIPv6)
            return ReserveIPv4Port();

        var listener = new TcpListener(System.Net.IPAddress.IPv6Any, 0);
        listener.Server.DualMode = true;
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


    private static async Task StopIgnoringServerTaskFailure(HTTPServer server)
    {
        try
        {
            await server.Stop();
        }
        catch
        {
            // A matrix failure can fault the background accept task. The
            // connection assertion above is the diagnostic result we retain.
        }
    }

}
