using System;
using System.Windows;
using System.Text;
using System.Management;
using System.IO;
using System.Net;
using System.Net.Sockets;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using LiveCharts;
using LiveCharts.Wpf;
using System.Net.Security;
using System.Security.Authentication;
using System.Collections;
using System.Security.Cryptography.X509Certificates;

namespace SocketsClient
{
    /// <summary>
    /// Lógica de interacción para MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        double gb = 1073741824;

        public MainWindow()
        {
            InitializeComponent();
            hidden_card();
        }

        private static Hashtable certificateErrors = new Hashtable();

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

        static bool ValidateServerCertificate(Object sender,
      X509Certificate certificate, X509Chain chain,
      SslPolicyErrors sslPolicyErrors)
        {
            if (sslPolicyErrors == SslPolicyErrors.None)
            { return true; }
            if (sslPolicyErrors ==
               SslPolicyErrors.RemoteCertificateChainErrors)
            { return true; }
            return false;
        }

        private void RunClient(string machineName, string serverName, string request)
        {
            TcpClient client = new TcpClient(machineName/*, 443*/,8080);
            Console.WriteLine("Client connected.");
            SslStream sslStream = new SslStream(
                client.GetStream(),
                false,
                new RemoteCertificateValidationCallback(ValidateServerCertificate),
                null
                );
            try
            {
                var clientCertificateCollection = new
         X509CertificateCollection(new X509Certificate[]
         { getServerCert() });
                sslStream.AuthenticateAsClient(serverName, clientCertificateCollection, SslProtocols.Tls, true);
            }
            catch (AuthenticationException e)
            {
                Console.WriteLine("Exception: {0}", e.Message);
                if (e.InnerException != null)
                {
                    Console.WriteLine("Inner exception: {0}", e.InnerException.Message);
                }
                Console.WriteLine("Authentication failed - closing the connection.");
                client.Close();
                return;
            }
            byte[] messsage = Encoding.UTF8.GetBytes(request+ "<EOF>");
            
            sslStream.Write(messsage);
            sslStream.Flush();
            
            string serverMessage = ReadMessage(sslStream);
            Console.WriteLine("Server says: {0}", serverMessage);
            
            client.Close();
            Console.WriteLine("Client closed.");
            switch (request)
            {
                case "getAll":

                    cardram.Visibility = Visibility.Hidden;
                    hidden_card();
                    hidden_video();
                    txtjson.Text = serverMessage;
                    All all = JsonConvert.DeserializeObject<All>(serverMessage);
                    serverMessage = all.MemoryRam.TotalFreeSpace.ToString();
                    break;
                case "getVideoController":
                    cardram.Visibility = Visibility.Hidden;
                    hidden_card();

                    txtjson.Text = serverMessage;
                    List<GPU> videoControllers = JsonConvert.DeserializeObject<List<GPU>>(serverMessage);
                    ListGPUS(videoControllers);
                    break;
                case "getStorage":

                    cardram.Visibility = Visibility.Hidden;
                    hidden_video();
                    txtjson.Text = serverMessage;
                    List<Storage> storages = JsonConvert.DeserializeObject<List<Storage>>(serverMessage);
                    Storages(storages);

                    break;
                case "getMemoryRam":

                    hidden_video();
                    hidden_card();
                    txtjson.Text = serverMessage;
                    MemoryRam memoryRam = JsonConvert.DeserializeObject<MemoryRam>(serverMessage);
                    Ram(memoryRam);

                    break;
            }
        }
        private string ReadMessage(SslStream sslStream)
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

