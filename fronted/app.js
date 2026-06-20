// 🔥 API URL
const API = "https://localhost:7076/api";

// ======================
// VARIABLES GLOBALES
// ======================
let carrito = [];
let totalVenta = 0;
let ventaPendienteActual = 0;

// ===========================
// 🔄 CAMBIAR SECCIONES
// ===========================
function mostrar(seccion) {

    document.querySelectorAll(".seccion")
    .forEach(div => {
        div.style.display = "none";
    });

    const elemento = document.getElementById(seccion);

    if(elemento){
        elemento.style.display = "block";
    }

    // VENTAS
    if(seccion === "ventas"){
        cargarCategoriasPOS();
        cargarPendientes();
        renderCarrito();
    }

    // INVENTARIO
    if(seccion === "inventario"){
        cargarAlertas();
        cargarHistorial();
        listarProductos();
    }

    // DASHBOARD
    if(seccion === "dashboard"){
        cargarDashboard();
        cargarTopClientes();
        cargarTopGamers();
    }

    // PRODUCTOS
    if(seccion === "productos"){
        listarProductos();
        cargarSelectsProducto();
    }

    // CIERRE
    if(seccion === "cierre"){
        cargarCierre();
    }

    // TORNEOS
    if(seccion === "torneos"){
        cargarTorneos();
    }

    //Consolas

    if(seccion === "ventas") {

    cargarCategoriasPOS();

    cargarPendientes();

    cargarConsolas();
}
}

// ===========================
// DASHBOARD
// ===========================
function cargarDashboard() {

    fetch(`${API}/dashboard`)
    .then(r => r.json())
    .then(data => {

        const container =
        document.getElementById("dashboardData");

        if(container){

            container.innerHTML = `
                <div class="card">
                    Ventas: Q${data.ventas_dia || 0}
                </div>

                <div class="card">
                    Pendientes: ${data.pedidos_pendientes || 0}
                </div>

                <div class="card">
                    Balance: Q${data.balance || 0}
                </div>
            `;
        }
    })
    .catch(err => {
        console.error(err);
    });
}

// ===========================
// CREAR CLIENTE
// ===========================
function crearCliente() {

    fetch(`${API}/clientes`, {

        method: "POST",

        headers: {
            "Content-Type": "application/json"
        },

        body: JSON.stringify({

            nombre:
            document.getElementById("nombre").value,

            telefono:
            document.getElementById("telefono").value,

            apodo:
            document.getElementById("apodo").value
        })
    })

    .then(r => r.json())

    .then(d => {

        alert("Cliente creado: " + d.codigo);

        buscarClientes();
    })

    .catch(() => {

        alert("Error creando cliente");
    });
}

// ===========================
// BUSCAR CLIENTES
// ===========================
function buscarClientes() {

    let texto =
    document.getElementById("buscar").value;

    fetch(`${API}/clientes/buscar?texto=${texto}`)

    .then(r => r.json())

    .then(data => {

        let html = "";

        data.forEach(c => {

            html += `
                <div class="card">

                    <h3>${c.nombre}</h3>

                    <p>${c.codigo}</p>

                    <img
                    src="${API}/clientes/qr/${c.codigo}"
                    width="120">

                </div>
            `;
        });

        const lista =
        document.getElementById("lista");

        if(lista){
            lista.innerHTML = html;
        }
    });
}

// ======================
// VENTA RAPIDA
// ======================
function ventaRapida() {

    let cliente =
    document.getElementById("idClienteVenta").value;

    let total =
    document.getElementById("totalVenta").value;

    fetch(`${API}/dashboard/venta-rapida?id_cliente=${cliente}&total=${total}`, {

        method: "POST"
    })

    .then(r => r.json())

    .then(() => {

        alert("✅ Venta registrada");

        cargarPendientes();

        cargarDashboard();
    })

    .catch(error => {

        console.log(error);

        alert("❌ Error registrando venta");
    });
}

// ======================
// ABRIR PENDIENTE
// ======================
function abrirPendiente(id){

    ventaPendienteActual = id;

    const modal =
    document.getElementById("modalPendiente");

    if(modal){
        modal.style.display = "flex";
    }
}

// ======================
// CERRAR PENDIENTE
// ======================
function cerrarPendiente(){

    const modal =
    document.getElementById("modalPendiente");

    if(modal){
        modal.style.display = "none";
    }
}

