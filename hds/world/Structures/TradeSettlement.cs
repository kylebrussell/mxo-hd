using System.Collections.Generic;

namespace hds
{
    /// <summary>Committed inventory and wallet updates for the two sides of a trade.</summary>
    public sealed class TradeSettlement
    {
        public sealed class Receipt
        {
            public uint Balance { get; }
            public long InfoChange { get; }
            public IReadOnlyList<byte[]> BagRecords { get; }

            internal Receipt(uint balance, long infoChange, IReadOnlyList<byte[]> bagRecords)
            {
                Balance = balance;
                InfoChange = infoChange;
                BagRecords = bagRecords;
            }
        }

        public Receipt Requester { get; }
        public Receipt Target { get; }
        public string Failure { get; }

        internal TradeSettlement(Receipt requester, Receipt target, string failure = null)
        {
            Requester = requester;
            Target = target;
            Failure = failure;
        }

        internal static TradeSettlement Denied(string reason) => new TradeSettlement(null, null, reason);
    }
}
