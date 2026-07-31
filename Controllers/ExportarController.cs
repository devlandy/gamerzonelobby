using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using GamerZoneAPI.Data;
using ClosedXML.Excel;
using System.Text.RegularExpressions;

namespace GamerZoneAPI.Controllers
{
    [Authorize(Roles = "ADMIN")]
    [ApiController]
    [Route("api/exportar")]
    public class ExportarController : ControllerBase
    {
        private readonly DbManager _db;

        public ExportarController(DbManager db) => _db = db;

        private static string MesSafe(string? mes) =>
            Regex.IsMatch(mes ?? "", @"^\d{4}-\d{2}$") ? mes! : "";

        [HttpGet("ventas")]
        public IActionResult ExportarVentas([FromQuery] string? mes = null)
        {
            var workbook = new XLWorkbook();

            string mesFiltrado = MesSafe(mes);
            string filtroWhere = string.IsNullOrEmpty(mesFiltrado)
                ? "WHERE v.estado != 'CANCELADO'"
                : $"WHERE DATE_FORMAT(v.fecha, '%Y-%m') = '{mesFiltrado}' AND v.estado != 'CANCELADO'";

            string[] meses = { "Enero","Febrero","Marzo","Abril","Mayo","Junio","Julio","Agosto","Septiembre","Octubre","Noviembre","Diciembre" };
            string tituloMes = "Todas las ventas";
            if (!string.IsNullOrEmpty(mesFiltrado) && mesFiltrado.Length == 7)
            {
                int m = int.Parse(mesFiltrado.Split('-')[1]);
                string a = mesFiltrado.Split('-')[0];
                tituloMes = meses[m - 1] + " " + a;
            }

            // Paleta oscura consistente
            var colorFondo    = XLColor.FromHtml("#0F172A");
            var colorHeader   = XLColor.FromHtml("#1E3A5F");
            var colorSubtitle = XLColor.FromHtml("#1E293B");
            var colorVerde    = XLColor.FromHtml("#052E16");
            var colorVerdeFont= XLColor.FromHtml("#4ADE80");
            var colorRojo     = XLColor.FromHtml("#2D0A0A");
            var colorRojoFont = XLColor.FromHtml("#F87171");
            var colorAmbar    = XLColor.FromHtml("#1C1200");
            var colorAmbarFont= XLColor.FromHtml("#FCD34D");
            var colorGris     = XLColor.FromHtml("#1E293B");
            var colorTexto    = XLColor.FromHtml("#E2E8F0");
            var colorMuted    = XLColor.FromHtml("#94A3B8");

            // ── HOJA 1: VENTAS ──────────────────────────────────────────────
            var ventas = _db.ExecuteQuery($@"
                SELECT v.id_venta, v.fecha, v.total, v.descuento_pct, v.forma_cobro, v.metodo_pago,
                       c.nombre AS cliente, u.nombre AS usuario
                FROM ventas v
                JOIN clientes c ON v.id_cliente = c.id_cliente
                JOIN usuarios u ON v.id_usuario = u.id_usuario
                {filtroWhere}
                ORDER BY v.fecha DESC");

            var wsV = workbook.Worksheets.Add("Ventas");

            // Título
            wsV.Range("A1:H1").Merge();
            wsV.Cell(1, 1).Value = "REPORTE DE VENTAS — " + tituloMes.ToUpper();
            wsV.Cell(1, 1).Style.Font.Bold = true;
            wsV.Cell(1, 1).Style.Font.FontSize = 14;
            wsV.Cell(1, 1).Style.Font.FontColor = colorTexto;
            wsV.Cell(1, 1).Style.Fill.BackgroundColor = colorFondo;
            wsV.Row(1).Height = 28;

            // Sub-resumen
            decimal totalVentas = ventas.Sum(r => Convert.ToDecimal(r["total"]));
            wsV.Range("A2:D2").Merge();
            wsV.Cell(2, 1).Value = ventas.Count + " ventas registradas";
            wsV.Cell(2, 1).Style.Font.FontColor = colorMuted;
            wsV.Cell(2, 1).Style.Fill.BackgroundColor = colorSubtitle;
            wsV.Range("E2:H2").Merge();
            wsV.Cell(2, 5).Value = "Total: Q " + totalVentas.ToString("N2");
            wsV.Cell(2, 5).Style.Font.Bold = true;
            wsV.Cell(2, 5).Style.Font.FontColor = colorVerdeFont;
            wsV.Cell(2, 5).Style.Fill.BackgroundColor = colorSubtitle;
            wsV.Row(2).Height = 20;

            wsV.Row(3).Height = 6; // Espacio

            // Encabezados
            string[] colsV = { "ID", "Fecha", "Total (Q)", "Descuento (%)", "Cliente", "Usuario", "Forma Cobro", "Método Pago" };
            for (int i = 0; i < colsV.Length; i++)
            {
                wsV.Cell(4, i + 1).Value = colsV[i];
                wsV.Cell(4, i + 1).Style.Font.Bold = true;
                wsV.Cell(4, i + 1).Style.Font.FontColor = XLColor.White;
                wsV.Cell(4, i + 1).Style.Fill.BackgroundColor = colorHeader;
                wsV.Cell(4, i + 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                wsV.Cell(4, i + 1).Style.Border.BottomBorder = XLBorderStyleValues.Medium;
                wsV.Cell(4, i + 1).Style.Border.BottomBorderColor = XLColor.FromHtml("#3B82F6");
            }
            wsV.Row(4).Height = 22;

            // Datos con color por método de pago
            int row = 5;
            foreach (var r in ventas)
            {
                string metodo = r["metodo_pago"]?.ToString() ?? "";
                string forma  = r["forma_cobro"]?.ToString() ?? "";

                XLColor bgColor = colorGris;
                XLColor ftColor = colorTexto;
                if (metodo == "EFECTIVO")       { bgColor = XLColor.FromHtml("#052014"); ftColor = colorVerdeFont; }
                else if (metodo == "TARJETA")   { bgColor = XLColor.FromHtml("#0A1527"); ftColor = XLColor.FromHtml("#93C5FD"); }
                else if (metodo == "TRANSFERENCIA") { bgColor = XLColor.FromHtml("#1C0A27"); ftColor = XLColor.FromHtml("#C4B5FD"); }

                wsV.Cell(row, 1).Value = r["id_venta"]?.ToString();
                string fechaStr = r["fecha"]?.ToString() ?? "";
                wsV.Cell(row, 2).Value = fechaStr.Length >= 19 ? fechaStr.Substring(0, 19) : fechaStr;
                wsV.Cell(row, 3).Value = Convert.ToDecimal(r["total"]);
                wsV.Cell(row, 4).Value = Convert.ToDecimal(r["descuento_pct"]);
                wsV.Cell(row, 5).Value = r["cliente"]?.ToString();
                wsV.Cell(row, 6).Value = r["usuario"]?.ToString();
                wsV.Cell(row, 7).Value = forma;
                wsV.Cell(row, 8).Value = metodo;

                for (int col = 1; col <= 8; col++)
                {
                    wsV.Cell(row, col).Style.Fill.BackgroundColor = bgColor;
                    wsV.Cell(row, col).Style.Font.FontColor = ftColor;
                }
                wsV.Cell(row, 3).Style.NumberFormat.Format = "#,##0.00";
                wsV.Cell(row, 4).Style.NumberFormat.Format = "0.00\"%\"";
                wsV.Row(row).Height = 18;
                row++;
            }

            // Fila total
            wsV.Cell(row, 2).Value = "TOTAL:";
            wsV.Cell(row, 2).Style.Font.Bold = true;
            wsV.Cell(row, 2).Style.Font.FontColor = colorTexto;
            wsV.Cell(row, 2).Style.Fill.BackgroundColor = colorFondo;
            wsV.Cell(row, 3).FormulaA1 = $"SUM(C5:C{row - 1})";
            wsV.Cell(row, 3).Style.Font.Bold = true;
            wsV.Cell(row, 3).Style.Font.FontColor = colorVerdeFont;
            wsV.Cell(row, 3).Style.Fill.BackgroundColor = colorVerde;
            wsV.Cell(row, 3).Style.NumberFormat.Format = "#,##0.00";

            wsV.SheetView.FreezeRows(4);
            wsV.Columns().AdjustToContents();

            // ── HOJA 2: TOP PRODUCTOS ────────────────────────────────────────
            var productos = _db.ExecuteQuery($@"
                SELECT p.nombre, SUM(d.cantidad) AS total_vendidos, SUM(d.subtotal) AS total_ingresos
                FROM detalle_ventas d
                JOIN productos p ON d.id_producto = p.id_producto
                JOIN ventas v ON d.id_venta = v.id_venta
                {filtroWhere}
                GROUP BY p.nombre
                ORDER BY total_vendidos DESC
                LIMIT 20");

            var wsP = workbook.Worksheets.Add("Top Productos");

            wsP.Range("A1:C1").Merge();
            wsP.Cell(1, 1).Value = "TOP PRODUCTOS — " + tituloMes.ToUpper();
            wsP.Cell(1, 1).Style.Font.Bold = true;
            wsP.Cell(1, 1).Style.Font.FontSize = 14;
            wsP.Cell(1, 1).Style.Font.FontColor = colorTexto;
            wsP.Cell(1, 1).Style.Fill.BackgroundColor = colorFondo;
            wsP.Row(1).Height = 28;

            string[] colsP = { "Producto", "Unidades Vendidas", "Total Ingresos (Q)" };
            for (int i = 0; i < colsP.Length; i++)
            {
                wsP.Cell(3, i + 1).Value = colsP[i];
                wsP.Cell(3, i + 1).Style.Font.Bold = true;
                wsP.Cell(3, i + 1).Style.Font.FontColor = XLColor.White;
                wsP.Cell(3, i + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#14532D");
                wsP.Cell(3, i + 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                wsP.Cell(3, i + 1).Style.Border.BottomBorder = XLBorderStyleValues.Medium;
                wsP.Cell(3, i + 1).Style.Border.BottomBorderColor = colorVerdeFont;
            }
            wsP.Row(3).Height = 22;

            int rowP = 4;
            foreach (var r in productos)
            {
                XLColor bg = rowP % 2 == 0 ? XLColor.FromHtml("#071D0F") : colorVerde;
                wsP.Cell(rowP, 1).Value = r["nombre"]?.ToString();
                wsP.Cell(rowP, 2).Value = Convert.ToInt32(r["total_vendidos"]);
                wsP.Cell(rowP, 3).Value = Convert.ToDecimal(r["total_ingresos"]);
                for (int col = 1; col <= 3; col++)
                {
                    wsP.Cell(rowP, col).Style.Fill.BackgroundColor = bg;
                    wsP.Cell(rowP, col).Style.Font.FontColor = colorVerdeFont;
                }
                wsP.Cell(rowP, 3).Style.NumberFormat.Format = "#,##0.00";
                wsP.Row(rowP).Height = 18;
                rowP++;
            }

            wsP.Cell(rowP, 1).Value = "TOTAL:";
            wsP.Cell(rowP, 1).Style.Font.Bold = true;
            wsP.Cell(rowP, 1).Style.Font.FontColor = colorTexto;
            wsP.Cell(rowP, 1).Style.Fill.BackgroundColor = colorFondo;
            wsP.Cell(rowP, 2).FormulaA1 = $"SUM(B4:B{rowP - 1})";
            wsP.Cell(rowP, 2).Style.Font.Bold = true;
            wsP.Cell(rowP, 2).Style.Font.FontColor = colorVerdeFont;
            wsP.Cell(rowP, 2).Style.Fill.BackgroundColor = colorVerde;
            wsP.Cell(rowP, 3).FormulaA1 = $"SUM(C4:C{rowP - 1})";
            wsP.Cell(rowP, 3).Style.Font.Bold = true;
            wsP.Cell(rowP, 3).Style.Font.FontColor = colorVerdeFont;
            wsP.Cell(rowP, 3).Style.Fill.BackgroundColor = colorVerde;
            wsP.Cell(rowP, 3).Style.NumberFormat.Format = "#,##0.00";

            wsP.SheetView.FreezeRows(3);
            wsP.Columns().AdjustToContents();

            // ── HOJA 3: MÉTODOS DE PAGO ─────────────────────────────────────
            var metodos = _db.ExecuteQuery($@"
                SELECT metodo_pago, COUNT(*) AS cantidad, SUM(total) AS total
                FROM ventas v
                {filtroWhere}
                GROUP BY metodo_pago
                ORDER BY total DESC");

            var wsM = workbook.Worksheets.Add("Métodos de Pago");

            wsM.Range("A1:C1").Merge();
            wsM.Cell(1, 1).Value = "MÉTODOS DE PAGO — " + tituloMes.ToUpper();
            wsM.Cell(1, 1).Style.Font.Bold = true;
            wsM.Cell(1, 1).Style.Font.FontSize = 14;
            wsM.Cell(1, 1).Style.Font.FontColor = colorTexto;
            wsM.Cell(1, 1).Style.Fill.BackgroundColor = colorFondo;
            wsM.Row(1).Height = 28;

            string[] colsM = { "Método de Pago", "N° Transacciones", "Total (Q)" };
            for (int i = 0; i < colsM.Length; i++)
            {
                wsM.Cell(3, i + 1).Value = colsM[i];
                wsM.Cell(3, i + 1).Style.Font.Bold = true;
                wsM.Cell(3, i + 1).Style.Font.FontColor = XLColor.White;
                wsM.Cell(3, i + 1).Style.Fill.BackgroundColor = colorHeader;
                wsM.Cell(3, i + 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                wsM.Cell(3, i + 1).Style.Border.BottomBorder = XLBorderStyleValues.Medium;
                wsM.Cell(3, i + 1).Style.Border.BottomBorderColor = XLColor.FromHtml("#3B82F6");
            }
            wsM.Row(3).Height = 22;

            int rowM = 4;
            foreach (var r in metodos)
            {
                string metodo = r["metodo_pago"]?.ToString() ?? "";
                XLColor bg = metodo == "EFECTIVO" ? XLColor.FromHtml("#052014")
                           : metodo == "TARJETA"  ? XLColor.FromHtml("#0A1527")
                           : XLColor.FromHtml("#1C0A27");
                XLColor ft = metodo == "EFECTIVO" ? colorVerdeFont
                           : metodo == "TARJETA"  ? XLColor.FromHtml("#93C5FD")
                           : XLColor.FromHtml("#C4B5FD");

                wsM.Cell(rowM, 1).Value = metodo;
                wsM.Cell(rowM, 2).Value = Convert.ToInt32(r["cantidad"]);
                wsM.Cell(rowM, 3).Value = Convert.ToDecimal(r["total"]);
                for (int col = 1; col <= 3; col++)
                {
                    wsM.Cell(rowM, col).Style.Fill.BackgroundColor = bg;
                    wsM.Cell(rowM, col).Style.Font.FontColor = ft;
                }
                wsM.Cell(rowM, 3).Style.NumberFormat.Format = "#,##0.00";
                wsM.Row(rowM).Height = 20;
                rowM++;
            }

            wsM.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            string nombreArchivo = string.IsNullOrEmpty(mes)
                ? "ReporteVentas_Todo.xlsx"
                : $"ReporteVentas_{mes}.xlsx";
            return File(stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                nombreArchivo);
        }
    }
}
