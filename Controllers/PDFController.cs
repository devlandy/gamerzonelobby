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
            decimal totalFinal   = venta["total"] != DBNull.Value ? Convert.ToDecimal(venta["total"]) : 0;
            decimal descuentoPct = venta.ContainsKey("descuento_pct") && venta["descuento_pct"] != DBNull.Value ? Convert.ToDecimal(venta["descuento_pct"]) : 0;
            decimal descuento    = subtotalBruto - totalFinal;
            var numDoc = venta.ContainsKey("id_venta") ? venta["id_venta"] : venta["id_factura"];
            var fechaDoc = Convert.ToDateTime(venta["fecha"]).AddHours(-6);

            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginHorizontal(36);
                page.MarginVertical(28);
                page.Background(Colors.White);
                page.DefaultTextStyle(t => t.FontFamily("Arial").FontSize(10).FontColor("#1a1a1a"));

                // ── HEADER ──────────────────────────────────────────────
                page.Header().Column(h =>
                {
                    // Fila superior: logo | título | info negocio
                    h.Item().PaddingBottom(6).Row(r =>
                    {
                        // Logo izquierda
                        if (logo != null)
                            r.ConstantItem(110).AlignMiddle()
                                .Image(logo).FitWidth();
                        else
                            r.ConstantItem(110).AlignMiddle()
                                .Text("EL LOBBY ZONE").FontSize(16).Bold().FontColor(ColorPrimario);

                        r.ConstantItem(12);

                        // Título centrado
                        r.RelativeItem().AlignMiddle().AlignCenter().Column(c =>
                        {
                            c.Item().AlignCenter()
                                .Text(titulo)
                                .FontSize(14).Bold()
                                .Underline();
                        });

                        r.ConstantItem(12);

                        // Info negocio derecha
                        r.ConstantItem(160).AlignMiddle().Column(c =>
                        {
                            c.Item().Text("EL LOBBY ZONE").FontSize(9).Bold();
                            c.Item().Text("Gaming · Entretenimiento · Comida").FontSize(8).FontColor("#555");
                            c.Item().PaddingTop(2).Text("Guatemala").FontSize(8).FontColor("#555");
                        });
                    });

                    // Línea separadora doble
                    h.Item().BorderTop(3).BorderColor("#1a1a1a").PaddingTop(1).BorderTop(1).BorderColor("#1a1a1a");
                    h.Item().PaddingBottom(4);
                });

                // ── CONTENIDO ────────────────────────────────────────────
                page.Content().Column(col =>
                {
                    // Bloque de info: datos doc (izq) | datos cliente (der)
                    col.Item().PaddingBottom(8).Row(r =>
                    {
                        // Izquierda: número de documento + fechas
                        r.RelativeItem().Column(c =>
                        {
                            c.Item().Text(tb => {
                                tb.Span("Documento N°  ").FontSize(9).Bold();
                                tb.Span($"0{numDoc}").FontSize(9);
                            });
                            c.Item().PaddingTop(2).Text(tb => {
                                tb.Span("Fecha de emisión:  ").FontSize(9).Bold();
                                tb.Span(fechaDoc.ToString("dd/MM/yyyy")).FontSize(9);
                            });
                            c.Item().PaddingTop(2).Text(tb => {
                                tb.Span("Hora:  ").FontSize(9).Bold();
                                tb.Span(fechaDoc.ToString("hh:mm:ss tt")).FontSize(9);
                            });
                            c.Item().PaddingTop(2).Text(tb => {
                                tb.Span("Tipo:  ").FontSize(9).Bold();
                                tb.Span(venta.ContainsKey("tipo") ? venta["tipo"]?.ToString() ?? titulo : titulo).FontSize(9);
                            });
                        });

                        r.ConstantItem(16);

                        // Derecha: datos del cliente
                        r.RelativeItem().Border(1).BorderColor("#cccccc").Padding(8).Column(c =>
                        {
                            c.Item().Text("CLIENTE").FontSize(8).Bold().FontColor(ColorPrimario);
                            c.Item().PaddingTop(3).Text(nombreCliente).FontSize(10).Bold();
                            if (!string.IsNullOrWhiteSpace(infoExtra))
                                c.Item().PaddingTop(2).Text(infoExtra).FontSize(8).FontColor("#555");
                            c.Item().PaddingTop(2).Text(tb => {
                                tb.Span("Método de pago: ").FontSize(8).Bold();
                                tb.Span(venta["metodo_pago"]?.ToString() ?? "").FontSize(8);
                            });
                        });
                    });

                    // Tabla de productos
                    col.Item().Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(40);    // Cant
                            c.RelativeColumn(5);     // Descripción
                            c.RelativeColumn(2);     // Precio unit
                            c.RelativeColumn(2);     // Importe
                        });

                        // Encabezado negro
                        void Th(string txt, bool right = false) {
                            var cell = t.Cell().Background("#1a1a1a").PaddingVertical(6).PaddingHorizontal(6);
                            var txt2 = cell.Text(txt).FontSize(9).Bold().FontColor(Colors.White);
                            if (right) txt2.AlignRight();
                        }

                        Th("CANT."); Th("DESCRIPCIÓN"); Th("PRECIO", true); Th("IMPORTE", true);

                        bool par = false;
                        foreach (var d in detalles)
                        {
                            bool esIngrediente = Convert.ToDecimal(d["precio"]) == 0;
                            string bg = esIngrediente ? "#f9f9f9" : (par ? ColorGris : Colors.White);
                            if (!esIngrediente) par = !par;

                            string borde = "#dddddd";

                            if (esIngrediente)
                            {
                                t.Cell().BorderBottom(1).BorderColor(borde).Background(bg).Padding(5).Text("").FontSize(9);
                                t.Cell().BorderBottom(1).BorderColor(borde).Background(bg).Padding(5).PaddingLeft(14)
                                    .Text($"└ {QuitarEmojis(d["nombre"]?.ToString())}").FontSize(9).FontColor("#888");
                                t.Cell().BorderBottom(1).BorderColor(borde).Background(bg).Padding(5).Text("").FontSize(9);
                                t.Cell().BorderBottom(1).BorderColor(borde).Background(bg).Padding(5).Text("").FontSize(9);
                            }
                            else
                            {
                                t.Cell().BorderBottom(1).BorderColor(borde).Background(bg).Padding(5).AlignCenter()
                                    .Text(d["cantidad"].ToString()).FontSize(9);
                                t.Cell().BorderBottom(1).BorderColor(borde).Background(bg).Padding(5)
                                    .Text(QuitarEmojis(d["nombre"]?.ToString())).FontSize(9);
                                t.Cell().BorderBottom(1).BorderColor(borde).Background(bg).Padding(5).AlignRight()
                                    .Text($"Q{Convert.ToDecimal(d["precio"]):F2}").FontSize(9);
                                t.Cell().BorderBottom(1).BorderColor(borde).Background(bg).Padding(5).AlignRight()
                                    .Text($"Q{Convert.ToDecimal(d["subtotal"]):F2}").FontSize(9);
                            }
                        }
                    });

                    // Observaciones
                    col.Item().PaddingTop(12).Border(1).BorderColor("#cccccc").Padding(8).Column(c =>
                    {
                        c.Item().Text("OBSERVACIONES:").FontSize(9).Bold();
                        c.Item().PaddingTop(20);
                    });

                    // ── BARRA DE TOTALES (dentro del contenido para que siempre sea visible) ──
                    col.Item().PaddingTop(12).Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(1);  // Página
                            c.RelativeColumn(2);  // Subtotal
                            c.RelativeColumn(2);  // Descuento
                            c.RelativeColumn(2);  // Forma de pago
                            c.RelativeColumn(2);  // TOTAL
                        });

                        void Th2(string txt) =>
                            t.Cell().Background("#1a1a1a").PaddingVertical(7).PaddingHorizontal(6)
                                .AlignCenter().Text(txt).FontSize(9).Bold().FontColor(Colors.White);

                        Th2("PÁGINA"); Th2("SUBTOTAL"); Th2("DESCUENTO"); Th2("FORMA DE PAGO"); Th2("TOTAL");

                        t.Cell().Background("#f0f0f0").PaddingVertical(9).PaddingHorizontal(6)
                            .AlignCenter().Text("1/1").FontSize(10).Bold();
                        t.Cell().Background("#f0f0f0").PaddingVertical(9).PaddingHorizontal(6)
                            .AlignCenter().Text($"Q{subtotalBruto:F2}").FontSize(10);
                        t.Cell().Background(descuento > 0 ? "#fff0f0" : "#f0f0f0").PaddingVertical(9).PaddingHorizontal(6)
                            .AlignCenter()
                            .Text(descuento > 0 ? $"-Q{descuento:F2} ({descuentoPct:G29}%)" : "—")
                            .FontSize(9).FontColor(descuento > 0 ? "#cc0000" : "#777");
                        t.Cell().Background("#f0f0f0").PaddingVertical(9).PaddingHorizontal(6)
                            .AlignCenter().Text(venta["metodo_pago"]?.ToString() ?? "").FontSize(9);
                        t.Cell().Background(ColorPrimario).PaddingVertical(9).PaddingHorizontal(6)
                            .AlignCenter().Text($"Q{totalFinal:F2}").FontSize(13).Bold().FontColor(Colors.White);
                    });
                });

                // ── FOOTER ───────────────────────────────────────────────
                page.Footer().Column(f =>
                {
                    f.Item().PaddingTop(6).AlignCenter()
                        .Text("¡Gracias por tu visita! · El Lobby Zone · Gaming · Entretenimiento · Comida")
                        .FontSize(8).FontColor("#888");
                });
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
