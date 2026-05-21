/*
 * Copyright (c) 2010-2026 GraphDefined GmbH <achim.friedland@graphdefined.com>
 * This file is part of Vanaheimr Hermod <https://www.github.com/Vanaheimr/Hermod>
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

using System.Diagnostics;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace org.GraphDefined.Vanaheimr.Hermod.SunSpecModbusTLS.Common;

/// <summary>
/// Minimal Modbus/TCP and Modbus/TLS client.
///
/// Plain TCP is useful for forwarding already-authorized requests to a downstream
/// legacy Modbus/TCP server. TLS is the SunSpec mbaps mode and authenticates the
/// client certificate, including the SunSpec role extension, during the handshake.
///
/// The PDU is treated as opaque by <see cref="ExchangeAsync"/>. Higher-level
/// helpers build/parse the small set of PDUs used by the demo stack.
/// </summary>
public sealed class ModbusTCPClient : IAsyncDisposable
{

    private readonly TcpClient       tcpClient;
    private readonly Stream          stream;
    private readonly SslStream?      tlsStream;
    private readonly SemaphoreSlim   exchangeLock;
    private ushort                   txId;


    private ModbusTCPClient(TcpClient   TCPClient,
                            Stream      Stream,
                            SslStream?  TLSStream = null)
    {

        this.tcpClient     = TCPClient;
        this.stream        = Stream;
        this.tlsStream     = TLSStream;
        this.exchangeLock  = new SemaphoreSlim(1, 1);

    }


    public Boolean IsTLS

        => tlsStream is not null;


    public static async Task<ModbusTCPClient> ConnectAsync(String             host,
                                                            UInt16             port,
                                                            TimeSpan           connectTimeout,
                                                           CancellationToken  ct)
    {

        var tcp = new TcpClient {
                      NoDelay = true
                  };

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(connectTimeout);

        try
        {

            await tcp.ConnectAsync(host, port, cts.Token).
                      ConfigureAwait(false);

        }
        catch
        {
            tcp.Dispose();
            throw;
        }

        return new ModbusTCPClient(
                   tcp,
                   tcp.GetStream()
               );

    }


    public static async Task<ModbusTCPClient> ConnectTLSAsync(String             host,
                                                              UInt16             port,
                                                              X509Certificate2   clientCertificate,
                                                              X509Certificate2   trustedCA,
                                                              TimeSpan           connectTimeout,
                                                              TimeSpan           handshakeTimeout,
                                                              CancellationToken  ct,
                                                              String?            targetHost          = null,
                                                              SslProtocols       enabledProtocols    = SslProtocols.Tls12 | SslProtocols.Tls13)
    {

        var tcp           = new TcpClient {
                                NoDelay = true
                            };

        var tlsTargetHost = targetHost ?? host;
        var stopwatch     = Stopwatch.StartNew();
        var outcome       = "ok";

        try
        {

            using (var connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                connectCts.CancelAfter(connectTimeout);
                await tcp.ConnectAsync(host, port, connectCts.Token).
                          ConfigureAwait(false);
            }

            var networkStream = tcp.GetStream();
            var tlsStream     = new SslStream(
                                    networkStream,
                                    leaveInnerStreamOpen: false,
                                    userCertificateValidationCallback: BuildServerCertificateValidator(trustedCA)
                                );

            var options = new SslClientAuthenticationOptions {
                              TargetHost                      = tlsTargetHost,
                              ClientCertificates              = [ clientCertificate ],
                              EnabledSslProtocols             = enabledProtocols,
                              CertificateRevocationCheckMode  = X509RevocationMode.NoCheck,
                              AllowRenegotiation              = false,
                              EncryptionPolicy                = EncryptionPolicy.RequireEncryption
                          };

            using (var handshakeCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                handshakeCts.CancelAfter(handshakeTimeout);
                await tlsStream.AuthenticateAsClientAsync(options, handshakeCts.Token).
                                ConfigureAwait(false);
            }

            return new ModbusTCPClient(tcp, tlsStream, tlsStream);

        }
        catch (AuthenticationException)
        {
            outcome = "bad_cert";
            tcp.Dispose();
            throw;
        }
        catch (OperationCanceledException)
        {
            outcome = ct.IsCancellationRequested
                          ? "cancelled"
                          : "timeout";
            tcp.Dispose();
            throw;
        }
        catch
        {
            outcome = "io";
            tcp.Dispose();
            throw;
        }
        finally
        {
            stopwatch.Stop();
            ClientMetrics.Handshakes.Add(1, new TagList {
                { "outcome", outcome }
            });
            ClientMetrics.HandshakeDuration.Record(stopwatch.Elapsed.TotalSeconds);
        }

    }


    private static RemoteCertificateValidationCallback BuildServerCertificateValidator(X509Certificate2 trustedCA)

        => (sender, certificate, chainFromTLS, policyErrors) => {

            if (certificate is null)
                return false;

            var nonChainPolicyErrors = policyErrors & ~SslPolicyErrors.RemoteCertificateChainErrors;
            if (nonChainPolicyErrors != SslPolicyErrors.None)
                return false;

            var serverCertificate = certificate as X509Certificate2 ??
                                    X509CertificateLoader.LoadCertificate(certificate.Export(X509ContentType.Cert));

            using var chain = new X509Chain {
                ChainPolicy = {
                    RevocationMode    = X509RevocationMode.NoCheck,
                    TrustMode         = X509ChainTrustMode.CustomRootTrust,
                    VerificationFlags = X509VerificationFlags.NoFlag
                }
            };

            chain.ChainPolicy.CustomTrustStore.Add(trustedCA);

            if (!chain.Build(serverCertificate))
                return false;

            var eku = serverCertificate.Extensions["2.5.29.37"] as X509EnhancedKeyUsageExtension;

            return eku is null ||
                   eku.EnhancedKeyUsages.
                       OfType<System.Security.Cryptography.Oid>().
                       Any(oid => oid.Value == "1.3.6.1.5.5.7.3.1");

        };


    public Task<ushort[]> ReadHoldingRegistersAsync(Byte               unitId,
                                                    UInt16             startAddress,
                                                    UInt16             quantity,
                                                    CancellationToken  ct = default)

        => ExchangeReadAsync(unitId, ModbusPDU.BuildReadHoldingRegisters(startAddress, quantity), ct);


    public async Task WriteSingleRegisterAsync(Byte               unitId,
                                               UInt16             address,
                                               UInt16             value,
                                               CancellationToken  ct = default)
    {

        await ExchangeAndThrowOnModbusExceptionAsync(
                  unitId,
                  ModbusPDU.BuildWriteSingleRegister(address, value),
                  ct
              ).ConfigureAwait(false);

    }


    public async Task WriteMultipleRegistersAsync(Byte               unitId,
                                                  UInt16             address,
                                                  UInt16[]           values,
                                                  CancellationToken  ct = default)
    {

        await ExchangeAndThrowOnModbusExceptionAsync(
                  unitId,
                  ModbusPDU.BuildWriteMultipleRegisters(address, values),
                  ct
              ).ConfigureAwait(false);

    }


    private async Task<ushort[]> ExchangeReadAsync(Byte               unitId,
                                                   Byte[]             requestPdu,
                                                   CancellationToken  ct)
    {

        var responsePdu = await ExchangeAndThrowOnModbusExceptionAsync(unitId, requestPdu, ct).
                                  ConfigureAwait(false);

        return ModbusPDU.DecodeReadResponse(responsePdu);

    }


    private async Task<Byte[]> ExchangeAndThrowOnModbusExceptionAsync(Byte               unitId,
                                                                      Byte[]             requestPdu,
                                                                      CancellationToken  ct)
    {

        var responsePdu = await ExchangeAsync(unitId, requestPdu, ct).
                                  ConfigureAwait(false);

        if (ModbusPDU.TryGetException(responsePdu) is { } exceptionCode)
            throw new ModbusException(exceptionCode);

        return responsePdu;

    }


    /// <summary>
    /// Send <paramref name="requestPdu"/> to the downstream server using a fresh
    /// MBAP transaction id, return the response PDU. Validates that the response
    /// MBAP fields match the request (TID, UnitID, ProtocolId).
    /// </summary>
    public async Task<byte[]> ExchangeAsync(Byte                  unitId,
                                            ReadOnlyMemory<Byte>  requestPdu,
                                            CancellationToken     ct = default)
    {

        if (requestPdu.Length == 0)
            throw new ArgumentException("The Modbus request PDU must not be empty.", nameof(requestPdu));

        await exchangeLock.WaitAsync(ct).ConfigureAwait(false);

        var fcLabel    = $"0x{requestPdu.Span[0]:X2}";
        var stopwatch  = Stopwatch.StartNew();
        var outcome    = "ok";

        try
        {

            var tid    = unchecked(++txId);
            var req    = new ModbusFrame(tid, unitId, requestPdu.ToArray());
            var bytes  = req.ToBytes();

            await stream.WriteAsync(bytes, ct).ConfigureAwait(false);
            await stream.FlushAsync(ct).       ConfigureAwait(false);

            var resp = await ModbusFrame.ReadAsync(stream, ct).
                                          ConfigureAwait(false);

            // Light sanity checks - mismatch indicates a broken / misbehaving downstream.
            if (resp.TransactionId != tid)
                throw new InvalidDataException(
                    $"downstream TID mismatch: sent {tid}, got {resp.TransactionId}");

            if (resp.UnitId != unitId)
                throw new InvalidDataException(
                    $"downstream UnitID mismatch: sent {unitId}, got {resp.UnitId}");

            if (ModbusPDU.TryGetException(resp.Pdu) is not null)
                outcome = "modbus_exc";

            return resp.Pdu;

        }
        catch (OperationCanceledException)
        {
            outcome = "cancelled";
            throw;
        }
        catch
        {
            outcome = "transient";
            throw;
        }
        finally
        {
            stopwatch.Stop();
            ClientMetrics.Requests.Add(1, new TagList {
                { "fc",      fcLabel  },
                { "outcome", outcome  }
            });
            ClientMetrics.RequestDuration.Record(stopwatch.Elapsed.TotalSeconds, new TagList {
                { "fc",      fcLabel  },
                { "outcome", outcome  }
            });
            exchangeLock.Release();
        }

    }

    public ValueTask DisposeAsync()
    {

        try { tlsStream?.Dispose(); } catch { }
        try { stream.   Dispose(); } catch { }
        try { tcpClient.Close();   } catch { }

        tcpClient.Dispose();
        exchangeLock.Dispose();

        return ValueTask.CompletedTask;

    }

}
