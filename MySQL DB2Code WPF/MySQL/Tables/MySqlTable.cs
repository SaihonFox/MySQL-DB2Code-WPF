using System.Data;
using System.Data.Common;
using System.IO;
using System.Text;
using System.Windows;
using Microsoft.Win32;
using MySql.Data.MySqlClient;
using MySQL_DB2Code_WPF.MySQL.DataBases;

namespace MySQL_DB2Code_WPF.MySQL.Tables;

public static class MySqlTable
{
    static MySqlTable()
    {
		file_types.Add("x-png", "png");
		file_types.Add("pjpeg", "jpeg");
    }

    static async Task Throw(this MySqlConnection connection, string table)
    {
        if (!await connection.PingAsync())
            throw new Exception("connection was not opened");
        ArgumentException.ThrowIfNullOrWhiteSpace(connection.Database);
        ArgumentException.ThrowIfNullOrWhiteSpace(table);
    }

    public static async ValueTask<bool> ContainsColumns(this MySqlConnection connection, string table) =>
	    (await GetColumnsName(connection, table)).Count > 0;

    public static async ValueTask<IReadOnlyList<string>> GetColumnsName(this MySqlConnection connection, string table)
    {
        await Throw(connection, table);

        var list = new List<string>();

		await using var command = connection.CreateCommand();
		command.CommandText = $"select column_name from information_schema.columns where table_schema = '{connection.Database}' and table_name = '{table}'";
		await using var reader = await command.ExecuteReaderAsync();

        while(await reader.ReadAsync())
            list.Add(reader.GetString(0));

        return list;
    }

    public static async ValueTask<IReadOnlyList<MySqlTableKeys>> GetColumnsKeys(this MySqlConnection connection, string table)
    {
        await Throw(connection, table);

        var list = new List<MySqlTableKeys>();

        await using var command = connection!.CreateCommand();
        command.CommandText = $"show keys from `{connection.Database}`.`{table}`";
        await using var reader2 = await command.ExecuteReaderAsync();
        while (await reader2.ReadAsync())
            list.Add(new MySqlTableKeys(reader2));

        return list;
    }

    public static async ValueTask<IReadOnlyList<string>> GetColumnsType(this MySqlConnection connection, string table)
    {
        await Throw(connection, table);

        var list = new List<string>();

        await using var command = connection!.CreateCommand();
        command.CommandText = $"select * from `{connection.Database}`.`{table}`";
        await using var reader = await command.ExecuteReaderAsync();
        var schema = await reader.GetSchemaTableAsync();

        for (int i = 0; i < schema!.Rows.Count; i++)
            list.Add(reader.GetDataTypeName(i).ToLower());

        return list;
	}

    public static async ValueTask<DbDataReader?> GetReader(this MySqlConnection connection, string table)
    {
	    await using var command = connection!.CreateCommand();
	    command.CommandText = $"select * from `{connection.Database}`.`{table}`";
	    return await command.ExecuteReaderAsync();
    }

	public static async ValueTask<DataTable?> GetSchemaTable(this MySqlConnection connection, string table)
    {
	    await using var command = connection!.CreateCommand();
	    command.CommandText = $"select * from `{connection.Database}`.`{table}`";
	    await using var reader = await command.ExecuteReaderAsync();
	    return await reader.GetSchemaTableAsync();
	}

    public static async ValueTask<IReadOnlyList<DbColumn>> GetColumnSchema(this MySqlConnection connection, string table)
    {
	    await using var command = connection!.CreateCommand();
	    command.CommandText = $"select * from `{connection.Database}`.`{table}`";
	    await using var reader = await command.ExecuteReaderAsync();
	    return await reader.GetColumnSchemaAsync();
    }

	public static Action? OnTableDropped;

