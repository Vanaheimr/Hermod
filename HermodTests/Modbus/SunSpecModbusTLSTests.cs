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
using System.Collections.Concurrent;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

using Microsoft.Extensions.Logging;

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

        using var meter        = new SunSpecMeterDevice("meter-test-001",     SunSpecMeterMode.ImportOnly);
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

        using var meter        = new SunSpecMeterDevice("meter-test-sni-001", SunSpecMeterMode.ImportOnly);
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


    #region Hermod_ModbusTLSClient_WriteSingleRegister_SunSpecEnergyMeter_Test()

    /// <summary>
    /// Function code 06 over Modbus/TLS: the client writes the meter mode
    /// register, and reads back what the meter holds now.
    /// </summary>
    [Test]
    public async Task Hermod_ModbusTLSClient_WriteSingleRegister_SunSpecEnergyMeter_Test()
    {

        using       var meter    = new SunSpecMeterDevice("meter-test-write-06", SunSpecMeterMode.ImportOnly);
        await using var sunSpec  = await SunSpecMeterServer.StartAsync(meter);
        using       var client   = await sunSpec.ConnectAsync(SunSpecRoles.GridService,
                                                              StartingAddressOffset: 1,
                                                              UnitAddress:           7);

        var modeRegister  = SunSpecMeterMap.Addr(SunSpecMeterMap.OffMeterMeterMode);

        var response      = await client.WriteSingleRegister(
                                      modeRegister,
                                      [ 0x00, (Byte) SunSpecMeterMode.ExportOnly ]
                                  );

        var readBack      = (await client.ReadHoldingRegisters(modeRegister, 1)).HoldingRegisters.ToArray();

        var request       = sunSpec.Requests.Single(info => info.FunctionCode == ModbusFunctionCodes.WriteSingleRegister);

        Assert.Multiple(() => {

            // What the meter was asked...
            Assert.That(request.UnitId,         Is.EqualTo(7));
            Assert.That(request.Address,        Is.EqualTo(modeRegister));
            Assert.That(request.Quantity,       Is.EqualTo(1));
            Assert.That(request.Allowed,        Is.True, request.DenyReason);
            Assert.That(request.ExceptionCode,  Is.Null);

            // ...what it answered, which for this function code is the request itself...
            Assert.That(response, Is.EqualTo(new Byte[] {
                                      (Byte) (request.TransactionId >> 8), (Byte) request.TransactionId,
                                      0x00, 0x00,                                       // protocol identifier
                                      0x00, 0x06,                                       // length
                                      0x07,                                             // unit identifier
                                      0x06,                                             // write single register
                                      (Byte) (modeRegister >> 8), (Byte) modeRegister,
                                      0x00, (Byte) SunSpecMeterMode.ExportOnly
                                  }));

            // ...and what it is now.
            Assert.That(meter.Mode,  Is.EqualTo(SunSpecMeterMode.ExportOnly));
            Assert.That(readBack,    Is.EqualTo(new[] { (UInt16) SunSpecMeterMode.ExportOnly }));

        });

    }

    #endregion

    #region Hermod_ModbusTLSClient_WriteMultipleRegister_SunSpecEnergyMeter_Test()

    /// <summary>
    /// Function code 16 over Modbus/TLS: two registers in one request - the
    /// meter mode, and the register after it, which clears both energy
    /// counters when 0xCAFE is written to it.
    /// </summary>
    [Test]
    public async Task Hermod_ModbusTLSClient_WriteMultipleRegister_SunSpecEnergyMeter_Test()
    {

        // Stepped by hand rather than by its background task, so that between
        // the write and the reads nothing changes but what the write changed.
        using var meter = new SunSpecMeterDevice("meter-test-write-16", SunSpecMeterMode.ImportOnly, runSimulation: false);

        meter.Advance(TimeSpan.FromHours(1));

        Assert.That(meter.EnergyCounters.ImportedWh, Is.GreaterThan(0), "a meter in front of a load has counted something after an hour");

        await using var sunSpec  = await SunSpecMeterServer.StartAsync(meter);
        using       var client   = await sunSpec.ConnectAsync(SunSpecRoles.GridService,
                                                              StartingAddressOffset: 1);

        var modeRegister  = SunSpecMeterMap.Addr(SunSpecMeterMap.OffMeterMeterMode);

        var response      = await client.WriteMultipleRegister(
                                      modeRegister,
                                      [ 0x00, (Byte) SunSpecMeterMode.ExportOnly,
                                        0xCA, 0xFE ]
                                  );

        // Both energy counters, their scale factor, the mode and the reset register
        var readBack      = (await client.ReadHoldingRegisters(
                                       SunSpecMeterMap.Addr(SunSpecMeterMap.OffMeterTotWhExp),
                                       SunSpecMeterMap.OffMeterResetEnergy - SunSpecMeterMap.OffMeterTotWhExp + 1
                                   )).HoldingRegisters.ToArray();

        var request       = sunSpec.Requests.Single(info => info.FunctionCode == ModbusFunctionCodes.WriteMultipleRegisters);

        Assert.Multiple(() => {

            Assert.That(request.UnitId,         Is.EqualTo(1));
            Assert.That(request.Address,        Is.EqualTo(modeRegister));
            Assert.That(request.Quantity,       Is.EqualTo(2));
            Assert.That(request.Allowed,        Is.True, request.DenyReason);
            Assert.That(request.ExceptionCode,  Is.Null);

            // Answered with where the registers were written, and how many
            Assert.That(response, Is.EqualTo(new Byte[] {
                                      (Byte) (request.TransactionId >> 8), (Byte) request.TransactionId,
                                      0x00, 0x00,                                       // protocol identifier
                                      0x00, 0x06,                                       // length
                                      0x01,                                             // unit identifier
                                      0x10,                                             // write multiple registers
                                      (Byte) (modeRegister >> 8), (Byte) modeRegister,
                                      0x00, 0x02                                        // two registers
                                  }));

            Assert.That(meter.Mode,                       Is.EqualTo(SunSpecMeterMode.ExportOnly));
            Assert.That(meter.EnergyCounters.ImportedWh,  Is.Zero);
            Assert.That(meter.EnergyCounters.ExportedWh,  Is.Zero);

            Assert.That(readBack, Is.EqualTo(new UInt16[] {
                                      0, 0,                                             // exported
                                      0, 0,                                             // imported
                                      unchecked((UInt16) meter.EnergyCounters.ScaleFactor),
                                      (UInt16) SunSpecMeterMode.ExportOnly,
                                      0                                                 // what was written to it is not kept
                                  }));

        });

    }

    #endregion

    #region Hermod_ModbusTLSClient_WritesTheRegisterItReads_Test(StartingAddressOffset)

    /// <summary>
    /// The client numbers registers from 1, as the Modbus data model does, and
    /// adds its StartingAddressOffset: a read of register N asks for address
    /// N + offset - 1. A write of register N has to go to the same address, or
    /// the client changes a register it does not read back.
    /// </summary>
    [TestCase((Int16)   0)]
    [TestCase((Int16)   1)]
    [TestCase((Int16) 100)]
    public async Task Hermod_ModbusTLSClient_WritesTheRegisterItReads_Test(Int16 StartingAddressOffset)
    {

        using       var meter    = new SunSpecMeterDevice($"meter-test-offset-{StartingAddressOffset}", SunSpecMeterMode.ImportOnly);
        await using var sunSpec  = await SunSpecMeterServer.StartAsync(meter);
        using       var client   = await sunSpec.ConnectAsync(SunSpecRoles.GridService,
                                                              StartingAddressOffset);

        var modeAddress   = SunSpecMeterMap.Addr(SunSpecMeterMap.OffMeterMeterMode);
        var modeRegister  = (UInt16) (modeAddress + 1 - StartingAddressOffset);

        await client.WriteSingleRegister  (modeRegister, [ 0x00, (Byte) SunSpecMeterMode.ExportOnly ]);

        var modeAfterSingle        = meter.Mode;
        var readBackAfterSingle    = (await client.ReadHoldingRegisters(modeRegister, 1)).HoldingRegisters.ToArray();

        await client.WriteMultipleRegister(modeRegister, [ 0x00, (Byte) SunSpecMeterMode.Net ]);

        var modeAfterMultiple      = meter.Mode;
        var readBackAfterMultiple  = (await client.ReadHoldingRegisters(modeRegister, 1)).HoldingRegisters.ToArray();

        Assert.Multiple(() => {

            Assert.That(sunSpec.Requests.Select(info => (info.FunctionCode, info.Address)),
                        Is.EqualTo(new[] {
                            (ModbusFunctionCodes.WriteSingleRegister,    modeAddress),
                            (ModbusFunctionCodes.ReadHoldingRegisters,   modeAddress),
                            (ModbusFunctionCodes.WriteMultipleRegisters, modeAddress),
                            (ModbusFunctionCodes.ReadHoldingRegisters,   modeAddress)
                        }));

            Assert.That(modeAfterSingle,        Is.EqualTo(SunSpecMeterMode.ExportOnly));
            Assert.That(readBackAfterSingle,    Is.EqualTo(new[] { (UInt16) SunSpecMeterMode.ExportOnly }));

            Assert.That(modeAfterMultiple,      Is.EqualTo(SunSpecMeterMode.Net));
            Assert.That(readBackAfterMultiple,  Is.EqualTo(new[] { (UInt16) SunSpecMeterMode.Net }));

        });

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

            Assert.That(meterRegisters[SunSpecMeterMap.OffMeterPhV - SunSpecMeterMap.OffMeterId], Is.InRange((ushort)2280, (ushort)2320));
            Assert.That(meterRegisters[SunSpecMeterMap.OffMeterHz  - SunSpecMeterMap.OffMeterId], Is.InRange((ushort)4990, (ushort)5010));

            // This meter is in front of a load, so it draws - and SunSpec "A"
            // is the TOTAL current, which is the three phases added up.
            Assert.That((short) meterRegisters[SunSpecMeterMap.OffMeterW - SunSpecMeterMap.OffMeterId], Is.GreaterThan(0));
            Assert.That((short) meterRegisters[SunSpecMeterMap.OffMeterA - SunSpecMeterMap.OffMeterId],
                        Is.EqualTo((short) meterRegisters[SunSpecMeterMap.OffMeterAphA - SunSpecMeterMap.OffMeterId] +
                                   (short) meterRegisters[SunSpecMeterMap.OffMeterAphB - SunSpecMeterMap.OffMeterId] +
                                   (short) meterRegisters[SunSpecMeterMap.OffMeterAphC - SunSpecMeterMap.OffMeterId]));

            Assert.That(meterRegisters[SunSpecMeterMap.OffMeterMeterMode - SunSpecMeterMap.OffMeterId],
                        Is.EqualTo((ushort) SunSpecMeterMode.ImportOnly));

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

    /// <summary>
    /// A simulated SunSpec energy meter behind Hermod's Modbus/TLS frontend, on
    /// a free loopback port and with a PKI of its own - and every request the
    /// frontend saw, as it saw it.
    /// </summary>
    private sealed class SunSpecMeterServer : IAsyncDisposable
    {

        private readonly ModbusTlsFrontend        frontend;
        private readonly CancellationTokenSource  frontendCts;
        private readonly Task                     frontendTask;

        public String                              PKIDirectory    { get; }
        public Int32                               ListenPort      { get; }

        /// <summary>
        /// Every Modbus request the frontend saw, refused ones included.
        /// </summary>
        public ConcurrentQueue<ModbusRequestInfo>  Requests        { get; } = new();

        private SunSpecMeterServer(String                   PKIDirectory,
                                   Int32                    ListenPort,
                                   ModbusTlsFrontend        Frontend,
                                   CancellationTokenSource  FrontendCts)
        {

            this.PKIDirectory  = PKIDirectory;
            this.ListenPort    = ListenPort;
            this.frontend      = Frontend;
            this.frontendCts   = FrontendCts;

            frontend.OnModbusRequest += Requests.Enqueue;

            this.frontendTask  = frontend.RunAsync(frontendCts.Token);

        }

        public static async Task<SunSpecMeterServer> StartAsync(SunSpecMeterDevice Meter)
        {

            var pkiDirectory  = Path.Combine(
                                    TestContext.CurrentContext.WorkDirectory,
                                    "SunSpecModbusTLS",
                                    Guid.NewGuid().ToString("N")
                                );

            await new ModbusPKI().BuildPKI(pkiDirectory);

            var listenPort    = GetFreeTcpPort();

            var server        = new SunSpecMeterServer(
                                    pkiDirectory,
                                    listenPort,
                                    new ModbusTlsFrontend(
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
                                        new SunSpecBackendFactory(Meter),
                                        new AuthorizationPolicy(Meter),
                                        new NUnitLogger<ModbusTlsFrontend>()
                                    ),
                                    new CancellationTokenSource(TimeSpan.FromSeconds(20))
                                );

            await WaitForListenerAsync(listenPort, server.frontendCts.Token);

            return server;

        }

        /// <summary>
        /// A Hermod Modbus/TLS client, connected with the client certificate
        /// of the given SunSpec role.
        /// </summary>
        public async Task<HermodModbusTCPClient> ConnectAsync(String  Role,
                                                              Int16   StartingAddressOffset,
                                                              Byte    UnitAddress   = 1)
        {

            var expectedServerCertificate  = X509CertificateLoader.LoadCertificateFromFile(
                                                 Path.Combine(PKIDirectory, "server.crt")
                                             );

            var clientCertificateChain     = LoadPkcs12CertificateChain(
                                                 Path.Combine(PKIDirectory, $"client-{Role}.pfx"),
                                                 "demo",
                                                 "Issuing Clients CA"
                                             );

            var client                     = new HermodModbusTCPClient(
                                                 IPv4Address.Localhost,
                                                 IPPort.Parse(ListenPort),
                                                 UnitAddress:                UnitAddress,
                                                 StartingAddressOffset:      StartingAddressOffset,
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
                                                 TLSProtocol:                SslProtocols.Tls12 | SslProtocols.Tls13,
                                                 PreferIPv4:                 true,
                                                 RequestTimeout:             TimeSpan.FromSeconds(5),
                                                 MaxNumberOfRetries:         1
                                             );

            var connectResult              = await client.ReconnectAsync(frontendCts.Token);

            Assert.That(connectResult.IsSuccess,
                        Is.True,
                        String.Join(", ", connectResult.Errors.Select(error => error.ToString())));

            return client;

        }

        public async ValueTask DisposeAsync()
        {

            await frontendCts.CancelAsync();
            await frontendTask.WaitAsync(TimeSpan.FromSeconds(2));

            frontend.Dispose();
            frontendCts.Dispose();

        }

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
