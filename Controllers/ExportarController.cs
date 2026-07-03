using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using GamerZoneAPI.Data;
using ClosedXML.Excel;

namespace GamerZoneAPI.Controllers
{
    [Authorize(Roles = "ADMIN")]
    [ApiController]
    [Route("api/exportar")]
    public class ExportarController : ControllerBase
    {
        private readonly DbManager _db;

        public ExportarController(DbManager db) => _db = db;

        [HttpGet("ventas")]
        public IActionResult ExportarVentas()
        {
            var workbook = new XLWorkbook();

            // HOJA 1: VENTAS
            var ventas = _db.ExecuteQuery(@"
                SELECT v.id_venta, v.fecha, v.total, v.forma_cobro, v.metodo_pago,
                       c.nombre AS cliente, u.nombre AS usuario
                FROM ventas v
                JOIN clientes c ON v.id_cliente = c.id_cliente
                JOIN usuarios u ON v.id_usuario = u.id_usuario");

            var wsVentas = workbook.Worksheets.Add("Ventas");
            wsVentas.Cell(1, 1).Value = "ID";
            wsVentas.Cell(1, 2).Value = "Fecha";
            wsVentas.Cell(1, 3).Value = "Total";
            wsVentas.Cell(1, 4).Value = "Cliente";
            wsVentas.Cell(1, 5).Value = "Usuario";
            wsVentas.Cell(1, 6).Value = "Forma Cobro";
            wsVentas.Cell(1, 7).Value = "Método Pago";

            var headerVentas = wsVentas.Range("A1:G1");
            headerVentas.Style.Font.Bold = true;
            headerVentas.Style.Fill.BackgroundColor = XLColor.DarkBlue;
            headerVentas.Style.Font.FontColor = XLColor.White;
            headerVentas.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            headerVentas.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            headerVentas.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            int row = 2;
            foreach (var r in ventas)
            {
                wsVentas.Cell(row, 1).Value = r["id_venta"]?.ToString();
                wsVentas.Cell(row, 2).Value = r["fecha"]?.ToString();
                wsVentas.Cell(row, 3).Value = Convert.ToDecimal(r["total"]);
                wsVentas.Cell(row, 4).Value = r["cliente"]?.ToString();
                wsVentas.Cell(row, 5).Value = r["usuario"]?.ToString();
                wsVentas.Cell(row, 6).Value = r["forma_cobro"]?.ToString();
                wsVentas.Cell(row, 7).Value = r["metodo_pago"]?.ToString();
                row++;
            }

            wsVentas.Column(3).Style.NumberFormat.Format = "Q #,##0.00";
            wsVentas.Cell(row, 2).Value = "TOTAL:";
            wsVentas.Cell(row, 2).Style.Font.Bold = true;
            wsVentas.Cell(row, 3).FormulaA1 = $"SUM(C2:C{row - 1})";
            wsVentas.Cell(row, 3).Style.Font.Bold = true;
            wsVentas.SheetView.FreezeRows(1);
            wsVentas.Columns().AdjustToContents();

            // HOJA 2: TOP PRODUCTOS
            var productos = _db.ExecuteQuery(@"
                SELECT p.nombre, SUM(d.cantidad) AS total_vendidos
                FROM detalle_ventas d
                JOIN productos p ON d.id_producto = p.id_producto
                GROUP BY p.nombre
                ORDER BY total_vendidos DESC
                LIMIT 10");

            var wsProd = workbook.Worksheets.Add("Top Productos");
            wsProd.Cell(1, 1).Value = "Producto";
            wsProd.Cell(1, 2).Value = "Vendidos";

            var headerProd = wsProd.Range("A1:B1");
            headerProd.Style.Font.Bold = true;
            headerProd.Style.Fill.BackgroundColor = XLColor.DarkGreen;
            headerProd.Style.Font.FontColor = XLColor.White;
            headerProd.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            headerProd.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            headerProd.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            int rowP = 2;
            foreach (var r in productos)
            {
                wsProd.Cell(rowP, 1).Value = r["nombre"]?.ToString();
                wsProd.Cell(rowP, 2).Value = Convert.ToInt32(r["total_vendidos"]);
                rowP++;
            }

            wsProd.Column(2).Style.NumberFormat.Format = "#,##0";
            wsProd.Cell(rowP, 1).Value = "TOTAL:";
            wsProd.Cell(rowP, 1).Style.Font.Bold = true;
            wsProd.Cell(rowP, 2).FormulaA1 = $"SUM(B2:B{rowP - 1})";
            wsProd.Cell(rowP, 2).Style.Font.Bold = true;
            wsProd.SheetView.FreezeRows(1);
            wsProd.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return File(stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "ReporteVentas_PRO.xlsx");
        }
    }
}
