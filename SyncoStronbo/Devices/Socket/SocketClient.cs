using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace SyncoStronbo.Devices.Socket {
    internal class SocketClient {

        private int port;

        List<TcpListener> listeners;

        public SocketClient(int port_) {
            this.port = port_;
        }


        public static List<TcpListener> FindDevicesWithOpenPort(int port) {
            List<TcpListener> openDevices = new List<TcpListener>();

            // Get the local machine's IP address
            string localIpAddress = GetLocalIpAddress();

            // Define the range of IP addresses to scan (you may customize this based on your network)
            string baseIpAddress = localIpAddress.Substring(0, localIpAddress.LastIndexOf('.') + 1);
            for (int i = 2; i <= 254; i++) {
                string targetIpAddress = baseIpAddress + i.ToString();

                // Check if the port is open on the target device
                if (IsPortOpen(targetIpAddress, port)) {
                    // Create a TcpListener for the open port on the target device
                    TcpListener tcpListener = new TcpListener(IPAddress.Parse(targetIpAddress), port);
                    openDevices.Add(tcpListener);
                }
            }

            return openDevices;
        }

        private static string GetLocalIpAddress() {
            string localIpAddress = "";

            foreach (NetworkInterface networkInterface in NetworkInterface.GetAllNetworkInterfaces()) {
                if (networkInterface.OperationalStatus == OperationalStatus.Up) {
                    foreach (UnicastIPAddressInformation ip in networkInterface.GetIPProperties().UnicastAddresses) {
                        if (ip.Address.AddressFamily == AddressFamily.InterNetwork) {
                            localIpAddress = ip.Address.ToString();
                            break;
                        }
                    }
                }

                if (!string.IsNullOrEmpty(localIpAddress)) {
                    break;
                }
            }

            return localIpAddress;
        }

        private static bool IsPortOpen(string ipAddress, int port) {
            try {
                using (TcpClient tcpClient = new TcpClient()) {
                    tcpClient.Connect(ipAddress, port);
                    return true;
                }
            } catch (SocketException) {
                return false;
            }
        }



    }
}