// ======================
// GUARDAR PENDIENTE
// ======================
function guardarPendiente(){

    let metodo =
    document.getElementById("metodoPendiente").value;

    let nit =
    document.getElementById("nitFactura").value;

    let nombre =
    document.getElementById("nombreFactura").value;

    let direccion =
    document.getElementById("direccionFactura").value;

    fetch(`${API}/ventas/${ventaPendienteActual}`, {

        method: "PUT",

        headers: {
            "Content-Type": "application/json"
        },

        body: JSON.stringify({

            forma_cobro: "PAGADO",

            metodo_pago: metodo,

            observacion: "Factura generada"
        })
    })

    .then(r => r.json())

    .then(() => {

        return fetch(`${API}/factura`, {

            method: "POST",

            headers: {
                "Content-Type": "application/json"
            },

            body: JSON.stringify({

                id_venta: ventaPendienteActual,

                nit: nit,

                nombre: nombre,

                direccion: direccion
            })
        });
    })

    .then(r => r.json())

    .then(() => {

        alert("✅ Pago registrado");

        cerrarPendiente();

        cargarPendientes();

        cargarDashboard();
    });
}

// ======================
// PAGAR VENTA
// ======================
function pagarVenta(id) {

    fetch(`${API}/ventas/pagar/${id}`, {

        method: "PUT"
    })

    .then(r => r.json())

    .then(() => {

        alert("✅ Venta pagada");

        cargarPendientes();

        cargarDashboard();
    })

    .catch(error => {

        console.log(error);

        alert("❌ Error pagando venta");
    });
}

// ===========================
// PENDIENTES
// ===========================
function cargarPendientes(){

    fetch(`${API}/ventas/pendientes`)

    .then(r => r.json())

    .then(data => {

        let html = "";

        data.forEach(v => {

            html += `

            <div class="card">

                <h3>${v.cliente}</h3>

                <p>
                Total: Q${v.total}
                </p>

                <p>
                Estado: ${v.estado}
                </p>

                <button class="btn"
                onclick="abrirPendiente(${v.id})">

                    Abrir

                </button>

                <button class="btn"
onclick="descargarFactura(${v.id})">

    PDF

</button>

            </div>
            `;
        });

        const pendientes =
        document.getElementById("pendientes");

        if(pendientes){
            pendientes.innerHTML = html;
        }
    })

    .catch(error => {
        console.log(error);
    });
}

// ======================
// DESCARGAR FACTURA
// ======================
function descargarFactura(id){

    window.open(
        `${API}/pdf/factura/${id}`,
        "_blank"
    );
}

// ======================
// PRODUCTOS
// ======================
function listarProductos(){

    fetch(`${API}/productos`)

    .then(r => r.json())

    .then(data => {

        let html = "";

        data.forEach(p => {

            html += `

            <div class="card">

                <h2>${p.nombre}</h2>

                <p style="color:var(--text-faint);font-size:12px;">ID: ${p.id}</p>

                <p>
                Precio: Q${p.precio_venta}
                </p>

                <p>
                Stock: ${ p.controla_stock == 0 ? "Sin control (hecho al momento)" : p.stock }
                </p>

                <hr>

                <input
                id="precio${p.id}"
                placeholder="Nuevo precio"
                value="${p.precio_venta}">

                <input
                id="stock${p.id}"
                placeholder="Nuevo stock"
                value="${p.stock}">

                <button class="btn"
                onclick="editarProducto(${p.id})">

                    Guardar

                </button>

                <button class="btn cancelar"
                onclick="eliminarProducto(${p.id})">

                    Eliminar

                </button>

            </div>
            `;
        });

        const productos =
        document.getElementById("productosLista");

        if(productos){
            productos.innerHTML = html;
        }
    });
}

// ======================
// EDITAR PRODUCTO
// ======================
function editarProducto(id){

    let usuario =
    JSON.parse(localStorage.getItem("usuario"));

    let precio =
    document.getElementById(`precio${id}`).value;

    let stock =
    document.getElementById(`stock${id}`).value;

    fetch(`${API}/productos/${id}`, {

        method: "PUT",

        headers: {
            "Content-Type": "application/json"
        },

        body: JSON.stringify({

            precio_venta: precio,

            stock: stock,

            usuario:
            usuario ? usuario.nombre : "ADMIN"
        })
    })

    .then(r => r.json())

    .then(() => {

        alert("✅ Producto actualizado");

        listarProductos();
    });
}

