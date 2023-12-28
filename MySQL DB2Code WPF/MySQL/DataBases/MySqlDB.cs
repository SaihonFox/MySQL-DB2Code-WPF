using System.Data;
using System.Windows;
using MySql.Data.MySqlClient;

namespace MySQL_DB2Code_WPF.MySQL.DataBases;

class MySqlDB
{
	static async Task Throw(MySqlConnection connection, string table)
	{
		if (connection.State != ConnectionState.Open)
			throw new Exception("connection was not opened");
		ArgumentException.ThrowIfNullOrWhiteSpace(connection.Database);
		ArgumentException.ThrowIfNullOrWhiteSpace(table);
	}

	public static async Task<IReadOnlyList<MySqlDBConstraints>> GetConstraints(MySqlConnection connection)
	{

		var str = $"""
		           select kcu.table_schema, kcu.constraint_name, kcu.table_name, kcu.column_name, kcu.referenced_table_schema, kcu.referenced_table_name, kcu.referenced_column_name from information_schema.key_column_usage kcu
		           inner join information_schema.columns refcol
		           on refcol.table_schema = kcu.referenced_table_schema
		           	and refcol.table_name   = kcu.referenced_table_name
		               and refcol.column_name  = kcu.referenced_column_name
		           inner join information_schema.columns childcol
		           	on childcol.table_schema = kcu.table_schema
		           	and childcol.table_name   = kcu.table_name
		           	and childcol.column_name  = kcu.column_name
		           where (refcol.is_nullable <> childcol.is_nullable or refcol.column_type <> childcol.column_type)
		           and kcu.table_schema = '{connection.Database}';
		           """;

		var list = new List<MySqlDBConstraints>();

		await using var command = connection!.CreateCommand();
		command.CommandText = str;
		await using var reader = await command.ExecuteReaderAsync();

		while(await reader.ReadAsync())
			list.Add(new MySqlDBConstraints(reader));

		await reader.CloseAsync();

		return list;
	}

	public static async Task<IReadOnlyList<MySqlDBConstraints>> GetConstraints4Table(MySqlConnection connection, string table) =>
		(await GetConstraints(connection)).Where(msqldbc => msqldbc.TABLE_NAME!.Equals(table)).ToList();

	public static async Task<IReadOnlyList<MySqlDBConstraints>> GetConstraints4RefTable(MySqlConnection connection, string table) =>
		(await GetConstraints(connection)).Where(msqldbc => msqldbc.REFERENCED_TABLE_NAME!.Equals(table)).ToList();

	public static async Task<IReadOnlyList<string>> GetDBList(MySqlConnection connection)
	{
		if (connection!.State != ConnectionState.Open)
			return [];

		var list = new List<string>();

		await using var command = connection.CreateCommand();
		command.CommandText = "show databases";
		await using var reader = await command.ExecuteReaderAsync();
		while (await reader.ReadAsync())
		{
			for (var i = 0; i < reader.FieldCount; i++)
				list.Add(reader.GetString(i));
		}

		await reader.CloseAsync();

		return list;
	}

	public static async Task<IReadOnlyList<string>> GetTables(MySqlConnection connection)
	{
		var list = new List<string>();

		await using(var command = connection!.CreateCommand())
		{
			command.CommandText = $"show tables from `{connection.Database}`";
			try
			{
				await using var reader = await command.ExecuteReaderAsync();
				while (await reader.ReadAsync())
					list.Add(reader.GetString(0));

				reader.Close();
				reader.Dispose();
			} catch {}
		}

		return list;
	}

	public static Action OnDBDropped;

	public static async Task<int> DropDB(MySqlConnection connection)
	{
		await using var command = connection!.CreateCommand();
		command.CommandText = $"drop database {connection.Database}";
		var ret = await command.ExecuteNonQueryAsync();

		OnDBDropped?.Invoke();

		return ret;
	}
}