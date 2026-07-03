# Arquitectura

## Stack tecnológico

| Capa | Tecnología |
|---|---|
| API | ASP.NET Core 8, C# |
| Base de datos | MySQL 8.0 |
| ORM / acceso a datos | `DbManager` (wrapper propio sobre MySql.Data) |
| Autenticación | JWT Bearer — `Microsoft.AspNetCore.Authentication.JwtBearer` |
| Generación de PDF | QuestPDF (licencia Community) |
| Exportación Excel | ClosedXML |
| Códigos QR | QRCoder |
| Frontend | HTML/JS/CSS vanilla |

## Estructura de carpetas

```
gamerzonelobby/
├── Controllers/          # Un controlador por dominio (14 controladores)
├── Data/
│   └── DbManager.cs      # Capa única de acceso a la base de datos
├── Models/               # Request bodies (DTOs de entrada)
├── docs/                 # Esta documentación
├── fronted/              # Frontend vanilla (login, panel, app.js, style.css)
├── Properties/
│   └── launchSettings.json
├── Program.cs            # Configuración de servicios y middleware
├── appsettings.json      # Cadena de conexión y JWT
└── GamerZoneAPI.csproj
```

## Flujo de una petición

```
Frontend (JS)
    → Authorization: Bearer <token>
    → HTTP request
        → Program.cs middleware:
            UseCors → UseAuthentication → UseAuthorization
        → Controller [Authorize]
            → DbManager
                → MySQL (gamer_zone_control)
        → JSON response
```

## Componentes clave

### Program.cs
Registra todos los servicios y define el pipeline de middleware. El orden importa: `UseAuthentication` debe ir antes de `UseAuthorization`. El `DbManager` se registra como `Singleton`.

### DbManager (`Data/DbManager.cs`)
Abstracción sobre `MySqlConnection`. Expone tres métodos:
- `ExecuteNonQuery` — INSERT, UPDATE, DELETE
- `ExecuteScalar` — consultas que devuelven un solo valor
- `ExecuteQuery` — consultas que devuelven filas como `List<Dictionary<string, object>>`
- `GetConnection()` — para transacciones manuales (Ventas, Torneos)

### UsuariosController
Único punto de autenticación. `POST /api/usuarios/login` recibe `{usuario, password}` y devuelve el token JWT junto con los datos del usuario.

### Frontend
Single-page informal: `login.html` → `panel.html`. El token JWT se guarda en `localStorage` y se adjunta como `Authorization: Bearer` en cada llamada a la API. Los PDFs y Excel se descargan pasando el token como query string (`?token=...`) porque los navegadores no permiten cabeceras en descargas directas.
