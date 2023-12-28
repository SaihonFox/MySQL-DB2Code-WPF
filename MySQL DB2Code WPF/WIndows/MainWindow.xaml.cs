﻿using System.Collections.ObjectModel;
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
		
		var dialog = new SaveFileDialog() { Filter = "SQL File(*.sql)|*.sql"};
		if (dialog.ShowDialog()!.Value)
		{
			var text = $"""
			            create database if not exists `{connection?.Database}`;
			            use `{connection?.Database}`;
			            """;
			await File.WriteAllTextAsync(dialog.FileName, text);
		}
	}

	public async void export_table_ctx_btn_Click(object sender, RoutedEventArgs e)
	{
		if (tablelist_lb.SelectedIndex == -1)
			return;

		var dialog = new SaveFileDialog() { Filter = "SQL File(*.sql)|*.sql" };
		if (!dialog.ShowDialog()!.Value) return;

		var table = (string)tablelist_lb.SelectedValue;
		var text = $"""
		            create database if not exists `{connection!.Database}`;
		            use `{connection!.Database}`;

		            
		            """;
		var sb = new StringBuilder(text);
		sb.Append(await MySqlTable.ExportTable(connection, table));
		/*if (await MySqlTable.ContainsColumns(connection, table))
		{
			var types = new List<string>();

			await using var command = connection!.CreateCommand();
			command.CommandText = $"select * from `{connection.Database}`.`{table}`";
			await using var reader = await command.ExecuteReaderAsync();
			var schema = await reader.GetSchemaTableAsync();
			var columns = new ReadOnlyCollection<DbColumn>(await reader.GetColumnSchemaAsync());

			for (var i = 0; i < schema!.Rows.Count; i++)
				types.Add(reader.GetDataTypeName(i).ToLower());
			await reader.CloseAsync();

			//---------------------------------------------
			var keys = new Dictionary<string, string>();
			await using var command2 = connection!.CreateCommand();
			command2.CommandText = $"show keys from `{connection.Database}`.`{table}`";
			await using var reader2 = await command2.ExecuteReaderAsync();
			while (await reader2.ReadAsync())
				keys.Add(reader2["Column_name"].ToString()!, reader2["Key_name"].ToString()!);
			await reader2.CloseAsync();

			//---------------------------------------------
			var constraints = new Dictionary<string, List<string>>();
			await using var command3 = connection!.CreateCommand();
			command3.CommandText =
				$"select * from information_schema.key_column_usage where constraint_schema = '{connection.Database}' and table_name = '{table}'";
			await using var reader3 = await command3.ExecuteReaderAsync();
			while (await reader3.ReadAsync())
			{
				var i = 0;
				if (i++ >= reader3.FieldCount)
					continue;

				if (!string.IsNullOrEmpty(reader3["REFERENCED_TABLE_SCHEMA"].ToString()))
					constraints.Add(reader3["COLUMN_NAME"].ToString()!,
					[
						reader3["REFERENCED_TABLE_NAME"].ToString()!,
						reader3["REFERENCED_COLUMN_NAME"].ToString()
					]);
			}

			//---------------------------------------------
			sb.Append("(\n");
			for (var i = 0; i < schema!.Rows.Count; i++)
			{
				var row = schema.Rows[i];
				*//*text += $"\t`{row[0]}` {types[i]}({columns[i].ColumnSize!.Value})";
				text += keys.Count(key => key.Key.Equals(row[0]) && key.Value.Equals("PRIMARY")) > 0 ? " primary key" : "";
				text += columns[i].IsAutoIncrement!.Value ? " auto_increment" : "";
				text += columns[i].AllowDBNull!.Value ? "" : " not null";
				text += (i == schema.Rows.Count - 1) && (constraints.Count == 0) ? "" : ",";
				text += "\n";*//*
				sb.Append($"\t`{row[0]}` {types[i]}({columns[i].ColumnSize!.Value})");
				sb.Append(keys.Any(key => key.Key.Equals(row[0]) && key.Value.Equals("PRIMARY"))
					? " primary key"
					: "");
				sb.Append(columns[i].IsAutoIncrement!.Value ? " auto_increment" : "");
				sb.Append(columns[i].AllowDBNull!.Value ? "" : " not null");
				sb.Append((i == schema.Rows.Count - 1) && (constraints.Count == 0) ? "" : ",");
				sb.Append('\n');
			}

			if (constraints.Count > 0)
				sb.Append('\n');
			sb.Append(string.Join(",\n",
				constraints.Select(constraint =>
					$"\tforeign key ({constraint.Key}) references {constraint.Value[0]}({constraint.Value[1]})")));
			if(constraints.Count > 0)
				sb.Append('\n');

			sb.Append(");\n");

		}
		else
			sb.Append(';');*/

		await File.WriteAllTextAsync(dialog.FileName, sb.ToString());
	}
	#endregion

	#region Regex
	[GeneratedRegex("[A-Za-z]+@[A-Za-z]+\\|[A-Za-z0-9]+", RegexOptions.IgnoreCase)]
	public partial Regex MyRegex();
	#endregion

	public async void search_tb_TextChanged(object sender, TextChangedEventArgs e)
	{
		if(string.IsNullOrEmpty(search_tb.Text))
		{
			await m.SetTableList();
			return;
		}

		var dv = (DataView)tablecolumns_lv.ItemsSource;

		if(dv == null)
			return;

		tablecolumns_lv.ItemsSource = dv.Table.Rows.OfType<DataRow>().Where(dr => dr.ItemArray.Any(obj => obj.ToString().Equals(search_tb.Text, StringComparison.InvariantCultureIgnoreCase)));
	}
}