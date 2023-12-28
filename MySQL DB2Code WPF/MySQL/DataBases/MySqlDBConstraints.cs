using System.Data;

namespace MySQL_DB2Code_WPF.MySQL.DataBases;

internal class MySqlDBConstraints(IDataRecord reader)
{
	public string? TABLE_SCHEMA { get; init; } = (string)reader[nameof(TABLE_SCHEMA)];
	public string? CONSTRAINT_NAME { get; init; } = (string)reader[nameof(CONSTRAINT_NAME)];
	public string? TABLE_NAME { get; init; } = (string)reader[nameof(TABLE_NAME)];
	public string? COLUMN_NAME { get; init; } = (string)reader[nameof(COLUMN_NAME)];
	public string? REFERENCED_TABLE_SCHEMA { get; init; } = (string)reader[nameof(REFERENCED_TABLE_SCHEMA)];
	public string? REFERENCED_TABLE_NAME { get; init; } = (string)reader[nameof(REFERENCED_TABLE_NAME)];
	public string? REFERENCED_COLUMN_NAME { get; init; } = (string)reader[nameof(REFERENCED_COLUMN_NAME)];

	public override string ToString() =>
		$"{{TABLE_SCHEMA={TABLE_SCHEMA}, CONSTRAINT_NAME={CONSTRAINT_NAME}, TABLE_NAME={TABLE_NAME}, COLUMN_NAME={COLUMN_NAME}, REFERENCED_TABLE_SCHEMA={REFERENCED_TABLE_SCHEMA}, REFERENCED_TABLE_NAME={REFERENCED_TABLE_NAME}, REFERENCED_COLUMN_NAME={REFERENCED_COLUMN_NAME}}}";
}