        private void btnSendRequest_Click(object sender, RoutedEventArgs e)
        {
           
        }
        void ListGPUS(List<GPU> listgpu) {

            if (listgpu.Count()==1)
            {
                txtgrafic1.Text = listgpu[0].Name;
                txtresname.Text= listgpu[0].Name;
                txtresstatus.Text = listgpu[0].Status;
                txtresAdapter.Text = listgpu[0].AdapterRAM;
                txtresAdapterDAC.Text = listgpu[0].AdapterDACType;
                txtresDriver.Text = listgpu[0].DriverVersion;

                card_video1.Visibility = Visibility.Visible;
                card_video2.Visibility = Visibility.Hidden;
            }
            if (listgpu.Count() == 2)
            {
                txtgrafic1.Text = listgpu[0].Name;
                txtresname.Text = listgpu[0].Name;
                txtresstatus.Text = listgpu[0].Status;
                txtresAdapter.Text = listgpu[0].AdapterRAM;
                txtresAdapterDAC.Text = listgpu[0].AdapterDACType;
                txtresDriver.Text = listgpu[0].DriverVersion;

                txtgrafic2.Text = listgpu[1].Name;
                txtresname1.Text = listgpu[1].Name;
                txtresstatus1.Text = listgpu[1].Status;
                txtresAdapter1.Text = listgpu[1].AdapterRAM;
                txtresAdapterDAC1.Text = listgpu[1].AdapterDACType;
                txtresDriver1.Text = listgpu[1].DriverVersion;

                card_video1.Visibility = Visibility.Visible;
                card_video2.Visibility = Visibility.Visible;
            }

        }