// ======================
// LOGIN
// ======================
function login(){

    let usuario =
    document.getElementById("usuario").value;

    let password =
    document.getElementById("password").value;

    fetch(`${API}/usuarios/login`, {

        method: "POST",

        headers: {
            "Content-Type": "application/json"
        },

        body: JSON.stringify({

            usuario: usuario,

            password: password
        })
    })

    .then(r => r.json())

    .then(data => {

        localStorage.setItem(

            "usuario",

            JSON.stringify({

                id_usuario:
                data.id_usuario,

                nombre:
                data.nombre,

                rol:
                data.rol
            })
        );

        window.location = "panel.html";
    })

    .catch(error => {

        console.log(error);

        alert("❌ Usuario incorrecto");
    });
}

// ======================
// LOGOUT
// ======================
function logout() {

    localStorage.removeItem("usuario");

    window.location.href = "login.html";
}

// ======================
// CATEGORIAS POS
// ======================
function cargarCategoriasPOS(){

    fetch(`${API}/productos/categorias`)

    .then(r => r.json())

    .then(data => {

        console.log(data);

        let html = "";

        data.forEach(c => {

            html += `

            <button
            class="btn"
            onclick="cargarProductosCategoria(${c.id})">

                ${c.nombre}

            </button>

            `;
        });

        document.getElementById(
        "categoriasPOS"
        ).innerHTML = html;
    })

    .catch(error => {

        console.log(error);

        alert(
        "Error cargando categorías"
        );
    });
}

// ======================
// SUBCATEGORIAS
// ======================
// ======================
// SUBCATEGORIAS
// ======================
function cargarProductosCategoria(id){

    fetch(`${API}/productos/subcategorias/${id}`)

    .then(r => r.json())

    .then(data => {

        console.log(
        "SUBCATEGORIAS:",
        data
        );

        let html = "";

        data.forEach(s => {

            html += `

            <button
            class="btn"
            onclick="cargarSubcategoria(${s.id_subcategoria})">

                ${s.nombre}

            </button>

            `;
        });

        document.getElementById(
        "subcategoriasPOS"
        ).innerHTML = html;
    })

    .catch(error => {

        console.log(error);

        alert(
        "Error cargando subcategorías"
        );
    });
}

// ======================
// PRODUCTOS SUBCATEGORIA
// ======================

function cargarSubcategoria(id){

    fetch(`${API}/productos/subcategoria/${id}`)

    .then(response => {

        if(!response.ok){

            throw new Error(
            "Error del servidor"
            );
        }

        return response.json();
    })

    .then(data => {

        console.log(
        "PRODUCTOS:",
        data
        );

        let html = "";

        // LIMPIAR
        document.getElementById(
        "productosPOS"
        ).innerHTML = "";

        // VALIDAR ARRAY
        if(!Array.isArray(data)){

            throw new Error(
            "La respuesta no es un array"
            );
        }

        // SI NO HAY PRODUCTOS
        if(data.length === 0){

            html = `

            <div class="card">

                No hay productos

            </div>

            `;

            document.getElementById(
            "productosPOS"
            ).innerHTML = html;

            return;
        }

        // RECORRER PRODUCTOS
        data.forEach(p => {

            console.log(p);

            html += `

            <div class="card">

                <h3>
                    ${p.nombre || 'Sin nombre'}
                </h3>

                <p>
                    Precio:
                    Q${p.precio_venta || 0}
                </p>

                <p>
                    Stock:
                    ${p.stock || 0}
                </p>

                <button
                class="btn"
                onclick="agregarCarrito(
                    ${p.id_producto},
                    '${p.nombre}',
                    ${p.precio_venta}
                )">

                    Agregar

                </button>

            </div>

            `;
        });

        document.getElementById(
        "productosPOS"
        ).innerHTML = html;
    })

    .catch(error => {

        console.error(
        "ERROR PRODUCTOS:",
        error
        );

        alert(
        "Error cargando productos"
        );
    });
}
// ======================
// AGREGAR CARRITO
// ======================
function agregarCarrito(
id,
nombre,
precio
){

    let existente =
    carrito.find(
        p => p.id_producto === id
    );

    if(existente){

        existente.cantidad++;
    }
    else{

        carrito.push({

            id_producto: id,

            nombre: nombre,

            precio: precio,

            cantidad: 1
        });
    }

    renderCarrito();

    mostrarMensaje("✅ Producto agregado");
}

