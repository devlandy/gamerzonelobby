using MySql.Data.MySqlClient;

namespace GamerZoneAPI.Data
{
    public class DbManager
    {
        private readonly string _connectionString;

        public DbManager(IConfiguration config)
        {
            _connectionString = config.GetConnectionString("Default")
                ?? throw new InvalidOperationException("Connection string 'Default' not found.");
        }

        public MySqlConnection GetConnection() => new MySqlConnection(_connectionString);

        public int ExecuteNonQuery(string sql, params MySqlParameter[] parameters)
        {
            using var conn = GetConnection();
            conn.Open();
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddRange(parameters);
            return cmd.ExecuteNonQuery();
        }

        public object? ExecuteScalar(string sql, params MySqlParameter[] parameters)
        {
            using var conn = GetConnection();
            conn.Open();
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddRange(parameters);
            return cmd.ExecuteScalar();
        }

        public List<Dictionary<string, object>> ExecuteQuery(string sql, params MySqlParameter[] parameters)
        {
            using var conn = GetConnection();
            conn.Open();
            using var cmd = new MySqlCommand(sql, conn);
            cmd.Parameters.AddRange(parameters);
            using var reader = cmd.ExecuteReader();

            var results = new List<Dictionary<string, object>>();
            while (reader.Read())
            {
                var row = new Dictionary<string, object>();
                for (int i = 0; i < reader.FieldCount; i++)
                    row[reader.GetName(i)] = reader.GetValue(i);
                results.Add(row);
            }
            return results;
        }
    }
}
