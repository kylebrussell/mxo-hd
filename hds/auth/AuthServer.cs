using System.Net;
using NetCoreServer;

namespace hds.auth
{
    public class AuthServer : TcpServer
    {
        protected override TcpSession CreateSession()
        {
            return new AuthClientSession(this);
        }

        public AuthServer() : base(IPAddress.Any, 11000)
        {
            Output.WriteLine("Auth server set and ready at port 11000");
        }

        public void startServer()
        {
            Start();
        }

        public void stopServer()
        {
            Stop();
        }
    }
}
