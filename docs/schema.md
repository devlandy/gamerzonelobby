# Schema de base de datos

Base de datos: `gamer_zone_control` (MySQL 8.0)

## Tablas principales

### usuarios
Credenciales y rol del personal. Columnas clave: `id_usuario`, `nombre`, `usuario`, `password` (texto plano — pendiente hashear), `rol` (ADMIN / CAJERO).

### clientes
Clientes de la sala. Tienen `codigo` único generado automáticamente (formato `CLI-{timestamp}`), `apodo` para torneos, y `estado` (ACTIVO / INACTIVO).

### productos
Catálogo de productos del bar/tienda. `controla_stock` indica si se descuenta inventario al vender. `activo` permite baja lógica.

### consolas
Consolas de la sala. `tipo` (PS4, PS5, XBOX, etc.), `precio_hora`, `estado` (DISPONIBLE / EN USO / MANTENIMIENTO).

### ventas
Cabecera de cada transacción. `tipo` distingue PRODUCTO vs CONSOLA. `forma_cobro` / `estado`: PAGADO, PENDIENTE. Referencia a `clientes` y `usuarios`.

### detalle_ventas
Líneas de cada venta. `id_producto` puede ser NULL (servicios de consola). Tiene columna `nombre` para guardar el nombre en el momento de la venta.

### gastos
Egresos del día. Se usan en el cálculo del cierre diario.

### cierre_diario
Registro del cierre por día. `total_ventas`, `total_gastos`, `balance`, `estado` (CERRADO). Referencia a `usuarios`.

### torneos / torneo_participantes
`torneos` guarda la info del evento. `torneo_participantes` liga torneo con cliente y registra posición final.

### historial_puntos
Acumulación de puntos por cliente. `tipo`: JUEGO o CONSUMO. `motivo`: TORNEO, CAMPEON, COMPRA, MANUAL.

### facturas
Datos fiscales (NIT, nombre, dirección) asociados a una venta para generar factura formal.

### historial_inventario
Movimientos de stock: ENTRADA o SALIDA, con cantidad, observación, usuario y fecha.

### combos / combo_detalle
Paquetes de productos vendidos juntos. `combo_detalle` lista los productos que componen cada combo.

## Relaciones clave

```
clientes ←── ventas ──→ usuarios
                └──→ detalle_ventas ──→ productos
ventas ──→ facturas
clientes ──→ historial_puntos
clientes ──→ torneo_participantes ──→ torneos
productos ──→ historial_inventario
cierre_diario ──→ usuarios
combos ──→ combo_detalle ──→ productos
```
