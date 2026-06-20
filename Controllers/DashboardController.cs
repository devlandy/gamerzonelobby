using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using GamerZoneAPI.Data;

namespace GamerZoneAPI.Controllers
{
    [ApiController]
    [Route("api/dashboard")]
    public class DashboardController : ControllerBase
    {
        private Conexion conexion = new Conexion();

        [HttpGet]
        public IActionResult ObtenerDashboard()
        {
            using (var conn = conexion.GetConnection())
            {
                conn.Open();

                // =============================
                // VENTAS DEL DÍA
                // =============================
                string ventasQuery = @"
                SELECT IFNULL(SUM(total),0) 
                FROM ventas 
                WHERE DATE(fecha) = CURDATE()";

                MySqlCommand cmdVentas = new MySqlCommand(ventasQuery, conn);
                decimal ventasDia = Convert.ToDecimal(cmdVentas.ExecuteScalar());

                // =============================
                // PEDIDOS PENDIENTES
                // =============================
                string pendientesQuery = @"
                SELECT COUNT(*) 
                FROM ventas 
                WHERE forma_cobro = 'PENDIENTE'";

                MySqlCommand cmdPendientes = new MySqlCommand(pendientesQuery, conn);
                int pendientes = Convert.ToInt32(cmdPendientes.ExecuteScalar());

                // =============================
                // PRODUCTOS AGOTADOS
                // =============================
                string agotadosQuery = @"
                SELECT COUNT(*) 
                FROM productos 
                WHERE stock = 0";

                MySqlCommand cmdAgotados = new MySqlCommand(agotadosQuery, conn);
                int agotados = Convert.ToInt32(cmdAgotados.ExecuteScalar());

                // =============================
                // PRODUCTOS POR TERMINAR (<5)
                // =============================
                string porTerminarQuery = @"
                SELECT COUNT(*) 
                FROM productos 
                WHERE stock > 0 AND stock <= 5";

                MySqlCommand cmdPorTerminar = new MySqlCommand(porTerminarQuery, conn);
                int porTerminar = Convert.ToInt32(cmdPorTerminar.ExecuteScalar());

                // =============================
                // GASTOS DEL DÍA
                // =============================
                string gastosQuery = @"
                SELECT IFNULL(SUM(monto),0) 
                FROM gastos 
                WHERE DATE(fecha) = CURDATE()";

                MySqlCommand cmdGastos = new MySqlCommand(gastosQuery, conn);
                decimal gastosDia = Convert.ToDecimal(cmdGastos.ExecuteScalar());

                // =============================
                // BALANCE
                // =============================
                decimal balance = ventasDia - gastosDia;

                // =============================
                // CIERRE DEL DÍA
                // =============================
                string cierreQuery = @"
                SELECT COUNT(*) 
                FROM cierre_diario 
                WHERE DATE(fecha) = CURDATE()";

                MySqlCommand cmdCierre = new MySqlCommand(cierreQuery, conn);
                int cierre = Convert.ToInt32(cmdCierre.ExecuteScalar());

                string estadoCierre = cierre > 0 ? "REALIZADO" : "PENDIENTE";

                // =============================
                // CONSOLAS PENDIENTES
                // =============================
                string consolasQuery = @"
                SELECT COUNT(*) 
                FROM ventas 
                WHERE tipo = 'CONSOLA' 
                AND forma_cobro = 'PENDIENTE'";

                MySqlCommand cmdConsolas = new MySqlCommand(consolasQuery, conn);
                int consolasPendientes = Convert.ToInt32(cmdConsolas.ExecuteScalar());

                return Ok(new
                {
                    ventas_dia = ventasDia,
                    pedidos_pendientes = pendientes,
                    productos_agotados = agotados,
                    productos_por_terminar = porTerminar,
                    gastos_dia = gastosDia,
                    balance = balance,
                    cierre_dia = estadoCierre,
                    consolas_pendientes = consolasPendientes
                });
            }
        }

