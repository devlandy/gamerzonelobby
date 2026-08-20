using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MySql.Data.MySqlClient;
using GamerZoneAPI.Data;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using QuestPDF.Helpers;
using System.Text.RegularExpressions;

namespace GamerZoneAPI.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/pdf")]
    public class PDFController : ControllerBase
    {
        private readonly DbManager _db;
        private readonly IWebHostEnvironment _env;

        public PDFController(DbManager db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        private static string QuitarEmojis(string? texto)
        {
            if (string.IsNullOrEmpty(texto)) return "";
            return Regex.Replace(texto, @"[\p{So}\p{Cs}\p{Mn}]|[\uD800-\uDFFF]", "").Trim();
        }

        private void ConstruirPDF(IDocumentContainer container, string titulo,
            Dictionary<string, object> venta, List<Dictionary<string, object>> detalles,
            string nombreCliente, string infoExtra)
        {
            decimal subtotalBruto = detalles.Where(d => Convert.ToDecimal(d["precio"]) > 0)
                                            .Sum(d => Convert.ToDecimal(d["subtotal"]));
            decimal totalFinal   = venta.ContainsKey("total") && venta["total"] != DBNull.Value
                ? Convert.ToDecimal(venta["total"]) : 0;
            decimal descuentoPct = venta.ContainsKey("descuento_pct") && venta["descuento_pct"] != DBNull.Value
                ? Convert.ToDecimal(venta["descuento_pct"]) : 0;
            decimal descuento    = descuentoPct > 0 ? Math.Round(subtotalBruto * descuentoPct / 100, 2) : 0;
            var numDoc  = venta.ContainsKey("id_venta") ? venta["id_venta"]
                        : venta.ContainsKey("id_factura") ? venta["id_factura"] : "";
            var fechaDoc = venta.ContainsKey("fecha") && venta["fecha"] != DBNull.Value
                ? Convert.ToDateTime(venta["fecha"]).AddHours(-6) : DateTime.Now;
            string metodo = venta.ContainsKey("metodo_pago") ? venta["metodo_pago"]?.ToString() ?? "" : "";
            string tipo   = venta.ContainsKey("tipo") ? QuitarEmojis(venta["tipo"]?.ToString() ?? titulo) : titulo;

            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(10));

                // ── HEADER ──
                page.Header().Column(col =>
                {
                    col.Item().Text("EL LOBBY ZONE — " + titulo)
                        .FontSize(16).Bold();
                    col.Item().PaddingTop(2)
                        .Text("Gaming · Entretenimiento · Comida · Guatemala")
                        .FontSize(9).FontColor("#666666");
                    col.Item().PaddingTop(6).BorderTop(2).BorderColor("#000000");
                    col.Item().Height(6);
                });

                // ── CONTENT ──
                page.Content().Column(col =>
                {
                    // Datos del documento
                    col.Item().PaddingBottom(4)
                        .Text($"Documento N° 0{numDoc}   |   Fecha: {fechaDoc:dd/MM/yyyy}   Hora: {fechaDoc:hh:mm tt}   |   Tipo: {tipo}")
                        .FontSize(9).FontColor("#444444");

                    // Datos del cliente
                    col.Item().PaddingBottom(10).Border(1).BorderColor("#cccccc").Padding(6).Column(c =>
                    {
                        c.Item().Text("CLIENTE").FontSize(8).Bold().FontColor("#0d47a1");
                        c.Item().PaddingTop(2).Text(nombreCliente).FontSize(11).Bold();
                        if (!string.IsNullOrWhiteSpace(infoExtra))
                            c.Item().PaddingTop(1).Text(infoExtra).FontSize(8).FontColor("#555555");
                        c.Item().PaddingTop(1).Text($"Método de pago: {metodo}").FontSize(8).FontColor("#555555");
                    });

                    // Tabla productos
                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(32);
                            c.RelativeColumn(5);
                            c.RelativeColumn(2);
                            c.RelativeColumn(2);
                        });

                        // Encabezados
                        foreach (var (txt, right) in new[] {
                            ("CANT.", false), ("DESCRIPCIÓN", false), ("PRECIO", true), ("IMPORTE", true) })
                        {
                            var cell = t.Cell().Background("#1a1a1a").Padding(5);
                            var text = cell.Text(txt).FontSize(8).Bold().FontColor("#ffffff");
                            if (right) text.AlignRight();
                        }

                        // Filas
                        bool par = false;
                        foreach (var d in detalles)
                        {
                            bool esIng = Convert.ToDecimal(d["precio"]) == 0;
                            string bg  = (par && !esIng) ? "#f5f5f5" : "#ffffff";
                            if (!esIng) par = !par;

                            t.Cell().Background(bg).BorderBottom(1).BorderColor("#dddddd").Padding(4)
                                .Text(esIng ? "" : d["cantidad"]?.ToString()).FontSize(9).AlignCenter();
                            t.Cell().Background(bg).BorderBottom(1).BorderColor("#dddddd").Padding(4)
                                .Text((esIng ? "  └ " : "") + QuitarEmojis(d["nombre"]?.ToString()))
                                .FontSize(9).FontColor(esIng ? "#888888" : "#1a1a1a");
                            t.Cell().Background(bg).BorderBottom(1).BorderColor("#dddddd").Padding(4)
                                .Text(esIng ? "" : $"Q{Convert.ToDecimal(d["precio"]):F2}").FontSize(9).AlignRight();
                            t.Cell().Background(bg).BorderBottom(1).BorderColor("#dddddd").Padding(4)
                                .Text(esIng ? "" : $"Q{Convert.ToDecimal(d["subtotal"]):F2}").FontSize(9).AlignRight();
                        }
                    });

                    // Totales — tabla simple 2 columnas
                    col.Item().PaddingTop(10).Table(t =>
                    {
                        t.ColumnsDefinition(c => { c.RelativeColumn(3); c.RelativeColumn(1); });

                        if (descuento > 0)
                        {
                            t.Cell().Text("Subtotal:").FontSize(9).FontColor("#555555").AlignRight();
                            t.Cell().Text($"Q{subtotalBruto:F2}").FontSize(9).AlignRight();
                            t.Cell().Text($"Descuento ({descuentoPct:F1}%):").FontSize(9).FontColor("#cc0000").AlignRight();
                            t.Cell().Text($"-Q{descuento:F2}").FontSize(9).FontColor("#cc0000").AlignRight();
                        }
                        t.Cell().Text("TOTAL:").FontSize(13).Bold().FontColor("#0d47a1").AlignRight();
                        t.Cell().Text($"Q{totalFinal:F2}").FontSize(13).Bold().FontColor("#0d47a1").AlignRight();
                        t.Cell().Text("Forma de pago:").FontSize(9).FontColor("#555555").AlignRight();
                        t.Cell().Text(metodo).FontSize(9).AlignRight();
                    });
                });

                // ── FOOTER ──
                page.Footer().AlignCenter()
                    .Text("¡Gracias por tu visita! · El Lobby Zone")
                    .FontSize(8).FontColor("#888888");
            });
        }

        [HttpGet("venta/{id}")]
        public IActionResult GenerarTicketVenta(int id)
        {
            try
            {
                var ventas = _db.ExecuteQuery(@"
                    SELECT v.id_venta, v.total, IFNULL(v.metodo_pago,'') AS metodo_pago,
                           v.fecha, IFNULL(v.tipo,'PRODUCTO') AS tipo,
                           IFNULL(v.descuento_pct,0) AS descuento_pct,
                           COALESCE(c.nombre, 'Consumidor Final') AS cliente
                    FROM ventas v
                    LEFT JOIN clientes c ON v.id_cliente = c.id_cliente
                    WHERE v.id_venta = @id",
                    new MySqlParameter("@id", id));

                if (ventas.Count == 0) return NotFound();
                var v = ventas[0];

                var detalles = _db.ExecuteQuery(@"
                    SELECT COALESCE(p.nombre, d.nombre, 'Servicio') AS nombre,
                           IFNULL(d.cantidad, 1) AS cantidad,
                           IFNULL(d.precio, 0) AS precio,
                           IFNULL(d.subtotal, 0) AS subtotal
                    FROM detalle_ventas d
                    LEFT JOIN productos p ON d.id_producto = p.id_producto
                    WHERE d.id_venta = @id ORDER BY d.id_detalle",
                    new MySqlParameter("@id", id));

                var pdf = Document.Create(c => ConstruirPDF(c, "COMPROBANTE",
                    v, detalles, v["cliente"]?.ToString() ?? "Consumidor Final", "")).GeneratePdf();

                return File(pdf, "application/pdf", $"Venta_{id}.pdf");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message, stack = ex.StackTrace });
            }
        }

        [HttpGet("factura/{id}")]
        public IActionResult GenerarFactura(int id)
        {
            try
            {
                var rows = _db.ExecuteQuery(@"
                    SELECT f.id_factura, f.nombre, f.nit, f.direccion, f.fecha,
                           v.total, v.metodo_pago, v.id_venta, IFNULL(v.descuento_pct,0) AS descuento_pct
                    FROM facturas f
                    JOIN ventas v ON f.id_venta = v.id_venta
                    WHERE f.id_factura = @id",
                    new MySqlParameter("@id", id));

                if (rows.Count == 0) return NotFound();
                var f = rows[0];
                int idVenta = Convert.ToInt32(f["id_venta"]);

                var detalles = _db.ExecuteQuery(@"
                    SELECT COALESCE(p.nombre, d.nombre, 'Servicio') AS nombre,
                           IFNULL(d.cantidad, 1) AS cantidad,
                           IFNULL(d.precio, 0) AS precio,
                           IFNULL(d.subtotal, 0) AS subtotal
                    FROM detalle_ventas d
                    LEFT JOIN productos p ON d.id_producto = p.id_producto
                    WHERE d.id_venta = @id ORDER BY d.id_detalle",
                    new MySqlParameter("@id", idVenta));

                var datos = new Dictionary<string, object>
                {
                    ["id_factura"]    = f["id_factura"],
                    ["total"]         = f["total"],
                    ["metodo_pago"]   = f["metodo_pago"],
                    ["fecha"]         = f["fecha"],
                    ["descuento_pct"] = f["descuento_pct"],
                    ["tipo"]          = "FACTURA"
                };

                string infoExtra = $"NIT: {f["nit"]}  |  {f["direccion"]}";

                var pdf = Document.Create(c => ConstruirPDF(c, "FACTURA",
                    datos, detalles, f["nombre"]?.ToString() ?? "", infoExtra)).GeneratePdf();

                return File(pdf, "application/pdf", $"Factura_{id}.pdf");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message, stack = ex.StackTrace });
            }
        }
    }
}
