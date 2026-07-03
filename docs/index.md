# GamerZone / Lobby Zone — Documentación

Sistema de punto de venta y gestión para sala de gaming en Guatemala.

## Descripción

Lobby Zone es un POS diseñado para salas de videojuegos. Permite registrar ventas de productos y servicios de consola, gestionar inventario, administrar torneos y clientes con sistema de puntos, y generar reportes y PDFs.

## Componentes

| Componente | Descripción |
|---|---|
| **Backend** | ASP.NET Core 8 Web API (C#) |
| **Base de datos** | MySQL 8.0 — `gamer_zone_control` |
| **Frontend** | HTML / JS / CSS vanilla (carpeta `fronted/`) |
| **Autenticación** | JWT Bearer (8 horas) |

## Documentación disponible

- [arquitectura.md](arquitectura.md) — Stack, estructura de carpetas y componentes
- [flujos.md](flujos.md) — Flujos principales del negocio
- [roles_y_permisos.md](roles_y_permisos.md) — Roles, claims JWT y permisos por endpoint
- [schema.md](schema.md) — Tablas de la base de datos
- [decisiones.md](decisiones.md) — Decisiones técnicas y por qué se tomaron

## Arranque rápido

1. Restaurar la base de datos MySQL desde `backend/database/schema.sql`
2. Ajustar `appsettings.json` con la cadena de conexión y la clave JWT
3. `dotnet run` dentro de la carpeta del proyecto
4. Abrir `fronted/login.html` en el navegador

## Configuración mínima (`appsettings.json`)

```json
{
  "ConnectionStrings": {
    "Default": "server=localhost;database=gamer_zone_control;user=root;password=TU_PASS;"
  },
  "Jwt": {
    "Key": "ClaveSuperSecretaDe64CaracteresMinimo!!!!",
    "Issuer": "GamerZoneAPI",
    "Audience": "GamerZoneFrontend"
  }
}
```
