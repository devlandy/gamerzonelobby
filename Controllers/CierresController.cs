using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using GamerZoneAPI.Data;
using GamerZoneAPI.Models;

namespace GamerZoneAPI.Controllers
{
    [ApiController]
    [Route("api/cierres")]
    public class CierresController : ControllerBase
    {
        private Conexion conexion =
        new Conexion();

        // ======================
        // RESUMEN
        // ======================
        [HttpGet("resumen")]
        public IActionResult Resumen()
        {
            using (var conn =
            conexion.GetConnection())
            {
                conn.Open();

                decimal ventas = 0;
                decimal gastos = 0;

                string ventasQuery = @"

SELECT IFNULL(SUM(total),0)

FROM ventas

WHERE estado='PAGADO'
";

                MySqlCommand cmdVentas =
                new MySqlCommand(
                ventasQuery, conn);

                ventas = Convert.ToDecimal(
                cmdVentas.ExecuteScalar());

                string gastosQuery = @"

SELECT IFNULL(SUM(monto),0)

FROM gastos
";

                MySqlCommand cmdGastos =
                new MySqlCommand(
                gastosQuery, conn);

                gastos = Convert.ToDecimal(
                cmdGastos.ExecuteScalar());

                return Ok(new
                {
                    ventas,
                    gastos,
                    balance =
                    ventas - gastos
                });
            }
        }

        // ======================
        // REGISTRAR
        // ======================
        [HttpPost]
        public IActionResult Registrar(
        [FromBody]
        CierreRequest request)
        {
            using (var conn =
            conexion.GetConnection())
            {
                conn.Open();

                decimal ventas = 0;
                decimal gastos = 0;

                string ventasQuery = @"

SELECT IFNULL(SUM(total),0)

FROM ventas

WHERE estado='PAGADO'
";

                MySqlCommand cmdVentas =
                new MySqlCommand(
                ventasQuery, conn);

                ventas = Convert.ToDecimal(
                cmdVentas.ExecuteScalar());

                string gastosQuery = @"

SELECT IFNULL(SUM(monto),0)

FROM gastos
";

                MySqlCommand cmdGastos =
                new MySqlCommand(
                gastosQuery, conn);

                gastos = Convert.ToDecimal(
                cmdGastos.ExecuteScalar());

                decimal balance =
                ventas - gastos;

                string query = @"

INSERT INTO cierre_diario
(
total_ventas,
total_gastos,
balance,
estado,
fecha,
id_usuario,
observacion
)

VALUES
(
@ventas,
@gastos,
@balance,
'CERRADO',
NOW(),
@usuario,
@observacion
)";
                MySqlCommand cmd =
                new MySqlCommand(
                query, conn);

                cmd.Parameters.AddWithValue(
                "@ventas", ventas);

                cmd.Parameters.AddWithValue(
                "@gastos", gastos);

                cmd.Parameters.AddWithValue(
                "@balance", balance);

                cmd.Parameters.AddWithValue(
                "@usuario",
                request.id_usuario);

                cmd.Parameters.AddWithValue(
                "@observacion",
                request.observacion);

                cmd.ExecuteNonQuery();

                return Ok(new
                {
                    mensaje =
                    "Cierre registrado"
                });
            }
        }

        // ======================
        // HISTORIAL
        // ======================
        [HttpGet]
        public IActionResult Historial()
        {
            using (var conn =
            conexion.GetConnection())
            {
                conn.Open();

                string query = @"

SELECT
c.*,
u.nombre as usuario

FROM cierre_diario c

JOIN usuarios u
ON c.id_usuario =
u.id_usuario

ORDER BY c.fecha DESC
";

                MySqlCommand cmd =
                new MySqlCommand(
                query, conn);

                var reader =
                cmd.ExecuteReader();

                List<object> lista =
                new List<object>();

                while (reader.Read())
                {
                    lista.Add(new
                    {
                        total_ventas =
                        reader["total_ventas"],

                        total_gastos =
                        reader["total_gastos"],

                        balance =
                        reader["balance"],

                        fecha =
                        reader["fecha"],

                        usuario =
                        reader["usuario"],

                        observacion =
                        reader["observacion"]
                    });
                }

                return Ok(lista);
            }
        }
    }
}