using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;

namespace AsyncPostgresClient
{
    public interface IConnectionStream
    {
        Stream CreateTcpStream(string hostname, int port);
    }

    public class ClrConnectionStream : IConnectionStream
    {
        public static readonly ClrConnectionStream Default =
            new ClrConnectionStream();

        public Stream CreateTcpStream(string hostname, int port)
        {
            var client = new TcpClient(hostname, port);

            return client.GetStream();
        }
    }
}