// ======================
// RENDER CARRITO
// ======================
function renderCarrito(){

    let html = "";

    totalVenta = 0;

    carrito.forEach((p,index) => {

        let subtotal =
        p.precio * p.cantidad;

        totalVenta += subtotal;

        html += `

        <div class="card">

            <h3>${p.nombre}</h3>

            <p>
            Cantidad: ${p.cantidad}
            </p>

            <p>
            Subtotal: Q${subtotal}
            </p>

            <button class="btn"
            onclick="eliminarCarrito(${index})">

                Eliminar

            </button>

        </div>
        `;
    });

    html += `

    <div class="card">

        <h2>Total: Q${totalVenta}</h2>

        <button class="btn"
        onclick="abrirModalPago()">

            Finalizar Venta

        </button>

    </div>
    `;

    const carritoDiv =
    document.getElementById("carrito");

    if(carritoDiv){
        carritoDiv.innerHTML = html;
    }
}

// ======================
// ELIMINAR CARRITO
// ======================
function eliminarCarrito(index){

    carrito.splice(index,1);

    renderCarrito();
}

// ======================
// MODAL PAGO
// ======================
function abrirModalPago(){

    if(carrito.length <= 0){

        mostrarMensaje("❌ Carrito vacío");

        return;
    }

    const modal =
    document.getElementById("modalPago");

    if(modal){
        modal.style.display = "flex";
    }
}

function cerrarModalPago(){

    const modal =
    document.getElementById("modalPago");

    if(modal){
        modal.style.display = "none";
    }
}

// ======================
// CONFIRMAR VENTA
// ======================
function confirmarVenta(){

    let metodo =
    document.getElementById("metodoPago").value;

    let observacion =
    document.getElementById("observacionVenta").value;

    registrarVenta(
        metodo,
        observacion
    );

    cerrarModalPago();
}

// ======================
// REGISTRAR VENTA
// ======================
function registrarVenta(
metodo,
observacion
){

    let usuario =
    JSON.parse(
    localStorage.getItem("usuario")
    );

    let venta = {

        id_cliente: 1,

        id_usuario:
        usuario.id_usuario,

        nombre_orden: "POS",

        numero_orden: "000",

        forma_cobro:
        metodo === "PENDIENTE"
        ? "PENDIENTE"
        : "PAGADO",

        metodo_pago: metodo,

        total: totalVenta,

        observacion: observacion,

        productos: carrito
    };

    fetch(`${API}/ventas`, {

        method: "POST",

        headers: {
            "Content-Type": "application/json"
        },

        body: JSON.stringify(venta)
    })

    .then(r => r.json())

    .then(() => {

        mostrarMensaje(
        "✅ Venta registrada"
        );

        carrito = [];

        totalVenta = 0;

        renderCarrito();

        cargarDashboard();

        cargarPendientes();

        listarProductos();
    })

    .catch(error => {

        console.log(error);

        mostrarMensaje(
        "❌ Error registrando venta"
        );
    });
}

// ======================
// VACIAR CARRITO
// ======================
function vaciarCarrito(){

    carrito = [];

    totalVenta = 0;

    renderCarrito();
}

// ======================
// MENSAJE
// ======================
function mostrarMensaje(texto){

    alert(texto);
}

// ======================
// WINDOW LOAD
// ======================
window.onload = function(){

    let usuario =
    JSON.parse(
    localStorage.getItem("usuario")
    );

    if(usuario){

        const nombre =
        document.getElementById("usuarioNombre");

        const rol =
        document.getElementById("usuarioRol");

        if(nombre){
            nombre.innerText = usuario.nombre;
        }

        if(rol){
            rol.innerText = usuario.rol;
        }
    }

    // 🔐 Permisos según el rol (admin vs cajero)
    aplicarPermisos();

    cargarDashboard();

    listarProductos();

    cargarCategoriasPOS();

    cargarPendientes();
};


// ===========================
// 🎮 CONSOLAS
// ===========================