        [HttpPost("cierre")]
        public IActionResult CerrarDia()
        {
            using (var conn = conexion.GetConnection())
            {
                conn.Open();

                // =============================
                // VALIDAR SI YA EXISTE CIERRE
                // =============================
                string validarQuery = @"
        SELECT COUNT(*) 
        FROM cierre_diario 
        WHERE DATE(fecha) = CURDATE()";

                MySqlCommand cmdValidar = new MySqlCommand(validarQuery, conn);
                int existe = Convert.ToInt32(cmdValidar.ExecuteScalar());

                if (existe > 0)
                {
                    return BadRequest("El cierre de hoy ya fue realizado");
                }

                // =============================
                // VENTAS DEL DÍA
                // =============================
                string ventasQuery = @"
        SELECT IFNULL(SUM(total),0) 
        FROM ventas 
        WHERE DATE(fecha) = CURDATE()";

                MySqlCommand cmdVentas = new MySqlCommand(ventasQuery, conn);
                decimal ventasDia = Convert.ToDecimal(cmdVentas.ExecuteScalar());

                // =============================
                // GASTOS DEL DÍA
                // =============================
                string gastosQuery = @"
        SELECT IFNULL(SUM(monto),0) 
        FROM gastos 
        WHERE DATE(fecha) = CURDATE()";

                MySqlCommand cmdGastos = new MySqlCommand(gastosQuery, conn);
                decimal gastosDia = Convert.ToDecimal(cmdGastos.ExecuteScalar());

                // =============================
                // BALANCE
                // =============================
                decimal balance = ventasDia - gastosDia;

                // =============================
                // GUARDAR CIERRE
                // =============================
                string insertQuery = @"
        INSERT INTO cierre_diario 
        (total_ventas, total_gastos, balance, estado)
        VALUES (@ventas, @gastos, @balance, 'CERRADO')";

                MySqlCommand cmdInsert = new MySqlCommand(insertQuery, conn);
                cmdInsert.Parameters.AddWithValue("@ventas", ventasDia);
                cmdInsert.Parameters.AddWithValue("@gastos", gastosDia);
                cmdInsert.Parameters.AddWithValue("@balance", balance);

                cmdInsert.ExecuteNonQuery();

                return Ok(new
                {
                    mensaje = "Cierre realizado correctamente",
                    ventas = ventasDia,
                    gastos = gastosDia,
                    balance = balance
                });
            }
        }


        [HttpPost("puntos/juego")]
        public IActionResult PuntosJuego(int id_cliente, int puntos)
        {
            using (var conn = conexion.GetConnection())
            {
                conn.Open();

                string query = @"INSERT INTO historial_puntos
        (id_cliente, tipo, puntos, motivo)
        VALUES (@cliente, 'JUEGO', @puntos, 'MANUAL')";

                MySqlCommand cmd = new MySqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@cliente", id_cliente);
                cmd.Parameters.AddWithValue("@puntos", puntos);

                cmd.ExecuteNonQuery();

                return Ok(new { mensaje = "Puntos de juego agregados" });
            }
        }

        [HttpPost("puntos/consumo")]
        public IActionResult PuntosConsumo(int id_cliente, decimal monto)
        {
            using (var conn = conexion.GetConnection())
            {
                conn.Open();

                decimal puntos = monto * 0.05m;

                string query = @"INSERT INTO historial_puntos
        (id_cliente, tipo, puntos, motivo)
        VALUES (@cliente, 'CONSUMO', @puntos, 'COMPRA')";

                MySqlCommand cmd = new MySqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@cliente", id_cliente);
                cmd.Parameters.AddWithValue("@puntos", puntos);

                cmd.ExecuteNonQuery();

                return Ok(new
                {
                    mensaje = "Puntos de consumo agregados",
                    puntos = puntos
                });
            }
        }

        [HttpPost("venta-rapida")]
        public IActionResult VentaRapida(
    [FromQuery] int id_cliente,
    [FromQuery] decimal total)
        {
            using (var conn = conexion.GetConnection())
            {
                conn.Open();

                string query = @"
        INSERT INTO ventas
        (id_cliente, total, forma_cobro, fecha)
        VALUES (@cliente, @total, 'CANCELADO', NOW())";

                MySqlCommand cmd = new MySqlCommand(query, conn);

                cmd.Parameters.AddWithValue("@cliente", id_cliente);
                cmd.Parameters.AddWithValue("@total", total);

                cmd.ExecuteNonQuery();

                return Ok(new
                {
                    mensaje = "Venta registrada correctamente",
                    cliente = id_cliente,
                    total = total
                });
            }
        }

        // ======================
        // TOP CLIENTES
        // ======================
        [HttpGet("top-clientes")]
        public IActionResult TopClientes()
        {
            using (var conn = conexion.GetConnection())
            {
                conn.Open();

                string query = @"

SELECT 

c.nombre,

COUNT(v.id_venta) AS compras,

SUM(v.total) AS total,

IFNULL(SUM(hp.puntos),0) AS puntos

FROM ventas v

JOIN clientes c
ON v.id_cliente = c.id_cliente

LEFT JOIN historial_puntos hp
ON c.id_cliente = hp.id_cliente

GROUP BY c.id_cliente, c.nombre

ORDER BY puntos DESC, total DESC

LIMIT 5";

                MySqlCommand cmd =
                new MySqlCommand(query, conn);

                var reader = cmd.ExecuteReader();

                List<object> lista =
                new List<object>();

                while (reader.Read())
                {
                    lista.Add(new
                    {
                        nombre =
                        reader["nombre"],

                        compras =
                        reader["compras"],

                        total =
                        reader["total"],

                        puntos =
                        reader["puntos"]
                    });
                }

                return Ok(lista);
            }
        }


