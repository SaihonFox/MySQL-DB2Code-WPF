using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace MySQL_DB2Code_WPF.Dialogs;

public partial class AddUserServerDialog : Window
{
	public string user = "root";
	public string server = "localhost";
	public string password = "";

	public AddUserServerDialog()
	{
		InitializeComponent();
	}

	void add_btn_Click(object sender, RoutedEventArgs e)
	{
		if (new[] { user_tb.Text, server_tb.Text, password_tb.Text }.Any(string.IsNullOrEmpty))
		{
			DialogResult = false;
			Close();
			return;
		}

		user = user_tb.Text;
		server = server_tb.Text;
		password = password_tb.Text;

		DialogResult = true;
		Close();
	}
}