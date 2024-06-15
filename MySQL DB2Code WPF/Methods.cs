using System.Data;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using MySql.Data.MySqlClient;

using MySQL_DB2Code_WPF.Windows;

namespace MySQL_DB2Code_WPF;

class Methods
{
	private MainWindow mWindow;

	public MySqlConnection connection;

	public Methods(MainWindow mWindow)
	{
		this.mWindow = mWindow;
		connection = mWindow.connection!;
	}

	public async Task<List<string>> GetDBList(MySqlConnection con)
	{
		if (connection!.State != ConnectionState.Open)
			return [];

		var list = new List<string>();

		await using var command = con.CreateCommand();
		command.CommandText = "show databases";
		await using var reader = await command.ExecuteReaderAsync();
		//MessageBox.Show(reader.FieldCount.ToString());
		while (await reader.ReadAsync())
		{
			for(int i = 0; i < reader.FieldCount; i++)
				list.Add(reader.GetString(i));
		}

		await reader.CloseAsync();

		return list;
	}

	public async void Init()
	{
		if (!File.Exists("user@server list.txt"))
			await File.WriteAllTextAsync("user@server list.txt", "");

		mWindow.Update_UserServer_Data();

		if (!await mWindow.Connect2DB())
			return;
		if(connection!.State != ConnectionState.Open)
			return;

		mWindow.Update_DBList_Data();
	}

	public async Task AddUserServer(string user = "root", string server = "localhost", string password = "")
	{
		ArgumentException.ThrowIfNullOrEmpty(user);

		HashSet<string> hashset = [..await File.ReadAllLinesAsync("user@server list.txt"), $"{user}@{server}|{password}" ];
		await File.WriteAllLinesAsync("user@server list.txt", hashset);
		mWindow.Update_UserServer_Data();
	}

	public async Task<List<string>> GetUserServer()
	{
		if (!File.Exists("user@server list.txt"))
			await File.WriteAllTextAsync("user@server list.txt", "");
		var lines = await File.ReadAllLinesAsync("user@server list.txt");
		return lines.Where(l => mWindow.MyRegex().IsMatch(l)).ToList();
	}

	public async Task<int> AddDB(string dbname)
	{
		ArgumentNullException.ThrowIfNull(connection);

		await using var command = connection.CreateCommand();
		command.CommandText = $"create database if not exists `{dbname}`;";
		return await command.ExecuteNonQueryAsync();
	}

	public async Task<int> DropDB(string dbname)
	{
		ArgumentNullException.ThrowIfNull(connection);

		await using var command = connection.CreateCommand();
		command.CommandText = $"drop database if exists `{dbname}`";
		int ret = await command.ExecuteNonQueryAsync();
		mWindow.Update_DBList_Data();
		return ret;
	}

	public async Task<int> DropTable(string table)
	{
		ArgumentNullException.ThrowIfNull(connection);

		await using var command = connection.CreateCommand();
		command.CommandText = $"drop table `{connection.Database}`.`{table}`";
		try
		{
			int ret = await command.ExecuteNonQueryAsync();
			mWindow.Update_TableList_Data();
			return ret;
		}
		catch (MySqlException e)
		{
			if(e.Number == 3730)
				MessageBox.Show("Не удается удалить таблицу потому что она привазана к другой таблице");
			return e.Number;
		}
	}

	public async Task<List<string>> GetTableColumns(string table)
	{
		var list = new List<string>();

		await using var command = connection!.CreateCommand();
		command.CommandText = $"select * from `{connection.Database}`.`{table}`";
		await using var reader = await command.ExecuteReaderAsync();
		var schema = await reader.GetSchemaTableAsync();

		foreach(DataRow row in schema!.Rows)
			list.Add(row[0].ToString()!);

		return list;
	}

	public async Task<List<List<string>>> GetTableData(string table)
	{
		var list = new List<List<string>>();

		await using var command = connection!.CreateCommand();
		command.CommandText = $"select * from `{connection.Database}`.`{table}`";
		await using var reader = await command.ExecuteReaderAsync();

		while (await reader.ReadAsync())
		{
			var list2 = new List<string>();
			for(int i = 0; i < reader.FieldCount; i++)
				list2.Add(reader.GetValue(i).ToString()!);
			list.Add(list2);

			for (int i = 0; i < reader.FieldCount; i++)
			{
				
			}
		}

		return list;
	}

	public async Task<IDictionary<string, string>> GetTableKeyValue(string table)
	{
		var dict = new Dictionary<string, string>();

		await using var command = connection!.CreateCommand();
		command.CommandText = $"select * from `{connection.Database}`.`{table}`";
		await using var reader = await command.ExecuteReaderAsync();
		var schema = await reader.GetSchemaTableAsync();

		foreach (DataRow row in schema!.Rows)
		{
			MessageBox.Show(row[0].ToString()!);
		}

		return dict;
	}

	public async Task<List<List<string>>> GetConstraints()
	{
		var list = new List<List<string>>();

		var str = $"""
		          SELECT kcu.constraint_schema
		               , kcu.constraint_name
		               , kcu.referenced_table_name
		               , kcu.referenced_column_name
		               , kcu.table_name
		               , kcu.column_name
		               , refcol.column_type referenced_column_type
		               , childcol.column_type
		               , refcol.is_nullable referenced_is_nullable
		               , childcol.is_nullable
		          FROM information_schema.key_column_usage kcu
		          INNER JOIN information_schema.columns refcol
		                  ON refcol.table_schema = kcu.referenced_table_schema
		                 AND refcol.table_name   = kcu.referenced_table_name
		                 AND refcol.column_name  = kcu.referenced_column_name
		          INNER JOIN information_schema.columns childcol
		                  ON childcol.table_schema = kcu.table_schema
		                 AND childcol.table_name   = kcu.table_name
		                 AND childcol.column_name  = kcu.column_name
		          WHERE (
		                  refcol.is_nullable <> childcol.is_nullable
		                OR
		                  refcol.column_type <> childcol.column_type
		                )
		          AND kcu.TABLE_SCHEMA = '{connection!.Database}';
		          """;

		await using var command = connection!.CreateCommand();
		command.CommandText = str;
		await using var reader = await command.ExecuteReaderAsync();

		while (await reader.ReadAsync())
		{
			var l = new List<string>();
			for(int i = 0; i < reader.FieldCount; i++)
				l.Add(reader.GetString(i));
			list.Add(l);
		}

		return list;
	}

	public async Task SetTableList()
	{
		var table = (string)mWindow.tablelist_lb.SelectedItem;
		var headers = await GetTableColumns(mWindow.selected_table!);

		{
			var gh1 = new GridView();
			foreach (var t in headers)
			{
				var gvc = new GridViewColumn { Header = t };
				gh1.Columns.Add(gvc);
			}

			mWindow.tablecolumns_lv.View = gh1;
		}

		foreach (var row in await GetTableData(table))
		{
			var gv = new GridView();
			for (int i = 0; i < row.Count; i++)
			{
				var gvc = new GridViewColumn
				{
					Header = headers[i],
					DisplayMemberBinding = new Binding(headers[i])
				};
				gv.Columns.Add(gvc);
			}

			mWindow.tablecolumns_lv.View = gv;
			break;
		}

		var dt = new DataTable();
		await using var command = connection.CreateCommand();
		command.CommandText = $"select * from `{connection!.Database}`.`{table}`";
		await using var reader = await command.ExecuteReaderAsync();
		dt.Load(reader);
		mWindow.tablecolumns_lv.ItemsSource = dt.DefaultView;
	}
}