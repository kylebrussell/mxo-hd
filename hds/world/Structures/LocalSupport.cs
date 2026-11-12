using System;
using System.Collections.Generic;
using System.Linq;

namespace hds
{
    /// <summary>Local support policy; no SOE or external ticket service is contacted.</summary>
    public static class LocalSupport
    {
        public sealed class Ticket
        {
            public uint Id, Character, Created, Modified;
            public string Handle, Text;
            public bool Cancelled;
        }
        public sealed class Article
        {
            public string Id, Title, Text;
        }
        public static readonly Article[] Articles = {
            new Article { Id = "local-support", Title = "Local server support", Text =
                "Petitions are saved on this server for its administrators. This is a local workflow; no SOE service is contacted. List your tickets in Help and cancel a ticket when you no longer need assistance. There is no guaranteed response time." },
            new Article { Id = "local-stuck", Title = "Stuck or defeated", Text =
                "If defeated, use Reconstruct to return to the Loading Area. Choose a known hardline to return to the city. If you cannot move, submit a petition describing the district and your /whereami position." },
            new Article { Id = "local-code", Title = "Compiling code", Text =
                "Code compilation requires the appropriate tool, ability, known recipe, ingredients, information, and room in your inventory. Upload Code Bits and fragments at a hardline to use code storage." }
        };
        public static IEnumerable<Article> Search(string query) => Articles.Where(a => string.IsNullOrWhiteSpace(query) ||
            (a.Title + " " + a.Text).IndexOf(query.Trim(), StringComparison.OrdinalIgnoreCase) >= 0);

        public static byte[] Acknowledge(uint result, uint ticket, bool cancelled)
        {
            var b = ClientActionRequests.Packet(0xb1, 11);
            ClientActionRequests.U32(b, 2, result); ClientActionRequests.U32(b, 6, ticket); b[10] = cancelled ? (byte)1 : (byte)0;
            return b;
        }
        // 62147f40 / 62147790: embedded record starts at raw2; its string offset is relative to raw2.
        public static byte[] Header(Ticket ticket, bool more)
        {
            var b = ClientActionRequests.Packet(0xb3, 30); b[29] = more ? (byte)1 : (byte)0;
            if (ticket == null) return b; // id0 clears the cached list (621b2db0).
            ClientActionRequests.U32(b, 3, ticket.Id); ClientActionRequests.U32(b, 7, ticket.Created);
            ClientActionRequests.U32(b, 11, ticket.Modified); ClientActionRequests.U32(b, 15, ticket.Cancelled ? 5u : 0u);
            ClientActionRequests.U32(b, 19, 1); ClientActionRequests.U32(b, 23, 2);
            return ClientActionRequests.String(b, 27, ticket.Handle, 2);
        }
        public static byte[] Detail(Ticket ticket, uint requested)
        {
            var b = ClientActionRequests.Packet(0xb5, 17); ClientActionRequests.U32(b, 2, requested);
            ClientActionRequests.U32(b, 6, ticket == null ? 1u : 0u);
            return ClientActionRequests.String(b, 10, ticket?.Text ?? "");
        }
        public static byte[] SearchResult(Article article, bool more)
        {
            var b = ClientActionRequests.Packet(0xba, 13); b[12] = more ? (byte)1 : (byte)0;
            ClientActionRequests.U16(b, 10, article == null ? (ushort)0 : (ushort)100);
            b = ClientActionRequests.String(b, 6, article?.Title ?? "");
            return ClientActionRequests.String(b, 8, article?.Id ?? "");
        }
        public static byte[] ArticleResult(Article article)
        {
            var b = ClientActionRequests.Packet(0xbc, 11); ClientActionRequests.U32(b, 2, article == null ? 1u : 0u);
            b = ClientActionRequests.String(b, 6, article?.Title ?? "Unknown local article");
            return ClientActionRequests.String(b, 8, article?.Text ?? "");
        }
        // 6212e4d0 / 621b20a0: category submessage 81ad, then array of 12-byte subcategories.
        public static byte[] Categories()
        {
            var b = ClientActionRequests.Packet(0xb8, 31);
            b[2] = 0x81; b[3] = 0xad; ClientActionRequests.U32(b, 6, 1);
            ClientActionRequests.U16(b, 14, 17); ClientActionRequests.U16(b, 17, 1);
            b[19] = 0x81; b[20] = 0xad; ClientActionRequests.U32(b, 23, 2);
            b = ClientActionRequests.String(b, 4, "Local server support", 2);
            return ClientActionRequests.String(b, 21, "General assistance", 19);
        }
    }
}