function cargarConsolas() {

    fetch(`${API}/consolas`)
    .then(r => r.json())
    .then(data => {

        let html = "";

        data.forEach(c => {

            let colorEstado = "#22c55e";

            if(c.estado === "OCUPADA")
                colorEstado = "#ef4444";

            html += `

            <div class="card">

                <h3>${c.nombre}</h3>

                <p>
                Tipo: ${c.tipo}
                </p>

                <p>
                Precio Hora: Q${c.precio}
                </p>

                <p style="
                color:${colorEstado};
                font-weight:bold;
                ">
                ${c.estado}
                </p>

                ${
                    c.estado === "LIBRE"

                    ?

                    `
                    <button class="btn"
                    onclick="iniciarConsola(${c.id})">

                    Iniciar

                    </button>
                    `

                    :

                    `
                    <button class="btn"
                    onclick="finalizarConsola(${c.id})">

                    Finalizar

                    </button>
                    `
                }

            </div>

            `;
        });

        document.getElementById("listaConsolas").innerHTML = html;

    });
}

// ===========================
// INICIAR CONSOLA
// ===========================

function iniciarConsola(id) {

    fetch(`${API}/consolas/${id}/estado`, {

        method: "PUT",

        headers: {
            "Content-Type": "application/json"
        },

        body: JSON.stringify({
            estado: "OCUPADA"
        })

    })
    .then(r => r.json())
    .then(() => {

        alert("🎮 Consola iniciada");

        cargarConsolas();

    });
}

// ===========================
// FINALIZAR CONSOLA
// ===========================

function finalizarConsola(id){

    let minutos =
    prompt("Minutos jugados");

    if(!minutos) return;

    fetch(`${API}/consolas`)
    .then(r => r.json())
    .then(data => {

        let consola =
        data.find(c => c.id === id);

        if(!consola){

            alert("Consola no encontrada");
            return;
        }

        let total =
        (consola.precio / 60)
        * minutos;

        carrito.push({

            id_producto: 0,

            nombre:
            `🎮 ${consola.nombre} (${minutos} min)`,

            precio: total,

            cantidad: 1,

            tipo: "SERVICIO"
        });

        renderCarrito();

        return fetch(
        `${API}/consolas/${id}/estado`,
        {

            method:"PUT",

            headers:{
                "Content-Type":"application/json"
            },

            body:JSON.stringify({
                estado:"LIBRE"
            })
        });

    })

    .then(() => {

        cargarConsolas();

        mostrarMensaje(
        "✅ Servicio agregado al carrito"
        );

    })

    .catch(error => {

        console.log(error);

        alert("Error finalizando consola");
    });
}

// ==================================================================
// 🔐 PERMISOS POR ROL  (Fase 1)
// ------------------------------------------------------------------
// Julio y Cristian (rol ADMIN) ven todo.
// El resto (cajero/empleado) solo puede cobrar: ve Ventas y Cerrar Sesión.
// ==================================================================
const ROLES_ADMIN = ["ADMIN", "ADMINISTRADOR"];
const NOMBRES_ADMIN = ["julio", "cristian"];

function esAdmin() {

    let usuario =
    JSON.parse(localStorage.getItem("usuario"));

    if(!usuario) return false;

    let rol = (usuario.rol || "").toString().toUpperCase();
    let nombre = (usuario.nombre || "").toString().toLowerCase();

    return ROLES_ADMIN.includes(rol)
        || NOMBRES_ADMIN.some(n => nombre.includes(n));
}

function aplicarPermisos() {

    if(esAdmin()) return; // admin: sin restricciones

    // Cajero: ocultar todo el menú menos Ventas y Cerrar Sesión
    document.querySelectorAll(".sidebar button").forEach(btn => {

        let accion = (btn.getAttribute("onclick") || "");

        let permitido =
            accion.includes("'ventas'") ||
            accion.includes("logout");

        if(!permitido){
            btn.style.display = "none";
        }
    });

    // Llevarlo directo al POS de Ventas
    mostrar("ventas");
}


// ==================================================================
// 🪟 MODALES DE RANKINGS  (abrir / cerrar)
// ==================================================================
function abrirModal(id){

    const modal = document.getElementById(id);

    if(modal){
        modal.style.display = "flex";

        // cargar datos al abrir
        if(id === "modalClientes") cargarTopClientes();
        if(id === "modalGamers")   cargarTopGamers();
    }
}

function cerrarModal(id){

    const modal = document.getElementById(id);

    if(modal){
        modal.style.display = "none";
    }
}


