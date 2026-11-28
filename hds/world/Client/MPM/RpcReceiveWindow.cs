using System;
using System.Collections.Generic;

namespace hds
{
    /// <summary>
    /// Spots client RPCs resent because the client saw no ack in time: without this one click on Buy was charged twice.
    /// A block carries the sequence number of its first RPC (<c>04 01 &lt;seq u16 big-endian&gt; &lt;count&gt;</c>), the next RPC has
    /// the next number. A number alone is not enough: the client restarts its numbering on a world reload (retail logs go
    /// from 502 back to 1, and 2,453 of 2,701 repeated numbers in the captures carry different RPCs), so a resend is the
    /// same number with the same bytes. The reset flag clears the window as well.
    /// </summary>
    public class RpcReceiveWindow
    {
        /// <summary>RPCs remembered; far more than can be waiting for an ack at once.</summary>
        public const int Capacity = 2048;

        private readonly struct Entry
        {
            public readonly int Header;
            public readonly byte[] Values;
            public readonly ulong Ticket;

            public Entry(int header, byte[] values, ulong ticket)
            {
                Header = header;
                Values = values;
                Ticket = ticket;
            }
        }

        private readonly Dictionary<UInt16, Entry> seen = new Dictionary<UInt16, Entry>();
        private readonly Queue<(UInt16 Sequence, ulong Ticket)> order = new Queue<(UInt16 Sequence, ulong Ticket)>();
        private ulong nextTicket;

        /// <summary>True when this RPC was already handled; otherwise remembers it.</summary>
        public bool IsResend(UInt16 sequence, int header, byte[] values)
        {
            ReadOnlySpan<byte> payload = values != null ? values.AsSpan() : ReadOnlySpan<byte>.Empty;

            lock (seen)
            {
                if (seen.TryGetValue(sequence, out Entry earlier))
                {
                    if (earlier.Header == header && earlier.Values.AsSpan().SequenceEqual(payload))
                    {
                        return true;
                    }

                    // The numbering restarted: replace with a fresh ticket so stale queue entries
                    // do not prematurely evict this newly accepted RPC.
                    ulong replacedTicket = ++nextTicket;
                    seen[sequence] = new Entry(header, ClonePayload(payload), replacedTicket);
                    order.Enqueue((sequence, replacedTicket));
                    TrimExpiredOrderEntriesLocked();
                    return false;
                }

                ulong ticket = ++nextTicket;
                seen[sequence] = new Entry(header, ClonePayload(payload), ticket);
                order.Enqueue((sequence, ticket));

                while (seen.Count > Capacity && order.Count > 0)
                {
                    var oldest = order.Dequeue();
                    if (seen.TryGetValue(oldest.Sequence, out Entry current) && current.Ticket == oldest.Ticket)
                    {
                        seen.Remove(oldest.Sequence);
                    }
                }
                TrimExpiredOrderEntriesLocked();
                return false;
            }
        }

        private void TrimExpiredOrderEntriesLocked()
        {
            while (order.Count > Capacity * 2)
            {
                var head = order.Peek();
                if (seen.TryGetValue(head.Sequence, out Entry current) && current.Ticket == head.Ticket)
                {
                    break;
                }
                order.Dequeue();
            }
        }

        private static byte[] ClonePayload(ReadOnlySpan<byte> payload)
        {
            if (payload.Length == 0)
            {
                return Array.Empty<byte>();
            }
            byte[] copy = new byte[payload.Length];
            payload.CopyTo(copy);
            return copy;
        }

        public void Reset()
        {
            lock (seen)
            {
                seen.Clear();
                order.Clear();
                nextTicket = 0;
            }
        }
    }
}
