using MySql.Data.MySqlClient;

namespace GamerZoneAPI.Data
{
    public class Conexion
    {
        private string connectionString = "server=localhost;database=gamer_zone_control;user=root;password=Landy23;";

        public MySqlConnection GetConnection()
        {
            return new MySqlConnection(connectionString);
        }
    }
}