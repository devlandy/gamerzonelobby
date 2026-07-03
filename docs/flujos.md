# Flujos del negocio

## 1. Login

```
1. Cajero / Admin abre login.html
2. Ingresa usuario y contraseña
3. POST /api/usuarios/login { usuario, password }
4. API consulta usuarios WHERE usuario=? AND password=?
5. Si existe → genera JWT con claims: id_usuario, nombre, usuario, rol
6. Frontend guarda token en localStorage
7. Redirige a panel.html
```

**Endpoint:** `POST /api/usuarios/login` (público, sin autenticación)

---

## 2. Venta POS (productos)

```
1. Cajero busca cliente (opcional): GET /api/clientes/buscar?texto=...
2. Agrega productos al carrito (frontend acumula en memoria)
3. Selecciona método de pago (EFECTIVO / TARJETA / PENDIENTE)
4. POST /api/ventas
   Body: { id_cliente, id_usuario, productos[], metodo_pago, ... }
5. API abre transacción MySQL:
   a. INSERT INTO ventas → obtiene id_venta
   b. INSERT INTO detalle_ventas por cada producto
   c. UPDATE productos SET stock=stock-cantidad (si controla_stock=1)
   d. INSERT INTO historial_inventario (movimiento de SALIDA)
   e. COMMIT
6. Frontend recibe { id_venta, total }
7. Cajero puede generar ticket PDF: GET /api/pdf/venta/{id}?token=...
```

**Nota:** Si `id_producto = 0`, se trata como servicio de consola y no descuenta stock.

---

## 3. Venta de consola (tiempo de juego)

```
1. Cajero selecciona consola disponible: GET /api/consolas
2. Registra inicio de sesión de juego (frontend maneja el tiempo)
3. Al finalizar: POST /api/ventas con tipo='CONSOLA' e id_producto=0
4. Consola vuelve a estado DISPONIBLE: PUT /api/consolas/{id}/estado
```

---

## 4. Cierre diario

```
1. Admin abre sección de cierre en panel
2. GET /api/dashboard → muestra ventas del día, gastos, balance
3. Si no hay cierre hoy: POST /api/dashboard/cierre
   - Suma ventas del día (SUM total WHERE DATE(fecha)=CURDATE())
   - Suma gastos del día (SUM monto WHERE DATE(fecha)=CURDATE())
   - INSERT INTO cierre_diario con balance
4. Si ya existe cierre → 400 Bad Request
```

**Alternativa:** `POST /api/cierres` acepta `{ id_usuario, observacion }` y registra el cierre con usuario responsable.

---

## 5. Torneos

```
1. Admin crea torneo: POST /api/torneos
   Body: { nombre, juego, premio, inscripcion, cupos, participantes[] }
2. API abre transacción:
   a. INSERT INTO torneos
   b. Por cada participante:
      - INSERT INTO torneo_participantes (con posición)
      - INSERT INTO historial_puntos (puntos según posición: 1°→10, 2°→5, 3°→3, resto→2)
   c. COMMIT
3. Ranking visible en GET /api/torneos/top10
```

---

## 6. Factura (NIT)

```
1. Cajero solicita datos fiscales del cliente
2. POST /api/factura { id_venta, nit, nombre, direccion }
3. GET /api/pdf/factura/{id}?token=... → descarga PDF formal
```

---

## 7. Exportar reportes

```
GET /api/exportar/ventas?token=...
→ Genera Excel (.xlsx) con hoja "Ventas" y hoja "Top Productos"
→ Descarga directa al navegador
```