        // ======================
        // TOP 10 GAMER
        // ======================
        [HttpGet("top-gamers")]
        public IActionResult TopGamers()
        {
            using (var conn =
            conexion.GetConnection())
            {
                conn.Open();

                string query = @"

        SELECT 

        c.nombre,

        c.apodo,

        SUM(h.puntos) AS puntos

        FROM historial_puntos h

        JOIN clientes c
        ON h.id_cliente = c.id_cliente

        WHERE h.tipo = 'JUEGO'

        GROUP BY c.id_cliente

        ORDER BY puntos DESC

        LIMIT 10";

                MySqlCommand cmd =
                new MySqlCommand(query, conn);

                var reader =
                cmd.ExecuteReader();

                List<object> lista =
                new List<object>();

                while (reader.Read())
                {
                    lista.Add(new
                    {
                        nombre =
                        reader["nombre"],

                        apodo =
                        reader["apodo"],

                        puntos =
                        reader["puntos"]
                    });
                }

                return Ok(lista);
            }

        }

        // ======================
        // CIERRE DIA
        // ======================
        // ======================
        // CIERRE DIA
        // ======================
        [HttpPost("cierre-dia")]
        public IActionResult CierreDia()
        {
            using (var conn =
            conexion.GetConnection())
            {
                conn.Open();

                // TOTAL VENTAS PAGADAS
                string ventasQuery = @"

        SELECT IFNULL(SUM(total),0)
        FROM ventas
        WHERE forma_cobro = 'PAGADO'";

                MySqlCommand ventasCmd =
                new MySqlCommand(
                ventasQuery, conn);

                decimal totalVentas =
                Convert.ToDecimal(
                ventasCmd.ExecuteScalar());

                // TOTAL GASTOS
                decimal totalGastos = 0;

                // BALANCE
                decimal balance =
                totalVentas - totalGastos;

                // INSERTAR CIERRE
                string insert = @"

        INSERT INTO cierre_diario
        (
            total_ventas,
            total_gastos,
            balance,
            estado
        )

        VALUES
        (
            @ventas,
            @gastos,
            @balance,
            'CERRADO'
        )";

                MySqlCommand cmd =
                new MySqlCommand(insert, conn);

                cmd.Parameters.AddWithValue(
                "@ventas",
                totalVentas);

                cmd.Parameters.AddWithValue(
                "@gastos",
                totalGastos);

                cmd.Parameters.AddWithValue(
                "@balance",
                balance);

                cmd.ExecuteNonQuery();

                return Ok(new
                {
                    ventas = totalVentas,
                    gastos = totalGastos,
                    balance = balance
                });
            }
        }


            // ======================
            // HISTORIAL CIERRES
            // ======================
            [HttpGet("historial-cierres")]
            public IActionResult HistorialCierres()
            {
                using (var conn =
                conexion.GetConnection())
                {
                    conn.Open();

                    string query = @"

        SELECT *

        FROM cierre_diario

        ORDER BY fecha DESC";

                    MySqlCommand cmd =
                    new MySqlCommand(query, conn);

                    var reader =
                    cmd.ExecuteReader();

                    List<object> lista =
                    new List<object>();

                    while (reader.Read())
                    {
                        lista.Add(new
                        {
                            id =
                            reader["id_cierre"],

                            ventas =
                            reader["total_ventas"],

                            gastos =
                            reader["total_gastos"],

                            balance =
                            reader["balance"],

                            estado =
                            reader["estado"],

                            fecha =
                            reader["fecha"]
                        });
                    }

                    return Ok(lista);
                }
            }


        // ======================
        // GRAFICA VENTAS
        // ======================
        [HttpGet("grafica-ventas")]
        public IActionResult GraficaVentas()
        {
            using (var conn =
            conexion.GetConnection())
            {
                conn.Open();

                string query = @"

        SELECT 

        DATE(fecha) AS dia,

        SUM(total) AS total

        FROM ventas

        GROUP BY DATE(fecha)

        ORDER BY fecha";

                MySqlCommand cmd =
                new MySqlCommand(query, conn);

                var reader =
                cmd.ExecuteReader();

                List<object> lista =
                new List<object>();

                while (reader.Read())
                {
                    lista.Add(new
                    {
                        dia =
                        reader["dia"],

                        total =
                        reader["total"]
                    });
                }

                return Ok(lista);
            }
        }
    }

    }
        
        
    
    
