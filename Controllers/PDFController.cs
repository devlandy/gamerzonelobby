using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MySql.Data.MySqlClient;
using GamerZoneAPI.Data;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using QuestPDF.Helpers;

namespace GamerZoneAPI.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/pdf")]
    public class PDFController : ControllerBase
    {
        private readonly DbManager _db;

        public PDFController(DbManager db) => _db = db;

        [HttpGet("venta/{id}")]
        public IActionResult GenerarTicketVenta(int id)
        {
            var ventas = _db.ExecuteQuery(@"
                SELECT v.id_venta, v.total, v.metodo_pago, v.fecha, v.tipo, v.descuento_pct,
                       COALESCE(c.nombre, 'Consumidor Final') AS cliente
                FROM ventas v
                LEFT JOIN clientes c ON v.id_cliente = c.id_cliente
                WHERE v.id_venta = @id",
                new MySqlParameter("@id", id));

            if (ventas.Count == 0)
                return NotFound();

            var v = ventas[0];

            var detalles = _db.ExecuteQuery(@"
                SELECT COALESCE(p.nombre, d.nombre, 'Servicio') AS nombre, d.cantidad, d.precio, d.subtotal
                FROM detalle_ventas d
                LEFT JOIN productos p ON d.id_producto = p.id_producto
                WHERE d.id_venta = @id
                ORDER BY d.id_detalle",
                new MySqlParameter("@id", id));

            var pdf = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(40);

                    page.Header().Column(h =>
                    {
                        h.Item().Text("LOBBY ZONE").FontSize(28).Bold();
                        h.Item().Text("Comprobante de venta").FontSize(12).FontColor(Colors.Grey.Medium);
                        h.Item().PaddingTop(4).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                    });

                    page.Content().PaddingTop(20).Column(col =>
                    {
                        col.Item().Text($"Venta #{v["id_venta"]}").FontSize(16).Bold();
                        col.Item().PaddingTop(8).Table(t =>
                        {
                            t.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(); });
                            t.Cell().Text("Cliente:").Bold();
                            t.Cell().Text(v["cliente"].ToString());
                            t.Cell().Text("Tipo:").Bold();
                            t.Cell().Text(v["tipo"].ToString());
                            t.Cell().Text("Fecha:").Bold();
                            t.Cell().Text(v["fecha"].ToString());
                            t.Cell().Text("Método de pago:").Bold();
                            t.Cell().Text(v["metodo_pago"].ToString());
                        });

                        col.Item().PaddingTop(20).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                        col.Item().PaddingTop(12).Text("Detalle de consumo").FontSize(14).Bold();

                        if (detalles.Count > 0)
                        {
                            col.Item().PaddingTop(8).Table(t =>
                            {
                                t.ColumnsDefinition(c =>
                                {
                                    c.RelativeColumn(4);
                                    c.RelativeColumn(1);
                                    c.RelativeColumn(2);
                                    c.RelativeColumn(2);
                                });
                                t.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Producto").Bold();
                                t.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Cant.").Bold();
                                t.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Precio").Bold();
                                t.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Subtotal").Bold();

                                foreach (var d in detalles)
                                {
                                    bool esIngrediente = Convert.ToDecimal(d["precio"]) == 0;
                                    if (esIngrediente)
                                    {
                                        // Ingrediente de combo: nombre indentado, sin precio
                                        t.Cell().Padding(4).PaddingLeft(16)
                                            .Text($"└ {d["nombre"]}").FontSize(10).FontColor(Colors.Grey.Medium);
                                        t.Cell().Padding(4).Text(d["cantidad"].ToString()).FontSize(10).FontColor(Colors.Grey.Medium);
                                        t.Cell().Padding(4).Text("").FontSize(10);
                                        t.Cell().Padding(4).Text("").FontSize(10);
                                    }
                                    else
                                    {
                                        t.Cell().Padding(4).Text(d["nombre"].ToString());
                                        t.Cell().Padding(4).Text(d["cantidad"].ToString());
                                        t.Cell().Padding(4).Text($"Q{Convert.ToDecimal(d["precio"]):F2}");
                                        t.Cell().Padding(4).Text($"Q{Convert.ToDecimal(d["subtotal"]):F2}");
                                    }
                                }
                            });
                        }

                        col.Item().PaddingTop(20).LineHorizontal(2).LineColor(Colors.Black);

                        decimal subtotalBruto = detalles.Where(d => Convert.ToDecimal(d["precio"]) > 0).Sum(d => Convert.ToDecimal(d["subtotal"]));
                        decimal totalFinal = Convert.ToDecimal(v["total"]);
                        decimal descuentoPct = Convert.ToDecimal(v["descuento_pct"]);
                        decimal descuento = subtotalBruto - totalFinal;

                        col.Item().PaddingTop(8).AlignRight()
                            .Text($"Subtotal: Q{subtotalBruto:F2}").FontSize(13);
                        if (descuento > 0)
                            col.Item().PaddingTop(4).AlignRight()
                                .Text($"Descuento ({descuentoPct:G29}%): -Q{descuento:F2}").FontSize(13).FontColor(Colors.Orange.Medium);
                        col.Item().PaddingTop(4).AlignRight()
                            .Text($"TOTAL: Q{totalFinal:F2}").FontSize(20).Bold();
                    });

                    page.Footer().AlignCenter()
                        .Text($"Lobby Zone — {DateTime.Now:dd/MM/yyyy HH:mm}")
                        .FontSize(9).FontColor(Colors.Grey.Medium);
                });
            }).GeneratePdf();

            return File(pdf, "application/pdf", $"Venta_{v["id_venta"]}.pdf");
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

            if (rows.Count == 0)
                return NotFound();

            var f = rows[0];
            int idVenta = Convert.ToInt32(f["id_venta"]);

            var detalles = _db.ExecuteQuery(@"
                SELECT COALESCE(p.nombre, d.nombre, 'Servicio') AS nombre, d.cantidad, d.precio, d.subtotal
                FROM detalle_ventas d
                LEFT JOIN productos p ON d.id_producto = p.id_producto
                WHERE d.id_venta = @id
                ORDER BY d.id_detalle",
                new MySqlParameter("@id", idVenta));

            var pdf = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(40);

                    page.Header().Column(h =>
                    {
                        h.Item().Text("LOBBY ZONE").FontSize(28).Bold();
                        h.Item().Text("Panel de Control").FontSize(12).FontColor(Colors.Grey.Medium);
                        h.Item().PaddingTop(4).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                    });

                    page.Content().PaddingTop(20).Column(col =>
                    {
                        // Info factura
                        col.Item().Text($"Factura #{f["id_factura"]}").FontSize(16).Bold();
                        col.Item().PaddingTop(8).Table(t =>
                        {
                            t.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(); });
                            t.Cell().Text("Cliente:").Bold();
                            t.Cell().Text(f["nombre"].ToString());
                            t.Cell().Text("NIT:").Bold();
                            t.Cell().Text(f["nit"].ToString());
                            t.Cell().Text("Dirección:").Bold();
                            t.Cell().Text(f["direccion"].ToString());
                            t.Cell().Text("Fecha:").Bold();
                            t.Cell().Text(f["fecha"].ToString());
                            t.Cell().Text("Método de pago:").Bold();
                            t.Cell().Text(f["metodo_pago"].ToString());
                        });

                        col.Item().PaddingTop(20).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                        // Detalle de consumo
                        col.Item().PaddingTop(12).Text("Detalle de consumo").FontSize(14).Bold();

                        if (detalles.Count > 0)
                        {
                            col.Item().PaddingTop(8).Table(t =>
                            {
                                t.ColumnsDefinition(c =>
                                {
                                    c.RelativeColumn(4);
                                    c.RelativeColumn(1);
                                    c.RelativeColumn(2);
                                    c.RelativeColumn(2);
                                });

                                // Encabezados
                                t.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Producto").Bold();
                                t.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Cant.").Bold();
                                t.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Precio").Bold();
                                t.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Subtotal").Bold();

                                foreach (var d in detalles)
                                {
                                    bool esIngrediente = Convert.ToDecimal(d["precio"]) == 0;
                                    if (esIngrediente)
                                    {
                                        t.Cell().Padding(4).PaddingLeft(16)
                                            .Text($"└ {d["nombre"]}").FontSize(10).FontColor(Colors.Grey.Medium);
                                        t.Cell().Padding(4).Text(d["cantidad"].ToString()).FontSize(10).FontColor(Colors.Grey.Medium);
                                        t.Cell().Padding(4).Text("").FontSize(10);
                                        t.Cell().Padding(4).Text("").FontSize(10);
                                    }
                                    else
                                    {
                                        t.Cell().Padding(4).Text(d["nombre"].ToString());
                                        t.Cell().Padding(4).Text(d["cantidad"].ToString());
                                        t.Cell().Padding(4).Text($"Q{Convert.ToDecimal(d["precio"]):F2}");
                                        t.Cell().Padding(4).Text($"Q{Convert.ToDecimal(d["subtotal"]):F2}");
                                    }
                                }
                            });
                        }
                        else
                        {
                            col.Item().PaddingTop(8).Text("Sin detalle de productos registrado.").FontColor(Colors.Grey.Medium);
                        }

                        col.Item().PaddingTop(20).LineHorizontal(2).LineColor(Colors.Black);

                        decimal subtotalBrutoF = detalles.Where(d => Convert.ToDecimal(d["precio"]) > 0).Sum(d => Convert.ToDecimal(d["subtotal"]));
                        decimal totalFinalF = Convert.ToDecimal(f["total"]);
                        decimal descuentoPctF = Convert.ToDecimal(f["descuento_pct"]);
                        decimal descuentoF = subtotalBrutoF - totalFinalF;

                        col.Item().PaddingTop(8).AlignRight()
                            .Text($"Subtotal: Q{subtotalBrutoF:F2}").FontSize(13);
                        if (descuentoF > 0)
                            col.Item().PaddingTop(4).AlignRight()
                                .Text($"Descuento ({descuentoPctF:G29}%): -Q{descuentoF:F2}").FontSize(13).FontColor(Colors.Orange.Medium);
                        col.Item().PaddingTop(4).AlignRight()
                            .Text($"TOTAL: Q{totalFinalF:F2}").FontSize(20).Bold();
                    });

                    page.Footer().AlignCenter()
                        .Text($"Lobby Zone — Documento generado el {DateTime.Now:dd/MM/yyyy HH:mm}")
                        .FontSize(9).FontColor(Colors.Grey.Medium);
                });
            }).GeneratePdf();

            return File(pdf, "application/pdf", $"Factura_{f["id_factura"]}.pdf");
        }
    }
}
