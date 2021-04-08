using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Net;
using System.Net.Sockets;
using System.Management;
using Newtonsoft.Json;
using System.IO;
using System.Diagnostics;
using Microsoft.VisualBasic.Devices;
using System.Security.Cryptography.X509Certificates;
using System.Security.Authentication;
using System.Net.Security;
using System.Threading;

namespace SocketsServer
{
    /// <summary>
    /// Lógica de interacción para MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static X509Certificate serverCertificate = null;

        private static X509Certificate getServerCert()
        {
            X509Store store = new X509Store(StoreName.My,
               StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadOnly);

            X509Certificate2 foundCertificate = null;
            foreach (X509Certificate2 currentCertificate
               in store.Certificates)
            {
                if (currentCertificate.IssuerName.Name
                   != null && currentCertificate.IssuerName.
                   Name.Equals("CN=sslmonterrey.com"))
                {
                    foundCertificate = currentCertificate;
                    break;
                }
            }
            return foundCertificate;
        }

        public void RunServer()
        {
            CambiarEstado("Levantando...", null);
            serverCertificate = getServerCert();

            TcpListener listener = new TcpListener(IPAddress.Any, 8080);
            listener.Start();
            CambiarEstado("Activo", null);
            while (true)
            {
                TcpClient client = listener.AcceptTcpClient();
                CambiarEstado("Conectado", "...");
                ProcessClient(client);
                CambiarEstado("Desconectado", null);
            }
        }

