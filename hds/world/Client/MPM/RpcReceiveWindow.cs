using System;
using System.Collections.Generic;
using System.Linq;

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

        private readonly Dictionary<UInt16, byte[]> seen = new Dictionary<UInt16, byte[]>();
        private readonly Queue<UInt16> order = new Queue<UInt16>();

        /// <summary>True when this RPC was already handled; otherwise remembers it.</summary>
        public bool IsResend(UInt16 sequence, int header, byte[] values)
        {
            byte[] record = new byte[values.Length + 2];
            record[0] = (byte)(header >> 8);
            record[1] = (byte)header;
            Array.Copy(values, 0, record, 2, values.Length);

            lock (seen)
            {
                if (seen.TryGetValue(sequence, out byte[] earlier))
                {
                    if (earlier.SequenceEqual(record))
                    {
                        return true;
                    }
                    seen[sequence] = record; // the numbering restarted: this one is new
                    return false;
                }

                seen[sequence] = record;
                order.Enqueue(sequence);
                if (order.Count > Capacity)
                {
                    seen.Remove(order.Dequeue());
                }
                return false;
            }
        }

        public void Reset()
        {
            lock (seen)
            {
                seen.Clear();
                order.Clear();
            }
        }
    }
}