        List<string> list_storages = new List<string>();
        Func<ChartPoint, string> labelPoint = chartpoint => string.Format("{0} ({1:P})", chartpoint.Y, chartpoint.Participation);
        void Storages(List<Storage> List_storages) {
            list_storages.Clear();
            list_storages.Add("Total Available Space GB");
            list_storages.Add("Total used Space GB");
            SeriesCollection series1 = new SeriesCollection();
            SeriesCollection series2 = new SeriesCollection();
            SeriesCollection series3 = new SeriesCollection();
            SeriesCollection series4 = new SeriesCollection();
            
            if (List_storages.Count()==1)
            {
                series1.Add(new PieSeries() { Title = list_storages[0], Values = new ChartValues<double> { Math.Round((List_storages[0].TotalAvailableSpace / gb), 2) }, DataLabels = true, LabelPoint = labelPoint });
                piechart1.Series = series1;
                series1.Add(new PieSeries() { Title = list_storages[1], Values = new ChartValues<double> { Math.Round(((List_storages[0].TotalSizeOfDrive - List_storages[0].TotalAvailableSpace) / gb), 2) }, DataLabels = true, LabelPoint = labelPoint });
                piechart1.Series = series1;
                txtstorage1.Text = "Disco: " + List_storages[0].RootDirectory;

                card1.Visibility=Visibility.Visible;
                card2.Visibility = Visibility.Hidden;
                card3.Visibility = Visibility.Hidden;
                card4.Visibility = Visibility.Hidden;
            }
            if (List_storages.Count() == 2)
            {
                series1.Add(new PieSeries() { Title = list_storages[0], Values = new ChartValues<double> { Math.Round((List_storages[0].TotalAvailableSpace / gb), 2) }, DataLabels = true, LabelPoint = labelPoint });
                piechart1.Series = series1;
                series1.Add(new PieSeries() { Title = list_storages[1], Values = new ChartValues<double> { Math.Round(((List_storages[0].TotalSizeOfDrive - List_storages[0].TotalAvailableSpace) / gb), 2) }, DataLabels = true, LabelPoint = labelPoint });
                piechart1.Series = series1;
                txtstorage1.Text = "Disco: " + List_storages[0].RootDirectory;

                series2.Add(new PieSeries() { Title = list_storages[0], Values = new ChartValues<double> { Math.Round((List_storages[1].TotalAvailableSpace / gb), 2) }, DataLabels = true, LabelPoint = labelPoint });
                piechart2.Series = series2;
                series2.Add(new PieSeries() { Title = list_storages[1], Values = new ChartValues<double> { Math.Round(((List_storages[1].TotalSizeOfDrive - List_storages[1].TotalAvailableSpace) / gb), 2) }, DataLabels = true, LabelPoint = labelPoint });
                piechart2.Series = series2;
                txtstorage2.Text = "Disco: " + List_storages[1].RootDirectory;

                card1.Visibility = Visibility.Visible;
                card2.Visibility = Visibility.Visible;
                card3.Visibility = Visibility.Hidden;
                card4.Visibility = Visibility.Hidden;
            }
            if (List_storages.Count() == 3)
            {
                series1.Add(new PieSeries() { Title = list_storages[0], Values = new ChartValues<double> { Math.Round((List_storages[0].TotalAvailableSpace / gb), 2) }, DataLabels = true, LabelPoint = labelPoint });
                piechart1.Series = series1;
                series1.Add(new PieSeries() { Title = list_storages[1], Values = new ChartValues<double> { Math.Round(((List_storages[0].TotalSizeOfDrive - List_storages[0].TotalAvailableSpace) / gb), 2) }, DataLabels = true, LabelPoint = labelPoint });
                piechart1.Series = series1;
                txtstorage1.Text = "Disco: " + List_storages[0].RootDirectory;

                series2.Add(new PieSeries() { Title = list_storages[0], Values = new ChartValues<double> { Math.Round((List_storages[1].TotalAvailableSpace / gb), 2) }, DataLabels = true, LabelPoint = labelPoint });
                piechart2.Series = series2;
                series2.Add(new PieSeries() { Title = list_storages[1], Values = new ChartValues<double> { Math.Round(((List_storages[1].TotalSizeOfDrive - List_storages[1].TotalAvailableSpace) / gb), 2) }, DataLabels = true, LabelPoint = labelPoint });
                piechart2.Series = series2;
                txtstorage2.Text = "Disco: " + List_storages[1].RootDirectory;

                series3.Add(new PieSeries() { Title = list_storages[0], Values = new ChartValues<double> { Math.Round((List_storages[2].TotalAvailableSpace / gb), 2) }, DataLabels = true, LabelPoint = labelPoint });
                piechart3.Series = series3;
                series3.Add(new PieSeries() { Title = list_storages[1], Values = new ChartValues<double> { Math.Round(((List_storages[2].TotalSizeOfDrive - List_storages[2].TotalAvailableSpace) / gb), 2) }, DataLabels = true, LabelPoint = labelPoint });
                piechart3.Series = series3;
                txtstorage3.Text = "Disco: " + List_storages[2].RootDirectory;

                card1.Visibility = Visibility.Visible;
                card2.Visibility = Visibility.Visible;
                card3.Visibility = Visibility.Visible;
                card4.Visibility = Visibility.Hidden;
            }
            if (List_storages.Count() == 4)
            {
                series1.Add(new PieSeries() { Title = list_storages[0], Values = new ChartValues<double> { Math.Round((List_storages[0].TotalAvailableSpace / gb), 2)  }, DataLabels = true, LabelPoint = labelPoint });
                piechart1.Series = series1;
                series1.Add(new PieSeries() { Title = list_storages[1], Values = new ChartValues<double> { Math.Round(((List_storages[0].TotalSizeOfDrive - List_storages[0].TotalAvailableSpace) / gb), 2) }, DataLabels = true, LabelPoint = labelPoint });
                piechart1.Series = series1;
                txtstorage1.Text = "Disco: " + List_storages[0].RootDirectory;

                series2.Add(new PieSeries() { Title = list_storages[0], Values = new ChartValues<double> { Math.Round((List_storages[1].TotalAvailableSpace / gb), 2) }, DataLabels = true, LabelPoint = labelPoint });
                piechart2.Series = series2;
                series2.Add(new PieSeries() { Title = list_storages[1], Values = new ChartValues<double> { Math.Round(((List_storages[1].TotalSizeOfDrive - List_storages[1].TotalAvailableSpace) / gb), 2) }, DataLabels = true, LabelPoint = labelPoint });
                piechart2.Series = series2;
                txtstorage2.Text = "Disco: " + List_storages[1].RootDirectory;

                series3.Add(new PieSeries() { Title = list_storages[0], Values = new ChartValues<double> { Math.Round((List_storages[2].TotalAvailableSpace / gb), 2) }, DataLabels = true, LabelPoint = labelPoint });
                piechart3.Series = series3;
                series3.Add(new PieSeries() { Title = list_storages[1], Values = new ChartValues<double> { Math.Round(((List_storages[2].TotalSizeOfDrive - List_storages[2].TotalAvailableSpace) / gb), 2) }, DataLabels = true, LabelPoint = labelPoint });
                piechart3.Series = series3;
                txtstorage3.Text = "Disco: " + List_storages[2].RootDirectory;

                series4.Add(new PieSeries() { Title = list_storages[0], Values = new ChartValues<double> { Math.Round((List_storages[3].TotalAvailableSpace / gb), 2) }, DataLabels = true, LabelPoint = labelPoint });
                piechart4.Series = series4;
                series4.Add(new PieSeries() { Title = list_storages[1], Values = new ChartValues<double> { Math.Round(((List_storages[3].TotalSizeOfDrive - List_storages[3].TotalAvailableSpace) / gb), 2) }, DataLabels = true, LabelPoint = labelPoint });
                piechart4.Series = series4;
                txtstorage4.Text = "Disco: " + List_storages[3].RootDirectory;

                card1.Visibility = Visibility.Visible;
                card2.Visibility = Visibility.Visible;
                card3.Visibility = Visibility.Visible;
                card4.Visibility = Visibility.Visible;
            }

        }

