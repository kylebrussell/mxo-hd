using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace hds{

	public class ConsoleSocket{
		
		private readonly TcpListener tcpListener;	 
    	private readonly Thread listenThread;
		private volatile bool mainThreadWorking;
        private readonly int consoleport;


        public ConsoleSocket() : this(NetworkBind.ConsolePort)
        {
        }

        internal ConsoleSocket(int port){
			this.consoleport = port;
			this.mainThreadWorking = true;
			 
      		this.tcpListener = new TcpListener(NetworkBind.Address, consoleport);
			Output.WriteLine("[Console] Server console operating on port " + consoleport);
      		this.listenThread = new Thread(new ThreadStart(ListenForClients))
			{
				IsBackground = true,
				Name = "HdsConsoleSocket"
			};
		}

		internal int BoundPort => ((IPEndPoint)this.tcpListener.LocalEndpoint).Port;
		internal bool IsListenerAlive => this.listenThread.IsAlive;
		
		public void startServer(){
			this.listenThread.Start();
		}
		
		public void stopServer(){
			this.mainThreadWorking = false;
			try
			{
				this.tcpListener.Stop();
			}
			catch (Exception)
			{
				// Listener may already be stopped or never started.
			}
		}
		
		private void ListenForClients(){
			try
			{
				this.tcpListener.Start();
			}
			catch (Exception ex)
			{
				Output.WriteLine("[Console] Failed to start listener on port " + consoleport + ": " + ex.Message);
				return;
			}

			byte[] message = new byte[2048];
			while (mainThreadWorking){
				TcpClient client;
				try
				{
					client = this.tcpListener.AcceptTcpClient();
				}
				catch (SocketException ex)
				{
					if (!mainThreadWorking)
					{
						break;
					}
					Output.WriteLine("[Console] Accept socket error: " + ex.SocketErrorCode + " " + ex.Message);
					continue;
				}
				catch (ObjectDisposedException)
				{
					break;
				}
				catch (Exception ex)
				{
					if (!mainThreadWorking)
					{
						break;
					}
					Output.WriteLine("[Console] Accept error: " + ex.Message);
					continue;
				}

				if (!mainThreadWorking)
				{
					try { client.Close(); } catch { }
					break;
				}

				using (client)
				using (NetworkStream clientStream = client.GetStream())
				{
					Output.WriteLine("[Console] Console client Connected.");
					while (mainThreadWorking)
					{
						int bytesRead;
						try
						{
							bytesRead = clientStream.Read(message, 0, message.Length);
						}
						catch
						{
							break;
						}

						if (bytesRead == 0)
						{
							break;
						}

						try
						{
							byte[] response = parseCommand(message, bytesRead);
							if (response.Length == 1 && response[0] == 0x00)
							{
								break;
							}
							clientStream.Write(response, 0, response.Length);
						}
						catch (Exception e)
						{
							Output.WriteLine("[Console] Server console Error\nDebug:" + e);
							break;
						}
					}
					Output.WriteLine("[Console] Console client closing");
				}
			}
		}	
		
		private byte[] parseCommand(byte[] command,int length){
			byte[] temp = new byte[length];
            

			ArrayUtils.fastCopy(command,temp,length); //saves time and so
			string response = "";
			
			string txtCommand = StringUtils.charBytesToString(temp);
			bool responseSet = false;

            //TODO: REMOVE CLIENT CONSOLE

			if (!responseSet){
				return StringUtils.stringToBytes("Unrecognized command: "+txtCommand);
			}
			
			return StringUtils.stringToBytes(response);
		}
		
	}
}

