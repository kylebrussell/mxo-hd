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

        public AuthServer() : base(NetworkBind.Address, NetworkBind.AuthPort)
        {
            Output.WriteLine("Auth server set and ready at port " + NetworkBind.AuthPort);
            if (AuthHandler.DevAcceptAnyPassword)
            {
                Output.WriteLine("WARNING: MXO_DEV_ANY_PASSWORD is set - any password is accepted for existing accounts. Local testing only.");
            }
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
