using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Win32;
using MySql.Data.MySqlClient;

using MySQL_DB2Code_WPF.Dialogs;
using MySQL_DB2Code_WPF.MySQL.DataBases;
using MySQL_DB2Code_WPF.MySQL.Tables;

namespace MySQL_DB2Code_WPF.Windows;

public partial class MainWindow : Window
{
	public MySqlConnection? connection = null;

	private string? server;
	private string? user;
	private string password;

	public string? selected_table = null;

	private Methods m;

	#region Constructor
	public MainWindow()
	{
		InitializeComponent();

		m = new(this);

		Update_UserServer_Data();

		MySqlTable.OnTableDropped += Update_TableList_Data;
		MySqlDB.OnDBDropped += Update_DBList_Data;

		drop_db_ctx_btn.Click += async (s, e) => await MySqlDB.DropDB(connection!);
		drop_table_ctx_btn.Click += async (s, e) => await MySqlTable.DropTable(connection!, (string)tablelist_lb.SelectedValue);
	}
	#endregion

	public async Task<bool> Connect2DB()
	{
		try
		{
			connection = new MySqlConnection($"server={server ?? "localhost"};uid={user ?? "root"};password={password};");
			m = new(this);
			await connection.OpenAsync();
			await m.GetDBList(connection);
			return true;
		} catch(MySqlException ex)
		{
			MessageBox.Show(ex.InnerException!.Message);
			if (ex.Number == (int)MySqlErrorCode.AccessDenied)
				MessageBox.Show("Некорректное имя пользователя или пароль");
			else if (ex.Number == (int)MySqlErrorCode.UnknownDatabase)
				MessageBox.Show($"Неизвестное название БД - \"{connection!.Database}\"");
			foreach (var data in ex.Data)
				MessageBox.Show(data.ToString());

			return false;
		}
	}

	#region UpdateData
	public async void Update_DBList_Data() =>
		dblist_lb.ItemsSource = await MySqlDB.GetDBList(connection!);

	public async void Update_UserServer_Data() =>
		user0server_cb.ItemsSource = (await m.GetUserServer()).Select(us => us.Split('|')[0]);

	public async void Update_TableList_Data() =>
		tablelist_lb.ItemsSource = await MySqlDB.GetTables(connection!);
	#endregion

	#region XAML Defined Methods
	async void add_user0server_btn_Click(object sender, RoutedEventArgs e)
	{
		var dialog = new AddUserServerDialog();
		if (dialog.ShowDialog()!.Value)
			await m.AddUserServer(user = dialog.user, server = dialog.server, password = dialog.password);

		await m.GetUserServer();
		Update_UserServer_Data();
	}

	async void dblist_lb_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (dblist_lb.SelectedIndex == -1)
		{
			tablecolumns_lv.Items.Clear();
			tablelist_lb.ItemsSource = null;
			return;
		}

		await connection!.ChangeDatabaseAsync((string)dblist_lb.SelectedItem);
		Update_TableList_Data();
	}

	async void tablelist_lb_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (tablelist_lb.SelectedIndex == -1)
		{
			tablecolumns_lv.ItemsSource = null;
			return;
		}

		tablename_tb.Text = selected_table = (string) tablelist_lb.SelectedValue;
		await m.GetConstraints();
		await m.SetTableList();
	}

	async void user0server_cb_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if(user0server_cb.SelectedIndex == -1)
			return;

		var str = (await m.GetUserServer())[user0server_cb.SelectedIndex];
		var splitted = str.Split('|');
		user = splitted[0].Split('@')[0];
		server = splitted[0].Split('@')[1];
		password = splitted[1];

		await Connect2DB();
		Update_DBList_Data();
	}

	void update_btn_Click(object sender, RoutedEventArgs e)
	{
		Update_DBList_Data();
		Update_TableList_Data();
	}

	public async void export_db_ctx_btn_Click(object sender, RoutedEventArgs e)
	{
		if(dblist_lb.SelectedIndex == -1)
			return;

		MySqlTable.save_byteA2folder = null;
		var dialog = new SaveFileDialog() { Filter = "SQL File(*.sql)|*.sql"};
		if (dialog.ShowDialog()!.Value)
		{
			var text = $"""
			            create database if not exists `{connection?.Database}`;
			            use `{connection?.Database}\n\n`;
			            """;

			var sb = new StringBuilder(text);
			foreach (var table in await MySqlDB.GetTables(connection!))
			{
				sb.AppendLine(await MySqlTable.ExportTable(connection!, table));
			}

			await File.WriteAllTextAsync(dialog.FileName, sb.ToString());
			MessageBox.Show("Успешно экспортировано");
		}
	}

	public async void export_table_ctx_btn_Click(object sender, RoutedEventArgs e)
	{
		if (tablelist_lb.SelectedIndex == -1)
			return;

		MySqlTable.save_byteA2folder = null;
		var dialog = new SaveFileDialog() { Filter = "SQL File(*.sql)|*.sql" };
		if (!dialog.ShowDialog()!.Value) return;

		var table = (string)tablelist_lb.SelectedValue;
		var text = $"""
		            create database if not exists `{connection!.Database}`;
		            use `{connection!.Database}`;

		            
		            """;
		var sb = new StringBuilder(text);
		sb.Append(await MySqlTable.ExportTable(connection, table));

		await File.WriteAllTextAsync(dialog.FileName, sb.ToString());
		MessageBox.Show("Успешно экспортировано");
	}
	#endregion

	#region Regex
	[GeneratedRegex("[A-Za-z]+@[A-Za-z]+\\|[A-Za-z0-9]+", RegexOptions.IgnoreCase)]
	public partial Regex MyRegex();
	#endregion
}