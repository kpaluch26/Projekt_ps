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

namespace Projekt_ps
{
    public partial class MainWindow : Window
    {
        private MySqlConnection msqlcon = new MySqlConnection("Server=remotemysql.com;Port=3306;Database=DTzose51Js;Uid=DTzose51Js;pwd=ER2VaqmwbB;");
        private int users_counter=0;

        public MainWindow()
        {
            InitializeComponent();
            DatabaseConnection();
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

        private void addUser()
        {
            try
            {
                string ask = "INSERT INTO Users(ID,Login,Password) VALUES (" + (users_counter + 1) +  ",'kot','pies')";
                MySqlCommand task = new MySqlCommand(ask, msqlcon);
                task.ExecuteNonQuery();
                users_counter++;
                lbl_all_users.Content = users_counter.ToString();
            }
            catch
            {
                MessageBox.Show("Taki użytkownik już istnieje!", "Błędny login!");
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
                MessageBox.Show("Nie udało się połączyć z bazą danych", "Błąd");
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

    }
}
