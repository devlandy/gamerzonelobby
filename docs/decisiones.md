# Decisiones técnicas

## 1. DbManager en lugar de Conexion

**Decisión:** Se reemplazó la clase `Conexion` (con credenciales hardcodeadas) por `DbManager`, que lee la cadena de conexión desde `appsettings.json`.

**Por qué:** La clase anterior almacenaba usuario y contraseña directamente en el código fuente, lo que representa un riesgo crítico al subir el código a un repositorio. `DbManager` centraliza el acceso a la base de datos, elimina la repetición de código de conexión en cada controlador, y hace posible cambiar credenciales sin tocar el código.

**Registro:** `AddSingleton<DbManager>` porque la cadena de conexión no cambia en runtime y crear una instancia por request sería innecesario.

---

## 2. JWT en lugar de sesión de servidor

**Decisión:** Autenticación stateless con tokens JWT (HMAC-SHA256, 8 horas de expiración).

**Por qué:** El frontend es estático (archivos HTML/JS) sin servidor de sesiones. JWT permite que el backend sea completamente stateless: cualquier instancia del API puede validar el token sin consultar base de datos. El token viaja en el header `Authorization: Bearer` y, para descargas de archivos, como query string `?token=`.

---

## 3. Token por query string para descargas

**Decisión:** `Program.cs` incluye `OnMessageReceived` para leer el token desde `context.Request.Query["token"]`.

**Por qué:** Los navegadores no permiten agregar headers personalizados a peticiones iniciadas por `<a href>` o `window.open()`. Para que PDFs y Excel se descarguen directamente se necesita el token en la URL. Es una concesión de seguridad menor (el token queda en logs del servidor) justificada por la experiencia de usuario.

---

## 4. Eliminación de AuthController

**Decisión:** Se eliminó `AuthController.cs`.

**Por qué:** Era un controlador duplicado de login que recibía usuario y contraseña como **query string** (`?usuario=&password=`), exponiendo credenciales en la URL (logs, historial del navegador, proxies). El login correcto vive en `UsuariosController.Login` con `[FromBody]`.

---

## 5. Transacciones manuales en Ventas y Torneos

**Decisión:** `VentasController` y `TorneosController` usan `_db.GetConnection()` con `BeginTransaction()` en lugar de los métodos helper de `DbManager`.

**Por qué:** Una venta implica múltiples inserts atómicos (ventas + detalle + stock + historial). Si cualquier paso falla, se hace rollback completo. `DbManager` no expone transacciones en sus métodos helper por diseño; para operaciones multi-step se accede directamente a la conexión.

---

## 6. Soft-delete en productos

**Decisión:** `ProductosController` intenta DELETE y, si MySQL lanza error de foreign key, pone `activo=0`.

**Por qué:** Un producto con ventas asociadas no puede borrarse sin violar integridad referencial. El soft-delete permite mantener el historial de ventas intacto mientras el producto desaparece del catálogo activo. No es la solución más elegante pero funciona sin cambiar el schema.

---

## Pendientes conocidos

| Problema | Impacto | Solución recomendada |
|---|---|---|
| Contraseñas en texto plano | Crítico | Implementar BCrypt |
| CORS abierto (`AllowAnyOrigin`) | Medio | Restringir a dominio del frontend |
| Sin separación de permisos por rol en backend | Medio | `[Authorize(Roles = "ADMIN")]` en endpoints sensibles |
| XSS via `innerHTML` en frontend | Medio | Usar `textContent` o sanitización |
| `id_cliente: 1` hardcodeado en POS | Bajo | Usar cliente seleccionado o "Consumidor Final" dinámico |
