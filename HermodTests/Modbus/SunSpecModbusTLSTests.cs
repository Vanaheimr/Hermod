/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of Hermod <https://www.github.com/Vanaheimr/Hermod>
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

#region Usings

using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

using Microsoft.Extensions.Logging;

using NUnit.Framework;

using org.GraphDefined.Vanaheimr.Hermod.SunSpecModbusTLS.Common;
using org.GraphDefined.Vanaheimr.Hermod.SunSpecModbusTLS.PKI;

using HermodModbusTCPClient = org.GraphDefined.Vanaheimr.Hermod.Modbus.ModbusTCPClient;
using NetIPAddress          = System.Net.IPAddress;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Tests.Modbus;

public class SunSpecModbusTLSTests
{

    #region Hermod_ModbusTLSClient_Reads_SunSpecEnergyMeter_Test()

    [Test]
    public async Task Hermod_ModbusTLSClient_Reads_SunSpecEnergyMeter_Test()
    {

        var pkiDirectory = Path.Combine(
                               TestContext.CurrentContext.WorkDirectory,
                               "SunSpecModbusTLS",
                               Guid.NewGuid().ToString("N")
                           );

        await new ModbusPKI().BuildPKI(pkiDirectory);

        var listenPort = GetFreeTcpPort();

        using var meter        = new SunSpecMeterDevice("meter-test-001");
        using var frontendCts  = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        using var frontend     = new ModbusTlsFrontend(
                                     new ModbusTlsFrontendOptions(
                                         NetIPAddress.Loopback,
                                         listenPort,
                                         Path.Combine(pkiDirectory, "server.pfx"),
                                         "demo",
                                         Path.Combine(pkiDirectory, "issuing-clients-ca.crt"),
                                         TimeSpan.FromSeconds(5),
                                         TimeSpan.FromSeconds(5),
                                         TimeSpan.FromSeconds(5)
                                     ),
                                     new SunSpecBackendFactory(meter),
                                     new AuthorizationPolicy(meter),
                                     new NUnitLogger<ModbusTlsFrontend>()
                                 );

        var frontendTask = frontend.RunAsync(frontendCts.Token);
        await WaitForListenerAsync(listenPort, frontendCts.Token);

        await ReadAndAssertEnergyMeterAsync(
                  listenPort,
                  null,
                  pkiDirectory,
                  frontendCts.Token
              );

        await frontendCts.CancelAsync();
        await frontendTask.WaitAsync(TimeSpan.FromSeconds(2));

    }

    #endregion

    #region Hermod_ModbusTLSClient_Reads_SunSpecEnergyMeter_WithTwoRootCAs_SelectedBySNI_Test()

    [Test]
    public async Task Hermod_ModbusTLSClient_Reads_SunSpecEnergyMeter_WithTwoRootCAs_SelectedBySNI_Test()
    {

        var pkiDirectory = Path.Combine(
                               TestContext.CurrentContext.WorkDirectory,
                               "SunSpecModbusTLS",
                               Guid.NewGuid().ToString("N")
                           );

        var rootA = Path.Combine(pkiDirectory, "root-a");
        var rootB = Path.Combine(pkiDirectory, "root-b");

        await new ModbusPKI().BuildPKI(rootA);
        await new ModbusPKI().BuildPKI(rootB);

        const String sniRootA = "meter-a.sunspec.test";
        const String sniRootB = "meter-b.sunspec.test";

        var listenPort = GetFreeTcpPort();

        using var meter        = new SunSpecMeterDevice("meter-test-sni-001");
        using var frontendCts  = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        using var frontend     = new ModbusTlsFrontend(
                                     new ModbusTlsFrontendOptions(
                                         NetIPAddress.Loopback,
                                         listenPort,
                                         Path.Combine(rootA, "server.pfx"),
                                         "demo",
                                         Path.Combine(rootA, "issuing-clients-ca.crt"),
                                         TimeSpan.FromSeconds(5),
                                         TimeSpan.FromSeconds(5),
                                         TimeSpan.FromSeconds(5),
                                         [
                                             new ModbusTlsFrontendSNIBinding(
                                                 sniRootA,
                                                 Path.Combine(rootA, "server.pfx"),
                                                 "demo",
                                                 Path.Combine(rootA, "issuing-clients-ca.crt")
                                             ),
                                             new ModbusTlsFrontendSNIBinding(
                                                 sniRootB,
                                                 Path.Combine(rootB, "server.pfx"),
                                                 "demo",
                                                 Path.Combine(rootB, "issuing-clients-ca.crt")
                                             )
                                         ]
                                     ),
                                     new SunSpecBackendFactory(meter),
                                     new AuthorizationPolicy(meter),
                                     new NUnitLogger<ModbusTlsFrontend>()
                                 );

        var frontendTask = frontend.RunAsync(frontendCts.Token);
        await WaitForListenerAsync(listenPort, frontendCts.Token);

        await ReadAndAssertEnergyMeterAsync(
                  listenPort,
                  sniRootA,
                  rootA,
                  frontendCts.Token
              );

        await ReadAndAssertEnergyMeterAsync(
                  listenPort,
                  sniRootB,
                  rootB,
                  frontendCts.Token
              );

        await frontendCts.CancelAsync();
        await frontendTask.WaitAsync(TimeSpan.FromSeconds(2));

    }

