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

using org.GraphDefined.Vanaheimr.Hermod.Quic.Core.Buffers;

#endregion

namespace org.GraphDefined.Vanaheimr.Hermod.Quic.Frames;

/// <summary>
/// ACK_FREQUENCY frame (type 0xaf, draft-ietf-quic-ack-frequency §4): the data sender tells the
/// receiver how to delay its acknowledgments. Ack-eliciting and congestion controlled.
/// </summary>
/// <param name="SequenceNumber">Monotonically increasing; the receiver ignores any frame whose
/// sequence number is not larger than the largest it has processed.</param>
/// <param name="AckElicitingThreshold">Maximum ack-eliciting packets the receiver may take in
/// without acknowledging (0 = acknowledge every packet; 1 = the RFC 9000 §13.2.2 default).</param>
/// <param name="RequestedMaxAckDelayUs">The value the receiver should adopt as its max_ack_delay,
/// in MICROSECONDS (the max_ack_delay transport parameter is in milliseconds).</param>
/// <param name="ReorderingThreshold">Minimum packet reordering that triggers an immediate ACK
/// (§6.2); 0 = out-of-order packets never do, 1 = the RFC 9000 §13.2 default.</param>
public sealed record AckFrequencyFrame(
    ulong SequenceNumber,
    ulong AckElicitingThreshold,
    ulong RequestedMaxAckDelayUs,
    ulong ReorderingThreshold) : Frame
{
    public override void Write(ref BufferWriter writer)
    {
        writer.WriteVarInt(FrameType.AckFrequency);
        writer.WriteVarInt(SequenceNumber);
        writer.WriteVarInt(AckElicitingThreshold);
        writer.WriteVarInt(RequestedMaxAckDelayUs);
        writer.WriteVarInt(ReorderingThreshold);
    }

    public static bool TryReadBody(ref BufferReader reader, out AckFrequencyFrame? frame)
    {
        frame = null;
        if (!reader.TryReadVarInt(out ulong sequenceNumber) ||
            !reader.TryReadVarInt(out ulong ackElicitingThreshold) ||
            !reader.TryReadVarInt(out ulong requestedMaxAckDelayUs) ||
            !reader.TryReadVarInt(out ulong reorderingThreshold))
            return false;
        frame = new AckFrequencyFrame(sequenceNumber, ackElicitingThreshold, requestedMaxAckDelayUs, reorderingThreshold);
        return true;
    }
}

/// <summary>
/// IMMEDIATE_ACK frame (type 0x1f, draft-ietf-quic-ack-frequency §5): carries no fields and asks the
/// receiver to send an ACK at once. Ack-eliciting; not retransmitted when lost (§5).
/// </summary>
public sealed record ImmediateAckFrame : Frame
{
    public static readonly ImmediateAckFrame Instance = new();
    public override void Write(ref BufferWriter writer) => writer.WriteVarInt(FrameType.ImmediateAck);
}
