using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using MySql.Data;
using MySql.Data.MySqlClient;
using System.Threading;
using System.Data;
using System.ComponentModel;
using System.Net.Sockets;
using System.Net;

namespace Projekt_ps
{
    public partial class MainWindow : Window
    {
        private MySqlConnection msqlcon = new MySqlConnection("Server=remotemysql.com;Port=3306;Database=DTzose51Js;Uid=DTzose51Js;pwd=ER2VaqmwbB;");
        private int users_counter=0;
        private static int active_users=0;
        private static readonly Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        private static readonly List<Socket> clientSockets = new List<Socket>();
        private const int BUFFER_SIZE = 2048;
        private const int PORT = 8888;
        private static readonly byte[] buffer = new byte[BUFFER_SIZE];
        private BackgroundWorker m_oBackgroundWorker = null;
        private static List<Task> tasklist = new List<Task>();

        public MainWindow()
        {
            ThreadPool.SetMaxThreads(10, 20);
            InitializeComponent();
            DatabaseConnection();           
            Main();
        }

        private void Main()
        {
            int port = 8888;

            if (null == m_oBackgroundWorker)
                {
                    m_oBackgroundWorker = new BackgroundWorker();
                    m_oBackgroundWorker.DoWork += new DoWorkEventHandler(m_oBackgroundWorker_DoWork);
                }
                m_oBackgroundWorker.RunWorkerAsync(port);
        }

        private void m_oBackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            Dispatcher.Invoke(() => { lst_spis.Items.Add("Setting up server..."); });
            serverSocket.Bind(new IPEndPoint(IPAddress.Any, PORT));
            serverSocket.Listen(0);
            serverSocket.BeginAccept(AcceptCallback, null);
            Dispatcher.Invoke(() => { lst_spis.Items.Add("Server setup complete"); });
            while (true)
            {
                Task.WaitAll(tasklist.ToArray());
            }
        }

        private void AcceptCallback(IAsyncResult AR)
        {
            Socket socket;

            try
            {
                socket = serverSocket.EndAccept(AR);
            }
            catch (ObjectDisposedException) // I cannot seem to avoid this (on exit when properly closing sockets)
            {
                return;
            }

            clientSockets.Add(socket);
            socket.BeginReceive(buffer, 0, BUFFER_SIZE, SocketFlags.None, ReceiveCallback, socket);
            Dispatcher.Invoke(() => { lst_spis.Items.Add("Client "+ socket.RemoteEndPoint +" connected, waiting for request..."); });
            updateCounterOfActiveUsers(true);
            serverSocket.BeginAccept(AcceptCallback, null);
        }

        private void ReceiveCallback(IAsyncResult AR)
        {
            Socket current = (Socket)AR.AsyncState;
            int received;

            try
            {
                received = current.EndReceive(AR);
            }
            catch (SocketException)
            {
                Dispatcher.Invoke(() => { lst_spis.Items.Add("Client: " + current.RemoteEndPoint + " forcefully disconnected"); });
                updateCounterOfActiveUsers(false);
                // Don't shutdown because the socket may be disposed and its disconnected anyway.
                current.Close();
                clientSockets.Remove(current);
                return;
            }

            byte[] recBuf = new byte[received];
            Array.Copy(buffer, recBuf, received);
            string text = Encoding.ASCII.GetString(recBuf);

            if (text.ToLower() == "exit") // Client wants to exit gracefully
            {
                Dispatcher.Invoke(() => { lst_spis.Items.Add("Client: " + current.RemoteEndPoint + " disconnected"); }); 
                updateCounterOfActiveUsers(false);
                // Always Shutdown before closing
                current.Shutdown(SocketShutdown.Both);
                current.Close();
                clientSockets.Remove(current);
                return;
            }
            else
            {
                string[] roger = null;
                try
                {
                    roger = text.Split(';');
                }
                catch
                {
                    Dispatcher.Invoke(() => { lst_spis.Items.Add("Client: " + current.RemoteEndPoint + " used unknown commend"); });
                }

                if (roger[0].ToLower() == "registration")
                {
                    /*try
                    {*/
                        Task t = new Task(() => { Dispatcher.Invoke(() => { addUser(roger[1], roger[2]); }); });
                        tasklist.Add(SingletonSecured.Instance.AddTask(t));
                        t.Start();
                    //}
                    /*catch(Exception e)
                    {
                        MessageBox.Show(e.ToString());
                    }*/
                }
                //Console.WriteLine("Text is an invalid request");
                //byte[] data = Encoding.ASCII.GetBytes("Invalid request");
                //current.Send(data);
                //Console.WriteLine("Warning Sent");
                //Task t = new Task(() => { Dispatcher.Invoke(() => { lst_spis.Items.Add("Received Text from " + current.RemoteEndPoint + ": " + text); }); });
                //tasklist.Add(SingletonSecured.Instance.AddTask(t));
                //t.Start();
            }

            current.BeginReceive(buffer, 0, BUFFER_SIZE, SocketFlags.None, ReceiveCallback, current);
        }

