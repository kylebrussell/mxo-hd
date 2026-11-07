using System;

using hds.shared;

namespace hds{

	public class Protocol03Manager
	{
		/// <summary>
		/// Parses a client protocol 03 section: a list of <c>[view id u16 LE][view data]</c> entries ended
		/// by view id 0. Only the player's own view (2) is understood. Returns the offset just past the
		/// section, where a protocol 04 RPC section may follow in the same packet. When the section can't be
		/// walked (unknown view or field layout) it returns the packet length, so nothing after it is misread.
		/// </summary>
		public int parse(int _offset, ref byte[] packetData)
		{
			return ParseSection(packetData, _offset, ParseOwnView);
		}

		private static int ParseOwnView(byte[] packetData, int offset)
		{
			return Store.currentClient.playerInstance.parseAutoView(ref packetData, offset);
		}

		/// <summary>
		/// Walks the view list. <paramref name="parseOwnView"/> gets the offset of view 2's sub-view byte
		/// and returns the offset after its data (or the packet length if it couldn't tell).
		/// </summary>
		public static int ParseSection(byte[] packetData, int offset, Func<byte[], int, int> parseOwnView)
		{
			while (offset + 2 <= packetData.Length)
			{
				UInt16 viewId = (UInt16)(packetData[offset] | (packetData[offset + 1] << 8));
				offset += 2;

				if (viewId == 0)
				{
					return offset;
				}

				if (viewId != 2)
				{
					Output.WriteClientViewRequestLog("[03] Unknown client view " + viewId + ", skipping rest of packet: " +
					                                 StringUtils.bytesToString_NS(packetData));
					return packetData.Length;
				}

				offset = parseOwnView(packetData, offset);
			}

			return packetData.Length;
		}
	}
}
