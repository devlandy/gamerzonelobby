using Microsoft.AspNetCore.Mvc;

using GamerZoneAPI.Data;

using MySql.Data.MySqlClient;

using QuestPDF.Fluent;

using QuestPDF.Helpers;

using QuestPDF.Infrastructure;

namespace GamerZoneAPI.Controllers
{
    [ApiController]

    [Route("api/pdf")]

    public class PDFController : ControllerBase
    {
        private Conexion conexion =
        new Conexion();

        [HttpGet("factura/{id}")]
        public IActionResult GenerarFactura(int id)
        {
            using (var conn =
            conexion.GetConnection())
            {
                conn.Open();

                // ======================
                // FACTURA
                // ======================

                string query = @"

SELECT
f.id_factura,
f.nombre,
f.nit,
f.direccion,
f.fecha,

v.total,
v.metodo_pago

FROM facturas f

JOIN ventas v
ON f.id_venta = v.id_venta

WHERE f.id_factura = @id

";

                MySqlCommand cmd =
                new MySqlCommand(query, conn);

                cmd.Parameters.AddWithValue(
                "@id", id);

                var reader =
                cmd.ExecuteReader();

                if (!reader.Read())
                {
                    return NotFound();
                }

                var factura = new
                {
                    id =
                    reader["id_factura"],

                    cliente =
                    reader["nombre"],

                    nit =
                    reader["nit"],

                    direccion =
                    reader["direccion"],

                    fecha =
                    reader["fecha"],

                    total =
                    reader["total"],

                    metodo =
                    reader["metodo_pago"]
                };

                reader.Close();

                // ======================
                // PDF
                // ======================

                var pdf =
                Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Margin(30);

                        page.Header()

                        .Text("LOBBY ZONE")

                        .FontSize(28)

                        .Bold();

                        page.Content()

                        .Column(col =>
                        {
                            col.Item().Text(
                            $"Factura #{factura.id}");

                            col.Item().Text(
                            $"Cliente: {factura.cliente}");

                            col.Item().Text(
                            $"NIT: {factura.nit}");

                            col.Item().Text(
                            $"Dirección: {factura.direccion}");

                            col.Item().Text(
                            $"Fecha: {factura.fecha}");

                            col.Item().Text(
                            $"Método Pago: {factura.metodo}");

                            col.Item().PaddingTop(20);

                            col.Item().Text(
                            $"TOTAL: Q{factura.total}")

                            .FontSize(22)

                            .Bold();
                        });
                    });
                })

                .GeneratePdf();

                return File(

                    pdf,

                    "application/pdf",

                    $"Factura_{factura.id}.pdf"
                );
            }
        }
    }
}