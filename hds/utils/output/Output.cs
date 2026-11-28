using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace hds
{
	public class Output{

		private const string HexChars = "0123456789abcdef";
		private const int MaxQueuedLogEntries = 16_384;
		private const int FileRetryAttempts = 8;
		private const int FileRetryDelayMs = 10;

		private readonly struct QueuedLogEntry
		{
			public readonly string FullPath;
			public readonly string Text;
			public readonly long Sequence;

			public QueuedLogEntry(string fullPath, string text, long sequence)
			{
				FullPath = fullPath;
				Text = text;
				Sequence = sequence;
			}
		}

		private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);
		private static readonly object LogQueueLock = new object();
		private static readonly Queue<QueuedLogEntry> LogQueue = new Queue<QueuedLogEntry>(256);
		private static Thread LogWriterThread;
		private static long NextLogSequence;
		private static long FlushedLogSequence;
		private static bool ProcessExitRegistered;

		static int verbose = 0;
		// Environment-variable toggles to avoid per-packet disk I/O in production.
		private static readonly bool EnablePacketLogs = GetEnvFlag("MXO_PACKET_LOG", false);
		public static bool PacketLogsEnabled => EnablePacketLogs;
		private static readonly bool EnableDebugLogs = GetEnvFlag("MXO_DEBUG_LOG", false);
		private static readonly bool EnableRpcLogs = GetEnvFlag("MXO_RPC_LOG", false);
		private static readonly bool EnableClientViewRequestLog = GetEnvFlag("MXO_CLIENT_VIEW_LOG", false);
		private static readonly bool EnableServerLog = GetEnvFlag("MXO_SERVER_LOG", true);
		// Echo per-RPC debug notes ("Handle RPC Client Request Header ...", "Region Object ID ...") into
		// the player's chat. Off by default: right after a teleport the client sends dozens of object
		// requests, and the hundreds of resulting chat messages coincided with the client stopping to
		// act on server RPCs.
		public static readonly bool ChatDebugEnabled = GetEnvFlag("MXO_CHAT_DEBUG", false);

		public Output (){}

		private static bool GetEnvFlag(string name, bool defaultValue)
		{
			string value = Environment.GetEnvironmentVariable(name);
			if (string.IsNullOrEmpty(value))
			{
				return defaultValue;
			}

			switch (value.Trim().ToLowerInvariant())
			{
				case "1":
				case "true":
				case "yes":
				case "on":
					return true;
				case "0":
				case "false":
				case "no":
				case "off":
					return false;
				default:
					return defaultValue;
			}
		}

		internal static void EnqueueFileAppend(string filePath, string text)
		{
			if (string.IsNullOrEmpty(filePath) || text == null) return;
			string fullPath;
			try
			{
				fullPath = Path.GetFullPath(filePath);
			}
			catch
			{
				return;
			}

			lock (LogQueueLock)
			{
				if (LogQueue.Count >= MaxQueuedLogEntries)
				{
					// Bounded queue prevents runaway logging from exhausting server memory.
					return;
				}

				if (LogWriterThread == null)
				{
					if (!ProcessExitRegistered)
					{
						ProcessExitRegistered = true;
						AppDomain.CurrentDomain.ProcessExit += (sender, args) => FlushLogs(2000);
					}
					LogWriterThread = new Thread(RunLogWriterLoop)
					{
						IsBackground = true,
						Name = "HdsFileLogWriter"
					};
					LogWriterThread.Start();
				}

				long seq = ++NextLogSequence;
				LogQueue.Enqueue(new QueuedLogEntry(fullPath, text, seq));
				Monitor.Pulse(LogQueueLock);
			}
		}

		internal static bool FlushLogs(int timeoutMs = 5000)
		{
			long targetSeq;
			lock (LogQueueLock)
			{
				targetSeq = NextLogSequence;
				if (FlushedLogSequence >= targetSeq)
				{
					return true;
				}
				Monitor.Pulse(LogQueueLock);
				long deadline = Environment.TickCount64 + Math.Max(1, timeoutMs);
				while (FlushedLogSequence < targetSeq)
				{
					long remaining = deadline - Environment.TickCount64;
					if (remaining <= 0)
					{
						return false;
					}
					Monitor.Wait(LogQueueLock, (int)Math.Min(remaining, 250));
				}
				return true;
			}
		}

		private static void RunLogWriterLoop()
		{
			var batch = new List<QueuedLogEntry>(256);
			var grouped = new Dictionary<string, StringBuilder>(StringComparer.OrdinalIgnoreCase);

			while (true)
			{
				batch.Clear();
				lock (LogQueueLock)
				{
					while (LogQueue.Count == 0)
					{
						Monitor.Wait(LogQueueLock);
					}
					while (LogQueue.Count > 0 && batch.Count < 1024)
					{
						batch.Add(LogQueue.Dequeue());
					}
				}

				grouped.Clear();
				long maxSeq = 0;
				for (int i = 0; i < batch.Count; i++)
				{
					QueuedLogEntry entry = batch[i];
					if (entry.Sequence > maxSeq) maxSeq = entry.Sequence;
					if (!grouped.TryGetValue(entry.FullPath, out StringBuilder sb))
					{
						sb = new StringBuilder(entry.Text.Length + 64);
						grouped[entry.FullPath] = sb;
					}
					sb.Append(entry.Text);
				}

				foreach (KeyValuePair<string, StringBuilder> kv in grouped)
				{
					WriteBatchWithRetry(kv.Key, kv.Value.ToString());
				}

				lock (LogQueueLock)
				{
					if (maxSeq > FlushedLogSequence)
					{
						FlushedLogSequence = maxSeq;
					}
					Monitor.PulseAll(LogQueueLock);
				}
			}
		}

		private static void WriteBatchWithRetry(string fullPath, string payload)
		{
			for (int attempt = 0; attempt < FileRetryAttempts; attempt++)
			{
				try
				{
					using (var stream = new FileStream(fullPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
					using (var writer = new StreamWriter(stream, Utf8NoBom))
					{
						writer.Write(payload);
						writer.Flush();
					}
					return;
				}
				catch (IOException) when (attempt + 1 < FileRetryAttempts)
				{
					Thread.Sleep(FileRetryDelayMs);
				}
				catch
				{
					return;
				}
			}
		}

		static public void OptWriteLine(Object obj){
			if (verbose==1)
				Console.WriteLine(obj);
		}

		static public void WriteLine(Object obj){
				Console.WriteLine(obj);
                writeToLogForConsole(obj);
		}


		static public void WriteClientViewRequestLog(Object obj)
		{
			if (!EnableClientViewRequestLog)
			{
				return;
			}

			EnqueueFileAppend("UnknownClient03Request.txt", (obj?.ToString() ?? "") + Environment.NewLine);
		}

        static public void WriteRpcLog(Object obj)
        {
			if (!EnableRpcLogs)
			{
				return;
			}

            EnqueueFileAppend("UnknownRPC.txt", (obj?.ToString() ?? "") + Environment.NewLine);
        }


        static public void WriteDebugLog(Object obj)
        {
			if (!EnableDebugLogs)
			{
				return;
			}

            EnqueueFileAppend("DebugLog.txt", (obj?.ToString() ?? "") + Environment.NewLine);
        }


        internal static void WriteBonusDebugLog(uint characterId, ReadOnlySpan<byte> rpc)
        {
            // The queued RPC includes its one-byte length prefix. Locator kind zero is an individual modifier.
            if (!EnableDebugLogs || rpc.Length != 23 || rpc[0] != 22 || rpc[1] != 0x80 || rpc[2] != 0xbc || rpc[4] != 0)
                return;
            string operation = rpc[18] switch { 0 => "add", 1 => "remove", 2 => "update", _ => null };
            if (operation == null) return;

            byte kind = rpc[3];
            ushort record = BinaryPrimitives.ReadUInt16LittleEndian(rpc.Slice(5));
            uint source = BinaryPrimitives.ReadUInt32LittleEndian(rpc.Slice(8));
            ushort stat = BinaryPrimitives.ReadUInt16LittleEndian(rpc.Slice(12));
            int value = BinaryPrimitives.ReadInt32LittleEndian(rpc.Slice(14));
            float duration = BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(rpc.Slice(19)));
            WriteDebugLog(FormattableString.Invariant(
                $"[MODIFIER] char={characterId} source={source} stat={stat} {operation} value={value:+0;-0;0} duration={duration:G}s record={record} kind=0x{kind:x2}"));
        }

		static public string ConvertByteToReadablePacket(byte[] packet)
		{
			if (packet == null || packet.Length == 0)
			{
				return "\r\n";
			}

			int len = packet.Length;
			int rows = (len + 31) / 32;
			var sb = new StringBuilder(len * 3 + rows * 2 + 2);
			for (int offset = 0; offset < len; offset += 32)
			{
				int end = Math.Min(offset + 32, len);
				for (int i = offset; i < end; i++)
				{
					byte b = packet[i];
					sb.Append(HexChars[b >> 4]);
					sb.Append(HexChars[b & 0xF]);
					sb.Append(' ');
				}
				sb.Append("\r\n");
			}
			sb.Append("\r\n");
			return sb.ToString();
		}

	    static public void WritePacketLog(WorldClient client, byte[] obj, string type, string pss, string cseq, string sseq,
	        string timeConsuming, string cryptType)
	    {
		    if (!EnablePacketLogs)
		    {
			    return;
		    }

		    string charName = null;
		    UInt32 charId = 0;

		    if (client != null)
		    {
			    charId = client.playerData.getCharID();
			    byte[] rawName = client.playerInstance.CharacterName.getValue();
			    if (rawName.Length > 0)
			    {
				    charName = StringUtils.charBytesToString_NZ(rawName);
			    }
		    }

		    string messageConverted = ConvertByteToReadablePacket(obj);
		    string header = "";
	        switch (type)
	        {
	            case "CLIENT":
	                header = "Client->Server";
	                break;

	            case "SERVER":
	                header = "Server -> Client";
	                break;
	        }

	        DateTime now = DateTime.Now;
	        var sb = new StringBuilder(messageConverted.Length + 128);
	        if (type == "CLIENT" || type == "SERVER")
	        {
	            sb.Append('[').Append(header).Append(' ').Append(now.ToLongTimeString()).Append(' ').Append(now.ToLongDateString())
	              .Append(" PSS :").Append(pss).Append(" CSEQ: ").Append(cseq).Append(" SSEQ: ").Append(sseq).AppendLine(" ]");
	            if (charId > 0)
	            {
	                sb.Append("[Receiver |CharId: ").Append(charId).Append("| Handle: ").Append(charName).AppendLine(" ]");
	            }
	        }

	        if (type == "MARGINSERVER" || type == "MARGINCLIENT")
	        {
	            sb.Append(header).Append('[').Append(now.ToLongTimeString()).Append(' ').Append(now.ToLongDateString()).AppendLine(" ]");
	        }

	        sb.Append(messageConverted);
	        sb.AppendLine();
	        EnqueueFileAppend("PacketLog.txt", sb.ToString());
	    }



		static public void WriteUnencryptedPacketLog(byte[] obj, string type)
		{
			if (!EnablePacketLogs)
			{
				return;
			}
			string messageConverted = ConvertByteToReadablePacket(obj);
			string header = "";

			switch (type)
			{
				case "CLIENT":
					header = "Client->Server";
					break;

				case "SERVER":
					header = "Server -> Client";
					break;

				case "MARGINSERVER":
					header = "MarginServer -> MarginClient";
					break;

				case "MARGINCLIENT":
					header = "MarginClient -> MarginServer";
					break;
			}

			DateTime now = DateTime.Now;
			var sb = new StringBuilder(messageConverted.Length + 96);
			if (type == "CLIENT" || type == "SERVER")
			{
				sb.Append(header).Append('[').Append(now.ToLongTimeString()).Append(' ').Append(now.ToLongDateString()).AppendLine(" UNENCRYPTED PACKET]");
			}

			if (type == "MARGINSERVER" || type == "MARGINCLIENT")
			{
				sb.Append(header).Append('[').Append(now.ToLongTimeString()).Append(' ').Append(now.ToLongDateString()).AppendLine(" ]");
			}

			sb.Append(messageConverted);
			sb.AppendLine();
			EnqueueFileAppend("PacketLog.txt", sb.ToString());
		}


        static public void WritePacketLog(byte[] obj, string type, string pss, string cseq, string sseq){
	        if (!EnablePacketLogs)
	        {
		        return;
	        }

	        // Format Packet Data
	        string messageConverted = ConvertByteToReadablePacket(obj);
            string header = "";

            switch (type)
            {
                case "CLIENT":
                    header = "Client->Server";
                    break;

                case "SERVER":
                    header = "Server -> Client";
                    break;

                case "MARGINSERVER":
                    header = "MarginServer -> MarginClient";
                    break;

                case "MARGINCLIENT":
                    header = "MarginClient -> MarginServer";
                    break;
            }

            DateTime now = DateTime.Now;
            var sb = new StringBuilder(messageConverted.Length + 96);
            if (type == "CLIENT" || type == "SERVER")
            {
                sb.Append(header).Append('[').Append(now.ToLongTimeString()).Append(' ').Append(now.ToLongDateString())
                  .Append(" PSS :").Append(pss).Append(" CSEQ: ").Append(cseq).Append(" SSEQ: ").Append(sseq).AppendLine("]");
            }

            if (type == "MARGINSERVER" || type == "MARGINCLIENT")
            {
                sb.Append(header).Append('[').Append(now.ToLongTimeString()).Append(' ').Append(now.ToLongDateString()).AppendLine(" ]");
            }

            sb.Append(messageConverted);
            sb.AppendLine();
            EnqueueFileAppend("PacketLog.txt", sb.ToString());
        }


		static public void Write(Object obj){
				Console.Write(obj);
		}

        static public void writeToLogForConsole(Object obj)
        {
			if (!EnableServerLog)
			{
				return;
			}
            DateTime now = DateTime.Now;
            var sb = new StringBuilder(96);
            sb.Append('[').Append(now.ToLongTimeString()).Append(' ').Append(now.ToLongDateString()).AppendLine("] ");
            sb.AppendLine(obj?.ToString() ?? "");
            sb.AppendLine();
            EnqueueFileAppend("ServerLog.txt", sb.ToString());
        }
	}
}
