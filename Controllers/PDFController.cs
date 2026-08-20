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

        private byte[]? CargarLogo()
        {
            var path = Path.Combine(_env.ContentRootPath, "fronted", "logo.jpeg");
            return System.IO.File.Exists(path) ? System.IO.File.ReadAllBytes(path) : null;
        }

        // Color azul oscuro del logo
        private static readonly string ColorPrimario   = "#0d47a1";
        private static readonly string ColorAcento     = "#1565c0";
        private static readonly string ColorTextoClaro = "#ffffff";
        private static readonly string ColorGris       = "#f5f5f5";
        private static readonly string ColorBorde      = "#e0e0e0";

        private void ConstruirPDF(IDocumentContainer container, string titulo,
            Dictionary<string, object> venta, List<Dictionary<string, object>> detalles,
            string nombreCliente, string infoExtra)
        {
            var logo = CargarLogo();
            decimal subtotalBruto = detalles.Where(d => Convert.ToDecimal(d["precio"]) > 0)
                                            .Sum(d => Convert.ToDecimal(d["subtotal"]));
            decimal totalFinal   = venta.ContainsKey("total") && venta["total"] != DBNull.Value ? Convert.ToDecimal(venta["total"]) : 0;
            decimal descuentoPct = venta.ContainsKey("descuento_pct") && venta["descuento_pct"] != DBNull.Value ? Convert.ToDecimal(venta["descuento_pct"]) : 0;
            decimal descuento    = descuentoPct > 0 ? Math.Round(subtotalBruto * descuentoPct / 100, 2) : 0;
            var numDoc = venta.ContainsKey("id_venta") ? venta["id_venta"] : (venta.ContainsKey("id_factura") ? venta["id_factura"] : "");
            var fechaDoc = venta.ContainsKey("fecha") && venta["fecha"] != DBNull.Value
                ? Convert.ToDateTime(venta["fecha"]).AddHours(-6) : DateTime.Now;
            string metodo = venta.ContainsKey("metodo_pago") ? venta["metodo_pago"]?.ToString() ?? "" : "";
            string tipo   = venta.ContainsKey("tipo")       ? venta["tipo"]?.ToString() ?? titulo : titulo;

            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginHorizontal(40);
                page.MarginVertical(30);
                page.Background(Colors.White);
                page.DefaultTextStyle(t => t.FontSize(10).FontColor("#1a1a1a"));

                page.Header().Column(h =>
                {
                    // Logo + Título + Info en una fila simple
                    h.Item().PaddingBottom(8).Row(r =>
                    {
                        if (logo != null)
                            r.ConstantItem(90).Image(logo).FitWidth();
                        else
                            r.ConstantItem(90).Text("EL LOBBY ZONE").FontSize(13).Bold().FontColor(ColorPrimario);

                        r.RelativeItem().PaddingLeft(10).Column(c =>
                        {
                            c.Item().Text(titulo).FontSize(16).Bold();
                            c.Item().PaddingTop(2).Text("EL LOBBY ZONE · Gaming · Entretenimiento · Comida").FontSize(8).FontColor("#666");
                            c.Item().PaddingTop(1).Text("Guatemala").FontSize(8).FontColor("#666");
                        });
                    });
                    h.Item().BorderTop(2).BorderColor("#1a1a1a");
                    h.Item().PaddingBottom(4);
                });

                page.Content().Column(col =>
                {
                    // Info del documento
                    col.Item().PaddingBottom(10).Row(r =>
                    {
                        r.RelativeItem().Column(c =>
                        {
                            c.Item().Text($"Documento N° 0{numDoc}").FontSize(9).Bold();
                            c.Item().PaddingTop(2).Text($"Fecha: {fechaDoc:dd/MM/yyyy}  Hora: {fechaDoc:hh:mm tt}").FontSize(9);
                            c.Item().PaddingTop(2).Text($"Tipo: {QuitarEmojis(tipo)}").FontSize(9);
                        });
                        r.RelativeItem().Border(1).BorderColor("#ccc").Padding(7).Column(c =>
                        {
                            c.Item().Text("CLIENTE").FontSize(8).Bold().FontColor(ColorPrimario);
                            c.Item().PaddingTop(2).Text(nombreCliente).FontSize(10).Bold();
                            if (!string.IsNullOrWhiteSpace(infoExtra))
                                c.Item().PaddingTop(1).Text(infoExtra).FontSize(8).FontColor("#555");
                            c.Item().PaddingTop(2).Text($"Método de pago: {metodo}").FontSize(8);
                        });
                    });

                    // Tabla de productos
                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(36);
                            c.RelativeColumn(5);
                            c.RelativeColumn(2);
                            c.RelativeColumn(2);
                        });

                        void Th(string txt, bool right = false) {
                            var cell = t.Cell().Background("#1a1a1a").PaddingVertical(5).PaddingHorizontal(5);
                            var txt2 = cell.Text(txt).FontSize(8).Bold().FontColor(Colors.White);
                            if (right) txt2.AlignRight();
                        }
                        Th("CANT."); Th("DESCRIPCIÓN"); Th("PRECIO", true); Th("IMPORTE", true);

                        bool par = false;
                        foreach (var d in detalles)
                        {
                            bool esIng = Convert.ToDecimal(d["precio"]) == 0;
                            string bg  = par ? ColorGris : Colors.White;
                            if (!esIng) par = !par;
                            string borde = "#ddd";

                            t.Cell().BorderBottom(1).BorderColor(borde).Background(bg).Padding(4).AlignCenter()
                                .Text(esIng ? "" : d["cantidad"].ToString()).FontSize(9);
                            t.Cell().BorderBottom(1).BorderColor(borde).Background(bg).Padding(4)
                                .Text((esIng ? "  └ " : "") + QuitarEmojis(d["nombre"]?.ToString())).FontSize(9).FontColor(esIng ? "#888" : "#1a1a1a");
                            t.Cell().BorderBottom(1).BorderColor(borde).Background(bg).Padding(4).AlignRight()
                                .Text(esIng ? "" : $"Q{Convert.ToDecimal(d["precio"]):F2}").FontSize(9);
                            t.Cell().BorderBottom(1).BorderColor(borde).Background(bg).Padding(4).AlignRight()
                                .Text(esIng ? "" : $"Q{Convert.ToDecimal(d["subtotal"]):F2}").FontSize(9);
                        }
                    });

                    // Totales
                    col.Item().PaddingTop(10).AlignRight().Column(c =>
                    {
                        if (descuento > 0)
                        {
                            c.Item().Text($"Subtotal: Q{subtotalBruto:F2}").FontSize(9).FontColor("#555");
                            c.Item().Text($"Descuento ({descuentoPct:F1}%): -Q{descuento:F2}").FontSize(9).FontColor("#cc0000");
                        }
                        c.Item().PaddingTop(4).Text($"TOTAL: Q{totalFinal:F2}").FontSize(14).Bold().FontColor(ColorPrimario);
                        c.Item().PaddingTop(2).Text($"Forma de pago: {metodo}").FontSize(9).FontColor("#555");
                    });
                });

                page.Footer().AlignCenter()
                    .Text("¡Gracias por tu visita! · El Lobby Zone · Gaming · Entretenimiento · Comida")
                    .FontSize(8).FontColor("#888");
            });
        }

        [HttpGet("venta/{id}")]
        public IActionResult GenerarTicketVenta(int id)
        {
          try {
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
          } catch (Exception ex) {
            return StatusCode(500, new { error = ex.Message, stack = ex.StackTrace });
          }
        }

        [HttpGet("factura/{id}")]
        public IActionResult GenerarFactura(int id)
        {
            var rows = _db.ExecuteQuery(@"
                SELECT f.id_factura, f.nombre, f.nit, f.direccion, f.fecha, v.total, v.metodo_pago, v.id_venta, v.descuento_pct
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

            // Construir datos combinados para el helper
            var datos = new Dictionary<string, object>
            {
                ["id_factura"]   = f["id_factura"],
                ["total"]        = f["total"],
                ["metodo_pago"]  = f["metodo_pago"],
                ["fecha"]        = f["fecha"],
                ["descuento_pct"]= f["descuento_pct"],
                ["tipo"]         = "FACTURA"
            };

            string infoExtra = $"NIT: {f["nit"]}  |  {f["direccion"]}";

            var pdf = Document.Create(c => ConstruirPDF(c, "FACTURA",
                datos, detalles, f["nombre"]?.ToString() ?? "", infoExtra)).GeneratePdf();

            return File(pdf, "application/pdf", $"Factura_{id}.pdf");
        }
    }
}
