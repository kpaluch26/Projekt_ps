using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using MySql.Data.MySqlClient;
using System.Threading;
using System.ComponentModel;
using System.Net.Sockets;
using System.Net;
using System.IO;
using System.Data;
using System.Linq;

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
        private List<string> permits=new List<string>();
        private List<string> commits=new List<string>();
        private List<string> reglist=new List<string>();
        static object locker = new object();

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
                try { Task.WaitAll(tasklist.ToArray()); }
                catch { }
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
            bool help = true;

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
                    roger = text.Split('|');
                }
                catch
                {
                    Dispatcher.Invoke(() => { lst_spis.Items.Add("Client: " + current.RemoteEndPoint + " used unknown commend"); });
                }

                if (roger[0].ToLower() == "registration")
                {
                    int h = users_counter;

                    Task t = new Task(() => { Dispatcher.Invoke(() => { addUser(roger[1], roger[2]); }); });
                    tasklist.Add(SingletonSecured.Instance.AddTask(t));
                    t.Start();
                    t.Wait();

                    if (users_counter > h)
                    {
                        byte[] data = Encoding.ASCII.GetBytes("registration|correct");
                        current.Send(data);
                    }
                    else
                    {
                        byte[] data = Encoding.ASCII.GetBytes("registration|incorrect");
                        current.Send(data);
                    }
                        
                }
                else if (roger[0].ToLower() == "login")
                {
                    Task t = new Task(() => { Dispatcher.Invoke(() => { log_in(roger[1], roger[2], current.LocalEndPoint.ToString()); }); });
                    tasklist.Add(SingletonSecured.Instance.AddTask(t));
                    t.Start();
                    t.Wait();
                    foreach(string x in permits)
                    {                        
                        if (x == roger[1]+"|"+ current.LocalEndPoint.ToString())
                        {
                            byte[] data = Encoding.ASCII.GetBytes("login|correct");
                            current.Send(data);
                            current.BeginReceive(buffer, 0, BUFFER_SIZE, SocketFlags.None, ReceiveCallback_loged, current);
                            help = false;
                            break;
                        }
                        else
                        {
                            byte[] data = Encoding.ASCII.GetBytes("login|incorrect");
                            current.Send(data);
                        }
                    }
                }
                //Console.WriteLine("Text is an invalid request");
                //byte[] data = Encoding.ASCII.GetBytes("Invalid request");
                //current.Send(data);
                //Console.WriteLine("Warning Sent");
                //Task t = new Task(() => { Dispatcher.Invoke(() => { lst_spis.Items.Add("Received Text from " + current.RemoteEndPoint + ": " + text); }); });
                //tasklist.Add(SingletonSecured.Instance.AddTask(t));
                //t.Start();
                if (help)
                {
                    current.BeginReceive(buffer, 0, BUFFER_SIZE, SocketFlags.None, ReceiveCallback, current);
                }
            }
        }

        private void ReceiveCallback_loged(IAsyncResult AR)
        {
            Socket current = (Socket)AR.AsyncState;
            int received;
            string name="nazwa_pliku";
            string[] connect_data = null;
            foreach(string x in permits)
            {
                connect_data = x.Split('|');
                if (connect_data[1] == current.LocalEndPoint.ToString())
                {
                    name=connect_data[0];
                    break;
                }
            }

            try
            {
                received = current.EndReceive(AR);
            }
            catch (SocketException)
            {
                Dispatcher.Invoke(() => { lst_spis.Items.Add("Client: " + current.RemoteEndPoint + " forcefully disconnected"); });
                updateCounterOfActiveUsers(false);
                current.Close();
                clientSockets.Remove(current);
                foreach (string x in permits)
                {
                    if (x == name + "|" + current.LocalEndPoint.ToString())
                    {
                        permits.Remove(x);
                        break;
                    }
                }
                return;
            }

            byte[] recBuf = new byte[received];
            Array.Copy(buffer, recBuf, received);
            string text = Encoding.ASCII.GetString(recBuf);

            string[] roger = null;
            try
            {
                roger = text.Split('|');
            }
            catch
            {
                Dispatcher.Invoke(() => { lst_spis.Items.Add("Client: " + current.RemoteEndPoint + " used unknown commend"); });
            }

            if (roger[0].ToLower() == "exit") 
            {
                Dispatcher.Invoke(() => { lst_spis.Items.Add("Client: " + current.RemoteEndPoint + " disconnected"); });
                updateCounterOfActiveUsers(false);
                current.Shutdown(SocketShutdown.Both);
                current.Close();
                clientSockets.Remove(current);
                return;
            }
            else if (roger[0].ToLower() == "done")
            {
                Task t = new Task(() => { Dispatcher.Invoke(() => { SendToBase(name); }); });
                tasklist.Add(SingletonSecured.Instance.AddTask(t));
                t.Start();
            }
            else if(roger[0].ToLower() == "get")
            {
                Task t = new Task(() => { Dispatcher.Invoke(() => { DownloadPersonalizedData(name,roger[1].ToString(), current.LocalEndPoint.ToString()); }); });
                tasklist.Add(SingletonSecured.Instance.AddTask(t));
                t.Start();
                t.Wait();
                string[] value = null;
                Monitor.Enter(locker);
                foreach (string x in reglist)
                {
                    value = x.Split('|');
                    if (value[1] == current.LocalEndPoint.ToString())
                    {
                        byte[] data = Encoding.ASCII.GetBytes(value[0]);
                        current.Send(data);
                        reglist.Remove(x);
                    }
                }
                Monitor.Exit(locker);
            }
            else if (roger[0].ToLower() == "dateget")
            {
                Task t = new Task(() => { Dispatcher.Invoke(() => { DownloadAllData(name, current.LocalEndPoint.ToString()); }); });
                tasklist.Add(SingletonSecured.Instance.AddTask(t));
                t.Start();
                t.Wait();
                string[] value = null;
                foreach (string x in commits)
                {
                    value = x.Split('|');
                    if (value[1] == current.LocalEndPoint.ToString())
                    {
                        byte[] data = Encoding.ASCII.GetBytes(value[0]);
                        current.Send(data);
                        commits.Remove(x);
                    }
                }
            }
            else if(roger[0].ToLower()=="registry")
            {
                Dispatcher.Invoke(() => { lst_spis.Items.Add(text); });

                FileStream f = new FileStream(name+".txt", FileMode.Append, FileAccess.Write);
                StreamWriter w = new StreamWriter(f);
                string[] reg = null;
                try
                {
                    reg = text.Split('|');
                }
                catch
                {
                    Dispatcher.Invoke(() => { lst_spis.Items.Add("Client reg: " + current.RemoteEndPoint + " error"); });
                }
                for(int i = 1; i< reg.Length; i++)
                {
                    if (i == 1)
                    {
                        w.Write(reg[i]);
                    }
                    else
                    {
                        w.Write("|" + reg[i]);
                    }
                }
                w.Write("\n");
                w.Close();
                f.Close();
            }

            current.BeginReceive(buffer, 0, BUFFER_SIZE, SocketFlags.None, ReceiveCallback_loged, current);
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

        private void DownloadAllData(string l, string a)
        {
            try
            {
                DataTable dt = new DataTable();
                MySqlDataReader read;
                string ask = "SELECT DISTINCT r.Data_Created FROM Registry r JOIN Users u ON r.ID_user=u.ID WHERE u.Login = '" + l + "'";
                MySqlCommand task = new MySqlCommand(ask, msqlcon);
                read = task.ExecuteReader();
                dt.Load(read);
                DataRow dw;
                for (int i = 0; i < dt.Rows.Count; i++)
                {
                    dw = dt.Rows[i];
                    string txt = "";
                    for (int j = 0; j < dw.ItemArray.Count(); j++)
                    {
                        txt += (dw[j].ToString());
                    }
                    lst_spis.Items.Add(txt.Remove((txt.Length - 1), 1));
                    commits.Add(txt.Remove((txt.Length - 1), 1) + "|" + a);
                    txt = "";
                }
                read.Close();
            }
            catch (Exception e)
            {
                MessageBox.Show(e.ToString());
            }
        }

        private void DownloadPersonalizedData(string l, string d, string a)
        {
            try
            {
                DataTable dt = new DataTable();
                MySqlDataReader read;
                string ask = "SELECT r.Key, r.Value_Name, r.ID_Type, r.Value FROM Registry r JOIN Users u ON r.ID_user=u.ID WHERE u.Login = '" + l + "' AND r.Data_Created='"+ d + "'";
                MySqlCommand task = new MySqlCommand(ask, msqlcon);
                read = task.ExecuteReader();
                dt.Load(read);
                DataRow dw;
                Monitor.Enter(locker);
                for (int i = 0; i < dt.Rows.Count; i++)
                {
                    dw = dt.Rows[i];
                    string txt="";
                    for (int j = 0; j < dw.ItemArray.Count(); j++)
                    {
                        txt+= (dw[j].ToString() + "|");
                    }
                    lst_spis.Items.Add(txt.Remove((txt.Length - 1), 1));
                    reglist.Add(txt.Remove((txt.Length - 1), 1) + "|" + a);
                    txt = "";
                }
                Monitor.Exit(locker);
                read.Close();
            }
            catch(Exception e)
            {
                MessageBox.Show(e.ToString());
            }
        }

        private void log_in(string l, string p, string a)
        {
            try
            {
                MySqlDataReader read;
                string ask = "Select count(*) FROM Users WHERE Login ='" + l + "' AND Password='" + p + "'";
                MySqlCommand task = new MySqlCommand(ask, msqlcon);
                read = task.ExecuteReader();
                read.Read();
                if (read.GetInt32(0) == 1)
                {   
                    lst_spis.Items.Add("Pomyślnie zalogowano się na użytkownika: " + l);
                    permits.Add(l+"|"+a);
                }
                else
                {
                    lst_spis.Items.Add("Błędne dane uwierzytelniające z adresu: " + a);                    
                }
                read.Close();
            }
            catch (Exception e)
            {
                MessageBox.Show(e.ToString());  
            }
        }
        
        private void SendToBase(string name)
        {
            try
            {
                FileStream f = new FileStream(name + ".txt", FileMode.Open, FileAccess.Read);
                StreamReader r = new StreamReader(f);
                int id_user=0;
                try
                {
                    MySqlDataReader read;                   
                    string ask = "SELECT ID FROM Users WHERE Login='" + name + "'";
                    MySqlCommand task = new MySqlCommand(ask, msqlcon);
                    read = task.ExecuteReader();
                    read.Read();
                    id_user = read.GetInt32(0);
                    read.Close();
                }
                catch (Exception e)
                {
                    MessageBox.Show(e.ToString());
                }
                finally
                {
                    MessageBox.Show(id_user.ToString());
                }
                string line;
                string[] reg = null;
                while (r.Peek() >= 0)
                {
                    //lst_spis.Items.Add(r.ReadLine());
                    line = r.ReadLine();
                    try
                    {
                        reg = line.Split('|');
                    }
                    catch
                    {
                        Dispatcher.Invoke(() => { lst_spis.Items.Add("Client reg: " + name + " error"); });
                    }
                    try
                    {
                        string ask2 = "INSERT INTO Registry(`ID_user`, `Key`, `Value_Name`, `ID_type`, `Value`, `Data_created`) VALUES (" + id_user + ",'" + reg[0] + "','" + reg[1] + "'," + reg[2] + ",'" + reg[3] + "','" + reg[4] + "')";
                        MySqlCommand task = new MySqlCommand(ask2, msqlcon);
                        task.ExecuteNonQuery();
                    }
                    catch(Exception e)
                    {
                        MessageBox.Show(e.ToString());
                    }

                }

                r.Close();
                f.Close();
                File.Delete(name + ".txt");
            }
            catch (FileNotFoundException e)
            {
                lst_spis.Items.Add("Nie odnaleziono pliku użytkownika!");
            }
            catch (Exception e)
            {
                MessageBox.Show(e.ToString());
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
