using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using QuestPDF.Infrastructure;

QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSingleton<GamerZoneAPI.Data.DbManager>();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("PermitirFrontend",
        policy =>
        {
            policy.SetIsOriginAllowed(origin =>
                    origin == "http://localhost:5500" ||
                    origin == "http://127.0.0.1:5500" ||
                    origin == "http://localhost:3000"  ||
                    origin == "http://localhost:5069"  ||
                    origin.EndsWith(".ngrok-free.dev") ||
                    origin.EndsWith(".ngrok.io"))
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
});

// JWT
var jwtKey = builder.Configuration["Jwt:Key"]!;
var jwtIssuer = builder.Configuration["Jwt:Issuer"]!;
var jwtAudience = builder.Configuration["Jwt:Audience"]!;

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
        // Permitir token desde query string para descargas (PDF, Excel)
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var token = context.Request.Query["token"];
                if (!string.IsNullOrEmpty(token))
                    context.Token = token;
                return Task.CompletedTask;
            }
        };
    });

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("PermitirFrontend");
// app.UseHttpsRedirection();

// Servir archivos estáticos del frontend (panel.html, mis-puntos.html, app.js, etc.)
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(
        Path.Combine(builder.Environment.ContentRootPath, "fronted")),
    RequestPath = "",
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers["ngrok-skip-browser-warning"] = "true";
    }
});

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Migración: agregar columna entregado a detalle_ventas si no existe
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<GamerZoneAPI.Data.DbManager>();
    try { db.ExecuteNonQuery("ALTER TABLE detalle_ventas ADD COLUMN entregado TINYINT(1) NOT NULL DEFAULT 0"); } catch { }
    // Sincronizar productos de órdenes ya entregadas
    try { db.ExecuteNonQuery("UPDATE detalle_ventas SET entregado=1 WHERE id_venta IN (SELECT id_venta FROM ventas WHERE entregado=1)"); } catch { }
}

app.Run();