// ==================================================================
// 👑 TOP CLIENTES FRECUENTES
// ==================================================================
function cargarTopClientes(){

    fetch(`${API}/dashboard/top-clientes`)

    .then(r => r.json())

    .then(data => {

        let html = "";

        if(!data || data.length === 0){
            html = `<div class="card">Sin clientes aún</div>`;
        }

        data.forEach((c, i) => {

            html += `
            <div class="card">
                <h3>#${i + 1} ${c.nombre}</h3>
                <p>Compras: ${c.compras}</p>
                <p>Total: Q${c.total || 0}</p>
                <p>Puntos: ${c.puntos || 0}</p>
            </div>
            `;
        });

        const cont = document.getElementById("topClientes");
        if(cont) cont.innerHTML = html;
    })

    .catch(error => {
        console.log(error);
        mostrarMensaje("❌ Error cargando clientes frecuentes");
    });
}


// ==================================================================
// 🎮 TOP GAMERS (puntos de juego)
// ==================================================================
function cargarTopGamers(){

    fetch(`${API}/dashboard/top-gamers`)

    .then(r => r.json())

    .then(data => {

        let html = "";

        if(!data || data.length === 0){
            html = `<div class="card">Sin gamers aún</div>`;
        }

        data.forEach((g, i) => {

            html += `
            <div class="card">
                <h3>#${i + 1} ${g.nombre}</h3>
                <p>Apodo: ${g.apodo || "-"}</p>
                <p>Puntos: ${g.puntos || 0}</p>
            </div>
            `;
        });

        const cont = document.getElementById("topGamers");
        if(cont) cont.innerHTML = html;
    })

    .catch(error => {
        console.log(error);
        mostrarMensaje("❌ Error cargando top gamers");
    });
}


// ==================================================================
// ➕ AGREGAR STOCK RÁPIDO (dashboard)
// ==================================================================
function agregarStockRapido(){

    let id = document.getElementById("productoRapido").value;
    let cantidad = document.getElementById("cantidadRapida").value;

    if(!id || !cantidad){
        mostrarMensaje("❌ Ingresa ID del producto y cantidad");
        return;
    }

    fetch(`${API}/productos/stock`, {

        method: "PUT",

        headers: {
            "Content-Type": "application/json"
        },

        body: JSON.stringify({
            id_producto: parseInt(id),
            cantidad: parseInt(cantidad),
            observacion: "Ingreso rápido desde dashboard"
        })
    })

    .then(r => r.json())

    .then(() => {

        mostrarMensaje("✅ Stock agregado");

        document.getElementById("productoRapido").value = "";
        document.getElementById("cantidadRapida").value = "";

        listarProductos();
    })

    .catch(error => {
        console.log(error);
        mostrarMensaje("❌ Error agregando stock");
    });
}


// ==================================================================
// 📦 ALERTAS DE INVENTARIO
// ==================================================================
function cargarAlertas(){

    fetch(`${API}/inventario/alertas`)

    .then(r => r.json())

    .then(data => {

        let html = "";

        const agotados = data.agotados || [];
        const porTerminar = data.por_terminar || [];

        html += `
        <div class="card">
            <h3>🔴 Agotados</h3>
            ${
                agotados.length === 0
                ? "<p>Ninguno</p>"
                : agotados.map(p => `<p>${p.nombre} (${p.stock})</p>`).join("")
            }
        </div>

        <div class="card">
            <h3>🟡 Por terminar</h3>
            ${
                porTerminar.length === 0
                ? "<p>Ninguno</p>"
                : porTerminar.map(p => `<p>${p.nombre} (${p.stock})</p>`).join("")
            }
        </div>
        `;

        const cont = document.getElementById("alertas");
        if(cont) cont.innerHTML = html;
    })

    .catch(error => {
        console.log(error);
        mostrarMensaje("❌ Error cargando alertas");
    });
}


// ==================================================================
// 📜 HISTORIAL DE INVENTARIO
// ==================================================================
function cargarHistorial(){

    fetch(`${API}/inventario/historial`)

    .then(r => r.json())

    .then(data => {

        let html = "";

        if(!data || data.length === 0){
            html = `<div class="card">Sin movimientos</div>`;
        }

        data.forEach(m => {

            html += `
            <div class="card">
                <h4>${m.producto}</h4>
                <p>Movimiento: ${m.tipo}</p>
                <p>Cantidad: ${m.cantidad}</p>
                <p>${m.observacion || ""}</p>
                <p style="color:var(--text-faint);font-size:12px;">${m.fecha || ""}</p>
            </div>
            `;
        });

        const cont = document.getElementById("historial");
        if(cont) cont.innerHTML = html;
    })

    .catch(error => {
        console.log(error);
        mostrarMensaje("❌ Error cargando historial");
    });
}