    public static async ValueTask<int> DropTable(this MySqlConnection connection, string table)
    {
	    await Throw(connection, table);

	    await using var command = connection.CreateCommand();
	    command.CommandText = $"drop table `{connection.Database}`.`{table}`";
	    try
	    {
		    return await command.ExecuteNonQueryAsync();
	    }
	    catch (MySqlException e)
	    {
		    if (e.Number == 3730)
		    {
			    var res = MessageBox.Show(
				    $"Невозможно удалить, т.к. она привязана к другим таблицам: {
					    string.Join(", ", (await MySqlDB.GetConstraints4RefTable(connection, table)).Select(constraint => $"'{constraint.TABLE_SCHEMA}.{constraint.TABLE_NAME}'"))
						}\nВы правда хотите удалить ее?"
				    , "", MessageBoxButton.YesNo);

			    if (res == MessageBoxResult.Yes)
				    await DropAlterTable(connection, table);
		    }

            OnTableDropped?.Invoke();

		    return e.Number;
	    }
    }

    public static async ValueTask<int> DropAlterTable(this MySqlConnection connection, string table)
    {
	    await Throw(connection, table);

	    foreach (var constraint in await MySqlDB.GetConstraints4RefTable(connection, table))
	    {
		    await using var command = connection.CreateCommand();
		    command.CommandText =
			    $"alter table `{constraint.TABLE_SCHEMA}`.`{constraint.TABLE_NAME}` drop foreign key `{constraint.CONSTRAINT_NAME}`";
		    await command.ExecuteNonQueryAsync();
			await command.DisposeAsync();
	    }

		await using var command2 = connection.CreateCommand();
	    command2.CommandText = $"drop table `{connection.Database}`.`{table}`";
        var ret = await command2.ExecuteNonQueryAsync();

        OnTableDropped?.Invoke();

		return ret;
	}

    public static string? save_byteA2folder = null;

    public static async ValueTask<string> ExportTable(this MySqlConnection connection, string table)
    {
	    await Throw(connection, table);

	    var sb = new StringBuilder($"create table `{table}` (\n");

	    var columns = await connection.GetColumnsName(table);
	    var types = await connection.GetColumnsType(table);
	    var keys = await connection.GetColumnsKeys(table);
	    var constraints = await MySqlDB.GetConstraints4Table(connection, table);
	    var column_schema = await connection.GetColumnSchema(table);

	    for (int i = 0; i < columns.Count; i++)
	    {
			sb.Append($"\t`{columns[i]}` {GetColumnType(types[i], column_schema[i])}");

			sb.Append(keys.FirstOrDefault(key => key.Column_name!.Equals(columns[i]))?.Key_name == "PRIMARY" ? " primary key" : "");
			sb.Append(column_schema[i].IsAutoIncrement!.Value ? " auto_increment" : "");
			sb.Append((i == columns.Count - 1) && (constraints.Count == 0) ? string.Empty : ",\n");
	    }

	    if (constraints.Count > 0)
		    sb.AppendLine();

	    sb.AppendLine(string.Join(",\n",
		    constraints.Select(constraint =>
			    $"\tforeign key ({constraint.COLUMN_NAME}) references `{constraint.REFERENCED_TABLE_SCHEMA}`.`{constraint.REFERENCED_TABLE_NAME}`({constraint.REFERENCED_COLUMN_NAME})")));

	    sb.AppendLine(");");

	    await using var command = connection!.CreateCommand();
	    command.CommandText = $"select * from `{connection.Database}`.`{table}`";
	    await using var reader = await command.ExecuteReaderAsync();

	    if (!reader.HasRows)
		    return sb.ToString();

	    sb.AppendLine($"\ninsert into `{table}` values");
		
	    var values_lines = new List<string>();
	    int icolumn = 0;
	    while (await reader!.ReadAsync())
	    {
		    var line = "(";
		    for (int i = 0; i < reader.FieldCount; i++)
		    {
			    var type = column_schema[i].DataType;
			    if (type == typeof(byte[]))
			    {
				    var dialog = new OpenFolderDialog();
				    if (save_byteA2folder != null)
					{

						if (!Directory.Exists($"{save_byteA2folder}/table/"))
							Directory.CreateDirectory($"{save_byteA2folder}/{table}/");

						var mime = FileType.GetMimeFromBytes((byte[])reader.GetValue(i), "image/png");

						var ext = NormalFileType(mime.Split("/")[1]);

						line += $"load_file(\"{save_byteA2folder}/{table}/{icolumn}.{ext}\")";
						await File.WriteAllBytesAsync($"{save_byteA2folder}/{table}/{icolumn + 1}.{ext}", (byte[])reader.GetValue(i));
					}
					else if (dialog.ShowDialog()!.Value)
					{
						save_byteA2folder = dialog.FolderName;

						if (!Directory.Exists($"{save_byteA2folder}/table/"))
							Directory.CreateDirectory($"{save_byteA2folder}/{table}/");

						line += $"load_file(\"{save_byteA2folder}/{table}/{icolumn + 1}.png\")";

						if(reader.GetValue(i) != DBNull.Value)
							await File.WriteAllBytesAsync($"{save_byteA2folder}/{table}/{icolumn + 1}.png", (byte[])reader.GetValue(i));
					}

				    icolumn++;

			    }
			    else if(type == typeof(string) || type == typeof(char))
				    line += $"\"{reader.GetString(i)}\"";
				else if (type == typeof(DateOnly) || type == typeof(DateTime) || type == typeof(TimeOnly))
				{
					if (types[i] == "date")
						line += $"\"{DateOnly.FromDateTime(reader.GetDateTime(i))}\"";
					else if (types[i] == "time")
						line += $"\"{TimeOnly.FromDateTime(reader.GetDateTime(i))}\"";
					else if (types[i] == "datetime")
						line += $"\"{reader.GetDateTime(i)}\"";
					else
						line += $"\"{reader.GetValue(i)}\"";
				}
				else
				    line += reader.GetValue(i);
			    line += i == reader.FieldCount - 1 ? string.Empty : ", ";
		    }
		    line += ")";

			values_lines.Add(line);
	    }
		await reader.CloseAsync();

	    sb.Append(string.Join(",\n", values_lines));
	    sb.AppendLine(";");

	    return sb.ToString();
    }

    static string GetColumnType(string type, DbColumn column_schema)
    {
	    if (column_schema.DataType == typeof(string))
		    return $"{type}({column_schema.ColumnSize})";

	    return type;
    }


	
	static Dictionary<string, string> file_types = new Dictionary<string, string>();

	static string NormalFileType(string type)
	{
		if (file_types.ContainsKey(type))
			return file_types[type];
		return type;
	}
}