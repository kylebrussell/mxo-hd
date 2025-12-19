using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Microsoft.SqlServer.Server;

namespace hds
{
	public class Output{
		
		static int verbose = 0;
		// Environment-variable toggles to avoid per-packet disk I/O in production.
		private static readonly bool EnablePacketLogs = GetEnvFlag("MXO_PACKET_LOG", false);
		private static readonly bool EnableDebugLogs = GetEnvFlag("MXO_DEBUG_LOG", false);
		private static readonly bool EnableRpcLogs = GetEnvFlag("MXO_RPC_LOG", false);
		private static readonly bool EnableClientViewRequestLog = GetEnvFlag("MXO_CLIENT_VIEW_LOG", false);
		private static readonly bool EnableServerLog = GetEnvFlag("MXO_SERVER_LOG", true);
		
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

			try
			{
				StreamWriter w = File.AppendText("UnknownClient03Request.txt");
				w.Write(obj);
				w.WriteLine();
				w.Flush();
				w.Close();
			}
			catch
			{
				// just pass
			}
		}

        static public void WriteRpcLog(Object obj)
        {
			if (!EnableRpcLogs)
			{
				return;
			}

            try
            {
                StreamWriter w = File.AppendText("UnknownRPC.txt");
                w.WriteLine(obj);
                w.Flush();
                w.Close();
            }
            catch
            {
                // just pass
            }
        }

		
        static public void WriteDebugLog(Object obj)
        {
			if (!EnableDebugLogs)
			{
				return;
			}

            try
            {
                StreamWriter w = File.AppendText("DebugLog.txt");
                w.WriteLine(obj);
                w.Flush();
                w.Close();
            }
            catch
            {
                // just pass
            }
        }

		
		static public string ConvertByteToReadablePacket(byte[] packet)
		{
			
			ArrayList hexStrings = new ArrayList();
			ArrayList readablePacketStrings = new ArrayList();
			
			PacketReader reader = new PacketReader(packet);
			
			while (reader.GetOffset() < packet.Length)
			{
				int offsetCount = reader.GetOffset();
				int lengofPak = packet.Length;
				string lineHex = "";
				string lineHuman = "";
				if (packet.Length - reader.GetOffset() >= 32)
				{
					byte[] lineData = reader.ReadBytes(32);
					lineHex = StringUtils.bytesToString(lineData);
					lineHuman = StringUtils.charBytesToString_NZ(lineData);
				}
				else
				{
					byte[] lineData = reader.ReadBytes(packet.Length - reader.GetOffset());
					lineHex = StringUtils.bytesToString(lineData);
					lineHuman = StringUtils.charBytesToString_NZ(lineData);
				}
				hexStrings.Add(lineHex);
				readablePacketStrings.Add(lineHuman);
			}
			
			// Now build final packet
			string returnPacketString = "";
			foreach (string hexString in hexStrings)
			{
				returnPacketString += hexString + "\r\n";
			}

			returnPacketString += "\r\n";
			
			/*
			foreach (string readablePacketLine in readablePacketStrings)
			{
				string line = readablePacketLine.Replace(" ", ".");
				returnPacketString += line + "\r\n";
			}
			*/
		
			return returnPacketString;

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
			    if (client.playerInstance.CharacterName.getValue().Length > 0)
			    {
				    charName = StringUtils.charBytesToString_NZ(client.playerInstance.CharacterName.getValue());
			    }
			    
		    }

		    string messageConverted = ConvertByteToReadablePacket(obj);
		    
	        string header = "";
		    string timeHeader = cryptType + " Mode : " + timeConsuming + " Milliseconds";

	        switch (type)
	        {
	            case "CLIENT":
	                header = "Client->Server";
	                break;

	            case "SERVER":
	                header = "Server -> Client";
	                break;
	        }
            
            
	        try{
	            StreamWriter w = File.AppendText("PacketLog.txt");
	            if (type == "CLIENT" || type == "SERVER")
	            {
	                w.WriteLine("[" + header + " {0} {1} PSS :{2} CSEQ: {3} SSEQ: {4} ]", DateTime.Now.ToLongTimeString(), DateTime.Now.ToLongDateString(), pss, cseq, sseq);
	                if (charId > 0)
	                {
		                w.WriteLine("[Receiver |CharId: " + charId + "| Handle: " + charName + " ]");
	                }
	            }

	            if (type == "MARGINSERVER" || type == "MARGINCLIENT")
	            {
	                w.WriteLine(header + "[{0} {1} ]", DateTime.Now.ToLongTimeString(), DateTime.Now.ToLongDateString());
	            }
                
		        w.Write(messageConverted);
		        w.WriteLine();
		        w.Flush();
		        w.Close();
	        }
	        catch (Exception exception)
	        {
	            // just pass
		        string message = exception.Message;
	        }
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
			
			try{
				StreamWriter w = File.AppendText("PacketLog.txt");
				if (type == "CLIENT" || type == "SERVER")
				{
					w.WriteLine(header + "[{0} {1} UNENCRYPTED PACKET]", DateTime.Now.ToLongTimeString(), DateTime.Now.ToLongDateString());
				}

				if (type == "MARGINSERVER" || type == "MARGINCLIENT")
				{
					w.WriteLine(header + "[{0} {1} ]", DateTime.Now.ToLongTimeString(), DateTime.Now.ToLongDateString());
				}
				
				w.Write(messageConverted);
				w.WriteLine();
				w.Flush();
				w.Close();
			}
			catch (Exception exception)
			{
				// just pass
				string message = exception.Message;
			}
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
            
            
            try{
                StreamWriter w = File.AppendText("PacketLog.txt");
                if (type == "CLIENT" || type == "SERVER")
                {
                    w.WriteLine(header + "[{0} {1} PSS :{2} CSEQ: {3} SSEQ: {4}]", DateTime.Now.ToLongTimeString(), DateTime.Now.ToLongDateString(), pss, cseq, sseq);
                }

                if (type == "MARGINSERVER" || type == "MARGINCLIENT")
                {
                    w.WriteLine(header + "[{0} {1} ]", DateTime.Now.ToLongTimeString(), DateTime.Now.ToLongDateString());
                }

                w.Write(messageConverted);
	            w.WriteLine();
                w.Flush();
                w.Close();
            }
            catch (Exception exception)
            {
                // just pass
	            string message = exception.Message;
            }
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
            try
            {
                StreamWriter w = File.AppendText("ServerLog.txt");
                w.WriteLine("[{0} {1}] ", DateTime.Now.ToLongTimeString(), DateTime.Now.ToLongDateString());
                w.WriteLine(obj);
                w.WriteLine("");
                w.Flush();
                w.Close();
            }
            catch
            {
                // just pass
            }
        }
	}
}

