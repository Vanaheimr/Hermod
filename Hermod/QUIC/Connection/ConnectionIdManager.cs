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

using System.Security.Cryptography;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Frames;
using org.GraphDefined.Vanaheimr.Hermod.Quic.Packets;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Quic.Connection;

/// <summary>
/// Manages the connection IDs of a connection (RFC 9000 §5.1): the set of <b>locally</b> issued IDs
/// (which the peer uses as destination connection ID) and the set of <b>remote</b> IDs offered by
/// the peer via NEW_CONNECTION_ID (which we use as DCID). Every ID carries a sequence number;
/// number 0 is the one from the handshake. Produces the NEW_/RETIRE_CONNECTION_ID frames to send and
/// determines the new DCID on "Retire Prior To" or on a rotation.
/// </summary>
public sealed class ConnectionIdManager
{
    private sealed class Entry(ulong seq, ConnectionId cid, byte[] token)
    {
        public ulong Seq { get; } = seq;
        public ConnectionId Cid { get; } = cid;
        public byte[] Token { get; set; } = token; // 16-byte stateless-reset token (empty = none known)
    }

    private readonly List<Entry> _local = [];   // issued by us (peer → DCID)
    private readonly List<Entry> _remote = [];  // issued by the peer (we → DCID)
    private ulong _nextLocalSeq = 1;
    private ulong _currentRemoteSeq;

    /// <summary>
    /// Creates the manager with the local handshake CID (sequence 0).
    /// </summary>
    public ConnectionIdManager(ConnectionId localSeq0) => _local.Add(new Entry(0, localSeq0, []));

    /// <summary>
    /// Registers the remote handshake CID (sequence 0) once it is fixed. One-time.
    /// </summary>
    public void InitializeRemote(ConnectionId remoteSeq0)
    {
        if (_remote.Count == 0)
        {
            _remote.Add(new Entry(0, remoteSeq0, []));
            _currentRemoteSeq = 0;
        }
    }

    /// <summary>
    /// Number of active connection IDs issued by us (incl. sequence 0).
    /// </summary>
    public int LocalCount => _local.Count;

    /// <summary>
    /// Number of known connection IDs issued by the peer.
    /// </summary>
    public int RemoteCount => _remote.Count;

    /// <summary>
    /// Sequence number of the remote connection ID currently used as DCID.
    /// </summary>
    public ulong CurrentRemoteSequence => _currentRemoteSeq;

    /// <summary>
    /// <c>true</c> when <paramref name="cid"/> is one of our active local connection IDs.
    /// </summary>
    public bool IsLocalConnectionId(ConnectionId cid) => _local.Exists(e => e.Cid == cid);

    /// <summary>
    /// Sets the stateless-reset token of the remote handshake CID (from the <c>stateless_reset_token</c> TP).
    /// </summary>
    public void SetInitialRemoteToken(byte[] token)
    {
        Entry? seq0 = _remote.Find(e => e.Seq == 0);
        if (seq0 is not null && token.Length == StatelessReset.TokenLength)
            seq0.Token = token;
    }

    /// <summary>
    /// Checks in constant time whether <paramref name="candidate"/> matches one of the peer's known
    /// stateless-reset tokens (from NEW_CONNECTION_ID or the transport parameter). For detecting a
    /// stateless reset (RFC 9000 §10.3.1).
    /// </summary>
    public bool MatchesRemoteStatelessResetToken(ReadOnlySpan<byte> candidate)
    {
        bool match = false;
        foreach (Entry e in _remote)
            if (e.Token.Length == StatelessReset.TokenLength &&
                CryptographicOperations.FixedTimeEquals(e.Token, candidate))
                match = true; // no early exit, so the runtime does not depend on the token
        return match;
    }

    /// <summary>
    /// Issues a new local connection ID provided the limit announced by the peer
    /// (<paramref name="activeLimit"/>, incl. sequence 0) permits it. Returns the frame to send or <c>null</c>.
    /// </summary>
    public NewConnectionIdFrame? Issue(ConnectionId newCid, byte[] token, ulong activeLimit)
    {
        if ((ulong)_local.Count >= activeLimit)
            return null;
        ulong seq = _nextLocalSeq++;
        _local.Add(new Entry(seq, newCid, token));
        return new NewConnectionIdFrame(seq, RetirePriorTo: 0, newCid.ToArray(), token);
    }

    /// <summary>
    /// The peer retires one of our local connection IDs (RETIRE_CONNECTION_ID).
    /// </summary>
    public void RetireLocal(ulong sequenceNumber) => _local.RemoveAll(e => e.Seq == sequenceNumber);

    /// <summary>
    /// Processes a peer NEW_CONNECTION_ID: takes in the new remote ID and, on "Retire Prior To",
    /// retires all lower-numbered ones (returns the RETIRE frames to send). If the current DCID is
    /// retired in the process, we switch to the lowest remaining one
    /// (<paramref name="newDcid"/> set, <paramref name="dcidChanged"/> = <c>true</c>).
    /// </summary>
    public List<RetireConnectionIdFrame> OnNewConnectionId(NewConnectionIdFrame frame, out bool dcidChanged, out ConnectionId newDcid)
    {
        dcidChanged = false;
        newDcid = default;

        if (!_remote.Exists(e => e.Seq == frame.SequenceNumber))
            _remote.Add(new Entry(frame.SequenceNumber, new ConnectionId(frame.ConnectionId.Span), frame.StatelessResetToken.ToArray()));

        var retires = new List<RetireConnectionIdFrame>();
        if (frame.RetirePriorTo == 0)
            return retires;

        foreach (Entry e in _remote.FindAll(e => e.Seq < frame.RetirePriorTo))
        {
            _remote.Remove(e);
            retires.Add(new RetireConnectionIdFrame(e.Seq));
        }
        if (!_remote.Exists(e => e.Seq == _currentRemoteSeq) && LowestRemote() is { } next)
        {
            _currentRemoteSeq = next.Seq;
            newDcid = next.Cid;
            dcidChanged = true;
        }
        return retires;
    }

    /// <summary>
    /// Actively switches the DCID to an unused, higher-numbered remote connection ID and retires the
    /// previous one. Returns (RETIRE frame for the old one, new DCID) or <c>null</c> when no further
    /// ID is available.
    /// </summary>
    public (RetireConnectionIdFrame Retire, ConnectionId NewDcid)? Rotate()
    {
        Entry? next = null;
        foreach (Entry e in _remote)
            if (e.Seq > _currentRemoteSeq && (next is null || e.Seq < next.Seq))
                next = e;
        if (next is null)
            return null;

        ulong oldSeq = _currentRemoteSeq;
        _remote.RemoveAll(e => e.Seq == oldSeq);
        _currentRemoteSeq = next.Seq;
        return (new RetireConnectionIdFrame(oldSeq), next.Cid);
    }

    private Entry? LowestRemote()
    {
        Entry? lowest = null;
        foreach (Entry e in _remote)
            if (lowest is null || e.Seq < lowest.Seq)
                lowest = e;
        return lowest;
    }
}
