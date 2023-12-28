using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;

namespace MySQL_DB2Code_WPF.Exports;

class ExportTable
{
	private MySqlConnection connection;

	public ExportTable(MySqlConnection connection)
	{
		this.connection = connection;
	}

	async Task<string> Export()
	{
		var types = new List<string>();

		return string.Empty;
	}
}