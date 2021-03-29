using System.Data;
using System.Data.SqlClient;

namespace FlexGenDB
{
    public static class SQLInterface
    {
        private static SqlConnection sqlConnection;


        public static DataTable ExecuteQueryIntoDataTable(string query)
        {
            Logger.LogVerbose($"Executing query {query}");
            var table = ExecuteQueryIntoDataTableNoLog(query);
            Logger.LogVerbose($"{table.Rows.Count} rows returned");
            return table;
        }


        public static DataTable ExecuteQueryIntoDataTableNoLog(string query)
        {
            var table = new DataTable();
            using (var command = BuildNewSqlCommand(query))
            using (var adapter = new SqlDataAdapter(command))
                adapter.Fill(table);

            return table;
        }


        public static void ExecuteNonQuery(string statement)
        {
            Logger.LogVerbose($"Executing statement {statement}");
            using (var command = BuildNewSqlCommand(statement))
                command.ExecuteNonQuery();
        }


        private static void Initialize()
        {
            string connectionString = SessionConfiguration.ConnectionString;
            sqlConnection = new SqlConnection(connectionString);
            sqlConnection.Open();
        }


        private static SqlCommand BuildNewSqlCommand(string statement)
        {
            if (sqlConnection == null)
                Initialize();

            return new SqlCommand(statement, sqlConnection);
        }
    }
}
