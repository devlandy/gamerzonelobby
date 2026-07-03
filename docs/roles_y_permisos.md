# Roles y permisos

## Roles del sistema

| Rol | Descripción |
|---|---|
| `ADMIN` | Acceso total: cierre diario, reportes, gestión de usuarios, torneos, exportar |
| `CAJERO` | Operaciones de POS: ventas, clientes, consultar stock, generar tickets |

El rol se define en la columna `rol` de la tabla `usuarios` y se incluye como claim en el JWT.

## JWT Claims

Al hacer login exitoso, el token contiene:

| Claim | Valor |
|---|---|
| `ClaimTypes.NameIdentifier` | `id_usuario` (número) |
| `ClaimTypes.Name` | `usuario` (nombre de usuario) |
| `ClaimTypes.Role` | `ADMIN` o `CAJERO` |

El token expira en **8 horas** desde la emisión.

## Configuración JWT (`appsettings.json`)

```json
{
  "Jwt": {
    "Key": "...",        // Clave HMAC-SHA256 (mínimo 32 caracteres)
    "Issuer": "GamerZoneAPI",
    "Audience": "GamerZoneFrontend"
  }
}
```

## Protección de endpoints

Todos los controladores tienen `[Authorize]` a nivel de clase. La única excepción es:

| Endpoint | Acceso |
|---|---|
| `POST /api/usuarios/login` | Público (`[AllowAnonymous]`) |

El resto de endpoints requieren token JWT válido en el header:
```
Authorization: Bearer <token>
```

Para descargas de archivos (PDF, Excel), el token se puede pasar como query string:
```
GET /api/pdf/venta/5?token=<token>
GET /api/exportar/ventas?token=<token>
```

## Endpoints por dominio

| Dominio | Ruta base | Requiere auth |
|---|---|---|
| Usuarios / Login | `/api/usuarios` | Solo login es público |
| Ventas | `/api/ventas` | Sí |
| Clientes | `/api/clientes` | Sí |
| Productos | `/api/productos` | Sí |
| Consolas | `/api/consolas` | Sí |
| Combos | `/api/combos` | Sí |
| Torneos | `/api/torneos` | Sí |
| Dashboard | `/api/dashboard` | Sí |
| Cierres | `/api/cierres` | Sí |
| Inventario | `/api/inventario` | Sí |
| Reportes | `/api/reportes` | Sí |
| Exportar (Excel) | `/api/exportar` | Sí |
| PDF | `/api/pdf` | Sí |
| Factura | `/api/factura` | Sí |

## Pendiente

- Las contraseñas se almacenan en texto plano. Se debe implementar BCrypt antes de producción.
- No hay diferenciación de permisos por rol en el backend: cualquier usuario autenticado puede acceder a todos los endpoints. La separación ADMIN/CAJERO hoy es solo visual en el frontend.