        public void ProcessClient(TcpClient client)
        {
            SslStream sslStream = new SslStream(
                client.GetStream(), false);
            try
            {
                sslStream.AuthenticateAsServer(serverCertificate, false, SslProtocols.Tls, true);

                DisplaySecurityLevel(sslStream);
                DisplaySecurityServices(sslStream);
                DisplayCertificateInformation(sslStream);
                DisplayStreamProperties(sslStream);

                sslStream.ReadTimeout = 5000;
                sslStream.WriteTimeout = 5000;

                Console.WriteLine("Waiting for client message...");
                string messageData = ReadMessage(sslStream);
                messageData = messageData.Substring(0, messageData.Length - 5);
                CambiarEstado(null, messageData);
                Console.WriteLine("Received: {0}", messageData);

                if (messageData == "getVideoController") // Información de tarjetas gráficas
                {
                    Console.WriteLine("Text is a get video controller request");
                    List<GPU> videoControllers = new List<GPU>();
                    ManagementObjectSearcher myVideoObject = new ManagementObjectSearcher("select * from Win32_VideoController");
                    foreach (ManagementObject obj in myVideoObject.Get())
                    {
                        videoControllers.Add(new GPU()
                        {
                            Name = obj["Name"].ToString(),
                            Status = obj["Status"].ToString(),
                            AdapterRAM = obj["AdapterRAM"].ToString(),
                            AdapterDACType = obj["AdapterDACType"].ToString(),
                            DriverVersion = obj["DriverVersion"].ToString()
                        });
                    }
                    string result = JsonConvert.SerializeObject(videoControllers);
                    byte[] data = Encoding.UTF8.GetBytes(result);
                    sslStream.Write(data);
                    Console.WriteLine("Info sent to client");
                }
                else if (messageData == "getStorage") // Información de los discos de almacenamiento
                {
                    Console.WriteLine("Text is a get storage request");
                    List<Storage> storages = new List<Storage>();
                    DriveInfo[] allDrives = DriveInfo.GetDrives();
                    foreach (DriveInfo d in allDrives)
                    {
                        if (d.IsReady == true)
                        {
                            storages.Add(new Storage()
                            {
                                TotalAvailableSpace = d.TotalFreeSpace,
                                TotalSizeOfDrive = d.TotalSize,
                                RootDirectory = d.RootDirectory.Name
                            });
                        }
                    }
                    string result = JsonConvert.SerializeObject(storages);
                    byte[] data = Encoding.UTF8.GetBytes(result);
                    sslStream.Write(data);
                    Console.WriteLine("Info sent to client");
                }
                else if (messageData == "getMemoryRam") // Información de la memoria ram
                {
                    Console.WriteLine("Text is a get memory ram request");
                    PerformanceCounter ram = new PerformanceCounter();
                    ComputerInfo infoDevice = new ComputerInfo();
                    ram.CategoryName = "Memory";
                    ram.CounterName = "Available Bytes";
                    MemoryRam memoryRam = new MemoryRam()
                    {
                        TotalPhysicalMemory = infoDevice.TotalPhysicalMemory,
                        TotalFreeSpace = ram.NextValue()
                    };
                    string result = JsonConvert.SerializeObject(memoryRam);
                    byte[] data = Encoding.UTF8.GetBytes(result);
                    sslStream.Write(data);
                    Console.WriteLine("Info sent to client");
                }
                else if (messageData == "getAll") // Toda la información
                {
                    Console.WriteLine("Text is a get all request");
                    All all = new All()
                    {
                        GPUs = new List<GPU>(),
                        Storages = new List<Storage>()
                    };
                    ManagementObjectSearcher myVideoObject = new ManagementObjectSearcher("select * from Win32_VideoController");
                    foreach (ManagementObject obj in myVideoObject.Get())
                    {
                        all.GPUs.Add(new GPU()
                        {
                            Name = obj["Name"].ToString(),
                            Status = obj["Status"].ToString(),
                            AdapterRAM = obj["AdapterRAM"].ToString(),
                            AdapterDACType = obj["AdapterDACType"].ToString(),
                            DriverVersion = obj["DriverVersion"].ToString()
                        });
                    }
                    DriveInfo[] allDrives = DriveInfo.GetDrives();
                    foreach (DriveInfo d in allDrives)
                    {
                        if (d.IsReady == true)
                        {
                            all.Storages.Add(new Storage()
                            {
                                TotalAvailableSpace = d.TotalFreeSpace,
                                TotalSizeOfDrive = d.TotalSize,
                                RootDirectory = d.RootDirectory.Name
                            });
                        }
                    }
                    PerformanceCounter ram = new PerformanceCounter();
                    ComputerInfo infoDevice = new ComputerInfo();
                    ram.CategoryName = "Memory";
                    ram.CounterName = "Available Bytes";
                    all.MemoryRam = new MemoryRam()
                    {
                        TotalPhysicalMemory = infoDevice.TotalPhysicalMemory,
                        TotalFreeSpace = ram.NextValue()
                    };
                    string result = JsonConvert.SerializeObject(all);
                    byte[] data = Encoding.UTF8.GetBytes(result);
                    sslStream.Write(data);
                    Console.WriteLine("Info sent to client");
                }
                else
                {
                    Console.WriteLine("Peticion Invalida");
                    byte[] data = Encoding.UTF8.GetBytes("Peticion Invalida");
                    sslStream.Write(data);
                    Console.WriteLine("Alerta Enviada");
                }
            }
            catch (AuthenticationException e)
            {
                Console.WriteLine("Exception: {0}", e.Message);
                if (e.InnerException != null)
                {
                    Console.WriteLine("Inner exception: {0}", e.InnerException.Message);
                }
                Console.WriteLine("Authentication failed - closing the connection.");
                sslStream.Close();
                client.Close();
                return;
            }
            finally
            {
                sslStream.Close();
                client.Close();
            }
        }
        static string ReadMessage(SslStream sslStream)
        {
            byte[] buffer = new byte[2048];
            StringBuilder messageData = new StringBuilder();
            int bytes = -1;
            do
            {

                bytes = sslStream.Read(buffer, 0, buffer.Length);

                Decoder decoder = Encoding.UTF8.GetDecoder();
                char[] chars = new char[decoder.GetCharCount(buffer, 0, bytes)];
                decoder.GetChars(buffer, 0, bytes, chars, 0);
                messageData.Append(chars);
                if (messageData.ToString().IndexOf("<EOF>") != -1)
                {
                    break;
                }
            } while (bytes != 0);

            return messageData.ToString();
        }
        static void DisplaySecurityLevel(SslStream stream)
        {
            Console.WriteLine("Cipher: {0} strength {1}", stream.CipherAlgorithm, stream.CipherStrength);
            Console.WriteLine("Hash: {0} strength {1}", stream.HashAlgorithm, stream.HashStrength);
            Console.WriteLine("Key exchange: {0} strength {1}", stream.KeyExchangeAlgorithm, stream.KeyExchangeStrength);
            Console.WriteLine("Protocol: {0}", stream.SslProtocol);
        }
        static void DisplaySecurityServices(SslStream stream)
        {
            Console.WriteLine("Is authenticated: {0} as server? {1}", stream.IsAuthenticated, stream.IsServer);
            Console.WriteLine("IsSigned: {0}", stream.IsSigned);
            Console.WriteLine("Is Encrypted: {0}", stream.IsEncrypted);
        }
        static void DisplayStreamProperties(SslStream stream)
        {
            Console.WriteLine("Can read: {0}, write {1}", stream.CanRead, stream.CanWrite);
            Console.WriteLine("Can timeout: {0}", stream.CanTimeout);
        }
        static void DisplayCertificateInformation(SslStream stream)
        {
            Console.WriteLine("Certificate revocation list checked: {0}", stream.CheckCertRevocationStatus);

            X509Certificate localCertificate = stream.LocalCertificate;
            if (stream.LocalCertificate != null)
            {
                Console.WriteLine("Local cert was issued to {0} and is valid from {1} until {2}.",
                    localCertificate.Subject,
                    localCertificate.GetEffectiveDateString(),
                    localCertificate.GetExpirationDateString());
            }
            else
            {
                Console.WriteLine("Local certificate is null.");
            }

            X509Certificate remoteCertificate = stream.RemoteCertificate;
            if (stream.RemoteCertificate != null)
            {
                Console.WriteLine("Remote cert was issued to {0} and is valid from {1} until {2}.",
                    remoteCertificate.Subject,
                    remoteCertificate.GetEffectiveDateString(),
                    remoteCertificate.GetExpirationDateString());
            }
            else
            {
                Console.WriteLine("Remote certificate is null.");
            }
        }

        public MainWindow()
        {
            InitializeComponent();
        }

        delegate void CambiarEstadoDelegado(string status, string mensaje);
        private void CambiarEstado(string status, string mensaje)
        {
            if (!Dispatcher.CheckAccess())
            {
                CambiarEstadoDelegado delegado = new
                CambiarEstadoDelegado(CambiarEstado);
                object[] parametros = new object[] { status, mensaje };
                Dispatcher.Invoke(delegado, parametros);
            }
            else
            {
                if (status != null)
                    lblServerStatus.Content = status;
                if (mensaje != null)
                    lblReceived.Content = mensaje;
            }
        }

        private void btnSetUpServer_Click(object sender, RoutedEventArgs e)
        {
            btnSetUpServer.IsEnabled = false;
            ThreadStart ts = delegate { RunServer(); };
            Thread hilo = new Thread(ts);
            hilo.IsBackground = true;
            hilo.Start();
        }
    }
}