    #endregion


    private static Int32 GetFreeTcpPort()
    {

        var listener = new TcpListener(NetIPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint) listener.LocalEndpoint).Port;
        listener.Stop();
        return port;

    }

    private static async Task WaitForListenerAsync(Int32 listenPort, CancellationToken ct)
    {

        for (var i = 0; i < 50; i++)
        {
            try
            {
                using var tcpClient = new TcpClient();
                await tcpClient.ConnectAsync(NetIPAddress.Loopback, listenPort, ct);
                return;
            }
            catch when (!ct.IsCancellationRequested)
            {
                await Task.Delay(50, ct);
            }
        }

        Assert.Fail($"The SunSpec Modbus/TLS listener on 127.0.0.1:{listenPort} did not become reachable.");

    }

    private static async Task ReadAndAssertEnergyMeterAsync(Int32              listenPort,
                                                            String?            TLSHostname,
                                                            String             pkiDirectory,
                                                            CancellationToken  ct)
    {

        var expectedServerCertificate = X509CertificateLoader.LoadCertificateFromFile(
                                            Path.Combine(pkiDirectory, "server.crt")
                                        );

        var clientCertificateChain = LoadPkcs12CertificateChain(
                                         Path.Combine(pkiDirectory, $"client-{SunSpecRoles.ReadOnly}.pfx"),
                                         "demo",
                                         "Issuing Clients CA"
                                     );

        using var client = new HermodModbusTCPClient(
                               IPv4Address.Localhost,
                               IPPort.Parse(listenPort),
                               UnitAddress:                1,
                               StartingAddressOffset:      1,
                               RemoteCertificateValidator: (sender,
                                                            serverCertificate,
                                                            serverCertificateChain,
                                                            modbusClient,
                                                            policyErrors) =>
                                                                ValidatePinnedServerCertificate(
                                                                    serverCertificate,
                                                                    expectedServerCertificate
                                                                ),
                               ClientCert:                 clientCertificateChain[0],
                               ClientCertificateChain:     clientCertificateChain,
                               TLSHostname:                TLSHostname,
                               TLSProtocol:                SslProtocols.Tls12 | SslProtocols.Tls13,
                               PreferIPv4:                 true,
                               RequestTimeout:             TimeSpan.FromSeconds(5),
                               MaxNumberOfRetries:         1
                           );

        var connectResult = await client.ReconnectAsync(ct);
        Assert.That(connectResult.IsSuccess,
                    Is.True,
                    String.Join(", ", connectResult.Errors.Select(error => error.ToString())));

        await Task.Delay(1100, ct);

        var commonResponse = await client.ReadHoldingRegisters(
                                 SunSpecMeterMap.BaseAddress,
                                 2
                             );
        var commonRegisters = commonResponse.HoldingRegisters.ToArray();

        var meterResponse = await client.ReadHoldingRegisters(
                                SunSpecMeterMap.Addr(SunSpecMeterMap.OffMeterId),
                                (ushort) (SunSpecMeterMap.MeterModelLength + 4)
                            );
        var meterRegisters = meterResponse.HoldingRegisters.ToArray();

        Assert.Multiple(() => {

            Assert.That(commonRegisters, Has.Length.EqualTo(2));
            Assert.That(meterRegisters,  Has.Length.EqualTo(SunSpecMeterMap.MeterModelLength + 4));

            Assert.That(commonRegisters[0], Is.EqualTo((ushort)(SunSpecMeterMap.SunSpecMarker >> 16)));
            Assert.That(commonRegisters[1], Is.EqualTo((ushort)(SunSpecMeterMap.SunSpecMarker & 0xFFFF)));

            Assert.That(meterRegisters[0], Is.EqualTo(SunSpecMeterMap.MeterModelId));
            Assert.That(meterRegisters[1], Is.EqualTo(SunSpecMeterMap.MeterModelLength));

            Assert.That(meterRegisters[SunSpecMeterMap.OffMeterA   - SunSpecMeterMap.OffMeterId], Is.InRange((ushort)900,  (ushort)1100));
            Assert.That(meterRegisters[SunSpecMeterMap.OffMeterPhV - SunSpecMeterMap.OffMeterId], Is.InRange((ushort)2280, (ushort)2320));
            Assert.That(meterRegisters[SunSpecMeterMap.OffMeterHz  - SunSpecMeterMap.OffMeterId], Is.InRange((ushort)4990, (ushort)5010));
            Assert.That(meterRegisters[SunSpecMeterMap.OffMeterW   - SunSpecMeterMap.OffMeterId], Is.GreaterThan((ushort)0));

            Assert.That(meterRegisters[SunSpecMeterMap.OffEndModelId  - SunSpecMeterMap.OffMeterId], Is.EqualTo(SunSpecMeterMap.EndModelId));
            Assert.That(meterRegisters[SunSpecMeterMap.OffEndModelLen - SunSpecMeterMap.OffMeterId], Is.EqualTo((ushort)0));

        });

        await client.Close();

    }

    private static X509Certificate2[] LoadPkcs12CertificateChain(String path,
                                                                 String password,
                                                                 String issuingCAName)
    {

        var certificates = X509CertificateLoader.LoadPkcs12CollectionFromFile(
                               path,
                               password,
                               X509KeyStorageFlags.UserKeySet | X509KeyStorageFlags.Exportable
                           ).
                           OfType<X509Certificate2>().
                           ToArray();

        var clientCertificate = certificates.
                                    Single(certificate => certificate.HasPrivateKey &&
                                                          !IsCertificateAuthority(certificate));

        var issuingCA = certificates.
                            Single(certificate => IsCertificateAuthority(certificate) &&
                                                  certificate.Subject.Contains(issuingCAName, StringComparison.Ordinal));

        return [
            clientCertificate,
            issuingCA
        ];

    }

    private static Boolean IsCertificateAuthority(X509Certificate2 certificate)
        => certificate.Extensions.
               OfType<X509BasicConstraintsExtension>().
               Any(extension => extension.CertificateAuthority);

    private static TLSValidationResult ValidatePinnedServerCertificate(X509Certificate2? serverCertificate,
                                                                       X509Certificate2  expectedServerCertificate)
    {

        if (serverCertificate is null)
            return TLSValidationResult.Failed("The Modbus/TLS server certificate must not be null!");

        return String.Equals(serverCertificate.Thumbprint,
                             expectedServerCertificate.Thumbprint,
                             StringComparison.OrdinalIgnoreCase)
                   ? TLSValidationResult.Success()
                   : TLSValidationResult.Failed("The Modbus/TLS server certificate did not match the pinned test certificate!");

    }

    private sealed class NUnitLogger<T> : ILogger<T>
    {

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
            => null;

        public Boolean IsEnabled(LogLevel logLevel)
            => true;

        public void Log<TState>(LogLevel                         logLevel,
                                EventId                          eventId,
                                TState                           state,
                                Exception?                       exception,
                                Func<TState, Exception?, String> formatter)
        {

            TestContext.Out.WriteLine($"{logLevel}: {formatter(state, exception)}");

            if (exception is not null)
                TestContext.Out.WriteLine(exception);

        }

    }

}