        void Ram(MemoryRam ram) {
            cardram.Visibility = Visibility.Visible;
            list_storages.Clear();
            list_storages.Add("Total Available Ram GB");
            list_storages.Add("Total used Ram GB");
            SeriesCollection series = new SeriesCollection();
            series.Add(new PieSeries() { Title = list_storages[0], Values = new ChartValues<double> { Math.Round((ram.TotalFreeSpace / gb), 2)  }, DataLabels = true, LabelPoint = labelPoint });
            piechart_ram.Series = series;
            series.Add(new PieSeries() { Title = list_storages[1], Values = new ChartValues<double> { Math.Round(((ram.TotalPhysicalMemory - ram.TotalFreeSpace) / gb), 2) }, DataLabels = true, LabelPoint = labelPoint });
            piechart_ram.Series = series;
            txtram.Text = "Memory Ram: " ;
            txtphysicalram.Text = ram.TotalPhysicalMemory.ToString();
            txtspaceram.Text = ram.TotalFreeSpace.ToString();
        }
        private void BtnSendRequests_Click(object sender, RoutedEventArgs e)
        {
            int index = cmbRequest.SelectedIndex;
            string request = "";

            if (index == 0)
            {
                request = "getAll";
                hidden_card();
            }
            else if (index == 1)
            {
                request = "getVideoController";
                hidden_card();
            }
            else if (index == 2)
            {
                request = "getStorage";
                
            }
            else if (index == 3)
            {
                request = "getMemoryRam";
                hidden_card();
            }
            else
            {
                request = "exit";
            }

            string machineName = txtIp.Text;
            string serverCertificateName = "sslmonterrey.com";
            RunClient(machineName, serverCertificateName, request);
        }
        void hidden_card() {
            card1.Visibility = Visibility.Hidden;
            card2.Visibility = Visibility.Hidden;
            card3.Visibility = Visibility.Hidden;
            card4.Visibility = Visibility.Hidden;
        }
        void hidden_video() {
            card_video1.Visibility = Visibility.Hidden;
            card_video2.Visibility = Visibility.Hidden;
        }
        void clear_Grafics() {

            txtgrafic1.Text = string.Empty;
            txtresname.Text = string.Empty;
            txtresstatus.Text = string.Empty;
            txtresAdapter.Text = string.Empty;
            txtresAdapterDAC.Text = string.Empty;
            txtresDriver.Text = string.Empty;

            txtgrafic2.Text = string.Empty;
            txtresname1.Text = string.Empty;
            txtresstatus1.Text = string.Empty;
            txtresAdapter1.Text = string.Empty;
            txtresAdapterDAC1.Text = string.Empty;
            txtresDriver1.Text = string.Empty;
        }

        private void cmbRequest_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            btnSendRequests.IsEnabled = true;
        }
    }
}