        private void updateCounterOfActiveUsers(bool x)
        {
            if (x)
            {
                Dispatcher.Invoke(() => { lbl_active_users.Content = ++active_users; });               
            }
            else
            {
                Dispatcher.Invoke(() => { lbl_active_users.Content = --active_users; });
            }
        }

        private void DatabaseConnection()
        {
            try
            {
                msqlcon.Open();
                MessageBox.Show("Połączono z bazą danych", "Sukces");
                users_counter = CheckUsers();
                lbl_all_users.Content = users_counter.ToString();
            }
            catch
            {
                MessageBox.Show("Nie udało się połączyć z bazą danych, zamykanie aplikacji!", "Krytyczny błąd");
                Environment.Exit(0);
            }
        }

        private void addUser(string l,string p)
        {
            try
            {
                string ask = "INSERT INTO Users(ID,Login,Password) VALUES (" + (users_counter+1) +  ",'" + l + "','" + p+"')";
                MySqlCommand task = new MySqlCommand(ask, msqlcon);
                task.ExecuteNonQuery();
                lbl_all_users.Content = users_counter.ToString();
            }
            catch(MySqlException)
            {
                MessageBox.Show("Change your account login!", "Duplicated login!");
            }
            catch(Exception e)
            {
                MessageBox.Show(e.ToString());
            }
            finally
            {
                users_counter = CheckUsers();
                lbl_all_users.Content = users_counter.ToString();
            }
        }

        private int CheckUsers()
        {
            try
            {
                MySqlDataReader read;
                int x = 0;
                string ask = "SELECT COUNT(*) FROM Users";
                MySqlCommand task = new MySqlCommand(ask, msqlcon);
                read = task.ExecuteReader();
                read.Read();
                x = read.GetInt32(0);
                read.Close();
                return x;
            }
            catch
            {
                MessageBox.Show("Failed to connect to the database!", "Connection error");
                return 0;
            }
        }    

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            MessageBoxResult result = MessageBox.Show("Czy na pewno chcesz zamknąć aplikację serwera?", "Serwer", MessageBoxButton.YesNo);
            if (result == MessageBoxResult.No) { e.Cancel = true; }
            else
            {
                msqlcon.Close();
            }
        }

        /*public void DatabaseConnection_test()
{

    MySqlConnection msqlcon = new MySqlConnection("Server=remotemysql.com;Port=3306;Database=DTzose51Js;Uid=DTzose51Js;pwd=ER2VaqmwbB;");
    MySqlDataReader read;
    DataTable dt = new DataTable();
    string ask = "SELECT * FROM Test_table";

    try
    {
        msqlcon.Open();
        MessageBox.Show("Połączono z bazą danych", "Sukces");

        MySqlCommand task = new MySqlCommand(ask, msqlcon);
        read = task.ExecuteReader();
        dt.Load(read);
        DataRow dr;
        lst_test.Items.Add("[ID] [login]");

        for(int i = 0; i < dt.Rows.Count; i++)
        {
            dr = dt.Rows[i];
            string data = " ";
            for(int j = 0; j< dr.ItemArray.Count(); j++)
            {
                data += dr[j].ToString() + "    ";
            }
            lst_test.Items.Add(data);
        }

        read.Close();
        msqlcon.Close();
    }
    catch
    {
        MessageBox.Show("Nie udało się połączyć z bazą danych", "Błąd");
    }
}*/

    }
}