// ==================================================================
// 🆕 CREAR PRODUCTO
// (la API necesita categoría y subcategoría; se cargan en selects)
// ==================================================================
function crearProducto(){

    let nombre = document.getElementById("nombreProducto").value;
    let precioVenta = document.getElementById("precioProducto").value;
    let stock = document.getElementById("stockProducto").value;

    let catEl = document.getElementById("categoriaProducto");
    let subEl = document.getElementById("subcategoriaProducto");
    let compraEl = document.getElementById("precioCompraProducto");
    let ctrlEl = document.getElementById("controlaStockProducto");

    let id_categoria = catEl ? parseInt(catEl.value) : 0;
    let id_subcategoria = subEl ? parseInt(subEl.value) : 0;
    let precio_compra = compraEl ? parseFloat(compraEl.value || 0) : 0;
    let controla_stock = (ctrlEl && ctrlEl.checked) ? 1 : 0;

    if(!controla_stock) stock = 0;

    if(!nombre || !precioVenta){
        mostrarMensaje("❌ Completa al menos nombre y precio de venta");
        return;
    }

    if(controla_stock && !stock){
        mostrarMensaje("❌ Ingresa el stock inicial");
        return;
    }

    fetch(`${API}/productos`, {

        method: "POST",

        headers: {
            "Content-Type": "application/json"
        },

        body: JSON.stringify({
            nombre: nombre,
            id_categoria: id_categoria,
            id_subcategoria: id_subcategoria,
            precio_compra: precio_compra,
            precio_venta: parseFloat(precioVenta),
            stock: parseInt(stock || 0),
            controla_stock: controla_stock
        })
    })

    .then(r => r.json())

    .then(() => {

        mostrarMensaje("✅ Producto creado");

        document.getElementById("nombreProducto").value = "";
        document.getElementById("precioProducto").value = "";
        document.getElementById("stockProducto").value = "";

        listarProductos();
    })

    .catch(error => {
        console.log(error);
        mostrarMensaje("❌ Error creando producto");
    });
}

// Cargar categorías/subcategorías en el formulario de producto
function cargarSelectsProducto(){

    const cat = document.getElementById("categoriaProducto");
    const sub = document.getElementById("subcategoriaProducto");

    if(!cat) return;

    fetch(`${API}/productos/categorias`)
    .then(r => r.json())
    .then(data => {
        cat.innerHTML = data
            .map(c => `<option value="${c.id}">${c.nombre}</option>`)
            .join("");

        // cargar subcategorías de la primera categoría
        if(data.length > 0) cargarSubcategoriasProducto(data[0].id);
    })
    .catch(e => console.log(e));

    if(cat && !cat.dataset.listener){
        cat.dataset.listener = "1";
        cat.addEventListener("change", () => cargarSubcategoriasProducto(cat.value));
    }
}

function cargarSubcategoriasProducto(idCategoria){

    const sub = document.getElementById("subcategoriaProducto");
    if(!sub) return;

    fetch(`${API}/productos/subcategorias/${idCategoria}`)
    .then(r => r.json())
    .then(data => {
        sub.innerHTML = data
            .map(s => `<option value="${s.id_subcategoria}">${s.nombre}</option>`)
            .join("");
    })
    .catch(e => console.log(e));
}


// ==================================================================
// 🏆 TORNEOS
// ==================================================================
function crearTorneo(){

    let nombre = document.getElementById("torneoNombre").value;
    let juego = document.getElementById("torneoJuego").value;
    let premio = document.getElementById("torneoPremio").value;
    let inscripcion = document.getElementById("torneoInscripcion").value;
    let cupos = document.getElementById("torneoCupos").value;

    if(!nombre || !juego){
        mostrarMensaje("❌ Ingresa al menos nombre y juego");
        return;
    }

    fetch(`${API}/torneos`, {

        method: "POST",

        headers: {
            "Content-Type": "application/json"
        },

        body: JSON.stringify({
            nombre: nombre,
            juego: juego,
            premio: parseFloat(premio || 0),
            inscripcion: parseFloat(inscripcion || 0),
            cupos: parseInt(cupos || 0),
            participantes: []
        })
    })

    .then(r => r.json())

    .then(() => {

        mostrarMensaje("✅ Torneo creado");

        document.getElementById("torneoNombre").value = "";
        document.getElementById("torneoJuego").value = "";
        document.getElementById("torneoPremio").value = "";
        document.getElementById("torneoInscripcion").value = "";
        document.getElementById("torneoCupos").value = "";

        cargarTorneos();
    })

    .catch(error => {
        console.log(error);
        mostrarMensaje("❌ Error creando torneo");
    });
}

