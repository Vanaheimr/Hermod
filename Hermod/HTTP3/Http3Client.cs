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

#region Usings

using System.Net;
using System.Net.Sockets;

using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Tls.Handshake;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.HTTP3;

/// <summary>
/// An HTTP/3 request has failed for good (cancelled, rejected, malformed or the connection was
/// closed). <see cref="IsRetryable"/> indicates whether a repetition on a new connection is safe
/// (RFC 9114 §5.2: requests rejected via GOAWAY).
/// </summary>
public sealed class Http3RequestException(string message, bool isRetryable = false) : Exception(message)
{
    /// <summary>
    /// The request was provably not processed and may be repeated safely.
    /// </summary>
    public bool IsRetryable { get; } = isRetryable;
}

/// <summary>
/// Task-based facade over <see cref="Http3ClientConnection"/>: owns the UDP socket, runs a
/// background pump (receiving, timers, sending) and maps requests onto <c>await</c>-able tasks.
/// The deterministic, transport-agnostic core remains untouched — this class only adds the socket,
/// concurrency (all core accesses strictly serialised) and the asynchronous API.
/// For advanced features (datagrams, WebTransport, Extended CONNECT) there are
/// <see cref="PerformAsync"/>/<see cref="QueryAsync"/>/<see cref="WaitUntilAsync"/>.
/// </summary>
public sealed class Http3Client : IAsyncDisposable
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(20);

    private readonly string _host;
    private readonly int _port;
    private readonly TimeProvider _timeProvider;
    private readonly Http3ClientConnection _connection;
    private readonly SemaphoreSlim _mutex = new(1, 1);
    private readonly CancellationTokenSource _cts = new();
    private readonly Dictionary<ulong, TaskCompletionSource<Http3Response>> _pending = [];
    private readonly UdpBatchSender _sender = new();

    private UdpClient? _udp;
    private Task? _pumpTask;
    private bool _disposed;

    public Http3Client(string host,
                       int port = 443,
                       CertificateValidationOptions? certificateValidation = null,
                       bool enableDatagrams = false,
                       ulong webTransportMaxSessions = 0,
                       TimeProvider? timeProvider = null,
                       KeyLog? keyLog = null,
                       ServerCertificate? clientCertificate = null)
    {
        _host = host;
        _port = port;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _connection = new Http3ClientConnection(host,
            certificateValidation: certificateValidation,
            enableDatagrams: enableDatagrams,
            webTransportMaxSessions: webTransportMaxSessions,
            timeProvider: _timeProvider,
            keyLog: keyLog,
            clientCertificate: clientCertificate);
    }

    /// <summary>
    /// The underlying connection. After <see cref="ConnectAsync"/> the background pump is running —
    /// do NOT access it directly anymore, but via <see cref="PerformAsync"/>/<see cref="QueryAsync"/>
    /// (the pump and the API share the single-threaded core through a mutex).
    /// </summary>
    public Http3ClientConnection Connection => _connection;

    /// <summary>
    /// Establishes the QUIC/TLS connection (socket, handshake, HTTP/3 initialisation) and starts the
    /// background pump. Throws <see cref="TimeoutException"/> when the handshake is not confirmed
    /// within <paramref name="timeout"/> (default 10 s).
    /// </summary>
    public async Task ConnectAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_pumpTask is not null)
            throw new InvalidOperationException("ConnectAsync has already been called.");

        System.Net.IPAddress address = (await Dns.GetHostAddressesAsync(_host, cancellationToken).ConfigureAwait(false))
            .First(a => a.AddressFamily == AddressFamily.InterNetwork);
        _udp = new UdpClient(AddressFamily.InterNetwork);
        DisableIcmpUnreachableException(_udp);
        _udp.Connect(new IPEndPoint(address, _port));

        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _connection.Start();
            FlushLocked();
        }
        finally { _mutex.Release(); }

        _pumpTask = Task.Run(PumpLoopAsync, CancellationToken.None);

        if (!await WaitUntilAsync(c => c.HandshakeConfirmed, timeout ?? TimeSpan.FromSeconds(10), cancellationToken).ConfigureAwait(false))
            throw new TimeoutException($"QUIC/TLS handshake with {_host}:{_port} not completed.");
        await PerformAsync(c => c.InitializeHttp3(), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Windows reports an ICMP "port unreachable" as a SocketException on the UDP socket — disable
    /// that for a client socket so the pump does not fail on a (still) dead server port.
    /// </summary>
    private static void DisableIcmpUnreachableException(UdpClient udp)
    {
        if (!OperatingSystem.IsWindows())
            return;
        const int SIO_UDP_CONNRESET = unchecked((int)0x9800000C);
        udp.Client.IOControl(SIO_UDP_CONNRESET, [0, 0, 0, 0], null);
    }

    /// <summary>
    /// Sends a request and returns the complete response. On cancellation via
    /// <paramref name="cancellationToken"/> the request is annulled via RESET_STREAM/STOP_SENDING
    /// (RFC 9114 §4.1.1); final failures throw <see cref="Http3RequestException"/>.
    /// </summary>
    public async Task<Http3Response> SendAsync(Http3Request request, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var tcs = new TaskCompletionSource<Http3Response>(TaskCreationOptions.RunContinuationsAsynchronously);
        ulong streamId;

        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            streamId = _connection.SendRequest(request);
            _pending[streamId] = tcs;
            FlushLocked();
        }
        finally { _mutex.Release(); }

        await using CancellationTokenRegistration registration =
            cancellationToken.Register(() => _ = CancelRequestAsync(streamId, tcs));
        return await tcs.Task.ConfigureAwait(false);
    }

    /// <summary>
    /// Convenient GET request.
    /// </summary>
    public Task<Http3Response> GetAsync(string path = "/", CancellationToken cancellationToken = default)
        => SendAsync(Http3Request.Get(_host, path), cancellationToken);

    /// <summary>
    /// Convenient POST request with a body.
    /// </summary>
    public Task<Http3Response> PostAsync(string path, byte[] body,
                                         string contentType = "application/octet-stream",
                                         CancellationToken cancellationToken = default)
        => SendAsync(Http3Request.Post(_host, path, body, contentType), cancellationToken);

    /// <summary>
    /// Executes an action serialised on the connection (e.g. sending a datagram, opening
    /// WebTransport) and flushes pending QUIC datagrams afterwards.
    /// </summary>
    public async Task PerformAsync(Action<Http3ClientConnection> action, CancellationToken cancellationToken = default)
    {
        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            action(_connection);
            FlushLocked();
        }
        finally { _mutex.Release(); }
    }

    /// <summary>
    /// Reads a value from the connection, serialised.
    /// </summary>
    public async Task<T> QueryAsync<T>(Func<Http3ClientConnection, T> query, CancellationToken cancellationToken = default)
    {
        await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { return query(_connection); }
        finally { _mutex.Release(); }
    }

    /// <summary>
    /// Waits (polling, the pump keeps working meanwhile) until the condition holds;
    /// <c>false</c> when <paramref name="timeout"/> expires.
    /// </summary>
    public async Task<bool> WaitUntilAsync(Func<Http3ClientConnection, bool> condition, TimeSpan timeout,
                                           CancellationToken cancellationToken = default)
    {
        long start = _timeProvider.GetTimestamp();
        while (_timeProvider.GetElapsedTime(start) < timeout)
        {
            if (await QueryAsync(condition, cancellationToken).ConfigureAwait(false))
                return true;
            await Task.Delay(TimeSpan.FromMilliseconds(10), _timeProvider, cancellationToken).ConfigureAwait(false);
        }
        return false;
    }

    /// <summary>
    /// Closes the connection properly (CONNECTION_CLOSE with H3_NO_ERROR) and gives the peer a
    /// moment to receive the close packet.
    /// </summary>
    public async Task CloseAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed || _pumpTask is null)
            return;
        await PerformAsync(c => c.CloseGracefully(), cancellationToken).ConfigureAwait(false);
        await Task.Delay(TimeSpan.FromMilliseconds(50), _timeProvider, cancellationToken).ConfigureAwait(false); // still deliver the close packet
    }

    // ---- Pump -----------------------------------------------------------------------------

    /// <summary>
    /// Upper bound on datagrams processed under one lock hold. Keeps a burst from starving the API
    /// calls that share the mutex, while still amortising the async machinery over many packets.
    /// </summary>
    private const int MaxDatagramsPerBatch = 64;

    private async Task PumpLoopAsync()
    {
        Task<UdpReceiveResult>? receive = null;
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                receive ??= _udp!.ReceiveAsync(_cts.Token).AsTask();

                if (!receive.IsCompleted)
                {
                    // Wait for a datagram OR the tick — and CANCEL the timer afterwards. Leaving it
                    // to expire would abandon one timer per loop pass, i.e. thousands during a
                    // download.
                    using var tick = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
                    await Task.WhenAny(receive, Task.Delay(TickInterval, _timeProvider, tick.Token)).ConfigureAwait(false);
                    tick.Cancel();
                }

                UdpReceiveResult? first = null;
                if (receive.IsCompletedSuccessfully)
                {
                    first = receive.Result;
                    receive = null;
                }

                await _mutex.WaitAsync(_cts.Token).ConfigureAwait(false);
                try
                {
                    if (first is { } result)
                    {
                        _connection.ProcessDatagram(result.Buffer);

                        // Drain whatever else is already queued on the socket in the SAME lock hold.
                        // One datagram per await cycle would cost a full async round trip plus a
                        // lock acquisition per packet — the dominant cost during a download.
                        // No async receive is outstanding here, so the synchronous read is safe.
                        for (int batched = 1; batched < MaxDatagramsPerBatch && _udp!.Available > 0; batched++)
                        {
                            IPEndPoint? from = null;
                            _connection.ProcessDatagram(_udp.Receive(ref from));
                        }
                    }
                    _connection.CheckTimeouts();
                    FlushLocked();
                    CompleteFinishedRequestsLocked();
                }
                finally { _mutex.Release(); }
            }
            catch (OperationCanceledException) { break; }
            catch (ObjectDisposedException) { break; }
            catch (SocketException) { receive = null; } // e.g. ICMP on non-Windows — keep pumping
        }
    }

    /// <summary>
    /// Sends CONNECTION_CLOSE before the socket goes away (RFC 9000 §10.2, RFC 9114 §5.2 with
    /// H3_NO_ERROR). Without it the client just vanishes: the server keeps the connection, its
    /// streams and its flow-control state until the idle timeout — 30 s by default — and its loss
    /// recovery keeps retransmitting at a port nobody is listening on. That storm of undeliverable
    /// packets is what starved the server before the ICMP guard was added; the guard makes the
    /// server survive rude peers, this makes us not be one.
    /// <para>
    /// Strictly best-effort and bounded: a dispose must not hang because a socket is already dead or
    /// the pump is wedged. Whatever fails here is exactly the situation the peer's idle timeout
    /// exists for.
    /// </para>
    /// </summary>
    private async Task SayGoodbyeAsync()
    {
        try
        {
            if (!await _mutex.WaitAsync(TimeSpan.FromMilliseconds(250)).ConfigureAwait(false))
                return; // the pump is busy — leave it to the peer's idle timeout
            try
            {
                if (_udp is null || _connection.IsClosing)
                    return;
                _connection.CloseGracefully();
                FlushLocked(); // the CONNECTION_CLOSE packet itself
            }
            finally { _mutex.Release(); }
        }
        catch (ObjectDisposedException) { }
        catch (SocketException) { }
    }

    private void FlushLocked()
    {
        if (_udp is null)
            return;
        // Connected socket (remote = null) ⇒ Send; on Linux bundled via GSO (RFC-neutral, syscalls only).
        _sender.Send(_udp.Client, _connection.GetDatagramsToSend(), remote: null);
    }

    /// <summary>
    /// Completes the tasks of all requests whose outcome is settled (response present or failed for
    /// good); with a closed connection, all outstanding requests fail.
    /// </summary>
    private void CompleteFinishedRequestsLocked()
    {
        if (_pending.Count == 0)
            return;
        foreach ((ulong id, TaskCompletionSource<Http3Response> tcs) in _pending.ToArray())
        {
            if (_connection.TryGetResponse(id, out Http3Response? response))
                tcs.TrySetResult(response!);
            else if (_connection.IsRequestRejected(id))
                tcs.TrySetException(new Http3RequestException(
                    "The request was rejected via GOAWAY (RFC 9114 §5.2) — repeatable on a new connection.", isRetryable: true));
            else if (_connection.IsResponseMalformed(id))
                tcs.TrySetException(new Http3RequestException("The response was malformed and was discarded (RFC 9114 §4.1.2)."));
            else if (_connection.IsResponseTooLarge(id))
                tcs.TrySetException(new Http3RequestException("The response headers exceed our MAX_FIELD_SECTION_SIZE (RFC 9114 §4.2.2)."));
            else if (_connection.IsRequestCancelled(id))
                tcs.TrySetException(new Http3RequestException("The request was cancelled (RFC 9114 §4.1.1)."));
            else
                continue;
            _pending.Remove(id);
        }

        if (_connection.IsClosing || _connection.IsDraining || _connection.IsIdleTimedOut)
        {
            foreach (TaskCompletionSource<Http3Response> tcs in _pending.Values)
                tcs.TrySetException(new Http3RequestException("The connection was closed."));
            _pending.Clear();
        }
    }

    private async Task CancelRequestAsync(ulong streamId, TaskCompletionSource<Http3Response> tcs)
    {
        try
        {
            await _mutex.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_pending.Remove(streamId))
                {
                    _connection.CancelRequest(streamId); // §4.1.1: RESET_STREAM + STOP_SENDING
                    FlushLocked();
                }
            }
            finally { _mutex.Release(); }
        }
        catch (ObjectDisposedException) { }
        tcs.TrySetCanceled();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        _disposed = true;

        await SayGoodbyeAsync().ConfigureAwait(false);

        _cts.Cancel();
        if (_pumpTask is { } pump)
            try { await pump.ConfigureAwait(false); } catch { /* pump shutdown */ }
        foreach (TaskCompletionSource<Http3Response> tcs in _pending.Values)
            tcs.TrySetException(new Http3RequestException("The client was closed."));
        _pending.Clear();
        _udp?.Dispose();
        _connection.Dispose();
        _cts.Dispose();
        _mutex.Dispose();
    }
}
