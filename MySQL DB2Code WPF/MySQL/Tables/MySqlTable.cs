using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using MySql.Data.MySqlClient;
using MySQL_DB2Code_WPF.MySQL.DataBases;
using MySQL_DB2Code_WPF.Windows;

namespace MySQL_DB2Code_WPF.MySQL.Tables;

class MySqlTable
{
    static async Task Throw(MySqlConnection connection, string table)
    {
        if (!await connection.PingAsync())
            throw new Exception("connection was not opened");
        ArgumentException.ThrowIfNullOrWhiteSpace(connection.Database);
        ArgumentException.ThrowIfNullOrWhiteSpace(table);
    }

    public static async Task<bool> ContainsColumns(MySqlConnection connection, string table) =>
	    (await GetColumnsName(connection, table)).Count > 0;

    public static async Task<IReadOnlyList<string>> GetColumnsName(MySqlConnection connection, string table)
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

    public static async Task<IReadOnlyList<MySqlTableKeys>> GetColumnsKeys(MySqlConnection connection, string table)
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

    public static async Task<IReadOnlyList<string>> GetColumnsType(MySqlConnection connection, string table)
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

    public static async Task<DbDataReader?> GetReader(MySqlConnection connection, string table)
    {
	    await using var command = connection!.CreateCommand();
	    command.CommandText = $"select * from `{connection.Database}`.`{table}`";
	    return await command.ExecuteReaderAsync();
    }

	public static async Task<DataTable?> GetSchemaTable(MySqlConnection connection, string table)
    {
	    await using var command = connection!.CreateCommand();
	    command.CommandText = $"select * from `{connection.Database}`.`{table}`";
	    await using var reader = await command.ExecuteReaderAsync();
	    return await reader.GetSchemaTableAsync();
	}

    public static async Task<IReadOnlyList<DbColumn>> GetColumnSchema(MySqlConnection connection, string table)
    {
	    await using var command = connection!.CreateCommand();
	    command.CommandText = $"select * from `{connection.Database}`.`{table}`";
	    await using var reader = await command.ExecuteReaderAsync();
	    return await reader.GetColumnSchemaAsync();
    }

	public static Action? OnTableDropped;

    public static async Task<int> DropTable(MySqlConnection connection, string table)
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

    public static async Task<int> DropAlterTable(MySqlConnection connection, string table)
    {
	    await Throw(connection, table);

	    foreach (var constraint in await MySqlDB.GetConstraints4RefTable(connection, table))
	    {
		    await using var command = connection.CreateCommand();
		    command.CommandText =
			    $"alter table `{constraint.TABLE_SCHEMA}`.`{constraint.TABLE_NAME}` drop foreign key `{constraint.CONSTRAINT_NAME}`";
		    await command.ExecuteNonQueryAsync();
	    }

		await using var command2 = connection.CreateCommand();
	    command2.CommandText = $"drop table `{connection.Database}`.`{table}`";
        var ret = await command2.ExecuteNonQueryAsync();

        OnTableDropped?.Invoke();

		return ret;
	}

    private static string? save_byteA2folder = null;

    public static async Task<string> ExportTable(MySqlConnection connection, string table)
    {
	    await Throw(connection, table);

	    var sb = new StringBuilder($"create table `{table}` (\n");

	    var columns = await GetColumnsName(connection, table);
	    var types = await GetColumnsType(connection, table);
	    var keys = await GetColumnsKeys(connection, table);
	    var constraints = await MySqlDB.GetConstraints4Table(connection, table);
	    var column_schema = await GetColumnSchema(connection, table);

	    for (int i = 0; i < columns.Count; i++)
	    {
			//sb.Append($"\t`{columns[i]}` {types[i]}({column_schema[i].ColumnSize})");
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
				    //var dialog = new SaveFileDialog() { Filter = "Все файлы(*.*)|*.*" };
				    if (save_byteA2folder != null)
				    {
					    line += $"load_file(\"{save_byteA2folder}/{connection.Database}_{table}_{icolumn}.png\")";

					    await File.WriteAllBytesAsync($"{save_byteA2folder}/{connection.Database}_{table}_{icolumn}.png", (byte[])reader.GetValue(i));
					}
					else if (dialog.ShowDialog()!.Value)
					{
						save_byteA2folder = dialog.FolderName;

						line += $"load_file(\"{save_byteA2folder}/{connection.Database}_{table}_{icolumn}.png\")";

					    await File.WriteAllBytesAsync($"{save_byteA2folder}/{connection.Database}_{table}_{icolumn}.png", (byte[])reader.GetValue(i));

						//line += $"load_file(\"{dialog.FileName}\")";
						//$"'{Encoding.Default.GetString((byte[]) reader.GetValue(i))}'";

						//await File.WriteAllBytesAsync(dialog.FileName, (byte[])reader.GetValue(i));
					}

				    icolumn++;

			    }
			    else if(type == typeof(string) || type == typeof(char))
				    line += $"\"{reader.GetString(i)}\"";
				else
				    line += reader.GetValue(i);
			    line += i == reader.FieldCount - 1 ? string.Empty : ", ";
		    }
		    line += ")";

			values_lines.Add(line);
	    }

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
}