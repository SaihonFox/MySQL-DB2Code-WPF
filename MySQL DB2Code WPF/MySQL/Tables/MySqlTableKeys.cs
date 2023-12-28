using System.Data;

namespace MySQL_DB2Code_WPF.MySQL.Tables;

internal class MySqlTableKeys(IDataRecord reader)
{
    public string? Table { get; init; } = (string)reader[nameof(Table)];
    public bool? Non_unique { get; init; } = bool.Parse(reader[nameof(Non_unique)].ToString()!.Equals("1") ? "true" : "false");
    public string? Key_name { get; init; } = (string)reader[nameof(Key_name)];
    public uint? Seq_in_index { get; init; } = uint.Parse(reader[nameof(Seq_in_index)].ToString()!);
    public string? Column_name { get; init; } = (string)reader[nameof(Column_name)];
    public string? Collation { get; init; } = (string)reader[nameof(Collation)];
    public long? Cardinality { get; init; } = long.Parse(reader[nameof(Cardinality)].ToString()!);
    public object? Sub_part { get; init; } = reader[nameof(Sub_part)];
    public object? Packed { get; init; } = reader[nameof(Packed)];
    public string? Null { get; init; } = (string)reader[nameof(Null)];
    public string? Index_type { get; init; } = (string)reader[nameof(Index_type)];
    public string? Comment { get; init; } = (string)reader[nameof(Comment)];
    public object? Index_comment { get; init; } = reader[nameof(Index_comment)];
    public string? Visible { get; init; } = (string)reader[nameof(Visible)];
    public object? Expression { get; init; } = reader[nameof(Expression)];

    public override string ToString() =>
	    $"{{Table={Table}, Non_unique={Non_unique}, Key_name={Key_name}, Seq_in_index={Seq_in_index}, Column_name={Column_name}, Collation={Collation}, Cardinality={Cardinality}, Sub_part={Sub_part}, Packed={Packed}, Null={Null}, Index_type={Index_type}, Comment={Comment}, Index_comment={Index_comment}, Visible={Visible}, Expression={Expression}}}";
}