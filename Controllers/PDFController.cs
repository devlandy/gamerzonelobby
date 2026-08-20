using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MySql.Data.MySqlClient;
using GamerZoneAPI.Data;
using System.Text;
using System.Text.RegularExpressions;

namespace GamerZoneAPI.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/pdf")]
    public class PDFController : ControllerBase
    {
        private readonly DbManager _db;

        public PDFController(DbManager db) => _db = db;

        private static string E(object? val) =>
            System.Net.WebUtility.HtmlEncode(val?.ToString() ?? "");

        private static string QuitarEmojis(string? texto)
        {
            if (string.IsNullOrEmpty(texto)) return "";
            return Regex.Replace(texto, @"[\p{So}\p{Cs}\p{Mn}]|[\uD800-\uDFFF]", "").Trim();
        }

        private string GenerarHTML(string titulo, Dictionary<string, object> venta,
            List<Dictionary<string, object>> detalles, string cliente, string infoExtra)
        {
            decimal subtotal = detalles.Where(d => Convert.ToDecimal(d["precio"]) > 0)
                                       .Sum(d => Convert.ToDecimal(d["subtotal"]));
            decimal total = venta.ContainsKey("total") && venta["total"] != DBNull.Value
                ? Convert.ToDecimal(venta["total"]) : 0;
            decimal descPct = venta.ContainsKey("descuento_pct") && venta["descuento_pct"] != DBNull.Value
                ? Convert.ToDecimal(venta["descuento_pct"]) : 0;
            decimal descuento = descPct > 0 ? Math.Round(subtotal * descPct / 100, 2) : 0;
            var numDoc  = venta.ContainsKey("id_venta") ? venta["id_venta"]
                        : venta.ContainsKey("id_factura") ? venta["id_factura"] : "";
            var fecha = venta.ContainsKey("fecha") && venta["fecha"] != DBNull.Value
                ? Convert.ToDateTime(venta["fecha"]).AddHours(-6) : DateTime.Now;
            string metodo = venta.ContainsKey("metodo_pago") ? venta["metodo_pago"]?.ToString() ?? "" : "";
            string tipo   = QuitarEmojis(venta.ContainsKey("tipo") ? venta["tipo"]?.ToString() ?? titulo : titulo);

            var sb = new StringBuilder();
            sb.Append($@"<!DOCTYPE html>
<html lang='es'>
<head>
<meta charset='UTF-8'>
<title>{E(titulo)} #{numDoc}</title>
<style>
  * {{ margin:0; padding:0; box-sizing:border-box; }}
  body {{ font-family: Arial, Helvetica, sans-serif; font-size:12px; color:#111; background:#fff; padding:30px; }}
  h1 {{ font-size:20px; color:#0d47a1; margin-bottom:2px; }}
  .sub {{ font-size:11px; color:#666; margin-bottom:16px; }}
  hr {{ border:none; border-top:2px solid #111; margin:10px 0 16px; }}
  .info-row {{ display:flex; gap:20px; margin-bottom:16px; }}
  .info-left {{ flex:1; font-size:11px; color:#444; line-height:1.7; }}
  .info-right {{ flex:1; border:1px solid #ccc; padding:10px; font-size:11px; line-height:1.7; }}
  .info-right .label {{ font-size:9px; font-weight:bold; color:#0d47a1; text-transform:uppercase; letter-spacing:.5px; }}
  .info-right .nombre {{ font-size:14px; font-weight:bold; margin:2px 0; }}
  table {{ width:100%; border-collapse:collapse; margin-bottom:16px; }}
  th {{ background:#1a1a1a; color:#fff; padding:7px 8px; font-size:11px; text-align:left; }}
  th.r {{ text-align:right; }}
  td {{ padding:6px 8px; border-bottom:1px solid #ddd; font-size:11px; }}
  td.r {{ text-align:right; }}
  tr:nth-child(even) td {{ background:#f9f9f9; }}
  .ing td {{ color:#888; font-style:italic; }}
  .totales {{ margin-left:auto; width:260px; border-collapse:collapse; }}
  .totales td {{ padding:5px 8px; font-size:12px; border:none; }}
  .totales .lbl {{ color:#555; text-align:right; }}
  .totales .val {{ text-align:right; font-weight:600; }}
  .totales .desc {{ color:#cc0000; }}
  .total-final td {{ font-size:16px; font-weight:bold; color:#0d47a1; border-top:2px solid #0d47a1; padding-top:8px; }}
  .footer {{ margin-top:30px; text-align:center; font-size:10px; color:#888; border-top:1px solid #ddd; padding-top:10px; }}
  @media print {{
    body {{ padding:15px; }}
    @page {{ margin:1cm; size:A4; }}
  }}
</style>
</head>
<body>
<h1>EL LOBBY ZONE &mdash; {E(titulo)}</h1>
<div class='sub'>Gaming &middot; Entretenimiento &middot; Comida &middot; Guatemala</div>
<hr>
<div class='info-row'>
  <div class='info-left'>
    <b>Documento N&deg; 0{E(numDoc?.ToString())}</b><br>
    Fecha: {fecha:dd/MM/yyyy} &nbsp; Hora: {fecha:hh:mm tt}<br>
    Tipo: {E(tipo)}
  </div>
  <div class='info-right'>
    <div class='label'>Cliente</div>
    <div class='nombre'>{E(cliente)}</div>
    {(string.IsNullOrWhiteSpace(infoExtra) ? "" : $"<div>{E(infoExtra)}</div>")}
    <div>Método de pago: <b>{E(metodo)}</b></div>
  </div>
</div>
<table>
  <thead><tr>
    <th style='width:40px'>CANT.</th>
    <th>DESCRIPCIÓN</th>
    <th class='r'>PRECIO</th>
    <th class='r'>IMPORTE</th>
  </tr></thead>
  <tbody>");

            foreach (var d in detalles)
            {
                bool esIng = Convert.ToDecimal(d["precio"]) == 0;
                string nombre = E(QuitarEmojis(d["nombre"]?.ToString()));
                if (esIng)
                {
                    sb.Append($"<tr class='ing'><td></td><td>&nbsp;&nbsp;&#x2514; {nombre}</td><td></td><td></td></tr>");
                }
                else
                {
                    string cant = E(d["cantidad"]?.ToString());
                    string precio = $"Q{Convert.ToDecimal(d["precio"]):F2}";
                    string imp = $"Q{Convert.ToDecimal(d["subtotal"]):F2}";
                    sb.Append($"<tr><td style='text-align:center'>{cant}</td><td>{nombre}</td><td class='r'>{precio}</td><td class='r'>{imp}</td></tr>");
                }
            }

            sb.Append("</tbody></table>");

            // Totales
            sb.Append("<table class='totales'>");
            if (descuento > 0)
            {
                sb.Append($"<tr><td class='lbl'>Subtotal:</td><td class='val'>Q{subtotal:F2}</td></tr>");
                sb.Append($"<tr><td class='lbl desc'>Descuento ({descPct:F1}%):</td><td class='val desc'>-Q{descuento:F2}</td></tr>");
            }
            sb.Append($"<tr class='total-final'><td>TOTAL:</td><td>Q{total:F2}</td></tr>");
            sb.Append($"<tr><td class='lbl'>Forma de pago:</td><td class='val'>{E(metodo)}</td></tr>");
            sb.Append("</table>");

            sb.Append(@"
<div class='footer'>¡Gracias por tu visita! &middot; El Lobby Zone &middot; Gaming &middot; Entretenimiento &middot; Comida</div>
<script>window.onload = function(){ window.print(); }</script>
</body></html>");

            return sb.ToString();
        }

        [HttpGet("venta/{id}")]
        public IActionResult GenerarTicketVenta(int id)
        {
            var ventas = _db.ExecuteQuery(@"
                SELECT v.id_venta, v.total, IFNULL(v.metodo_pago,'') AS metodo_pago,
                       v.fecha, IFNULL(v.tipo,'PRODUCTO') AS tipo,
                       IFNULL(v.descuento_pct,0) AS descuento_pct,
                       COALESCE(c.nombre,'Consumidor Final') AS cliente
                FROM ventas v
                LEFT JOIN clientes c ON v.id_cliente = c.id_cliente
                WHERE v.id_venta = @id",
                new MySqlParameter("@id", id));

            if (ventas.Count == 0) return NotFound();
            var v = ventas[0];

            var detalles = _db.ExecuteQuery(@"
                SELECT COALESCE(p.nombre, d.nombre, 'Servicio') AS nombre,
                       IFNULL(d.cantidad,1) AS cantidad,
                       IFNULL(d.precio,0) AS precio,
                       IFNULL(d.subtotal,0) AS subtotal
                FROM detalle_ventas d
                LEFT JOIN productos p ON d.id_producto = p.id_producto
                WHERE d.id_venta = @id ORDER BY d.id_detalle",
                new MySqlParameter("@id", id));

            var html = GenerarHTML("COMPROBANTE", v, detalles,
                v["cliente"]?.ToString() ?? "Consumidor Final", "");

            return Content(html, "text/html", Encoding.UTF8);
        }

        [HttpGet("factura/{id}")]
        public IActionResult GenerarFactura(int id)
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
                       IFNULL(d.cantidad,1) AS cantidad,
                       IFNULL(d.precio,0) AS precio,
                       IFNULL(d.subtotal,0) AS subtotal
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
            var html = GenerarHTML("FACTURA", datos, detalles,
                f["nombre"]?.ToString() ?? "", infoExtra);

            return Content(html, "text/html", Encoding.UTF8);
        }
    }
}