function cargarTorneos(){

    fetch(`${API}/torneos`)

    .then(r => r.json())

    .then(data => {

        let html = "";

        if(!data || data.length === 0){
            html = `<div class="card">Sin torneos registrados</div>`;
        }

        data.forEach(t => {

            html += `
            <div class="card">
                <h3>${t.nombre}</h3>
                <p>Juego: ${t.juego}</p>
                <p>Premio: Q${t.premio}</p>
                <p>Inscripción: Q${t.inscripcion}</p>
                <p>Cupos: ${t.cupos}</p>
                <p>Estado: ${t.estado || "ABIERTO"}</p>
            </div>
            `;
        });

        const cont = document.getElementById("listaTorneos");
        if(cont) cont.innerHTML = html;
    })

    .catch(error => {
        console.log(error);
        mostrarMensaje("❌ Error cargando torneos");
    });
}


// ==================================================================
// 🔒 CIERRE DIARIO
// ==================================================================
function cargarCierre(){

    // Resumen (ventas, gastos, balance)
    fetch(`${API}/cierres/resumen`)

    .then(r => r.json())

    .then(data => {

        const v = document.getElementById("ventasDia");
        const g = document.getElementById("gastosDia");
        const b = document.getElementById("balanceDia");

        if(v) v.innerText = "Q" + (data.ventas || 0);
        if(g) g.innerText = "Q" + (data.gastos || 0);
        if(b) b.innerText = "Q" + (data.balance || 0);
    })

    .catch(error => console.log(error));

    // Historial de cierres
    fetch(`${API}/cierres`)

    .then(r => r.json())

    .then(data => {

        let html = "";

        if(!data || data.length === 0){
            html = `<div class="card">Sin cierres registrados</div>`;
        }

        data.forEach(c => {

            html += `
            <div class="card">
                <h4>${c.fecha || ""}</h4>
                <p>Usuario: ${c.usuario}</p>
                <p>Ventas: Q${c.total_ventas}</p>
                <p>Gastos: Q${c.total_gastos}</p>
                <p>Balance: Q${c.balance}</p>
                <p>${c.observacion || ""}</p>
            </div>
            `;
        });

        const cont = document.getElementById("historialCierres");
        if(cont) cont.innerHTML = html;
    })

    .catch(error => console.log(error));
}

function registrarCierre(){

    let usuario =
    JSON.parse(localStorage.getItem("usuario"));

    let observacion =
    document.getElementById("observacionCierre").value;

    fetch(`${API}/cierres`, {

        method: "POST",

        headers: {
            "Content-Type": "application/json"
        },

        body: JSON.stringify({
            id_usuario: usuario ? usuario.id_usuario : 0,
            observacion: observacion
        })
    })

    .then(r => r.json())

    .then(() => {

        mostrarMensaje("✅ Cierre registrado");

        document.getElementById("observacionCierre").value = "";

        cargarCierre();
    })

    .catch(error => {
        console.log(error);
        mostrarMensaje("❌ Error registrando cierre");
    });
}


// ==================================================================
// 📊 EXPORTAR A EXCEL
// ==================================================================
function exportarExcel(){

    // Abre el endpoint que descarga el Excel de ventas
    window.open(`${API}/exportar/ventas`, "_blank");
}


// ==================================================================
// 🗑️ ELIMINAR PRODUCTO
// (requiere el endpoint DELETE /api/productos/{id} en el backend)
// ==================================================================
function eliminarProducto(id){

    if(!confirm("¿Seguro que deseas eliminar este producto?")) return;

    fetch(`${API}/productos/${id}`, {
        method: "DELETE"
    })

    .then(async r => {
        const data = await r.json().catch(() => ({}));
        if(!r.ok){
            throw new Error(data.mensaje || "No se pudo eliminar");
        }
        return data;
    })

    .then((data) => {
        mostrarMensaje("✅ " + (data.mensaje || "Producto eliminado"));
        listarProductos();
    })

    .catch(error => {
        console.log(error);
        mostrarMensaje("❌ " + error.message);
    });
}