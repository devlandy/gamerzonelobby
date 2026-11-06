using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MySql.Data.MySqlClient;
using System.Security.Claims;
using GamerZoneAPI.Data;

namespace GamerZoneAPI.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/reportes-problema")]
    public class ReportesProblemaController : ControllerBase
    {
        private readonly DbManager _db;
        private readonly IHttpClientFactory _http;
        private readonly IConfiguration _config;

        public ReportesProblemaController(DbManager db, IHttpClientFactory http, IConfiguration config)
        {
            _db = db;
            _http = http;
            _config = config;
        }

        [HttpPost]
        public async Task<IActionResult> Reportar([FromBody] ReporteProblemaRequest request)
        {
            string descripcion = Recortar(request.descripcion, 1000);
            if (descripcion == "")
                return BadRequest(new { error = "Describe qué pasó" });

            string usuario = User.FindFirstValue(ClaimTypes.Name) ?? "desconocido";
            string rol     = User.FindFirstValue(ClaimTypes.Role) ?? "";
            string pantalla = Recortar(request.pantalla, 60);
            string error    = Recortar(request.error, 1500);
            string hora     = Recortar(request.hora, 40);

            var id = Convert.ToInt32(_db.ExecuteScalar(@"
                INSERT INTO reportes_problema (usuario, rol, pantalla, descripcion, detalle_error)
                VALUES (@usuario, @rol, @pantalla, @descripcion, @error);
                SELECT LAST_INSERT_ID();",
                new MySqlParameter("@usuario", usuario),
                new MySqlParameter("@rol", rol),
                new MySqlParameter("@pantalla", pantalla),
                new MySqlParameter("@descripcion", descripcion),
                new MySqlParameter("@error", error)));

            string mensaje =
                $"🐞 Reporte #{id} — Lobby Zone\n" +
                $"Usuario: {usuario} ({rol})\n" +
                $"Pantalla: {(pantalla == "" ? "—" : pantalla)}\n" +
                $"Hora: {(hora == "" ? "—" : hora)}\n\n" +
                $"Qué pasó: {descripcion}\n\n" +
                $"Último error: {(error == "" ? "ninguno registrado" : error)}";

            bool enviado = await EnviarWhatsApp(mensaje);
            if (enviado)
                _db.ExecuteNonQuery("UPDATE reportes_problema SET enviado_whatsapp = 1 WHERE id_reporte = @id",
                    new MySqlParameter("@id", id));

            return Ok(new { mensaje = "Reporte enviado", id, whatsapp = enviado });
        }

        [Authorize(Roles = "ADMIN")]
        [HttpGet]
        public IActionResult Listar()
        {
            var rows = _db.ExecuteQuery("SELECT * FROM reportes_problema ORDER BY fecha DESC LIMIT 100");
            return Ok(rows.Select(r => new
            {
                id = Convert.ToInt32(r["id_reporte"]),
                fecha = r["fecha"],
                usuario = r["usuario"]?.ToString(),
                rol = r["rol"]?.ToString(),
                pantalla = r["pantalla"]?.ToString(),
                descripcion = r["descripcion"]?.ToString(),
                detalle_error = r["detalle_error"]?.ToString(),
                whatsapp = Convert.ToInt32(r["enviado_whatsapp"]) == 1
            }));
        }

        // Envía el aviso con CallMeBot. Si no está configurado o falla, el reporte queda guardado igual.
        private async Task<bool> EnviarWhatsApp(string texto)
        {
            string? telefono = _config["CallMeBot:Phone"];
            string? apiKey   = _config["CallMeBot:ApiKey"];
            if (string.IsNullOrWhiteSpace(telefono) || string.IsNullOrWhiteSpace(apiKey))
                return false;

            try
            {
                var cliente = _http.CreateClient();
                cliente.Timeout = TimeSpan.FromSeconds(15);
                string url = "https://api.callmebot.com/whatsapp.php"
                    + "?phone=" + Uri.EscapeDataString(telefono)
                    + "&text=" + Uri.EscapeDataString(texto)
                    + "&apikey=" + Uri.EscapeDataString(apiKey);
                var resp = await cliente.GetAsync(url);
                return resp.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        private static string Recortar(string? valor, int max)
        {
            valor = (valor ?? "").Trim();
            return valor.Length > max ? valor.Substring(0, max) + "…" : valor;
        }
    }

    public class ReporteProblemaRequest
    {
        public string? descripcion { get; set; }
        public string? pantalla { get; set; }
        public string? error { get; set; }
        public string? hora { get; set; }
    }
}
