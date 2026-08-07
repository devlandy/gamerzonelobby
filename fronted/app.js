// API URL
const API = window.location.origin + "/api";

// Escapa HTML para prevenir XSS al insertar datos del servidor en innerHTML
function s(str) {
    if (str == null) return "";
    return String(str)
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;")
        .replace(/"/g, "&quot;")
        .replace(/'/g, "&#39;");
}

function fmtFecha(f) {
    if (!f) return "—";
    const d = new Date(f);
    return d.toLocaleString("es-GT", {
        timeZone: "America/Guatemala",
        day: "2-digit", month: "2-digit", year: "numeric",
        hour: "2-digit", minute: "2-digit", hour12: true
    });


}

// ======================
// VARIABLES GLOBALES
// ======================
let carrito = [];
let totalVenta = 0;
let ventaPendienteActual = 0;
let clienteSeleccionado = null; // { id, nombre } o null para Consumidor Final

// ======================
// JWT - TOKEN
// ======================
function getToken() {
    return localStorage.getItem("token");
}

function authHeaders(extra = {}) {
    return {
        "Content-Type": "application/json",
        "Authorization": `Bearer ${getToken()}`,
        ...extra
    };
}

function authFetch(url, options = {}) {
    const opts = {
        cache: 'no-store',
        ...options,
        headers: {
            ...authHeaders(),
            ...(options.headers || {})
        }
    };
    return fetch(url, opts).then(r => {
        if (r.status === 401) {
            localStorage.removeItem("token");
            localStorage.removeItem("usuario");
            window.location.href = "login.html";
            throw new Error("Sesión expirada");
        }
        return r;
    });
}

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
    if (seccion === "ventas") {
        cargarCategoriasPOS();
        cargarPendientes();
        renderCarrito();
        cargarConsolas();
        seleccionarModoPOS('consolas');
    }

    // INVENTARIO
    if(seccion === "inventario"){
        tabInventario('productos');
    }

    // DASHBOARD
    if(seccion === "dashboard"){
        cargarDashboard();
        cargarTopClientes();
        cargarTopGamers();
        cargarSelectProductosRapido();
    }

    // PRODUCTOS
    if(seccion === "productos"){
        tabCatalogo('categorias');
    }

    // CIERRE
    if(seccion === "cierre"){
        cargarCierre();
    }

    // TORNEOS
    if(seccion === "torneos"){
        cargarTorneos();
    }

    // FINANZAS
    if(seccion === "finanzas"){
        cargarFinanzas();
    }

    // REPORTES
    if(seccion === "reportes"){
        const mesEl = document.getElementById("mesReporte");
        if (mesEl) {
            const hoy = new Date();
            mesEl.value = `${hoy.getFullYear()}-${String(hoy.getMonth()+1).padStart(2,"0")}`;
        }
        cargarReportes();
    }

    // Aplicar restricciones de cajero al cambiar de sección
    setTimeout(aplicarRestriccionesCajero, 50);
}

// ===========================
// DASHBOARD
// ===========================
function cargarDashboard() {
    authFetch(`${API}/dashboard`)
    .then(r => r.json())
    .then(data => {
        const container = document.getElementById("dashboardData");
        if (!container) return;

        const ventas  = parseFloat(data.ventas_dia  || 0).toFixed(2);
        const gastos  = parseFloat(data.gastos_dia  || 0).toFixed(2);
        const balance = parseFloat(data.balance     || 0).toFixed(2);
        const balColor = parseFloat(balance) >= 0 ? '#4ade80' : '#f87171';

        container.innerHTML = `
            <div class="card" onclick="verDetalleVentasDash()" style="cursor:pointer;transition:background .15s;" title="Ver detalle de ventas">
                <div style="font-size:11px;color:#888;margin-bottom:4px;text-transform:uppercase;letter-spacing:.5px;">Ventas del período</div>
                <div style="font-size:22px;font-weight:700;color:#4ade80;">Q${ventas}</div>
                <div style="font-size:11px;color:#555;margin-top:4px;">👆 Click para ver detalle</div>
            </div>
            <div class="card" style="cursor:default;">
                <div style="font-size:11px;color:#888;margin-bottom:4px;text-transform:uppercase;letter-spacing:.5px;">Pendientes de cobro</div>
                <div style="font-size:22px;font-weight:700;color:#fcd34d;">${data.pedidos_pendientes || 0}</div>
            </div>
            <div class="card" onclick="verDetalleGastosDash()" style="cursor:pointer;transition:background .15s;" title="Ver detalle de gastos">
                <div style="font-size:11px;color:#888;margin-bottom:4px;text-transform:uppercase;letter-spacing:.5px;">Gastos del período</div>
                <div style="font-size:22px;font-weight:700;color:#f87171;">Q${gastos}</div>
                <div style="font-size:11px;color:#555;margin-top:4px;">👆 Click para ver detalle</div>
            </div>
            <div class="card" onclick="verDetalleBalanceDash()" style="cursor:pointer;transition:background .15s;" title="Ver resumen de balance">
                <div style="font-size:11px;color:#888;margin-bottom:4px;text-transform:uppercase;letter-spacing:.5px;">Balance neto</div>
                <div style="font-size:22px;font-weight:700;color:${balColor};">Q${balance}</div>
                <div style="font-size:11px;color:#555;margin-top:4px;">👆 Click para ver resumen</div>
            </div>
        `;

        // guardar para el modal de balance
        container.dataset.ventas  = ventas;
        container.dataset.gastos  = gastos;
        container.dataset.balance = balance;
    })
    .catch(err => console.error(err));
}

function verDetalleVentasDash() {
    const modal = _dashModal('Ventas del período', '<div style="text-align:center;color:#888;padding:20px;">Cargando...</div>');
    authFetch(`${API}/dashboard/detalle-ventas`).then(r => r.json()).then(rows => {
        if (!rows.length) { modal.body.innerHTML = '<p style="color:#555;text-align:center;padding:20px;">Sin ventas en este período.</p>'; return; }
        modal.body.innerHTML = `
        <table style="width:100%;border-collapse:collapse;font-size:13px;">
          <thead><tr style="color:#888;border-bottom:1px solid #222;">
            <th style="padding:6px 8px;text-align:left;">Fecha</th>
            <th style="padding:6px 8px;text-align:left;">Cliente</th>
            <th style="padding:6px 8px;text-align:right;">Total</th>
            <th style="padding:6px 8px;text-align:left;">Método</th>
          </tr></thead>
          <tbody>${rows.map(v => `
            <tr style="border-bottom:1px solid #111;">
              <td style="padding:6px 8px;color:#aaa;">${fmtFecha(v.fecha)}</td>
              <td style="padding:6px 8px;color:#e2e2e2;">${s(v.cliente)}</td>
              <td style="padding:6px 8px;text-align:right;color:#4ade80;font-weight:600;">Q${parseFloat(v.total).toFixed(2)}</td>
              <td style="padding:6px 8px;color:#888;">${s(v.metodo_pago||'—')}</td>
            </tr>`).join('')}
          </tbody>
          <tfoot><tr>
            <td colspan="2" style="padding:8px;color:#aaa;font-weight:600;">TOTAL</td>
            <td style="padding:8px;text-align:right;color:#4ade80;font-weight:700;">Q${rows.reduce((a,v)=>a+parseFloat(v.total),0).toFixed(2)}</td>
            <td></td>
          </tr></tfoot>
        </table>`;
    }).catch(() => { modal.body.innerHTML = '<p style="color:#f87171;text-align:center;">Error al cargar ventas</p>'; });
}

function verDetalleGastosDash() {
    const modal = _dashModal('Gastos del período', '<div style="text-align:center;color:#888;padding:20px;">Cargando...</div>');
    authFetch(`${API}/dashboard/detalle-gastos`).then(r => r.json()).then(rows => {
        if (!rows.length) { modal.body.innerHTML = '<p style="color:#555;text-align:center;padding:20px;">Sin gastos en este período.</p>'; return; }
        modal.body.innerHTML = `
        <table style="width:100%;border-collapse:collapse;font-size:13px;">
          <thead><tr style="color:#888;border-bottom:1px solid #222;">
            <th style="padding:6px 8px;text-align:left;">Fecha</th>
            <th style="padding:6px 8px;text-align:left;">Descripción</th>
            <th style="padding:6px 8px;text-align:right;">Monto</th>
          </tr></thead>
          <tbody>${rows.map(g => `
            <tr style="border-bottom:1px solid #111;">
              <td style="padding:6px 8px;color:#aaa;">${fmtFecha(g.fecha)}</td>
              <td style="padding:6px 8px;color:#e2e2e2;">${s(g.descripcion)}</td>
              <td style="padding:6px 8px;text-align:right;color:#f87171;font-weight:600;">Q${parseFloat(g.monto).toFixed(2)}</td>
            </tr>`).join('')}
          </tbody>
          <tfoot><tr>
            <td colspan="2" style="padding:8px;color:#aaa;font-weight:600;">TOTAL</td>
            <td style="padding:8px;text-align:right;color:#f87171;font-weight:700;">Q${rows.reduce((a,g)=>a+parseFloat(g.monto),0).toFixed(2)}</td>
          </tr></tfoot>
        </table>`;
    }).catch(() => { modal.body.innerHTML = '<p style="color:#f87171;text-align:center;">Error al cargar gastos</p>'; });
}

function verDetalleBalanceDash() {
    const cont = document.getElementById('dashboardData');
    const ventas  = parseFloat(cont?.dataset.ventas  || 0);
    const gastos  = parseFloat(cont?.dataset.gastos  || 0);
    const balance = parseFloat(cont?.dataset.balance || 0);
    const balColor = balance >= 0 ? '#4ade80' : '#f87171';

    _dashModal('Resumen del período', `
        <div style="display:grid;grid-template-columns:1fr 1fr 1fr;gap:12px;text-align:center;padding:8px 0;">
          <div style="background:#0a1a0a;border-radius:8px;padding:16px;">
            <div style="font-size:11px;color:#888;margin-bottom:6px;">VENTAS</div>
            <div style="font-size:20px;font-weight:700;color:#4ade80;">Q${ventas.toFixed(2)}</div>
          </div>
          <div style="background:#1a0a0a;border-radius:8px;padding:16px;">
            <div style="font-size:11px;color:#888;margin-bottom:6px;">GASTOS</div>
            <div style="font-size:20px;font-weight:700;color:#f87171;">Q${gastos.toFixed(2)}</div>
          </div>
          <div style="background:#0f0f1a;border-radius:8px;padding:16px;">
            <div style="font-size:11px;color:#888;margin-bottom:6px;">BALANCE</div>
            <div style="font-size:20px;font-weight:700;color:${balColor};">Q${balance.toFixed(2)}</div>
          </div>
        </div>
        <p style="color:#555;font-size:12px;text-align:center;margin-top:12px;">Período desde el último cierre de caja registrado.</p>
    `);
}

function _dashModal(titulo, contenidoHtml) {
    let overlay = document.getElementById('_dashModalOverlay');
    if (!overlay) {
        overlay = document.createElement('div');
        overlay.id = '_dashModalOverlay';
        overlay.style.cssText = 'position:fixed;inset:0;background:rgba(0,0,0,.75);z-index:9999;display:flex;align-items:center;justify-content:center;';
        overlay.addEventListener('click', e => { if (e.target === overlay) overlay.remove(); });
        document.body.appendChild(overlay);
    }
    overlay.innerHTML = `
        <div style="background:#181818;border-radius:12px;width:min(700px,95vw);max-height:80vh;display:flex;flex-direction:column;box-shadow:0 8px 32px #000a;">
          <div style="padding:16px 20px;border-bottom:1px solid #222;display:flex;align-items:center;justify-content:space-between;">
            <span style="font-weight:700;font-size:15px;color:#e2e2e2;">${titulo}</span>
            <button onclick="document.getElementById('_dashModalOverlay').remove()" style="background:none;border:none;color:#555;font-size:20px;cursor:pointer;line-height:1;">✕</button>
          </div>
          <div id="_dashModalBody" style="overflow-y:auto;padding:16px 20px;">${contenidoHtml}</div>
        </div>`;
    overlay.style.display = 'flex';
    return { body: document.getElementById('_dashModalBody') };
}

// ===========================
// CREAR CLIENTE
// ===========================
function crearCliente() {

    authFetch(`${API}/clientes`, {

        method: "POST",

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
        if (d.error) { mostrarMensaje("❌ " + d.error); return; }
        const nombre = document.getElementById("nombre").value;
        document.getElementById("nombre").value   = "";
        document.getElementById("telefono").value = "";
        document.getElementById("apodo").value    = "";
        mostrarMensaje("✅ Cliente creado");
        mostrarQR(d.codigo, nombre);
    })
    .catch(() => mostrarMensaje("❌ Error creando cliente"));
}

// ===========================
// RECALCULAR PUNTOS HISTÓRICOS
// ===========================
async function recalcularPuntos() {
    if (!await confirmarDialog("Recalcular puntos", "¿Recalcular puntos de TODOS los clientes basado en su historial de ventas? Esto reemplazará los puntos actuales.", "danger")) return;
    authFetch(`${API}/clientes/recalcular-puntos`, { method: 'POST' })
    .then(r => r.json())
    .then(d => mostrarMensaje(`✅ ${d.mensaje} — ${d.clientes_actualizados} clientes actualizados`))
    .catch(() => mostrarMensaje("❌ Error al recalcular puntos"));
}

// ===========================
// QR GENERAL DE CONSULTA DE PUNTOS
// ===========================
function mostrarQRGeneral() {
    let overlay = document.getElementById('_qrGeneralOverlay');
    if (!overlay) {
        overlay = document.createElement('div');
        overlay.id = '_qrGeneralOverlay';
        overlay.style.cssText = 'position:fixed;inset:0;background:rgba(0,0,0,.8);z-index:9999;display:flex;align-items:center;justify-content:center;';
        overlay.addEventListener('click', e => { if (e.target === overlay) overlay.remove(); });
        document.body.appendChild(overlay);
    }
    const urlGuardada = localStorage.getItem('_baseUrl') || window.location.origin;
    overlay.innerHTML = `
        <div style="background:#181818;border-radius:14px;padding:28px 24px;text-align:center;max-width:380px;width:90%;box-shadow:0 8px 32px #000a;">
            <h3 style="color:#e2e2e2;margin-bottom:6px;">📱 QR — Consulta de puntos</h3>
            <p style="color:#555;font-size:13px;margin-bottom:16px;">Los clientes escanean para ver sus puntos</p>

            <div style="text-align:left;margin-bottom:14px;">
                <label style="font-size:12px;color:#666;">URL pública</label>
                <div style="display:flex;gap:6px;margin-top:4px;">
                    <input id="_ngrokUrlInput" value="${urlGuardada}" placeholder="https://gamerzonelobby-production.up.railway.app"
                        style="flex:1;background:#0a0a14;border:1px solid #2a2a3e;border-radius:8px;color:#e2e2e2;font-size:13px;padding:8px 10px;outline:none;">
                    <button onclick="generarQR()" style="background:#3b82f6;color:#fff;border:none;border-radius:8px;padding:8px 14px;font-size:13px;cursor:pointer;">Generar</button>
                </div>
            </div>

            <div id="_qrImg" style="background:#fff;border-radius:10px;padding:12px;display:inline-block;margin-bottom:16px;min-width:120px;min-height:60px;">
                <p style="color:#aaa;font-size:13px;padding:20px 10px;">Presiona Generar</p>
            </div>
            <br>
            <button onclick="imprimirQRGeneral()" style="background:#7c3aed;color:#fff;border:none;border-radius:8px;padding:10px 20px;font-size:14px;font-weight:600;cursor:pointer;margin-right:8px;">🖨️ Imprimir</button>
            <button onclick="document.getElementById('_qrGeneralOverlay').remove()" style="background:#222;color:#888;border:1px solid #333;border-radius:8px;padding:10px 16px;font-size:14px;cursor:pointer;">Cerrar</button>
        </div>`;
    overlay.style.display = 'flex';

    if (urlGuardada) generarQR();
}

function generarQR() {
    const urlInput = document.getElementById('_ngrokUrlInput')?.value?.trim();
    if (urlInput) localStorage.setItem('_baseUrl', urlInput);

    document.getElementById('_qrImg').innerHTML = '<p style="color:#aaa;font-size:13px;padding:20px 10px;">Generando...</p>';

    const param = urlInput ? `&baseUrl=${encodeURIComponent(urlInput)}` : '';
    const img = new Image();
    img.onload = () => {
        img.style.cssText = 'width:220px;height:220px;display:block;';
        document.getElementById('_qrImg').innerHTML = '';
        document.getElementById('_qrImg').appendChild(img);
    };
    img.onerror = () => {
        document.getElementById('_qrImg').innerHTML = '<p style="color:#ef4444;font-size:13px;padding:10px;">Error al generar QR</p>';
    };
    img.src = `${API}/publico/qr-general?t=${Date.now()}${param}`;
}

function imprimirQRGeneral() {
    const img = document.querySelector('#_qrImg img');
    if (!img) return;
    const w = window.open('', '_blank');
    w.document.write(`
        <html><head><title>QR Lobby Zone</title>
        <style>
            body { margin:0; display:flex; flex-direction:column; align-items:center; justify-content:center; min-height:100vh; font-family:sans-serif; background:#fff; }
            h2 { font-size:22px; margin-bottom:4px; color:#111; }
            p  { color:#666; font-size:14px; margin-bottom:20px; }
            img { width:260px; height:260px; }
            small { color:#aaa; font-size:11px; margin-top:12px; }
        </style></head><body>
        <h2>🎮 El Lobby Zone</h2>
        <p>Escanea para consultar tus puntos</p>
        <img src="${img.src}">
        <small>Apunta con la cámara de tu celular</small>
        </body></html>`);
    w.document.close();
    w.onload = () => { w.print(); };
}

// BUSCAR CLIENTES
// ===========================
function buscarClientes() {
    const texto = document.getElementById("buscar").value;
    authFetch(`${API}/clientes/buscar?texto=${texto}`)
    .then(r => r.json())
    .then(data => {
        const lista = document.getElementById("lista");
        if (!lista) return;
        if (!data.length) {
            lista.innerHTML = `<p style="color:#555;padding:20px 0;">Sin resultados para "<strong>${s(texto)}</strong>".</p>`;
            return;
        }

        lista.innerHTML = `
        <div class="cli-tabla-wrap">
            <table class="cli-tabla">
                <thead>
                    <tr>
                        <th style="width:110px;">Código</th>
                        <th>Nombre</th>
                        <th>Apodo</th>
                        <th>Teléfono</th>
                        <th style="text-align:right;">Acciones</th>
                    </tr>
                </thead>
                <tbody>
                    ${data.map(c => `
                    <tr class="cli-fila" onclick="toggleClienteDetalle('${s(c.codigo)}','${s(c.nombre)}',${c.id})" id="tr-${s(c.codigo)}">
                        <td><span class="cli-codigo">${s(c.codigo)}</span></td>
                        <td>
                            <div style="display:flex;align-items:center;gap:10px;">
                                <div class="cli-avatar">${s(c.nombre).charAt(0).toUpperCase()}</div>
                                <span class="cli-nombre">${s(c.nombre)}</span>
                            </div>
                        </td>
                        <td style="color:#aaa;font-size:13px;">${c.apodo ? s(c.apodo) : '<span style="color:#333;">—</span>'}</td>
                        <td style="color:#aaa;font-size:13px;">${c.telefono ? s(c.telefono) : '<span style="color:#333;">—</span>'}</td>
                        <td style="text-align:right;">
                            <div style="display:flex;gap:6px;justify-content:flex-end;" onclick="event.stopPropagation()">
                                <button class="cli-btn" title="Ver QR y puntos"
                                    onclick="toggleClienteDetalle('${s(c.codigo)}','${s(c.nombre)}',${c.id})">QR</button>
                                <button class="cli-btn cli-btn-pos" title="Seleccionar en POS"
                                    onclick="seleccionarClientePOS(${c.id},'${s(c.nombre)}')">POS</button>
                                <button class="cli-btn cli-btn-hist" title="Historial de compras"
                                    onclick="verHistorialCliente(${c.id},'${s(c.nombre)}')">...</button>
                            </div>
                        </td>
                    </tr>
                    <tr class="cli-detalle-row" id="detalle-tr-${s(c.codigo)}" style="display:none;">
                        <td colspan="5" style="padding:0;">
                            <div class="cli-detalle-panel" id="detalle-${s(c.codigo)}">
                                <div style="display:flex;gap:28px;align-items:flex-start;flex-wrap:wrap;">
                                    <div style="text-align:center;">
                                        <div id="qrInline-${s(c.codigo)}" class="cli-qr-box"></div>
                                        <div style="font-size:11px;color:#555;margin-top:6px;">${s(c.codigo)}</div>
                                        <button class="cli-btn" style="margin-top:8px;font-size:11px;"
                                            onclick="descargarQRInline('${s(c.codigo)}','${s(c.nombre)}')">⬇ Descargar</button>
                                    </div>
                                    <div style="flex:1;min-width:220px;">
                                        <div style="font-size:11px;color:#555;text-transform:uppercase;letter-spacing:1px;margin-bottom:12px;">Puntos acumulados</div>
                                        <div id="puntosInline-${s(c.codigo)}" style="margin-bottom:18px;">
                                            <span style="color:#555;font-size:13px;">Cargando...</span>
                                        </div>
                                        <div style="display:flex;gap:8px;flex-wrap:wrap;">
                                            <button class="btn" style="font-size:13px;"
                                                onclick="seleccionarClientePOS(${c.id},'${s(c.nombre)}')">🛒 Usar en POS</button>
                                            <button class="btn" style="font-size:13px;background:#1a1a1a;border:1px solid #2a2a2a;"
                                                onclick="verHistorialCliente(${c.id},'${s(c.nombre)}')">📋 Historial</button>
                                        </div>
                                    </div>
                                </div>
                            </div>
                        </td>
                    </tr>
                    `).join("")}
                </tbody>
            </table>
        </div>`;
    });
}

function toggleClienteDetalle(codigo, nombre, id) {
    const detalleRow = document.getElementById(`detalle-tr-${codigo}`);
    const filaPrincipal = document.getElementById(`tr-${codigo}`);
    if (!detalleRow) return;

    const abierto = detalleRow.style.display !== "none";
    detalleRow.style.display = abierto ? "none" : "table-row";
    if (filaPrincipal) filaPrincipal.classList.toggle("cli-fila-activa", !abierto);
    if (abierto) return;

    // Generar QR
    const qrEl = document.getElementById(`qrInline-${codigo}`);
    if (qrEl && !qrEl.hasChildNodes()) {
        const baseUrl = window.location.origin;
        new QRCode(qrEl, {
            text: `${baseUrl}/mis-puntos.html?codigo=${encodeURIComponent(codigo)}`,
            width: 150, height: 150,
            colorDark: "#ffffff", colorLight: "#0d0d0d",
            correctLevel: QRCode.CorrectLevel.H
        });
    }

    // Cargar puntos
    const ptsEl = document.getElementById(`puntosInline-${codigo}`);
    fetch(`${window.location.origin}/api/publico/cliente/${encodeURIComponent(codigo)}`)
    .then(r => r.json())
    .then(data => {
        if (!ptsEl) return;
        const j  = parseFloat(data.puntos_juego   || 0);
        const c2 = parseFloat(data.puntos_consumo || 0);
        const fmt = n => n % 1 === 0 ? n : n.toFixed(2);
        ptsEl.innerHTML = `
            <div style="display:flex;gap:12px;flex-wrap:wrap;">
                <div class="cli-pts-card" style="border-color:#f59e0b44;">
                    <div style="font-size:28px;font-weight:800;color:#f59e0b;">${fmt(j)}</div>
                    <div class="cli-pts-label">Juego</div>
                </div>
                <div class="cli-pts-card" style="border-color:#60a5fa44;">
                    <div style="font-size:28px;font-weight:800;color:#60a5fa;">${fmt(c2)}</div>
                    <div class="cli-pts-label">Consumo</div>
                </div>
                <div class="cli-pts-card" style="border-color:#4ade8044;">
                    <div style="font-size:28px;font-weight:800;color:#4ade80;">${fmt(j + c2)}</div>
                    <div class="cli-pts-label">Total</div>
                </div>
            </div>`;
    })
    .catch(() => { if (ptsEl) ptsEl.innerHTML = `<span style="color:#555;font-size:13px;">Sin puntos registrados</span>`; });
}

function descargarQRInline(codigo, nombre) {
    const el = document.getElementById(`qrInline-${codigo}`);
    if (!el) return;
    const canvas = el.querySelector("canvas");
    const img    = el.querySelector("img");
    const src    = canvas ? canvas.toDataURL("image/png") : img?.src;
    if (!src) return;
    const a = document.createElement("a");
    a.href = src; a.download = `QR-${nombre.replace(/\s+/g,"-")}.png`; a.click();
}

// ======================
// SELECCIONAR CLIENTE PARA POS
// ======================
function seleccionarClientePOS(id, nombre) {
    clienteSeleccionado = { id, nombre };
    mostrarMensaje(`✅ Cliente seleccionado: ${nombre}`);
    mostrar("ventas");
}

// ======================
// QR CLIENTE
// ======================
function mostrarQR(codigo, nombre) {
    const modal = document.getElementById("modalQR");
    const canvas = document.getElementById("qrCanvas");
    if (!modal || !canvas) return;

    document.getElementById("qrNombreCliente").textContent = nombre;
    document.getElementById("qrCodigoCliente").textContent = codigo;
    canvas.innerHTML = "";

    // Cargar puntos del cliente
    const ptsEl = document.getElementById("qrPuntosCliente");
    if (ptsEl) ptsEl.innerHTML = `<span style="color:#555;font-size:12px;">Cargando puntos...</span>`;

    const baseUrl = window.location.origin;
    {
        const url = `${baseUrl}/mis-puntos.html?codigo=${encodeURIComponent(codigo)}`;
        canvas.innerHTML = "";
        new QRCode(canvas, {
            text: url,
            width: 200,
            height: 200,
            colorDark: "#ffffff",
            colorLight: "#111111",
            correctLevel: QRCode.CorrectLevel.H
        });
    }

    fetch(`${window.location.origin}/api/publico/cliente/${encodeURIComponent(codigo)}`)
    .then(r => r.json())
    .then(data => {
        if (ptsEl) ptsEl.innerHTML = `
            <div style="display:flex;gap:12px;justify-content:center;margin-top:4px;">
                <div style="text-align:center;">
                    <div style="font-size:22px;font-weight:800;color:#f59e0b;">${parseFloat(data.puntos_juego||0).toFixed(2).replace(/\.00$/,"")}</div>
                    <div style="font-size:10px;color:#555;text-transform:uppercase;letter-spacing:1px;">Pts juego</div>
                </div>
                <div style="width:1px;background:#222;"></div>
                <div style="text-align:center;">
                    <div style="font-size:22px;font-weight:800;color:#60a5fa;">${parseFloat(data.puntos_consumo||0).toFixed(2).replace(/\.00$/,"")}</div>
                    <div style="font-size:10px;color:#555;text-transform:uppercase;letter-spacing:1px;">Pts consumo</div>
                </div>
            </div>`;
    })
    .catch(() => { if (ptsEl) ptsEl.innerHTML = ""; });

    modal.style.display = "flex";
}

function cerrarModalQR() {
    const modal = document.getElementById("modalQR");
    if (modal) modal.style.display = "none";
}

function descargarQR() {
    const canvas = document.querySelector("#qrCanvas canvas");
    const img    = document.querySelector("#qrCanvas img");
    const nombre = document.getElementById("qrNombreCliente")?.textContent || "cliente";
    const src    = canvas ? canvas.toDataURL("image/png") : img?.src;
    if (!src) return;
    const a = document.createElement("a");
    a.href = src;
    a.download = `QR-${nombre.replace(/\s+/g,"-")}.png`;
    a.click();
}

function buscarClientePOSFn() {
    const q = document.getElementById("buscarClientePOS")?.value?.trim();
    const res = document.getElementById("resultadosClientePOS");
    if (!res) return;
    if (!q || q.length < 2) { res.innerHTML = ""; return; }

    authFetch(`${API}/clientes/buscar?texto=${encodeURIComponent(q)}`)
    .then(r => r.json())
    .then(data => {
        if (!data.length) { res.innerHTML = `<p style="color:#555;font-size:12px;padding:4px 0;">Sin resultados</p>`; return; }
        res.innerHTML = data.slice(0, 5).map(c =>
            `<div onclick="elegirClientePOS(${c.id},'${c.nombre.replace(/'/g,"\\'")}',this.parentElement)"
                  style="padding:6px 10px;cursor:pointer;border-radius:4px;font-size:13px;background:#1a1a1a;margin-top:3px;"
                  onmouseover="this.style.background='#2a2a2a'" onmouseout="this.style.background='#1a1a1a'">
                👤 <strong>${s(c.nombre)}</strong>${c.apodo ? ` · ${s(c.apodo)}` : ""}
            </div>`
        ).join("");
    })
    .catch(() => {});
}

function elegirClientePOS(id, nombre, contenedor) {
    clienteSeleccionado = { id, nombre };
    if (contenedor) contenedor.innerHTML = "";
    renderCarrito();
    mostrarMensaje(`✅ Cliente: ${nombre}`);
}

// ======================
// VENTA RAPIDA
// ======================
function ventaRapida() {

    let cliente =
    document.getElementById("idClienteVenta").value;

    let total =
    document.getElementById("totalVenta").value;

    authFetch(`${API}/dashboard/venta-rapida?id_cliente=${cliente}&total=${total}`, {

        method: "POST"
    })

    .then(r => r.json())

    .then(() => {

        mostrarMensaje("✅ Venta registrada");

        cargarPendientes();

        cargarDashboard();
    })

    .catch(error => {

        console.log(error);

        mostrarMensaje("❌ Error registrando venta");
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
function toggleFacturaPendiente(){
    const checked = document.getElementById("requiereFacturaPendiente").checked;
    document.getElementById("camposFacturaPendiente").style.display = checked ? "block" : "none";
}

function guardarPendiente(){
    const metodo = document.getElementById("metodoPendiente").value;
    const requiereFactura = document.getElementById("requiereFacturaPendiente").checked;

    authFetch(`${API}/ventas/${ventaPendienteActual}`, {
        method: "PUT",
        body: JSON.stringify({
            forma_cobro: "PAGADO",
            metodo_pago: metodo,
            observacion: requiereFactura ? "Con factura" : ""
        })
    })
    .then(r => r.json())
    .then(() => {
        if (!requiereFactura) {
            mostrarMensaje("✅ Pago registrado");
            cerrarPendiente();
            cargarPendientes();
            cargarDashboard();
            return;
        }

        const nit = document.getElementById("nitFactura").value || "CF";
        const nombre = document.getElementById("nombreFactura").value || "Consumidor Final";
        const direccion = document.getElementById("direccionFactura").value || "Ciudad";

        return authFetch(`${API}/factura`, {
            method: "POST",
            body: JSON.stringify({ id_venta: ventaPendienteActual, nit, nombre, direccion })
        })
        .then(r => r.json())
        .then(data => {
            mostrarMensaje("✅ Pago registrado");
            cerrarPendiente();
            cargarPendientes();
            cargarDashboard();
            if (data.id_factura) {
                window.open(`${API}/pdf/factura/${data.id_factura}?token=${getToken()}`, "_blank");
            }
        });
    });
}

// ======================
// PAGAR VENTA
// ======================
function pagarVenta(id) {

    authFetch(`${API}/ventas/pagar/${id}`, {

        method: "PUT"
    })

    .then(r => r.json())

    .then(() => {

        mostrarMensaje("✅ Venta pagada");

        cargarPendientes();

        cargarDashboard();
    })

    .catch(error => {

        console.log(error);

        mostrarMensaje("❌ Error pagando venta");
    });
}

// ===========================
// PENDIENTES
// ===========================
function cargarPendientes(){

    authFetch(`${API}/ventas/pendientes`)

    .then(r => r.json())

    .then(data => {

        let html = "";

        data.forEach(v => {

            const etiqueta = (v.nombre_orden && v.nombre_orden !== "POS")
                ? v.nombre_orden
                : v.cliente;

            html += `
            <div class="card">

                <h3>${s(etiqueta)}</h3>
                <p>Total: Q${v.total}</p>
                <p>Fecha: ${fmtFecha(v.fecha)}</p>

                <button class="btn" onclick="abrirPendiente(${v.id_venta})">Cobrar</button>
                <button class="btn" onclick="window.open('${API}/pdf/venta/${v.id_venta}?token=${getToken()}', '_blank')">PDF</button>
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
    window.open(`${API}/pdf/factura/${id}?token=${getToken()}`, "_blank");
}

// ======================
// HISTORIAL VENTAS REPORTE
// ======================
// ======================
// REPORTES COMPLETO
// ======================

function cargarReportes() {
    const mes = document.getElementById("mesReporte")?.value || "";
    const q = mes ? `?mes=${mes}` : "";

    const sinDatos = (elId, msg) => {
        const el = document.getElementById(elId);
        if (el) el.innerHTML = `<p style="color:#555;font-size:13px; padding: 4px 0;">${msg}</p>`;
    };

    // Top productos
    authFetch(`${API}/reportes/top-productos${q}`)
    .then(r => r.json())
    .then(data => {
        const el = document.getElementById("repTopProductos");
        if (!el) return;
        if (!data.length) { el.innerHTML = `<p style="color:#555;font-size:13px;">Sin ventas este mes.</p>`; return; }
        el.innerHTML = `<table style="width:100%;border-collapse:collapse;font-size:13px;">
            <thead><tr style="color:#aaa;border-bottom:1px solid #333;">
                <th style="padding:7px 6px;text-align:left;">#</th>
                <th style="padding:7px 6px;text-align:left;">Producto</th>
                <th style="padding:7px 6px;text-align:center;">Vendidos</th>
                <th style="padding:7px 6px;text-align:right;">Ingresos</th>
            </tr></thead>
            <tbody>${data.map((p,i) => `
                <tr style="border-bottom:1px solid #1a1a1a;">
                    <td style="padding:7px 6px;color:#555;">${i+1}</td>
                    <td style="padding:7px 6px;">${s(p.nombre)}</td>
                    <td style="padding:7px 6px;text-align:center;color:#aaa;">${p.vendidos}</td>
                    <td style="padding:7px 6px;text-align:right;color:#4ade80;font-weight:600;">Q${parseFloat(p.ingresos).toFixed(2)}</td>
                </tr>`).join("")}
            </tbody></table>`;
    }).catch(() => sinDatos("repTopProductos", "Sin ventas este mes."));

    // Métodos de pago
    authFetch(`${API}/reportes/metodos-pago${q}`)
    .then(r => r.json())
    .then(data => {
        const el = document.getElementById("repMetodosPago");
        if (!el) return;
        if (!data.length) { el.innerHTML = `<p style="color:#555;font-size:13px;">Sin ventas este mes.</p>`; return; }
        const totalGeneral = data.reduce((s,m) => s + parseFloat(m.total||0), 0);
        el.innerHTML = data.map(m => {
            const pct = totalGeneral > 0 ? (parseFloat(m.total)/totalGeneral*100).toFixed(1) : 0;
            return `<div style="margin-bottom:12px;">
                <div style="display:flex;justify-content:space-between;font-size:13px;margin-bottom:4px;">
                    <span>${s(m.metodo)}</span>
                    <span style="color:#aaa;">${m.cantidad} ventas · <strong>Q${parseFloat(m.total).toFixed(2)}</strong> (${pct}%)</span>
                </div>
                <div style="height:6px;background:#1a1a1a;border-radius:4px;">
                    <div style="height:100%;width:${pct}%;background:#e2e2e2;border-radius:4px;"></div>
                </div>
            </div>`;
        }).join("");
    }).catch(() => sinDatos("repMetodosPago", "Sin ventas este mes."));

    // Cancelaciones
    authFetch(`${API}/reportes/cancelaciones${q}`)
    .then(r => r.json())
    .then(data => {
        const el = document.getElementById("repCancelaciones");
        if (!el) return;
        if (!data.length) { el.innerHTML = `<p style="color:#555;font-size:13px;">Sin cancelaciones este mes.</p>`; return; }
        el.innerHTML = `<table style="width:100%;border-collapse:collapse;font-size:13px;">
            <thead><tr style="color:#aaa;border-bottom:1px solid #333;">
                <th style="padding:7px 6px;">Fecha</th>
                <th style="padding:7px 6px;">Cliente</th>
                <th style="padding:7px 6px;text-align:right;">Total</th>
                <th style="padding:7px 6px;">Motivo</th>
            </tr></thead>
            <tbody>${data.map(c => `
                <tr style="border-bottom:1px solid #1a1a1a;">
                    <td style="padding:7px 6px;color:#aaa;white-space:nowrap;">${fmtFecha(c.fecha)}</td>
                    <td style="padding:7px 6px;">${s(c.cliente)||"—"}</td>
                    <td style="padding:7px 6px;text-align:right;color:#ef4444;">Q${parseFloat(c.total).toFixed(2)}</td>
                    <td style="padding:7px 6px;color:#555;font-size:12px;">${s(c.observacion)||"—"}</td>
                </tr>`).join("")}
            </tbody></table>`;
    }).catch(() => sinDatos("repCancelaciones", "Sin cancelaciones este mes."));

    // Márgenes
    authFetch(`${API}/reportes/inventario`)
    .then(r => r.json())
    .then(data => {
        const el = document.getElementById("repMargenes");
        if (!el) return;
        if (!data.length) { el.innerHTML = `<p style="color:#555;font-size:13px;">Sin productos.</p>`; return; }
        el.innerHTML = `<table style="width:100%;border-collapse:collapse;font-size:13px;">
            <thead><tr style="color:#aaa;border-bottom:1px solid #333;">
                <th style="padding:7px 6px;">Producto</th>
                <th style="padding:7px 6px;text-align:right;">Compra</th>
                <th style="padding:7px 6px;text-align:right;">Venta</th>
                <th style="padding:7px 6px;text-align:right;">Ganancia</th>
                <th style="padding:7px 6px;text-align:right;">Margen</th>
            </tr></thead>
            <tbody>${data.map(p => `
                <tr style="border-bottom:1px solid #1a1a1a;">
                    <td style="padding:7px 6px;">${s(p.nombre)}</td>
                    <td style="padding:7px 6px;text-align:right;color:#aaa;">Q${parseFloat(p.precio_compra||0).toFixed(2)}</td>
                    <td style="padding:7px 6px;text-align:right;">Q${parseFloat(p.precio_venta||0).toFixed(2)}</td>
                    <td style="padding:7px 6px;text-align:right;color:#4ade80;">Q${parseFloat(p.ganancia||0).toFixed(2)}</td>
                    <td style="padding:7px 6px;text-align:right;color:#aaa;">${parseFloat(p.margen_pct||0).toFixed(1)}%</td>
                </tr>`).join("")}
            </tbody></table>`;
    }).catch(() => sinDatos("repMargenes", "Sin productos con precio configurado."));

    // Facturas SAT
    cargarFacturas();

    // Historial ventas
    cargarVentasReporte();
}

function tabReporte(tab) {
    ["stats","facturas","ventas"].forEach(t => {
        const el = document.getElementById(t === "stats" ? "repStats" : t === "facturas" ? "repFacturas" : "repVentas");
        if (el) el.style.display = t === tab ? "block" : "none";
        const btn = document.getElementById(`tabRep-${t}`);
        if (btn) btn.classList.toggle("tab-active", t === tab);
    });
}

function tabSAT(tab) {
    document.getElementById("listaFacturasPendientesSAT").style.display  = tab === "pendiente" ? "block" : "none";
    document.getElementById("listaFacturasFacturadasSAT").style.display  = tab === "facturada"  ? "block" : "none";
    document.getElementById("tabSat-pendiente").classList.toggle("tab-active", tab === "pendiente");
    document.getElementById("tabSat-facturada").classList.toggle("tab-active", tab === "facturada");
}

function cargarVentasReporte(){
    authFetch(`${API}/reportes/ventas`)
    .then(r => r.json())
    .then(data => {
        const cont = document.getElementById("listaVentasReporte");
        if (!cont) return;
        if (!data.length) { cont.innerHTML = "<p style='color:#555;'>No hay ventas registradas.</p>"; return; }
        const btnStyle = "background:transparent;padding:3px 8px;border-radius:4px;cursor:pointer;font-size:11px;";
        const renderFila = (v) => {
            const esCancelada = v.estado === "CANCELADO";
            const colorEstado = esCancelada ? "#ef4444" : v.estado === "PENDIENTE" ? "#f59e0b" : "#4ade80";
            const acciones = esCancelada
                ? `<button onclick="reactivarVenta(${v.id})" style="${btnStyle}border:1px solid #4ade80;color:#4ade80;">Reactivar</button>`
                : `<div style="display:flex;gap:4px;align-items:center;flex-wrap:wrap;">
                    <button onclick="window.open('${API}/pdf/venta/${v.id}?token=${getToken()}','_blank')" style="${btnStyle}border:1px solid #333;color:#aaa;">PDF</button>
                    <input id="motivo_${v.id}" placeholder="Motivo..." style="padding:3px 6px;font-size:11px;width:110px;border-radius:4px;background:#1a1a1a;color:#ccc;border:1px solid #333;">
                    <button onclick="cancelarVenta(${v.id})" style="${btnStyle}border:1px solid #ef4444;color:#ef4444;">Cancelar</button>
                   </div>`;
            return `<tr style="border-bottom:1px solid #1a1a1a;${esCancelada ? "opacity:0.5;" : ""}">
                <td style="padding:8px 6px;color:#555;">${v.id}</td>
                <td style="padding:8px 6px;color:#aaa;white-space:nowrap;">${fmtFecha(v.fecha)}</td>
                <td style="padding:8px 6px;">${s(v.cliente)||"—"}</td>
                <td style="padding:8px 6px;color:#aaa;">${s(v.metodo_pago)}</td>
                <td style="padding:8px 6px;text-align:right;font-weight:600;">Q${parseFloat(v.total).toFixed(2)}</td>
                <td style="padding:8px 6px;font-size:12px;color:${colorEstado};">${s(v.estado)}</td>
                <td style="padding:8px 6px;">${acciones}</td>
            </tr>`;
        };
        cont.innerHTML = `<table style="width:100%;border-collapse:collapse;font-size:13px;">
            <thead><tr style="color:#aaa;border-bottom:1px solid #333;text-align:left;">
                <th style="padding:8px 6px;">#</th>
                <th style="padding:8px 6px;">Fecha</th>
                <th style="padding:8px 6px;">Cliente</th>
                <th style="padding:8px 6px;">Método</th>
                <th style="padding:8px 6px;text-align:right;">Total</th>
                <th style="padding:8px 6px;">Estado</th>
                <th style="padding:8px 6px;"></th>
            </tr></thead>
            <tbody>${data.map(v => renderFila(v)).join("")}</tbody>
        </table>`;
    })
    .catch(() => mostrarMensaje("❌ Error cargando ventas"));
}

async function cancelarVenta(id) {
    const motivo = document.getElementById("motivo_" + id)?.value?.trim();
    if (!await confirmarDialog("Cancelar venta", "¿Seguro que deseas cancelar esta venta?" + (motivo ? " Motivo: " + motivo : ""), "danger")) return;
    authFetch(`${API}/ventas/${id}/cancelar`, {
        method: "PATCH",
        body: JSON.stringify({ motivo: motivo || "" })
    })
    .then(r => r.json())
    .then(data => {
        if (data.error) { mostrarMensaje("❌ " + data.error); return; }
        mostrarMensaje("✅ Venta cancelada");
        cargarReportes();
    })
    .catch(() => mostrarMensaje("❌ Error al cancelar la venta"));
}

async function reactivarVenta(id) {
    if (!await confirmarDialog("Reactivar venta", "¿Deseas reactivar esta venta cancelada?", "info")) return;
    authFetch(`${API}/ventas/${id}/reactivar`, { method: "PATCH" })
    .then(r => r.json())
    .then(data => {
        if (data.error) { mostrarMensaje("❌ " + data.error); return; }
        mostrarMensaje("✅ Venta reactivada");
        cargarReportes();
    })
    .catch(() => mostrarMensaje("❌ Error al reactivar la venta"));
}

function cargarFacturas(){
    authFetch(`${API}/factura`)
    .then(r => r.json())
    .then(data => {
        const pendientes = data.filter(f => f.sat_estado === "PENDIENTE" || !f.sat_estado);
        const facturadas = data.filter(f => f.sat_estado === "FACTURADA");

        const btnStyle = "background:transparent;padding:3px 8px;border-radius:4px;cursor:pointer;font-size:11px;";
        const renderFila = (f, esPendiente) => {
            const numSat = esPendiente
                ? `<input id="satNum_${f.id_factura}" placeholder="No. SAT" style="padding:4px 8px;font-size:12px;width:110px;">`
                : `<span style="color:#4ade80;font-size:12px;">${s(f.sat_numero||"—")}</span>`;
            const btnSat = esPendiente
                ? `<button onclick="marcarSAT(${f.id_factura})" style="${btnStyle}border:1px solid #4ade80;color:#4ade80;">Marcar SAT</button>`
                : `<button onclick="revertirSAT(${f.id_factura})" style="${btnStyle}border:1px solid #555;color:#555;">Revertir</button>`;
            return `<tr style="border-bottom:1px solid #1a1a1a;">
                <td style="padding:8px 6px;color:#aaa;">#${f.id_factura}</td>
                <td style="padding:8px 6px;color:#aaa;white-space:nowrap;">${fmtFecha(f.fecha)}</td>
                <td style="padding:8px 6px;">${s(f.nombre)}</td>
                <td style="padding:8px 6px;color:#aaa;">${s(f.nit)}</td>
                <td style="padding:8px 6px;text-align:right;font-weight:600;">Q${parseFloat(f.total).toFixed(2)}</td>
                <td style="padding:8px 6px;">${numSat}</td>
                <td style="padding:8px 6px;">
                    <button onclick="descargarFactura(${f.id_factura})" style="${btnStyle}border:1px solid #333;color:#aaa;margin-right:4px;">PDF</button>
                    ${btnSat}
                </td>
            </tr>`;
        };
        const renderTabla = (lista, esPendiente) => {
            if (!lista.length) return `<p style="color:#555;font-size:13px;padding:12px 0;">${esPendiente ? "Sin facturas pendientes." : "Sin facturas registradas en SAT."}</p>`;
            return `<table style="width:100%;border-collapse:collapse;font-size:13px;">
                <thead><tr style="color:#aaa;border-bottom:1px solid #333;text-align:left;">
                    <th style="padding:8px 6px;">Factura</th>
                    <th style="padding:8px 6px;">Fecha</th>
                    <th style="padding:8px 6px;">Cliente</th>
                    <th style="padding:8px 6px;">NIT</th>
                    <th style="padding:8px 6px;text-align:right;">Total</th>
                    <th style="padding:8px 6px;">${esPendiente ? "No. SAT" : "<span style='color:#4ade80'>No. SAT</span>"}</th>
                    <th style="padding:8px 6px;"></th>
                </tr></thead>
                <tbody>${lista.map(f => renderFila(f, esPendiente)).join("")}</tbody>
            </table>`;
        };

        const elP = document.getElementById("listaFacturasPendientesSAT");
        const elF = document.getElementById("listaFacturasFacturadasSAT");
        if (elP) elP.innerHTML = renderTabla(pendientes, true);
        if (elF) elF.innerHTML = renderTabla(facturadas, false);
    })
    .catch(() => {
        const el = document.getElementById("listaFacturasPendientesSAT");
        if (el) el.innerHTML = `<p style="color:#ef4444;font-size:13px;">Error cargando facturas. Verifica que la API esté activa.</p>`;
    });
}

function marcarSAT(id) {
    const num = document.getElementById(`satNum_${id}`)?.value?.trim() || "";
    authFetch(`${API}/factura/${id}/sat`, {
        method: "PATCH",
        body: JSON.stringify({ sat_numero: num })
    })
    .then(r => r.json())
    .then(() => { mostrarMensaje("✅ Marcada como facturada en SAT"); cargarFacturas(); })
    .catch(() => mostrarMensaje("❌ Error"));
}

async function revertirSAT(id) {
    if (!await confirmarDialog("Revertir venta", "¿Revertir esta venta a pendiente SAT?")) return;
    authFetch(`${API}/factura/${id}/sat-revertir`, { method: "PATCH" })
    .then(() => { mostrarMensaje("✅ Revertida a pendiente"); cargarFacturas(); })
    .catch(() => mostrarMensaje("❌ Error"));
}

// ======================
// PRODUCTOS
// ======================
function listarProductos(){

    authFetch(`${API}/productos`)

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

    authFetch(`${API}/productos/${id}`, {

        method: "PUT",

        body: JSON.stringify({

            precio_venta: precio,

            stock: stock,

            usuario:
            usuario ? usuario.nombre : "ADMIN"
        })
    })

    .then(r => r.json())

    .then(() => {

        mostrarMensaje("✅ Producto actualizado");

        listarProductos();
    });
}

// ======================
// LOGIN
// ======================
function login(){

    let usuario = document.getElementById("usuario").value;
    let password = document.getElementById("password").value;

    fetch(`${API}/usuarios/login`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ usuario, password })
    })
    .then(async r => {
        const data = await r.json();
        if (!r.ok) throw new Error(data.mensaje || "Credenciales incorrectas");
        return data;
    })
    .then(data => {
        localStorage.setItem("token", data.token);
        localStorage.setItem("usuario", JSON.stringify({
            id_usuario: data.id_usuario,
            nombre: data.nombre,
            rol: data.rol
        }));
        window.location = "panel.html";
    })
    .catch(error => {
        console.log(error);
        mostrarMensaje("❌ " + error.message);
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

    authFetch(`${API}/productos/categorias`)

    .then(r => r.json())

    .then(data => {

        let html = "";

        data.forEach(c => {
            const esCombos = c.nombre.toLowerCase().includes("combo");
            const accion = esCombos
                ? `cargarCombos()`
                : `cargarProductosCategoria(${c.id})`;

            html += `<button class="btn" onclick="${accion}">${c.nombre}</button>`;
        });

        document.getElementById("categoriasPOS").innerHTML = html;
    })

    .catch(error => {

        console.log(error);

        mostrarMensaje(
        "Error cargando categorías"
        );
    });
}

// ======================
// COMBOS POS
// ======================
function cargarCombos(){
    document.getElementById("subcategoriasPOS").innerHTML = "";

    authFetch(`${API}/combos`)
    .then(r => r.json())
    .then(data => {
        let html = "";
        data.forEach(c => {
            const itemsTexto = c.items.map(i => `${i.cantidad}x ${i.nombre_item}`).join(", ");
            html += `
            <div class="card">
                <h3>${c.nombre}</h3>
                <p style="font-size:12px; color:#aaa;">${itemsTexto}</p>
                <p><strong>Q${c.precio}</strong></p>
                <button class="btn" onclick="agregarComboAlCarrito(${c.id_combo})">AGREGAR</button>
            </div>`;
        });
        document.getElementById("productosPOS").innerHTML = html;
    })
    .catch(() => mostrarMensaje("❌ Error cargando combos"));
}

// Estado temporal para el modal de bebidas
let _comboPendiente = null;
let _bebidasElegidas = {};  // { id_producto: { nombre, cantidad } }

function agregarComboAlCarrito(idCombo){
    authFetch(`${API}/combos`)
    .then(r => r.json())
    .then(data => {
        const combo = data.find(c => c.id_combo === idCombo);
        if (!combo) return;

        const itemsSeleccionables = combo.items.filter(i => i.es_seleccionable);
        if (itemsSeleccionables.length === 0) {
            // Sin bebidas seleccionables, agregar directo
            _finalizarAgregarCombo(combo, []);
            return;
        }

        // Hay bebidas seleccionables → abrir modal
        _comboPendiente = combo;
        _bebidasElegidas = {};
        const totalBebidas = itemsSeleccionables.reduce((s, i) => s + i.cantidad, 0);

        document.getElementById("modalBebidasInstruccion").textContent =
            `Elige ${totalBebidas} bebida(s) para tu ${combo.nombre}`;
        document.getElementById("bebidasRequeridas").textContent = totalBebidas;
        document.getElementById("bebidasSeleccionadas").textContent = "0";

        // Cargar bebidas disponibles (categoria 1)
        const idCat = itemsSeleccionables[0].id_categoria_seleccion;
        authFetch(`${API}/productos/categoria/${idCat}`)
        .then(r => r.json())
        .then(bebidas => {
            // Excluir ingredientes (precio_venta = 0)
            const bebFiltr = bebidas.filter(b => parseFloat(b.precio_venta || 0) > 0);
            let html = "";
            bebFiltr.forEach(b => {
                html += `
                <div class="pos-producto-card">
                    <div class="pos-prod-info">
                        <span class="pos-prod-nombre">${s(b.nombre)}</span>
                        <span class="pos-prod-precio">Q${parseFloat(b.precio_venta||0).toFixed(2)}</span>
                        <span class="pos-prod-stock">· ${b.stock} en stock</span>
                    </div>
                    <div class="pos-prod-ctrl">
                        <button class="pos-ctrl-btn" onclick="cambiarBebida(${b.id}, '${s(b.nombre)}', -1)">−</button>
                        <span id="cnt_${b.id}" class="pos-ctrl-num">0</span>
                        <button class="pos-ctrl-btn" onclick="cambiarBebida(${b.id}, '${s(b.nombre)}', 1)">+</button>
                    </div>
                </div>`;
            });
            document.getElementById("listaBebidas").innerHTML = html;
            abrirModal("modalBebidas");
        });
    });
}

function cambiarBebida(id, nombre, delta){
    const requeridas = parseInt(document.getElementById("bebidasRequeridas").textContent);
    const actuales = Object.values(_bebidasElegidas).reduce((s, b) => s + b.cantidad, 0);

    if (delta > 0 && actuales >= requeridas) {
        mostrarMensaje(`⚠️ Solo puedes elegir ${requeridas} bebida(s)`);
        return;
    }

    if (!_bebidasElegidas[id]) _bebidasElegidas[id] = { nombre, cantidad: 0 };
    _bebidasElegidas[id].cantidad += delta;
    if (_bebidasElegidas[id].cantidad < 0) _bebidasElegidas[id].cantidad = 0;

    document.getElementById(`cnt_${id}`).textContent = _bebidasElegidas[id].cantidad;

    const total = Object.values(_bebidasElegidas).reduce((s, b) => s + b.cantidad, 0);
    document.getElementById("bebidasSeleccionadas").textContent = total;
}

function confirmarBebidas(){
    const requeridas = parseInt(document.getElementById("bebidasRequeridas").textContent);
    const total = Object.values(_bebidasElegidas).reduce((s, b) => s + b.cantidad, 0);

    if (total !== requeridas) {
        mostrarMensaje(`⚠️ Debes elegir exactamente ${requeridas} bebida(s)`);
        return;
    }

    // Construir ingredientes con las bebidas elegidas
    const bebidasIngredientes = Object.entries(_bebidasElegidas)
        .filter(([, v]) => v.cantidad > 0)
        .map(([id, v]) => ({ id_producto: parseInt(id), nombre_item: v.nombre, cantidad: v.cantidad }));

    // Los demás items del combo (no seleccionables)
    const otrosItems = _comboPendiente.items.filter(i => !i.es_seleccionable);

    cerrarModal("modalBebidas");
    _finalizarAgregarCombo(_comboPendiente, [...bebidasIngredientes, ...otrosItems]);
}

function _finalizarAgregarCombo(combo, ingredientes){
    carrito.push({
        id_producto: 0,
        nombre: `🎯 ${combo.nombre}`,
        precio: Number(combo.precio),
        cantidad: 1,
        tipo: "COMBO",
        ingredientes
    });
    renderCarrito();
    mostrarMensaje(`✅ ${combo.nombre} agregado`);
}

// ======================
// SUBCATEGORIAS
// ======================
// ======================
// SUBCATEGORIAS
// ======================
function cargarProductosCategoria(id){

    authFetch(`${API}/productos/subcategorias/${id}`)

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

        mostrarMensaje(
        "Error cargando subcategorías"
        );
    });
}

// ======================
// PRODUCTOS SUBCATEGORIA
// ======================

function cargarSubcategoria(id){

    authFetch(`${API}/productos/subcategoria/${id}`)

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

        mostrarMensaje(
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
    let subtotalBruto = 0;

    // Items del carrito
    let itemsHtml = "";
    if (carrito.length === 0) {
        itemsHtml = '<p style="color:#555;font-size:13px;text-align:center;padding:16px 0;">El carrito está vacío</p>';
    } else {
        carrito.forEach((p, index) => {
            const subtotal = p.precio * p.cantidad;
            subtotalBruto += subtotal;
            itemsHtml +=
                '<div style="display:flex;align-items:center;gap:8px;padding:8px 0;border-bottom:1px solid var(--border);">'
                + '<div style="flex:1;min-width:0;">'
                + '<div style="font-size:13px;font-weight:600;color:var(--text);white-space:nowrap;overflow:hidden;text-overflow:ellipsis;">' + s(p.nombre) + '</div>'
                + '<div style="font-size:12px;color:#888;">' + p.cantidad + ' × Q' + Number(p.precio).toFixed(2) + '</div>'
                + '</div>'
                + '<div style="font-size:13px;font-weight:700;color:#90caf9;white-space:nowrap;">Q' + subtotal.toFixed(2) + '</div>'
                + '<button onclick="eliminarCarrito(' + index + ')" style="background:transparent;border:none;color:#555;cursor:pointer;font-size:16px;padding:2px 4px;line-height:1;" title="Eliminar">✕</button>'
                + '</div>';
        });
    }

    // Calcular descuento y total
    const descPct = parseFloat(document.getElementById("descuentoPct")?.value) || 0;
    const descuento = subtotalBruto * (descPct / 100);
    totalVenta = subtotalBruto - descuento;

    // Cliente
    const clienteHtml = clienteSeleccionado
        ? '<div style="display:flex;align-items:center;gap:8px;background:#0d2a0d;border:1px solid #22c55e;border-radius:8px;padding:8px 12px;">'
          + '<span style="font-size:18px;">👤</span>'
          + '<span style="flex:1;font-size:13px;font-weight:600;color:#4ade80;">' + s(clienteSeleccionado.nombre) + '</span>'
          + '<button onclick="clienteSeleccionado=null;renderCarrito()" style="background:transparent;border:none;color:#555;cursor:pointer;font-size:15px;" title="Quitar">✕</button>'
          + '</div>'
        : '<div>'
          + '<input id="buscarClientePOS" placeholder="Buscar cliente..." style="width:100%;padding:8px 10px;font-size:13px;border-radius:8px;border:1px solid var(--border);background:var(--surface-1);color:var(--text);" oninput="buscarClientePOSFn()">'
          + '<div id="resultadosClientePOS" style="margin-top:4px;"></div>'
          + '</div>';

    // Descuento badge
    const descHtml = descuento > 0
        ? '<div style="display:flex;justify-content:space-between;font-size:13px;color:#f59e0b;">'
          + '<span>Descuento (' + descPct + '%)</span><span>-Q' + descuento.toFixed(2) + '</span></div>'
        : '';

    const footerHtml =
        '<div style="border-top:1px solid var(--border);padding-top:14px;margin-top:4px;">'
        // subtotal row
        + '<div style="display:flex;justify-content:space-between;font-size:13px;color:#888;margin-bottom:6px;">'
        + '<span>Subtotal</span><span>Q' + subtotalBruto.toFixed(2) + '</span></div>'
        // descuento
        + descHtml
        // descuento input
        + '<div style="display:flex;align-items:center;gap:8px;margin:8px 0;">'
        + '<label style="font-size:12px;color:#888;white-space:nowrap;">Desc. %</label>'
        + '<input id="descuentoPct" type="number" min="0" max="100" value="' + descPct + '" placeholder="0"'
        + ' style="width:70px;padding:5px 8px;font-size:13px;border-radius:6px;border:1px solid var(--border);background:var(--surface-1);color:var(--text);"'
        + ' oninput="renderCarrito()">'
        + '</div>'
        // total
        + '<div style="display:flex;justify-content:space-between;align-items:center;margin-bottom:14px;">'
        + '<span style="font-size:14px;font-weight:700;color:var(--text);">Total</span>'
        + '<span style="font-size:20px;font-weight:800;color:#4ade80;">Q' + totalVenta.toFixed(2) + '</span>'
        + '</div>'
        // cliente
        + '<div style="margin-bottom:12px;">'
        + '<div style="font-size:11px;color:#666;margin-bottom:5px;text-transform:uppercase;letter-spacing:0.05em;">Cliente</div>'
        + clienteHtml
        + '</div>'
        // botón
        + '<button onclick="abrirModalPago()" style="width:100%;padding:12px;border-radius:10px;border:none;background:linear-gradient(135deg,#1565c0,#0d47a1);color:#fff;font-size:14px;font-weight:700;cursor:pointer;letter-spacing:0.03em;">'
        + 'Finalizar Venta</button>'
        + '</div>';

    const carritoDiv = document.getElementById("carrito");
    if (carritoDiv) {
        carritoDiv.innerHTML = itemsHtml + footerHtml;
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
// TOGGLE CAMPOS FACTURA
// ======================
function toggleCamposFactura(){
    const checkbox = document.getElementById("requiereFactura");
    const campos = document.getElementById("camposFactura");
    if (campos) campos.style.display = checkbox.checked ? "block" : "none";
}

function toggleCamposPendiente(){
    const metodo = document.getElementById("metodoPago").value;
    const campo = document.getElementById("campoNombrePendiente");
    if (campo) campo.style.display = metodo === "PENDIENTE" ? "block" : "none";
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
    const modal = document.getElementById("modalPago");
    if (modal) modal.style.display = "none";
    const checkbox = document.getElementById("requiereFactura");
    if (checkbox) { checkbox.checked = false; toggleCamposFactura(); }
    const nombrePend = document.getElementById("nombreClientePendiente");
    if (nombrePend) nombrePend.value = "";
    const select = document.getElementById("metodoPago");
    if (select) { select.value = "EFECTIVO"; toggleCamposPendiente(); }
}

// ======================
// CONFIRMAR VENTA
// ======================
function confirmarVenta(){
    const metodo = document.getElementById("metodoPago").value;
    const observacion = document.getElementById("observacionVenta").value;
    const requiereFactura = document.getElementById("requiereFactura").checked;

    if (metodo === "PENDIENTE") {
        const nombreCliente = document.getElementById("nombreClientePendiente").value.trim();
        if (!nombreCliente) {
            mostrarMensaje("⚠️ Ingresa el nombre del cliente para la orden pendiente.");
            return;
        }
    }

    const nombreOrden = metodo === "PENDIENTE"
        ? document.getElementById("nombreClientePendiente").value.trim()
        : "POS";

    const datosFactura = requiereFactura ? {
        nit: document.getElementById("nitFacturaPOS").value || "CF",
        nombre: document.getElementById("nombreFacturaPOS").value || "Consumidor Final",
        direccion: document.getElementById("direccionFacturaPOS").value || "Ciudad"
    } : null;

    cerrarModalPago();
    registrarVenta(metodo, observacion, datosFactura, nombreOrden);
}

// ======================
// REGISTRAR VENTA
// ======================
// Expande los combos en sus ingredientes para descontar inventario
function expandirCarritoParaVenta(carrito){
    const items = [];
    carrito.forEach(item => {
        if (item.tipo === "COMBO" && item.ingredientes) {
            // El combo como línea de precio
            items.push({ id_producto: 0, nombre: item.nombre, cantidad: 1, precio: item.precio });
            // Los ingredientes para descontar inventario (precio 0, no suman al total)
            item.ingredientes.forEach(ing => {
                if (ing.id_producto) {
                    items.push({ id_producto: ing.id_producto, nombre: ing.nombre_item, cantidad: ing.cantidad, precio: 0 });
                }
            });
        } else {
            items.push(item);
        }
    });
    return items;
}

function registrarVenta(metodo, observacion, datosFactura = null, nombreOrden = "POS"){

    let usuario =
    JSON.parse(
    localStorage.getItem("usuario")
    );

    let venta = {

        id_cliente: clienteSeleccionado ? clienteSeleccionado.id : null,

        id_usuario:
        usuario.id_usuario,

        nombre_orden: nombreOrden,

        numero_orden: "000",

        forma_cobro:
        metodo === "PENDIENTE"
        ? "PENDIENTE"
        : "PAGADO",

        metodo_pago: metodo,

        total: totalVenta,

        observacion: observacion,

        descuento_pct: parseFloat(document.getElementById("descuentoPct")?.value) || 0,

        productos: expandirCarritoParaVenta(carrito)
    };

    authFetch(`${API}/ventas`, {

        method: "POST",

        body: JSON.stringify(venta)
    })

    .then(r => r.json())

    .then(data => {

        const mostrarBotonPDF = (url) => {
            const ultimaVenta = document.getElementById("ultimaVenta");
            const ultimaVentaPDF = document.getElementById("ultimaVentaPDF");
            if (ultimaVenta && ultimaVentaPDF) {
                ultimaVenta.style.display = "block";
                ultimaVentaPDF.onclick = () => window.open(url, "_blank");
                clearTimeout(ultimaVenta._timer);
                ultimaVenta._timer = setTimeout(() => {
                    ultimaVenta.style.transition = "opacity .5s";
                    ultimaVenta.style.opacity = "0";
                    setTimeout(() => { ultimaVenta.style.display = "none"; ultimaVenta.style.opacity = "1"; }, 500);
                }, 6000);
            }
        };

        if (datosFactura && data.id_venta) {
            authFetch(`${API}/factura`, {
                method: "POST",
                body: JSON.stringify({
                    id_venta: data.id_venta,
                    nit: datosFactura.nit,
                    nombre: datosFactura.nombre,
                    direccion: datosFactura.direccion
                })
            })
            .then(r => r.json())
            .then(fac => {
                mostrarMensaje("✅ Venta registrada con factura");
                const idFac = fac.id_factura ?? fac.id ?? fac.idFactura;
                mostrarBotonPDF(`${API}/pdf/factura/${idFac}?token=${getToken()}`);
            })
            .catch(() => {
                mostrarMensaje("✅ Venta registrada (error al crear factura)");
                mostrarBotonPDF(`${API}/pdf/venta/${data.id_venta}?token=${getToken()}`);
            });
        } else {
            mostrarMensaje("✅ Venta registrada");
            mostrarBotonPDF(`${API}/pdf/venta/${data.id_venta}?token=${getToken()}`);
        }

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
function mostrarMensaje(texto) {
    const isError   = /❌/.test(texto);
    const isWarning = /⚠️/.test(texto);
    const color  = isError ? '#ef4444' : isWarning ? '#f59e0b' : '#22c55e';
    const border = isError ? '#7f1d1d' : isWarning ? '#78350f' : '#14532d';
    const icon   = isError ? '✕' : isWarning ? '⚠' : '✓';

    const toast = document.createElement('div');
    toast.style.cssText = [
        'pointer-events:auto',
        'display:flex', 'align-items:center', 'gap:10px',
        'background:#111', 'border:1px solid ' + border,
        'border-left:4px solid ' + color,
        'border-radius:10px', 'padding:12px 16px',
        'min-width:240px', 'max-width:340px',
        'box-shadow:0 8px 24px rgba(0,0,0,.5)',
        'animation:toastIn .2s ease',
        'font-size:13px', 'color:#e2e2e2', 'line-height:1.4'
    ].join(';');

    toast.innerHTML =
        '<span style="font-size:14px;font-weight:700;color:' + color + ';">' + icon + '</span>'
        + '<span style="flex:1;">' + texto.replace(/^[✅❌⚠️🎮🛒📊🏆🥇🥈🥉]+\s*/, '') + '</span>'
        + '<button onclick="this.parentNode.remove()" style="background:transparent;border:none;color:#444;cursor:pointer;font-size:16px;padding:0 2px;line-height:1;">✕</button>';

    const container = document.getElementById('toastContainer');
    if (container) {
        container.appendChild(toast);
        setTimeout(() => {
            toast.style.animation = 'toastOut .3s ease forwards';
            setTimeout(() => toast.remove(), 300);
        }, 3500);
    } else {
        mostrarMensaje(texto);
    }
}

function confirmarDialog(titulo, mensaje, tipo) {
    return new Promise(resolve => {
        const modal  = document.getElementById('modalConfirm');
        const icon   = document.getElementById('modalConfirmIcon');
        const titEl  = document.getElementById('modalConfirmTitle');
        const msgEl  = document.getElementById('modalConfirmMsg');
        const btnOk  = document.getElementById('modalConfirmOk');
        const btnCan = document.getElementById('modalConfirmCancel');
        if (!modal) { resolve(window.confirm(mensaje)); return; }

        const esDanger = tipo === 'danger';
        icon.textContent  = esDanger ? '🗑️' : '❓';
        titEl.textContent = titulo || '¿Confirmar acción?';
        msgEl.innerHTML = mensaje || '';
        btnOk.textContent = esDanger ? 'Eliminar' : 'Confirmar';
        btnOk.style.background = esDanger ? '#dc2626' : 'linear-gradient(135deg,#1565c0,#0d47a1)';
        btnOk.style.color = '#fff';

        modal.style.display = 'flex';

        const close = (val) => {
            modal.style.display = 'none';
            btnOk.onclick  = null;
            btnCan.onclick = null;
            resolve(val);
        };
        btnOk.onclick  = () => close(true);
        btnCan.onclick = () => close(false);
        modal.onclick  = (e) => { if (e.target === modal) close(false); };
    });
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

    // Solo ejecutar en el panel, no en el login
    if (window.location.pathname.includes("panel")) {
        aplicarPermisos();
        cargarDashboard();
        cargarTopClientes();
        cargarTopGamers();
        listarProductos();
        cargarCategoriasPOS();
        cargarPendientes();
        cargarSelectProductosRapido();
    }
};


// ===========================
// 🎮 CONSOLAS
// ===========================

// Estado del selector de consolas
let _consolasData = [];
let _consolaSeleccionada = null;
let _tiempoConsola = { horas: 0, min30: 0, controles: 0 };
const PRECIO_CONTROL_EXTRA = 5;
const PRECIO_MEDIA_HORA = 8;

function seleccionarModoPOS(modo) {
    const panelConsolas   = document.getElementById("panelConsolas");
    const panelProductos  = document.getElementById("panelProductos");
    const btnConsolas     = document.getElementById("modoConsolasBtn");
    const btnProductos    = document.getElementById("modoProductosBtn");

    const activoStyle = `
        border:2px solid rgba(41,121,255,0.6);
        background:linear-gradient(135deg,rgba(21,101,192,0.3),rgba(41,121,255,0.15));
        color:var(--text);
        box-shadow:0 0 24px rgba(41,121,255,0.2);`;
    const inactivoStyle = `
        border:2px solid transparent;
        background:rgba(255,255,255,0.03);
        color:var(--text-dim);
        box-shadow:none;`;

    if (modo === 'consolas') {
        panelConsolas.style.display  = "block";
        panelProductos.style.display = "none";
        btnConsolas.style.cssText  += activoStyle;
        btnProductos.style.cssText += inactivoStyle;
    } else {
        panelConsolas.style.display  = "none";
        panelProductos.style.display = "block";
        btnProductos.style.cssText += activoStyle;
        btnConsolas.style.cssText  += inactivoStyle;
    }
}

function cargarConsolas() {
    authFetch(`${API}/consolas`)
    .then(r => r.json())
    .then(data => {
        _consolasData = data;
        renderSelectorConsolas();
    });
}

function renderSelectorConsolas() {
    const data = _consolasData;

    const iconoTipo = t => t === 'PS' ? '🎮' : t === 'XBOX' ? '🟢' : t === 'PC' ? '🖥️' : '🕹️';

    const botonesHtml = data.map(c => {
        const libre      = c.estado === 'LIBRE';
        const seleccion  = _consolaSeleccionada?.id === c.id;
        return `
        <div onclick="${libre ? `seleccionarConsola(${c.id})` : ''}"
             style="
                cursor:${libre ? 'pointer' : 'not-allowed'};
                opacity:${libre ? 1 : 0.45};
                border-radius:14px;
                padding:12px 16px;
                min-width:120px;
                text-align:center;
                background:${seleccion ? '#0d2137' : 'var(--card)'};
                border:2px solid ${seleccion ? '#1e88e5' : libre ? '#2a2a2a' : '#1a1a1a'};
                transition:border .15s, background .15s;
                position:relative;">
            <div style="font-size:26px; margin-bottom:4px;">${iconoTipo(c.tipo)}</div>
            <div style="font-weight:700; font-size:13px; color:${seleccion ? '#90caf9' : '#e2e2e2'};">${s(c.nombre)}</div>
            <div style="font-size:11px; color:#555; margin:2px 0;">Q${c.precio}/hr</div>
            <div style="
                display:inline-block; margin-top:6px;
                padding:2px 10px; border-radius:20px; font-size:10px; font-weight:700;
                background:${libre ? '#14532d' : '#7f1d1d'};
                color:${libre ? '#22c55e' : '#f87171'};">
                ${libre ? 'LIBRE' : 'OCUPADA'}
            </div>
            ${seleccion ? `<div style="position:absolute;top:8px;right:8px;width:8px;height:8px;border-radius:50%;background:#1e88e5;"></div>` : ''}
        </div>`;
    }).join('');

    // Total
    let totalConsola = 0;
    if (_consolaSeleccionada) {
        const ph = _consolaSeleccionada.precio;
        totalConsola = ph * _tiempoConsola.horas
            + PRECIO_MEDIA_HORA * _tiempoConsola.min30
            + PRECIO_CONTROL_EXTRA * _tiempoConsola.controles;
    }
    const totalMins = _tiempoConsola.horas * 60 + _tiempoConsola.min30 * 30;

    const btnActivo = _consolaSeleccionada && totalMins > 0;
    const nombre    = _consolaSeleccionada ? s(_consolaSeleccionada.nombre) : '';
    const precioHr  = _consolaSeleccionada ? 'Q' + _consolaSeleccionada.precio + '/hr' : '—';

    const ctrl = (label, sub, campo) =>
        '<div style="text-align:center;">'
        + '<div style="font-size:11px;font-weight:600;color:#90caf9;margin-bottom:2px;">' + label + '</div>'
        + '<div style="font-size:10px;color:#555;margin-bottom:6px;">' + sub + '</div>'
        + '<div style="display:inline-flex;align-items:center;border:1px solid #2a2a2a;border-radius:10px;overflow:hidden;">'
        + '<button onclick="cambiarTiempoConsola(\'' + campo + '\',-1)" style="width:34px;height:36px;background:transparent;border:none;color:#90caf9;font-size:20px;cursor:pointer;">−</button>'
        + '<span style="min-width:28px;text-align:center;font-size:16px;font-weight:700;color:#e2e2e2;">' + _tiempoConsola[campo] + '</span>'
        + '<button onclick="cambiarTiempoConsola(\'' + campo + '\',1)" style="width:34px;height:36px;background:transparent;border:none;color:#90caf9;font-size:20px;cursor:pointer;">+</button>'
        + '</div></div>';

    // Layout: consolas a la izquierda, tiempo a la derecha
    const panelTiempo = _consolaSeleccionada
        ? '<div style="flex:1;background:#0a0a0a;border:1px solid #1e1e1e;border-radius:14px;padding:18px;">'
          + '<div style="display:flex;align-items:center;gap:8px;margin-bottom:16px;">'
          +   '<span style="font-size:18px;">' + iconoTipo(_consolaSeleccionada.tipo) + '</span>'
          +   '<div>'
          +     '<div style="font-size:13px;font-weight:700;color:#90caf9;">' + nombre + '</div>'
          +     '<div style="font-size:11px;color:#555;">' + precioHr + '</div>'
          +   '</div>'
          + '</div>'
          + '<div style="display:grid;grid-template-columns:repeat(3,1fr);gap:8px;margin-bottom:16px;">'
          + ctrl('Horas', precioHr, 'horas')
          + ctrl('30 min', 'Q' + PRECIO_MEDIA_HORA + '/u', 'min30')
          + ctrl('Ctrl +', 'Q' + PRECIO_CONTROL_EXTRA + '/u', 'controles')
          + '</div>'
          + '<div style="background:#111;border-radius:10px;padding:12px 14px;display:flex;align-items:center;justify-content:space-between;gap:10px;">'
          +   '<div>'
          +     '<div style="font-size:10px;color:#555;margin-bottom:2px;">' + (totalMins > 0 ? totalMins + ' min de juego' : 'Sin tiempo seleccionado') + '</div>'
          +     '<div style="font-size:22px;font-weight:800;color:' + (totalConsola > 0 ? '#fff' : '#333') + ';">Q' + totalConsola.toFixed(2) + '</div>'
          +   '</div>'
          +   '<button onclick="agregarConsolaAlCarrito()" ' + (btnActivo ? '' : 'disabled')
          +   ' style="padding:12px 22px;border-radius:10px;border:none;cursor:' + (btnActivo ? 'pointer' : 'default') + ';font-size:13px;font-weight:700;'
          +   'background:' + (btnActivo ? 'linear-gradient(135deg,#1565c0,#0d47a1)' : '#1a1a1a') + ';color:' + (btnActivo ? '#fff' : '#444') + ';white-space:nowrap;">'
          +   '🛒 Agregar al carrito</button>'
          + '</div>'
          + '</div>'
        : '<div style="flex:1;background:#0a0a0a;border:1px solid #1a1a1a;border-radius:14px;padding:18px;display:flex;align-items:center;justify-content:center;">'
          + '<div style="text-align:center;color:#333;">'
          +   '<div style="font-size:36px;margin-bottom:8px;">🕹️</div>'
          +   '<div style="font-size:13px;">Selecciona una consola</div>'
          + '</div></div>';

    const html =
        '<div style="display:flex;gap:12px;align-items:flex-start;">'
        // Columna izquierda: grid de consolas
        + '<div style="display:grid;grid-template-columns:repeat(2,minmax(110px,1fr));gap:8px;">'
        + data.map(c => {
            const libre     = c.estado === 'LIBRE';
            const seleccion = _consolaSeleccionada?.id === c.id;
            return '<div onclick="' + (libre ? 'seleccionarConsola(' + c.id + ')' : '') + '"'
                + ' style="cursor:' + (libre ? 'pointer' : 'not-allowed') + ';opacity:' + (libre ? 1 : 0.4) + ';'
                + 'border-radius:12px;padding:14px 10px;text-align:center;'
                + 'background:' + (seleccion ? '#0d2137' : '#0a0a0a') + ';'
                + 'border:2px solid ' + (seleccion ? '#1e88e5' : libre ? '#1e1e1e' : '#111') + ';'
                + 'transition:border .15s,background .15s;position:relative;">'
                + (seleccion ? '<div style="position:absolute;top:8px;right:8px;width:7px;height:7px;border-radius:50%;background:#1e88e5;"></div>' : '')
                + '<div style="font-size:24px;margin-bottom:6px;">' + iconoTipo(c.tipo) + '</div>'
                + '<div style="font-size:12px;font-weight:700;color:' + (seleccion ? '#90caf9' : '#ccc') + ';margin-bottom:2px;">' + s(c.nombre) + '</div>'
                + '<div style="font-size:10px;color:#555;margin-bottom:6px;">Q' + c.precio + '/hr</div>'
                + '<div style="display:inline-block;padding:2px 8px;border-radius:20px;font-size:9px;font-weight:700;'
                + 'background:' + (libre ? '#14532d' : '#7f1d1d') + ';color:' + (libre ? '#22c55e' : '#f87171') + ';">'
                + (libre ? 'LIBRE' : 'OCUPADA') + '</div>'
                + '</div>';
        }).join('')
        + '</div>'
        // Columna derecha: tiempo de juego
        + panelTiempo
        + '</div>';

    document.getElementById('listaConsolas').innerHTML = html;
}

function seleccionarConsola(id) {
    _consolaSeleccionada = _consolasData.find(c => c.id === id);
    renderSelectorConsolas();
}

function cambiarTiempoConsola(tipo, delta) {
    _tiempoConsola[tipo] = Math.max(0, _tiempoConsola[tipo] + delta);
    renderSelectorConsolas();
}

function agregarConsolaAlCarrito() {
    if (!_consolaSeleccionada) return;
    const ph = _consolaSeleccionada.precio;
    const totalMins = _tiempoConsola.horas * 60 + _tiempoConsola.min30 * 30;
    const total = ph * _tiempoConsola.horas + PRECIO_MEDIA_HORA * _tiempoConsola.min30
                  + PRECIO_CONTROL_EXTRA * _tiempoConsola.controles;

    const partes = [];
    if (_tiempoConsola.horas > 0) partes.push(`${_tiempoConsola.horas}h`);
    if (_tiempoConsola.min30 > 0) partes.push(`${_tiempoConsola.min30 * 30}min`);
    if (_tiempoConsola.controles > 0) partes.push(`${_tiempoConsola.controles} ctrl extra`);

    carrito.push({
        id_producto: 0,
        nombre: `🎮 ${_consolaSeleccionada.nombre} (${partes.join(' + ')})`,
        precio: Math.round(total * 100) / 100,
        cantidad: 1,
        tipo: "SERVICIO",
        minutos: totalMins
    });

    // Reset
    _tiempoConsola = { horas: 0, min30: 0, controles: 0 };
    _consolaSeleccionada = null;
    renderSelectorConsolas();
    renderCarrito();
    mostrarMensaje("✅ Servicio de consola agregado");
}

// ===========================
// INICIAR CONSOLA
// ===========================

function iniciarConsola(id) {

    authFetch(`${API}/consolas/${id}/estado`, {

        method: "PUT",

        body: JSON.stringify({
            estado: "OCUPADA"
        })

    })
    .then(r => r.json())
    .then(() => {

        mostrarMensaje("🎮 Consola iniciada");

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

    authFetch(`${API}/consolas`)
    .then(r => r.json())
    .then(data => {

        let consola =
        data.find(c => c.id === id);

        if(!consola){

            mostrarMensaje("Consola no encontrada");
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

        return authFetch(
        `${API}/consolas/${id}/estado`,
        {
            method:"PUT",
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

        mostrarMensaje("Error finalizando consola");
    });
}

// ==================================================================
// 🔐 PERMISOS POR ROL  (Fase 1)
// ------------------------------------------------------------------
// Julio y Cristian (rol ADMIN) ven todo.
// Ludwin (CAJERO) tiene acceso parcial controlado.
// El resto solo puede cobrar: ve Ventas y Cerrar Sesión.
// ==================================================================
const ROLES_ADMIN  = ["ADMIN", "ADMINISTRADOR"];
const NOMBRES_ADMIN  = ["julio", "cristian"];
const NOMBRES_CAJERO = ["ludwin"];

function esAdmin() {
    const usuario = JSON.parse(localStorage.getItem("usuario"));
    if (!usuario) return false;
    const rol    = (usuario.rol    || "").toString().toUpperCase();
    const nombre = (usuario.nombre || "").toString().toLowerCase();
    return ROLES_ADMIN.includes(rol) || NOMBRES_ADMIN.some(n => nombre.includes(n));
}

function esCajero() {
    const usuario = JSON.parse(localStorage.getItem("usuario"));
    if (!usuario) return false;
    const nombre = (usuario.nombre || "").toString().toLowerCase();
    return NOMBRES_CAJERO.some(n => nombre.includes(n));
}

function aplicarPermisos() {
    if (esAdmin()) return;

    if (esCajero()) {
        // Cajero: Ventas, Productos, Inventario, Torneos, Clientes, Finanzas (solo gastos) y Cerrar Sesión
        const permitidosCajero = ["'ventas'", "'productos'", "'inventario'", "'torneos'", "'clientes'", "'finanzas'", "logout"];
        document.querySelectorAll(".sidebar button").forEach(btn => {
            const accion = btn.getAttribute("onclick") || "";
            if (!permitidosCajero.some(p => accion.includes(p))) btn.style.display = "none";
        });
        mostrar("ventas");
        return;
    }

    // Empleado básico: solo Ventas
    document.querySelectorAll(".sidebar button").forEach(btn => {
        const accion = btn.getAttribute("onclick") || "";
        if (!accion.includes("'ventas'") && !accion.includes("logout")) btn.style.display = "none";
    });
    mostrar("ventas");
}

// Aplica restricciones visuales dentro de las secciones para cajero
function aplicarRestriccionesCajero() {
    if (!esCajero()) return;

    // Inventario: ocultar pestaña Historial, forzar tab productos
    const tabHist = document.getElementById("tabHistorial");
    if (tabHist) tabHist.style.display = "none";

    // Productos: ocultar tabs de Categorías y Subcategorías
    const tabCat = document.getElementById("tabCat");
    const tabSub = document.getElementById("tabSub");
    if (tabCat) tabCat.style.display = "none";
    if (tabSub) tabSub.style.display = "none";

    // Redirigir a tab Productos si están en Categorías o Subcategorías
    const tabProd = document.getElementById("tabProd");
    if (tabProd && !tabProd.classList.contains("tab-active")) {
        tabProd?.click();
    }

    // Finanzas: ocultar botón Excel y selector de mes (siempre muestra el mes actual)
    const btnExcelFin = document.getElementById("btnExcelFinanzas");
    if (btnExcelFin) btnExcelFin.style.display = "none";

    const mesFin = document.getElementById("mesFinanzas");
    if (mesFin) {
        const hoy = new Date();
        mesFin.value = hoy.getFullYear() + '-' + String(hoy.getMonth() + 1).padStart(2, '0');
        mesFin.style.display = "none";
        cargarFinanzas();
    }

    // Finanzas: mostrar solo el formulario de gastos, ocultar resumen, ingresos extra y ventas por día
    // Selector del grid de 4 tarjetas de resumen (primer hijo del seccion finanzas después del header)
    const finSeccion = document.getElementById("finanzas");
    if (finSeccion) {
        // Ocultar las 4 tarjetas de resumen (primer grid después del header)
        const resumenCards = finSeccion.querySelector('[style*="grid-template-columns:repeat(4"]');
        if (resumenCards) resumenCards.style.display = "none";

        // Ocultar formulario de ingresos extra (segundo card del grid de formularios)
        const formGrid = finSeccion.querySelector('[style*="grid-template-columns:1fr 1fr"]');
        if (formGrid) {
            const cards = formGrid.querySelectorAll(':scope > div');
            if (cards[1]) cards[1].style.display = "none"; // oculta "Registrar ingreso extra"
        }

        // Ocultar columnas ingresos extra y ventas por día de las listas
        const listasGrid = finSeccion.querySelectorAll('[style*="grid-template-columns:1fr 1fr 1fr"]');
        listasGrid.forEach(grid => {
            const cols = grid.querySelectorAll(':scope > div');
            if (cols[1]) cols[1].style.display = "none"; // ingresos extra
            if (cols[2]) cols[2].style.display = "none"; // ventas por día
        });
    }
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
    authFetch(`${API}/dashboard/top-clientes`)
    .then(r => r.json())
    .then(data => {
        // Mini card en dashboard
        const mini = document.getElementById("miniTopClientes");
        if (mini) {
            if (!data || data.length === 0) {
                mini.innerHTML = `<p style="color:#aaa; font-size:13px;">Sin clientes aún.</p>`;
            } else {
                mini.innerHTML = data.slice(0, 5).map((c, i) => {
                    const medal = i === 0 ? '🥇' : i === 1 ? '🥈' : i === 2 ? '🥉' : `#${i+1}`;
                    return '<div style="display:flex;align-items:center;gap:8px;padding:8px 0;border-bottom:1px solid #1e1e1e;cursor:pointer;" onclick="verHistorialCliente(' + c.id + ',\'' + s(c.nombre) + '\')">'
                        + '<span style="font-size:15px;min-width:24px;text-align:center;">' + medal + '</span>'
                        + '<span style="flex:1;font-size:13px;font-weight:600;">' + s(c.nombre) + '</span>'
                        + '<div style="text-align:right;">'
                        +   '<div style="font-size:12px;color:#f59e0b;font-weight:700;">' + Number(c.puntos||0).toFixed(0) + ' pts</div>'
                        +   '<div style="font-size:10px;color:#555;">' + c.compras + ' compras · Q' + Number(c.total||0).toFixed(0) + '</div>'
                        + '</div></div>';
                }).join("");
            }
        }

        // Modal completo
        let html = "";
        if (!data || data.length === 0) {
            html = `<div class="card">Sin clientes aún</div>`;
        }
        data.forEach((c, i) => {
            html += `
            <div class="card">
                <h3>#${i + 1} ${s(c.nombre)}</h3>
                <p>Compras: ${c.compras}</p>
                <p>Total: Q${c.total || 0}</p>
                <p>Puntos: ${c.puntos || 0}</p>
                <button class="btn" style="margin-top:8px;" onclick="verHistorialCliente(${c.id}, '${s(c.nombre)}')">Ver historial</button>
            </div>`;
        });
        const cont = document.getElementById("topClientes");
        if (cont) cont.innerHTML = html;
    })
    .catch(error => {
        console.log(error);
        const mini = document.getElementById("miniTopClientes");
        if (mini) mini.innerHTML = `<p style="color:#555; font-size:13px;">Sin datos aún.</p>`;
    });
}


let _clienteHistorialId = null;

function verHistorialCliente(id, nombre) {
    _clienteHistorialId = id;
    document.getElementById("tituloHistorialCliente").textContent = `Historial de ${nombre}`;
    document.getElementById("contenidoHistorialCliente").innerHTML = "<p style='color:#aaa;'>Cargando...</p>";
    abrirModal("modalHistorialCliente");

    Promise.all([
        authFetch(`${API}/clientes/${id}/compras`).then(r => r.json()),
        authFetch(`${API}/clientes/${id}/puntos`).then(r => r.json()),
        authFetch(`${API}/clientes/${id}/historial-puntos`).then(r => r.json())
    ])
    .then(([compras, pts, histPuntos]) => {
        const ptsJuego   = Number(pts.puntos_juego   || 0).toFixed(2);
        const ptsConsumo = Number(pts.puntos_consumo || 0).toFixed(2);
        const ptsTotal   = Number(pts.puntos_total   || 0).toFixed(2);
        const totalGastado = compras.reduce((s, v) => s + Number(v.total || 0), 0);

        const motivoLabel = {
            'COMPRA': '🛒 Compra',
            'CONSOLA': '🎮 Consola',
            'RECALCULO_HISTORICO': '📊 Recálculo',
            'TORNEO': '🏆 Torneo',
            'CAMPEON': '🥇 Campeón',
            'FINALISTA': '🥈 Finalista',
            'SEMIFINALISTA': '🥉 Semifinalista'
        };

        // Tab puntos
        const tabPuntos =
            '<div style="display:grid;grid-template-columns:1fr 1fr 1fr;gap:10px;margin-bottom:20px;">'
            + '<div style="background:linear-gradient(135deg,#0d47a1,#1565c0);border-radius:12px;padding:16px;text-align:center;">'
            +   '<div style="font-size:10px;color:#90caf9;text-transform:uppercase;letter-spacing:.8px;margin-bottom:6px;">🎮 Pts Juego</div>'
            +   '<div style="font-size:26px;font-weight:800;color:#fff;">' + ptsJuego + '</div>'
            +   '<div style="font-size:10px;color:#5c8de0;margin-top:4px;">consola + torneos</div>'
            + '</div>'
            + '<div style="background:linear-gradient(135deg,#145214,#1b5e20);border-radius:12px;padding:16px;text-align:center;">'
            +   '<div style="font-size:10px;color:#81c784;text-transform:uppercase;letter-spacing:.8px;margin-bottom:6px;">🛒 Pts Consumo</div>'
            +   '<div style="font-size:26px;font-weight:800;color:#fff;">' + ptsConsumo + '</div>'
            +   '<div style="font-size:10px;color:#4c8f4c;margin-top:4px;">0.05 pts / Q1</div>'
            + '</div>'
            + '<div style="background:linear-gradient(135deg,#4a148c,#6a1b9a);border-radius:12px;padding:16px;text-align:center;">'
            +   '<div style="font-size:10px;color:#ce93d8;text-transform:uppercase;letter-spacing:.8px;margin-bottom:6px;">⭐ Total</div>'
            +   '<div style="font-size:26px;font-weight:800;color:#fff;">' + ptsTotal + '</div>'
            +   '<div style="font-size:10px;color:#8e4db0;margin-top:4px;">puntos acumulados</div>'
            + '</div>'
            + '</div>'
            + (histPuntos.length
                ? '<div style="border:1px solid #1e1e1e;border-radius:10px;overflow:hidden;">'
                  + '<div style="background:#111;padding:10px 14px;font-size:11px;color:#546e7a;text-transform:uppercase;letter-spacing:.5px;">Movimientos de puntos</div>'
                  + histPuntos.map(h =>
                      '<div style="display:flex;align-items:center;gap:10px;padding:9px 14px;border-top:1px solid #1a1a1a;">'
                      + '<span style="font-size:14px;">' + (h.tipo === 'JUEGO' ? '🎮' : '🛒') + '</span>'
                      + '<span style="flex:1;font-size:13px;color:#ccc;">' + (motivoLabel[h.motivo] || h.motivo) + '</span>'
                      + '<span style="font-size:11px;color:#444;">' + fmtFecha(h.fecha) + '</span>'
                      + '<span style="font-size:13px;font-weight:700;color:' + (h.tipo === 'JUEGO' ? '#42a5f5' : '#66bb6a') + ';min-width:70px;text-align:right;">+' + Number(h.puntos).toFixed(2) + ' pts</span>'
                      + '</div>'
                    ).join('')
                  + '</div>'
                : '<p style="color:#555;font-size:13px;text-align:center;padding:20px 0;">Sin movimientos de puntos.</p>'
            );

        // Tab compras
        const tabCompras = compras.length
            ? '<div style="display:flex;justify-content:space-between;align-items:center;margin-bottom:14px;">'
              + '<span style="font-size:12px;color:#aaa;">' + compras.length + ' venta(s)</span>'
              + '<span style="font-size:13px;font-weight:700;color:#66bb6a;">Total gastado: Q' + totalGastado.toFixed(2) + '</span>'
              + '</div>'
              + compras.map(v =>
                  '<div style="border:1px solid #1e1e1e;border-radius:10px;margin-bottom:10px;overflow:hidden;">'
                  + '<div style="background:#111;padding:10px 14px;display:flex;justify-content:space-between;align-items:center;">'
                  +   '<span style="font-size:13px;font-weight:600;">Venta #' + v.id_venta + '</span>'
                  +   '<span style="font-size:11px;color:#555;">' + fmtFecha(v.fecha) + '</span>'
                  + '</div>'
                  + '<div style="padding:10px 14px;">'
                  + v.items.map(i =>
                      '<div style="display:flex;justify-content:space-between;font-size:13px;padding:3px 0;color:#ccc;">'
                      + '<span>' + s(i.nombre) + ' <span style="color:#555;">×' + i.cantidad + '</span></span>'
                      + '<span style="color:#aaa;">Q' + (i.precio * i.cantidad).toFixed(2) + '</span>'
                      + '</div>'
                    ).join('')
                  + '<div style="border-top:1px solid #1e1e1e;margin-top:8px;padding-top:8px;display:flex;justify-content:space-between;align-items:center;">'
                  +   '<span style="font-size:11px;color:#555;">' + s(v.metodo_pago) + '</span>'
                  +   '<span style="font-weight:700;color:#fff;">Q' + Number(v.total).toFixed(2) + '</span>'
                  + '</div>'
                  + '</div></div>'
                ).join('')
            : '<p style="color:#555;font-size:13px;text-align:center;padding:30px 0;">Sin compras registradas.</p>';

        const tabAjuste =
            '<div style="background:#0a0a0a;border:1px solid #1e1e1e;border-radius:12px;padding:20px;">'
            + '<div style="font-size:12px;color:#555;text-transform:uppercase;letter-spacing:.5px;margin-bottom:16px;">Ajuste manual de puntos</div>'
            + '<div style="display:grid;grid-template-columns:1fr 1fr;gap:10px;margin-bottom:12px;">'
            +   '<div>'
            +     '<label style="font-size:11px;color:#666;display:block;margin-bottom:5px;">Tipo</label>'
            +     '<select id="ajusteTipo" style="width:100%;padding:9px 12px;background:#111;border:1px solid #2a2a2a;border-radius:8px;color:#e2e2e2;font-size:13px;">'
            +       '<option value="JUEGO">🎮 Juego</option>'
            +       '<option value="CONSUMO">🛒 Consumo</option>'
            +     '</select>'
            +   '</div>'
            +   '<div>'
            +     '<label style="font-size:11px;color:#666;display:block;margin-bottom:5px;">Puntos (negativo para restar)</label>'
            +     '<input id="ajustePuntos" type="number" step="0.5" placeholder="Ej: 5 o -3" style="width:100%;padding:9px 12px;background:#111;border:1px solid #2a2a2a;border-radius:8px;color:#e2e2e2;font-size:13px;box-sizing:border-box;">'
            +   '</div>'
            + '</div>'
            + '<div style="margin-bottom:14px;">'
            +   '<label style="font-size:11px;color:#666;display:block;margin-bottom:5px;">Motivo (opcional)</label>'
            +   '<input id="ajusteMotivo" type="text" placeholder="Ej: Corrección por error, premio especial..." style="width:100%;padding:9px 12px;background:#111;border:1px solid #2a2a2a;border-radius:8px;color:#e2e2e2;font-size:13px;box-sizing:border-box;">'
            + '</div>'
            + '<div style="display:flex;gap:8px;align-items:center;">'
            +   '<button onclick="confirmarAjustePuntos()" style="padding:10px 20px;border-radius:9px;border:none;background:linear-gradient(135deg,#1565c0,#0d47a1);color:#fff;font-size:13px;font-weight:700;cursor:pointer;">Aplicar ajuste</button>'
            +   '<span style="font-size:11px;color:#444;">El cambio aparecerá en el historial de movimientos</span>'
            + '</div>'
            + '</div>';

        const html =
            '<div style="display:flex;gap:0;margin-bottom:20px;border-bottom:2px solid #1e1e1e;">'
            + '<button id="tabBtnPuntos"  onclick="_switchTabHistorial(\'puntos\')"  style="background:transparent;border:none;padding:10px 16px;font-size:13px;font-weight:600;cursor:pointer;color:#42a5f5;border-bottom:2px solid #42a5f5;margin-bottom:-2px;">⭐ Puntos</button>'
            + '<button id="tabBtnCompras" onclick="_switchTabHistorial(\'compras\')" style="background:transparent;border:none;padding:10px 16px;font-size:13px;font-weight:600;cursor:pointer;color:#555;border-bottom:2px solid transparent;margin-bottom:-2px;">🛒 Compras (' + compras.length + ')</button>'
            + '<button id="tabBtnAjuste"  onclick="_switchTabHistorial(\'ajuste\')"  style="background:transparent;border:none;padding:10px 16px;font-size:13px;font-weight:600;cursor:pointer;color:#555;border-bottom:2px solid transparent;margin-bottom:-2px;">✏️ Editar</button>'
            + '</div>'
            + '<div id="tabPaneHistPuntos">' + tabPuntos + '</div>'
            + '<div id="tabPaneHistCompras" style="display:none;">' + tabCompras + '</div>'
            + '<div id="tabPaneHistAjuste"  style="display:none;">' + tabAjuste  + '</div>';

        document.getElementById("contenidoHistorialCliente").innerHTML = html;
    })
    .catch(() => {
        document.getElementById("contenidoHistorialCliente").innerHTML = "<p style='color:#ef4444;'>Error cargando historial.</p>";
    });
}

function _switchTabHistorial(tab) {
    const panes = { puntos: 'tabPaneHistPuntos', compras: 'tabPaneHistCompras', ajuste: 'tabPaneHistAjuste' };
    const btns  = { puntos: 'tabBtnPuntos',      compras: 'tabBtnCompras',      ajuste: 'tabBtnAjuste' };
    Object.keys(panes).forEach(k => {
        const pane = document.getElementById(panes[k]);
        const btn  = document.getElementById(btns[k]);
        const active = k === tab;
        if (pane) pane.style.display = active ? '' : 'none';
        if (btn)  { btn.style.color = active ? '#42a5f5' : '#555'; btn.style.borderBottomColor = active ? '#42a5f5' : 'transparent'; }
    });
}

async function confirmarAjustePuntos() {
    const pts = parseFloat(document.getElementById('ajustePuntos')?.value);
    if (!pts || pts === 0) { mostrarMensaje('⚠️ Ingresa un valor de puntos distinto de 0'); return; }
    if (!_clienteHistorialId) { mostrarMensaje('⚠️ No hay cliente seleccionado'); return; }

    const tipo = document.getElementById('ajusteTipo')?.value || 'JUEGO';
    const motivo = document.getElementById('ajusteMotivo')?.value?.trim() || '';

    const signo = pts > 0 ? '+' : '';
    const detalle = motivo ? ' — ' + motivo : '';
    const ok = await confirmarDialog(
        'Ajustar puntos',
        'Se ' + (pts > 0 ? 'agregarán' : 'quitarán') + ' ' + signo + pts + ' pts (' + tipo + ')' + detalle + '. ¿Confirmar?',
        pts < 0 ? 'danger' : 'normal'
    );
    if (!ok) return;

    authFetch(API + '/clientes/' + _clienteHistorialId + '/ajustar-puntos', {
        method: 'POST',
        body: JSON.stringify({ puntos: pts, tipo: tipo, descripcion: motivo })
    })
    .then(r => r.json())
    .then(data => {
        mostrarMensaje(data.mensaje || 'Puntos ajustados');
        const nombre = document.getElementById('tituloHistorialCliente')?.textContent?.replace('Historial de ', '') || '';
        verHistorialCliente(_clienteHistorialId, nombre);
    })
    .catch(() => mostrarMensaje('❌ Error al ajustar puntos'));
}

function _verGamerHistorial(id, nombre) {
    cerrarModal('modalGamers');
    verHistorialCliente(id, nombre);
}

// ==================================================================
// 🎮 TOP GAMERS (puntos de juego)
// ==================================================================
function cargarTopGamers(){
    authFetch(`${API}/dashboard/top-gamers`)
    .then(r => r.json())
    .then(data => {
        // Mini card en dashboard
        const mini = document.getElementById("miniTopGamers");
        if (mini) {
            if (!data || data.length === 0) {
                mini.innerHTML = '<p style="color:#555;font-size:13px;">Sin gamers aún.</p>';
            } else {
                mini.innerHTML = data.slice(0, 5).map((g, i) => {
                    const medal = i === 0 ? '🥇' : i === 1 ? '🥈' : i === 2 ? '🥉' : '#' + (i+1);
                    const apodoTxt = (g.apodo && typeof g.apodo === 'string') ? ' · ' + s(g.apodo) : '';
                    return '<div style="display:flex;align-items:center;gap:8px;padding:8px 0;border-bottom:1px solid #1e1e1e;cursor:pointer;" onclick="_verGamerHistorial(' + g.id + ',\'' + s(g.nombre) + '\')">'
                        + '<span style="font-size:15px;min-width:24px;text-align:center;">' + medal + '</span>'
                        + '<span style="flex:1;font-size:13px;font-weight:600;">' + s(g.nombre) + '<span style="color:#555;font-weight:400;">' + apodoTxt + '</span></span>'
                        + '<div style="text-align:right;">'
                        +   '<div style="font-size:12px;color:#42a5f5;font-weight:700;">' + Number(g.puntos||0).toFixed(0) + ' pts</div>'
                        +   '<div style="font-size:10px;color:#555;">🎮 ' + Number(g.pts_juego||0).toFixed(0) + ' · 🛒 ' + Number(g.pts_consumo||0).toFixed(0) + '</div>'
                        + '</div></div>';
                }).join("");
            }
        }

        // Modal completo
        const cont = document.getElementById("topGamers");
        if (!cont) return;

        if (!data || data.length === 0) {
            cont.innerHTML = '<p style="color:#555;font-size:13px;text-align:center;padding:30px 0;">Sin gamers registrados aún.</p>';
            return;
        }

        cont.innerHTML = data.map((g, i) => {
            const medal = i === 0 ? '🥇' : i === 1 ? '🥈' : i === 2 ? '🥉' : '#' + (i+1);
            const ptsJuego   = Number(g.pts_juego   || 0).toFixed(0);
            const ptsConsumo = Number(g.pts_consumo || 0).toFixed(0);
            const ptsTotal   = Number(g.puntos      || 0).toFixed(0);
            return '<div style="display:flex;align-items:center;gap:14px;padding:14px 0;border-bottom:1px solid #1a1a1a;cursor:pointer;" onclick="_verGamerHistorial(' + g.id + ',\'' + s(g.nombre) + '\')">'
                + '<div style="font-size:28px;min-width:40px;text-align:center;">' + medal + '</div>'
                + '<div style="flex:1;">'
                +   '<div style="font-size:14px;font-weight:700;">' + s(g.nombre) + (g.apodo && typeof g.apodo === 'string' && g.apodo.length ? ' <span style="color:#555;font-weight:400;font-size:12px;">@' + s(g.apodo) + '</span>' : '') + '</div>'
                +   '<div style="display:flex;gap:14px;margin-top:6px;">'
                +     '<div style="text-align:center;">'
                +       '<div style="font-size:10px;color:#555;text-transform:uppercase;letter-spacing:.5px;">🎮 Juego</div>'
                +       '<div style="font-size:15px;font-weight:700;color:#42a5f5;">' + ptsJuego + '</div>'
                +     '</div>'
                +     '<div style="text-align:center;">'
                +       '<div style="font-size:10px;color:#555;text-transform:uppercase;letter-spacing:.5px;">🛒 Consumo</div>'
                +       '<div style="font-size:15px;font-weight:700;color:#66bb6a;">' + ptsConsumo + '</div>'
                +     '</div>'
                +     '<div style="text-align:center;">'
                +       '<div style="font-size:10px;color:#555;text-transform:uppercase;letter-spacing:.5px;">⭐ Total</div>'
                +       '<div style="font-size:15px;font-weight:700;color:#f59e0b;">' + ptsTotal + '</div>'
                +     '</div>'
                +   '</div>'
                + '</div>'
                + '</div>';
        }).join('');
    })
    .catch(() => {
        const mini = document.getElementById("miniTopGamers");
        if (mini) mini.innerHTML = '<p style="color:#555;font-size:13px;">Sin datos aún.</p>';
    });
}


// ==================================================================
// ➕ AGREGAR STOCK RÁPIDO (dashboard)
// ==================================================================
function cargarSelectProductosRapido(){
    const sel = document.getElementById("productoRapido");
    if (!sel) return;
    authFetch(`${API}/productos`)
    .then(r => r.json())
    .then(data => {
        const contables = data.filter(p => p.controla_stock == 1);
        sel.innerHTML = `<option value="">— Selecciona producto —</option>` +
            contables.map(p => `<option value="${p.id}">${s(p.nombre)} (Stock: ${p.stock})</option>`).join("");
    });
}

function agregarStockRapido(){

    let id = document.getElementById("productoRapido").value;
    let cantidad = document.getElementById("cantidadRapida").value;
    let obs = document.getElementById("obsRapida").value || "Ingreso rápido desde dashboard";

    if(!id || !cantidad){
        mostrarMensaje("❌ Selecciona un producto e ingresa la cantidad");
        return;
    }

    authFetch(`${API}/productos/stock`, {
        method: "PUT",
        body: JSON.stringify({
            id_producto: parseInt(id),
            cantidad: parseInt(cantidad),
            observacion: obs
        })
    })
    .then(r => r.json())
    .then(() => {
        mostrarMensaje("✅ Stock agregado");
        document.getElementById("productoRapido").value = "";
        document.getElementById("cantidadRapida").value = "";
        document.getElementById("obsRapida").value = "";
        cargarSelectProductosRapido();
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
function tabInventario(tab) {
    const esProductos = tab === 'productos';
    document.getElementById("vistaProductosInv").style.display = esProductos ? "" : "none";
    document.getElementById("vistaHistorialInv").style.display = esProductos ? "none" : "";
    document.getElementById("tabProductos").style.background = esProductos ? "" : "#333";
    document.getElementById("tabProductos").style.color = esProductos ? "" : "var(--text)";
    document.getElementById("tabHistorial").style.background = esProductos ? "#333" : "";
    document.getElementById("tabHistorial").style.color = esProductos ? "var(--text)" : "";

    if (esProductos) {
        cargarAlertas();
        cargarProductosInventario();
    } else {
        cargarHistorial();
    }
}

function cargarProductosInventario() {
    authFetch(`${API}/productos`)
    .then(r => r.json())
    .then(data => {
        const contables = data.filter(p => (p.controla_stock == 1 || p.controla_stock === true) && !p.es_ingrediente);
        if (!contables.length) {
            document.getElementById("listaProductosInv").innerHTML = "<p style='color:#aaa;'>No hay productos contables.</p>";
            return;
        }
        const html = `<table style="width:100%; border-collapse:collapse; font-size:14px;">
            <thead>
                <tr style="border-bottom:1px solid #333; color:#aaa; text-align:left;">
                    <th style="padding:10px 12px;">Producto</th>
                    <th style="padding:10px 12px; text-align:center;">Stock</th>
                    <th style="padding:10px 12px; text-align:center;">Estado</th>
                </tr>
            </thead>
            <tbody>
                ${contables.map(p => {
                    const agotado = p.stock === 0;
                    const bajo = p.stock > 0 && p.stock <= 5;
                    const punto = (agotado || bajo)
                        ? `<span style="display:inline-block;width:9px;height:9px;border-radius:50%;background:#ef4444;margin-right:6px;"></span>`
                        : "";
                    const estadoTxt = agotado ? "Agotado" : bajo ? "Por terminar" : "OK";
                    const estadoColor = agotado ? "#ef4444" : bajo ? "#f59e0b" : "#22c55e";
                    return `<tr style="border-bottom:1px solid #1a1a2e;">
                        <td style="padding:10px 12px; font-weight:600;">${punto}${p.nombre}</td>
                        <td style="padding:10px 12px; text-align:center; font-size:18px; font-weight:bold; color:${estadoColor};">${p.stock}</td>
                        <td style="padding:10px 12px; text-align:center; color:${estadoColor}; font-size:12px; font-weight:bold;">${estadoTxt}</td>
                    </tr>`;
                }).join("")}
            </tbody>
        </table>`;
        document.getElementById("listaProductosInv").innerHTML = `<div class="card" style="padding:0; overflow:hidden;">${html}</div>`;
    });
}

function cargarAlertas(){

    authFetch(`${API}/inventario/alertas`)

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
function cargarHistorial() {
    const prod = encodeURIComponent(document.getElementById('filtroHistProd')?.value || '');
    const tipo = encodeURIComponent(document.getElementById('filtroHistTipo')?.value || '');
    const mes  = encodeURIComponent(document.getElementById('filtroHistMes')?.value || '');
    const qs   = [prod ? `producto=${prod}` : '', tipo ? `tipo=${tipo}` : '', mes ? `mes=${mes}` : ''].filter(Boolean).join('&');

    authFetch(`${API}/inventario/historial${qs ? '?' + qs : ''}`)
    .then(r => r.json())
    .then(data => {
        const cont = document.getElementById('historial');
        if (!cont) return;

        if (!data || !data.length) {
            cont.innerHTML = '<p style="color:#555; padding:20px 0;">Sin movimientos registrados.</p>';
            return;
        }

        // Agrupar por fecha (día)
        const grupos = {};
        data.forEach(m => {
            const dia = m.fecha ? m.fecha.toString().substring(0, 10) : 'Sin fecha';
            if (!grupos[dia]) grupos[dia] = [];
            grupos[dia].push(m);
        });

        let html = '';
        Object.entries(grupos).forEach(([dia, movs]) => {
            const entradas = movs.filter(m => m.tipo === 'ENTRADA').reduce((a, m) => a + Number(m.cantidad), 0);
            const salidas  = movs.filter(m => m.tipo === 'SALIDA').reduce((a, m) => a + Number(m.cantidad), 0);

            html += `
            <div style="margin-bottom:20px;">
                <div style="display:flex; align-items:center; gap:12px; margin-bottom:8px;">
                    <span style="font-size:12px; font-weight:700; color:#90caf9; text-transform:uppercase; letter-spacing:.5px;">${dia}</span>
                    <span style="font-size:11px; color:#555; margin-left:auto;">
                        ${entradas ? `<span style="color:#22c55e;">+${entradas} entradas</span>` : ''}
                        ${entradas && salidas ? ' · ' : ''}
                        ${salidas  ? `<span style="color:#ef4444;">-${salidas} salidas</span>` : ''}
                    </span>
                </div>
                <div class="cli-tabla-wrap">
                  <table class="cli-tabla">
                    <thead>
                      <tr>
                        <th>Producto</th>
                        <th style="text-align:center; width:90px;">Tipo</th>
                        <th style="text-align:center; width:70px;">Cant.</th>
                        <th>Observación</th>
                        <th style="text-align:right; width:110px;">Hora</th>
                      </tr>
                    </thead>
                    <tbody>
                      ${movs.map(m => {
                        const esEntrada = m.tipo === 'ENTRADA';
                        const hora = m.fecha ? m.fecha.toString().substring(11, 19) : '';
                        return `<tr>
                          <td style="font-weight:600;">${s(m.producto)}</td>
                          <td style="text-align:center;">
                            <span style="
                              display:inline-block; padding:2px 8px; border-radius:20px; font-size:11px; font-weight:700;
                              background:${esEntrada ? '#14532d' : '#7f1d1d'};
                              color:${esEntrada ? '#22c55e' : '#f87171'};">
                              ${esEntrada ? '↑ ENTRADA' : '↓ SALIDA'}
                            </span>
                          </td>
                          <td style="text-align:center; font-weight:700; color:${esEntrada ? '#22c55e' : '#f87171'};">
                            ${esEntrada ? '+' : '-'}${m.cantidad}
                          </td>
                          <td style="color:#aaa; font-size:13px;">${s(m.observacion || '—')}</td>
                          <td style="text-align:right; color:#555; font-size:12px;">${hora}</td>
                        </tr>`;
                      }).join('')}
                    </tbody>
                  </table>
                </div>
            </div>`;
        });

        cont.innerHTML = html;
    })
    .catch(() => mostrarMensaje('❌ Error cargando historial'));
}

function exportarInventarioExcel() {
    const mes = document.getElementById('filtroHistMes')?.value || '';
    if (!mes) { mostrarMensaje('⚠️ Selecciona un mes antes de exportar'); return; }

    const prod = encodeURIComponent(document.getElementById('filtroHistProd')?.value || '');
    const tipo = encodeURIComponent(document.getElementById('filtroHistTipo')?.value || '');
    const qs   = ['mes=' + encodeURIComponent(mes), prod ? 'producto=' + prod : '', tipo ? 'tipo=' + tipo : ''].filter(Boolean).join('&');

    mostrarMensaje('⏳ Generando Excel...');

    authFetch(API + '/inventario/historial?' + qs)
    .then(r => r.json())
    .then(data => {
        if (!data || !data.length) { mostrarMensaje('⚠️ Sin movimientos para ese mes'); return; }

        const [anio, mesNum] = mes.split('-');
        const nombreMes = ['Enero','Febrero','Marzo','Abril','Mayo','Junio','Julio','Agosto','Septiembre','Octubre','Noviembre','Diciembre'][parseInt(mesNum)-1];
        const totalEntradas = data.filter(m => m.tipo === 'ENTRADA').reduce((a, m) => a + Number(m.cantidad), 0);
        const totalSalidas  = data.filter(m => m.tipo === 'SALIDA').reduce((a, m)  => a + Number(m.cantidad), 0);

        const estilos =
            '<Style ss:ID="sTit"><Font ss:Bold="1" ss:Size="14" ss:Color="#FFFFFF"/><Interior ss:Color="#0F172A" ss:Pattern="Solid"/></Style>'
          + '<Style ss:ID="sSub"><Font ss:Bold="1" ss:Color="#94A3B8"/><Interior ss:Color="#1E293B" ss:Pattern="Solid"/></Style>'
          + '<Style ss:ID="sHead"><Font ss:Bold="1" ss:Color="#FFFFFF"/><Interior ss:Color="#1E3A5F" ss:Pattern="Solid"/><Borders><Border ss:Position="Bottom" ss:LineStyle="Continuous" ss:Weight="2" ss:Color="#3B82F6"/></Borders><Alignment ss:Horizontal="Center"/></Style>'
          + '<Style ss:ID="sEnt"><Interior ss:Color="#052E16" ss:Pattern="Solid"/><Font ss:Color="#86EFAC"/></Style>'
          + '<Style ss:ID="sEntB"><Interior ss:Color="#052E16" ss:Pattern="Solid"/><Font ss:Color="#4ADE80" ss:Bold="1"/><Alignment ss:Horizontal="Center"/></Style>'
          + '<Style ss:ID="sEntN"><Interior ss:Color="#052E16" ss:Pattern="Solid"/><Font ss:Color="#4ADE80" ss:Bold="1"/><Alignment ss:Horizontal="Center"/><NumberFormat ss:Format=\'#,##0\'/></Style>'
          + '<Style ss:ID="sSal"><Interior ss:Color="#2D0A0A" ss:Pattern="Solid"/><Font ss:Color="#FCA5A5"/></Style>'
          + '<Style ss:ID="sSalB"><Interior ss:Color="#2D0A0A" ss:Pattern="Solid"/><Font ss:Color="#F87171" ss:Bold="1"/><Alignment ss:Horizontal="Center"/></Style>'
          + '<Style ss:ID="sSalN"><Interior ss:Color="#2D0A0A" ss:Pattern="Solid"/><Font ss:Color="#F87171" ss:Bold="1"/><Alignment ss:Horizontal="Center"/><NumberFormat ss:Format=\'#,##0\'/></Style>'
          + '<Style ss:ID="sResEnt"><Interior ss:Color="#052E16" ss:Pattern="Solid"/><Font ss:Color="#4ADE80" ss:Bold="1" ss:Size="12"/><Alignment ss:Horizontal="Center"/></Style>'
          + '<Style ss:ID="sResSal"><Interior ss:Color="#2D0A0A" ss:Pattern="Solid"/><Font ss:Color="#F87171" ss:Bold="1" ss:Size="12"/><Alignment ss:Horizontal="Center"/></Style>'
          + '<Style ss:ID="sResLbl"><Interior ss:Color="#1E293B" ss:Pattern="Solid"/><Font ss:Color="#94A3B8" ss:Bold="1"/><Alignment ss:Horizontal="Center"/></Style>';

        const filasDatos = data.map(m => {
            const esEnt = m.tipo === 'ENTRADA';
            const fecha = m.fecha ? m.fecha.toString().replace('T', ' ').substring(0, 19) : '';
            const st    = esEnt ? 'sEnt' : 'sSal';
            const stB   = esEnt ? 'sEntB' : 'sSalB';
            const stN   = esEnt ? 'sEntN' : 'sSalN';
            return '<Row ss:Height="18">'
                + '<Cell ss:StyleID="' + st  + '"><Data ss:Type="String">'  + (m.producto    || '') + '</Data></Cell>'
                + '<Cell ss:StyleID="' + stB + '"><Data ss:Type="String">'  + (esEnt ? 'ENTRADA' : 'SALIDA') + '</Data></Cell>'
                + '<Cell ss:StyleID="' + stN + '"><Data ss:Type="Number">'  + (esEnt ? m.cantidad : -m.cantidad) + '</Data></Cell>'
                + '<Cell ss:StyleID="' + st  + '"><Data ss:Type="String">'  + (m.observacion || '') + '</Data></Cell>'
                + '<Cell ss:StyleID="' + st  + '"><Data ss:Type="String">'  + (m.usuario     || '') + '</Data></Cell>'
                + '<Cell ss:StyleID="' + st  + '"><Data ss:Type="String">'  + fecha + '</Data></Cell>'
                + '</Row>';
        }).join('');

        const xml = '<?xml version="1.0" encoding="UTF-8"?>'
            + '<?mso-application progid="Excel.Sheet"?>'
            + '<Workbook xmlns="urn:schemas-microsoft-com:office:spreadsheet" xmlns:ss="urn:schemas-microsoft-com:office:spreadsheet">'
            + '<Styles>' + estilos + '</Styles>'
            + '<Worksheet ss:Name="Inventario ' + nombreMes + ' ' + anio + '">'
            + '<Table ss:DefaultColumnWidth="130">'
            // Título
            + '<Row ss:Height="28"><Cell ss:MergeAcross="5" ss:StyleID="sTit"><Data ss:Type="String">Movimientos de Inventario — ' + nombreMes + ' ' + anio + '</Data></Cell></Row>'
            // Resumen entradas/salidas
            + '<Row ss:Height="22">'
            + '<Cell ss:StyleID="sResLbl"><Data ss:Type="String">TOTAL ENTRADAS</Data></Cell>'
            + '<Cell ss:StyleID="sResEnt"><Data ss:Type="Number">' + totalEntradas + '</Data></Cell>'
            + '<Cell ss:StyleID="sResLbl"><Data ss:Type="String">TOTAL SALIDAS</Data></Cell>'
            + '<Cell ss:StyleID="sResSal"><Data ss:Type="Number">' + totalSalidas + '</Data></Cell>'
            + '<Cell ss:StyleID="sResLbl"><Data ss:Type="String">DIFERENCIA NETA</Data></Cell>'
            + '<Cell ss:StyleID="' + (totalEntradas - totalSalidas >= 0 ? 'sResEnt' : 'sResSal') + '"><Data ss:Type="Number">' + (totalEntradas - totalSalidas) + '</Data></Cell>'
            + '</Row>'
            + '<Row></Row>'
            // Encabezados
            + '<Row ss:Height="22">'
            + '<Cell ss:StyleID="sHead"><Data ss:Type="String">Producto</Data></Cell>'
            + '<Cell ss:StyleID="sHead"><Data ss:Type="String">Tipo</Data></Cell>'
            + '<Cell ss:StyleID="sHead"><Data ss:Type="String">Cantidad</Data></Cell>'
            + '<Cell ss:StyleID="sHead"><Data ss:Type="String">Observación</Data></Cell>'
            + '<Cell ss:StyleID="sHead"><Data ss:Type="String">Usuario</Data></Cell>'
            + '<Cell ss:StyleID="sHead"><Data ss:Type="String">Fecha / Hora</Data></Cell>'
            + '</Row>'
            + filasDatos
            + '</Table></Worksheet></Workbook>';

        const blob = new Blob([xml], { type: 'application/vnd.ms-excel;charset=utf-8' });
        const url  = URL.createObjectURL(blob);
        const a    = document.createElement('a');
        a.href = url; a.download = 'inventario_' + mes + '.xls';
        document.body.appendChild(a); a.click();
        document.body.removeChild(a); URL.revokeObjectURL(url);
        mostrarMensaje('✅ Excel descargado: inventario_' + mes + '.xls');
    })
    .catch(() => mostrarMensaje('❌ Error generando el Excel'));
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
    const registraCosto = document.getElementById("registraCosto")?.checked || false;

    if(!controla_stock) stock = 0;

    if(!nombre){
        mostrarMensaje("❌ Ingresa el nombre del producto");
        return;
    }

    if(!precioVenta){
        mostrarMensaje("❌ Ingresa el precio de venta");
        return;
    }

    if(controla_stock && !stock){
        mostrarMensaje("❌ Ingresa el stock inicial");
        return;
    }

    if(registraCosto && !precio_compra){
        mostrarMensaje("❌ Ingresa el precio de compra");
        return;
    }

    const stockInicial = parseInt(stock || 0);

    authFetch(`${API}/productos`, {
        method: "POST",
        body: JSON.stringify({
            nombre: nombre,
            id_categoria: id_categoria,
            id_subcategoria: id_subcategoria,
            precio_compra: precio_compra,
            precio_venta: parseFloat(precioVenta),
            stock: stockInicial,
            controla_stock: controla_stock
        })
    })
    .then(r => r.json())
    .then(() => {
        // Si tiene precio de compra y stock inicial, registrar gasto en finanzas
        const gastoInicial = precio_compra > 0 && stockInicial > 0;
        const tareas = gastoInicial
            ? [authFetch(`${API}/gastos`, {
                method: "POST",
                body: JSON.stringify({
                    descripcion: `Compra inicial: ${nombre} x${stockInicial}`,
                    monto: precio_compra * stockInicial
                })
              }).then(r => r.json())]
            : [];

        return Promise.all(tareas);
    })
    .then(() => {
        mostrarMensaje("✅ Producto creado");
        document.getElementById("nombreProducto").value = "";
        document.getElementById("precioProducto").value = "";
        if (document.getElementById("precioCompraProducto")) document.getElementById("precioCompraProducto").value = "";
        if (document.getElementById("stockProducto")) document.getElementById("stockProducto").value = "";
        cargarProductosCatalogo();
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

    authFetch(`${API}/productos/categorias`)
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

    authFetch(`${API}/productos/subcategorias/${idCategoria}`)
    .then(r => r.json())
    .then(data => {
        sub.innerHTML = data
            .map(s => `<option value="${s.id_subcategoria}">${s.nombre}</option>`)
            .join("");
    })
    .catch(e => console.log(e));
}


// ==================================================================
// 📦 CATÁLOGO — Categorías, Subcategorías, Productos
// ==================================================================

function tabCatalogo(tab) {
    ['categorias','subcategorias','productos'].forEach(t => {
        const panel = document.getElementById(`tabPanel${t.charAt(0).toUpperCase()+t.slice(1)}`);
        const btn   = document.getElementById(`tab${t.charAt(0).toUpperCase()+t.slice(1).replace('ias','').replace('ucategorias','ub')}`);
        if (panel) panel.style.display = t === tab ? 'block' : 'none';
    });
    document.getElementById('tabCat')?.classList.toggle('tab-active', tab === 'categorias');
    document.getElementById('tabSub')?.classList.toggle('tab-active', tab === 'subcategorias');
    document.getElementById('tabProd')?.classList.toggle('tab-active', tab === 'productos');

    if (tab === 'categorias')   cargarCategoriasCatalogo();
    if (tab === 'subcategorias') { cargarCatsEnSubcatForm(); cargarSubcategoriasCatalogo(); }
    if (tab === 'productos')    { cargarCatsEnProductoForm(); cargarProductosCatalogo(); toggleFormProducto(); }
}

// ── CATEGORÍAS ──────────────────────────────────────────────────
function cargarCategoriasCatalogo() {
    authFetch(`${API}/productos/categorias`).then(r => r.json()).then(cats => {
        const el = document.getElementById('listaCategorias');
        if (!el) return;
        if (!cats.length) { el.innerHTML = '<p style="color:#555;">Sin categorías.</p>'; return; }
        const ocultarAcciones = esCajero();
        el.innerHTML = `
        <div class="cli-tabla-wrap">
          <table class="cli-tabla">
            <thead><tr><th>Nombre</th>${ocultarAcciones ? '' : '<th style="width:80px;text-align:center;">Acción</th>'}</tr></thead>
            <tbody>${cats.map(c => `
              <tr>
                <td><strong>${s(c.nombre)}</strong></td>
                ${ocultarAcciones ? '' : `<td style="text-align:center;"><button class="cli-btn" style="background:#7f1d1d;" onclick="eliminarCategoria(${c.id},'${s(c.nombre)}')">Eliminar</button></td>`}
              </tr>`).join('')}
            </tbody>
          </table>
        </div>`;
    });
}

function crearCategoria() {
    const nombre = document.getElementById('nuevaCategoriaNombre').value.trim();
    if (!nombre) { mostrarMensaje('❌ Escribe un nombre'); return; }
    authFetch(`${API}/productos/categorias`, { method:'POST', body: JSON.stringify({ nombre }) })
    .then(r => r.json()).then(d => {
        if (d.error) { mostrarMensaje('❌ ' + d.error); return; }
        mostrarMensaje('✅ Categoría creada');
        document.getElementById('nuevaCategoriaNombre').value = '';
        cargarCategoriasCatalogo();
    });
}

async function eliminarCategoria(id, nombre) {
    if (!await confirmarDialog("Eliminar categoría", `¿Eliminar la categoría "${nombre}"? Solo se puede si no tiene productos.`, "danger")) return;
    authFetch(`${API}/productos/categorias/${id}`, { method:'DELETE' })
    .then(r => r.json()).then(d => {
        mostrarMensaje(d.error ? '❌ ' + d.error : '✅ ' + d.mensaje);
        cargarCategoriasCatalogo();
    });
}

// ── SUBCATEGORÍAS ────────────────────────────────────────────────
function cargarCatsEnSubcatForm() {
    authFetch(`${API}/productos/categorias`).then(r => r.json()).then(cats => {
        const selForm   = document.getElementById('nuevaSubcatCategoria');
        const selFiltro = document.getElementById('filtroSubcatCategoria');
        const opts = cats.map(c => `<option value="${c.id}">${s(c.nombre)}</option>`).join('');
        if (selForm)   selForm.innerHTML   = opts;
        if (selFiltro) selFiltro.innerHTML = '<option value="">Todas</option>' + opts;
    });
}

function cargarSubcategoriasCatalogo() {
    const filtro = document.getElementById('filtroSubcatCategoria')?.value || '';
    const url = filtro ? `${API}/productos/subcategorias/${filtro}` : `${API}/productos/subcategorias/todas`;

    // Si no hay filtro, traemos todas con un fallback manual
    const fetch$ = filtro
        ? authFetch(url).then(r => r.json())
        : authFetch(`${API}/productos/categorias`).then(r => r.json()).then(cats =>
            Promise.all(cats.map(c =>
                authFetch(`${API}/productos/subcategorias/${c.id}`).then(r => r.json())
                    .then(subs => subs.map(sb => ({ ...sb, cat: c.nombre })))
            )).then(grupos => grupos.flat())
          );

    fetch$.then(subs => {
        const el = document.getElementById('listaSubcategorias');
        if (!el) return;
        if (!subs.length) { el.innerHTML = '<p style="color:#555;">Sin subcategorías.</p>'; return; }
        const ocultarAccSub = esCajero();
        el.innerHTML = `
        <div class="cli-tabla-wrap">
          <table class="cli-tabla">
            <thead><tr><th>Nombre</th>${filtro ? '' : '<th>Categoría</th>'}${ocultarAccSub ? '' : '<th style="width:80px;text-align:center;">Acción</th>'}</tr></thead>
            <tbody>${subs.map(sb => `
              <tr>
                <td><strong>${s(sb.nombre)}</strong></td>
                ${filtro ? '' : `<td style="color:#aaa;font-size:12px;">${s(sb.cat||'')}</td>`}
                ${ocultarAccSub ? '' : `<td style="text-align:center;"><button class="cli-btn" style="background:#7f1d1d;" onclick="eliminarSubcategoria(${sb.id_subcategoria},'${s(sb.nombre)}')">Eliminar</button></td>`}
              </tr>`).join('')}
            </tbody>
          </table>
        </div>`;
    });
}

function crearSubcategoria() {
    const id_categoria = parseInt(document.getElementById('nuevaSubcatCategoria').value);
    const nombre = document.getElementById('nuevaSubcatNombre').value.trim();
    if (!nombre) { mostrarMensaje('❌ Escribe un nombre'); return; }
    authFetch(`${API}/productos/subcategorias`, { method:'POST', body: JSON.stringify({ nombre, id_categoria }) })
    .then(r => r.json()).then(d => {
        if (d.error) { mostrarMensaje('❌ ' + d.error); return; }
        mostrarMensaje('✅ Subcategoría creada');
        document.getElementById('nuevaSubcatNombre').value = '';
        cargarSubcategoriasCatalogo();
    });
}

async function eliminarSubcategoria(id, nombre) {
    if (!await confirmarDialog("Eliminar subcategoría", `¿Eliminar la subcategoría "${nombre}"?`, "danger")) return;
    authFetch(`${API}/productos/subcategorias/${id}`, { method:'DELETE' })
    .then(r => r.json()).then(d => {
        mostrarMensaje(d.error ? '❌ ' + d.error : '✅ ' + d.mensaje);
        cargarSubcategoriasCatalogo();
    });
}

// ── PRODUCTOS (catálogo) ─────────────────────────────────────────
function cargarCatsEnProductoForm() {
    authFetch(`${API}/productos/categorias`).then(r => r.json()).then(cats => {
        ['categoriaProducto','filtroProdCategoria'].forEach(elId => {
            const el = document.getElementById(elId);
            if (!el) return;
            const conTodos = elId.startsWith('filtro');
            el.innerHTML = (conTodos ? '<option value="">Todas</option>' : '') +
                cats.map(c => `<option value="${c.id}">${s(c.nombre)}</option>`).join('');
        });
        if (cats.length) cargarSubcatsProducto();
        cargarSubcatsFiltro();
    });
}

function cargarSubcatsProducto() {
    const catId = document.getElementById('categoriaProducto')?.value;
    if (!catId) return;
    authFetch(`${API}/productos/subcategorias/${catId}`).then(r => r.json()).then(subs => {
        const el = document.getElementById('subcategoriaProducto');
        if (el) el.innerHTML = subs.map(s2 => `<option value="${s2.id_subcategoria}">${s(s2.nombre)}</option>`).join('');
    });
}

function cargarSubcatsFiltro() {
    const catId = document.getElementById('filtroProdCategoria')?.value;
    const el = document.getElementById('filtroProdSubcat');
    if (!el) return;
    if (!catId) { el.innerHTML = '<option value="">Todas</option>'; return; }
    authFetch(`${API}/productos/subcategorias/${catId}`).then(r => r.json()).then(subs => {
        el.innerHTML = '<option value="">Todas</option>' +
            subs.map(s2 => `<option value="${s2.id_subcategoria}">${s(s2.nombre)}</option>`).join('');
    });
}

function cargarProductosCatalogo() {
    const catId    = document.getElementById('filtroProdCategoria')?.value || '';
    const subcatId = document.getElementById('filtroProdSubcat')?.value || '';
    const filtNom  = (document.getElementById('filtroProdNombre')?.value || '').toLowerCase();

    const url = catId ? `${API}/productos/categoria/${catId}` : `${API}/productos`;

    authFetch(url).then(r => r.json()).then(prods => {
        let lista = prods;
        if (subcatId) lista = lista.filter(p => String(p.id_subcategoria) === String(subcatId));
        if (filtNom)  lista = lista.filter(p => (p.nombre||'').toLowerCase().includes(filtNom));

        // Solo mostrar productos con precio de venta (no ingredientes legacy)
        const productos = lista.filter(p => !p.es_ingrediente);

        const el = document.getElementById('productosLista');
        if (!el) return;
        if (!productos.length) { el.innerHTML = '<p style="color:#555;">Sin productos.</p>'; return; }

        const tabla = `
        <div class="cli-tabla-wrap">
          <table class="cli-tabla">
            <thead><tr>
              <th>Nombre</th>
              <th style="text-align:right;">P. Compra</th>
              <th style="text-align:right;">P. Venta</th>
              <th style="text-align:center;">Stock</th>
              <th style="text-align:center;">Acciones</th>
            </tr></thead>
            <tbody>${productos.map(p => {
                const tieneCompra = parseFloat(p.precio_compra || 0) > 0;
                return `
                  <tr>
                    <td>${s(p.nombre)}</td>
                    <td style="text-align:right; color:#aaa;">${tieneCompra ? 'Q' + parseFloat(p.precio_compra).toFixed(2) : '<span style="color:#444;">—</span>'}</td>
                    <td style="text-align:right; color:#66bb6a; font-weight:600;">Q${parseFloat(p.precio_venta||0).toFixed(2)}</td>
                    <td style="text-align:center; color:${p.stock==0?'#ef4444':p.stock<=5?'#f59e0b':'#aaa'};">${p.controla_stock ? p.stock : '∞'}</td>
                    <td style="text-align:center; display:flex; gap:4px; justify-content:center;">
                      <button class="cli-btn" onclick="abrirEntradaStock(${p.id},'${s(p.nombre)}',${parseFloat(p.precio_compra||0)},false)">+ Stock</button>
                      <button class="cli-btn" style="background:#7f1d1d;" onclick="eliminarProducto(${p.id})">Eliminar</button>
                    </td>
                  </tr>`;
            }).join('')}
            </tbody>
          </table>
        </div>`;

        el.innerHTML = tabla;
    });
}

// Modal entrada de stock / compra de ingrediente
function abrirEntradaStock(id, nombre, precioCompraActual) {
    document.getElementById('entradaStockNombre').textContent = nombre;
    document.getElementById('entradaStockId').value = id;
    document.getElementById('entradaStockCantidad').value = '';
    document.getElementById('entradaStockPrecio').value = precioCompraActual || '';
    document.getElementById('entradaStockObs').value = '';
    const nota = document.getElementById('entradaStockNota');
    if (nota) nota.style.display = 'none';
    abrirModal('modalEntradaStock');
}

function confirmarEntradaStock() {
    const id       = parseInt(document.getElementById('entradaStockId').value);
    const cantidad = parseInt(document.getElementById('entradaStockCantidad').value);
    const precio   = parseFloat(document.getElementById('entradaStockPrecio').value || 0);
    const obs      = document.getElementById('entradaStockObs').value || '';

    if (!cantidad || cantidad <= 0) { mostrarMensaje('❌ Ingresa una cantidad válida'); return; }

    authFetch(`${API}/productos/${id}/entrada`, {
        method: 'POST',
        body: JSON.stringify({ cantidad, precio_compra: precio, observacion: obs })
    }).then(r => r.json()).then(d => {
        if (d.error) { mostrarMensaje('❌ ' + d.error); return; }
        const msg = d.gasto_registrado
            ? `✅ Stock actualizado — gasto Q${(precio*cantidad).toFixed(2)} registrado en finanzas`
            : '✅ Stock actualizado';
        mostrarMensaje(msg);
        cerrarModal('modalEntradaStock');
        cargarProductosCatalogo();
    });
}

// ==================================================================
// 🏆 TORNEOS
// ==================================================================
// ===========================
// TORNEOS
// ===========================
function tabTorneo(tab) {
    ["microwin","flash"].forEach(t => {
        const sec = document.getElementById(`secTorneo${t.charAt(0).toUpperCase()+t.slice(1)}`);
        const btn = document.getElementById(`tabTorneo-${t}`);
        if (sec) sec.style.display = t === tab ? "block" : "none";
        if (btn) btn.classList.toggle("tab-active", t === tab);
    });
    cargarTorneosPorTipo(tab.toUpperCase());
}

function crearTorneo(tipo) {
    const pre = tipo === "MICROWIN" ? "mw" : "fl";
    const nombre      = document.getElementById(`${pre}Nombre`)?.value?.trim();
    const juego       = document.getElementById(`${pre}Juego`)?.value?.trim();
    const premio      = parseFloat(document.getElementById(`${pre}Premio`)?.value || 0);
    const inscripcion = parseFloat(document.getElementById(`${pre}Inscripcion`)?.value || 0);
    const cupos       = parseInt(document.getElementById(`${pre}Cupos`)?.value || 0);

    if (!nombre || !juego) { mostrarMensaje("❌ Ingresa nombre y juego"); return; }

    authFetch(`${API}/torneos`, {
        method: "POST",
        body: JSON.stringify({ nombre, tipo, juego, premio, inscripcion, cupos, participantes: [] })
    })
    .then(r => r.json())
    .then(data => {
        if (data.error) { mostrarMensaje("❌ " + data.error); return; }
        mostrarMensaje("✅ Torneo creado");
        [`${pre}Nombre`,`${pre}Juego`,`${pre}Premio`,`${pre}Inscripcion`,`${pre}Cupos`]
            .forEach(id => { const el = document.getElementById(id); if(el) el.value = ""; });
        cargarTorneosPorTipo(tipo);
    })
    .catch(() => mostrarMensaje("❌ Error creando torneo"));
}

function cargarTorneos() {
    cargarTorneosPorTipo("MICROWIN");
}

function cargarTorneosPorTipo(tipo) {
    const listaId = tipo === "MICROWIN" ? "listaMicrowin" : "listaFlash";
    authFetch(`${API}/torneos?tipo=${tipo}`)
    .then(r => r.json())
    .then(data => {
        const cont = document.getElementById(listaId);
        if (!cont) return;
        if (!data.length) { cont.innerHTML = `<p style="color:#555;font-size:13px;">Sin torneos ${tipo === "MICROWIN" ? "Microwin" : "Flash"} registrados.</p>`; return; }
        const colorEstado = e => e === "FINALIZADO" ? "#555" : e === "EN_CURSO" ? "#f59e0b" : "#4ade80";
        cont.innerHTML = data.map(t => `
            <div class="card" style="min-width:220px;">
                <div style="display:flex;justify-content:space-between;align-items:flex-start;">
                    <h3 style="margin:0 0 6px;">${s(t.nombre)}</h3>
                    <span style="font-size:11px;padding:2px 8px;border-radius:10px;background:#1a1a1a;color:${colorEstado(t.estado)};">${t.estado||"ABIERTO"}</span>
                </div>
                <p style="color:#aaa;font-size:13px;margin:2px 0;">🎮 ${s(t.juego)}</p>
                <p style="font-size:13px;margin:2px 0;">Premio: <strong>Q${parseFloat(t.premio).toFixed(2)}</strong></p>
                <p style="color:#aaa;font-size:12px;margin:2px 0;">Inscripción: Q${parseFloat(t.inscripcion).toFixed(2)} · Cupos: ${t.cupos}</p>
                <hr style="border-color:#222;margin:10px 0;">
                <button class="btn" onclick="verDetalleTorneo(${t.id_torneo})" style="font-size:12px;padding:4px 12px;">
                    ${t.estado === "FINALIZADO" ? "📊 Ver resultados" : "👥 Gestionar"}
                </button>
            </div>`).join("");
    })
    .catch(() => mostrarMensaje("❌ Error cargando torneos"));
}

function verDetalleTorneo(id) {
    document.getElementById("modalTorneo").style.display = "block";
    const cont = document.getElementById("modalTorneoContenido");
    cont.innerHTML = `<p style="color:#aaa;">Cargando...</p>`;

    Promise.all([
        authFetch(`${API}/torneos`).then(r => r.json()),
        authFetch(`${API}/torneos/${id}/participantes`).then(r => r.json())
    ]).then(([torneos, participantes]) => {
        const t = torneos.find(x => x.id_torneo == id);
        if (!t) { cont.innerHTML = `<p>Torneo no encontrado</p>`; return; }

        const finalizado = t.estado === "FINALIZADO";
        const posNombre = pos => pos === 1 ? "🥇 Campeón" : pos === 2 ? "🥈 Finalista" : pos === 3 ? "🥉 Semifinalista" : "🎮 Participante";

        const listaParticipantes = participantes.length
            ? participantes.map(p => `
                <tr style="border-bottom:1px solid #1a1a1a;">
                    <td style="padding:7px 6px;">${s(p.nombre)}${p.apodo ? ` <span style="color:#555;font-size:11px;">(${s(p.apodo)})</span>` : ""}</td>
                    <td style="padding:7px 6px;color:#aaa;">${p.posicion > 0 ? posNombre(p.posicion) : "—"}</td>
                    ${!finalizado ? `<td style="padding:7px 6px;">
                        <select id="pos_${p.id_cliente}" style="background:#1a1a1a;color:#ccc;border:1px solid #333;border-radius:4px;padding:3px 6px;font-size:12px;">
                            <option value="0">Sin posición</option>
                            <option value="1" ${p.posicion==1?"selected":""}>🥇 Campeón (10 pts)</option>
                            <option value="2" ${p.posicion==2?"selected":""}>🥈 Finalista (5 pts)</option>
                            <option value="3" ${p.posicion==3?"selected":""}>🥉 Semifinalista (3 pts)</option>
                        </select>
                    </td>` : ""}
                </tr>`).join("")
            : `<tr><td colspan="3" style="padding:12px;color:#555;text-align:center;">Sin participantes inscritos aún.</td></tr>`;

        const fechaTorneo = t.fecha ? fmtFecha(t.fecha) : '—';
        cont.innerHTML = `
            <h3 style="margin:0 0 4px;">${s(t.nombre)} <span style="font-size:13px;color:#aaa;">(${t.tipo})</span></h3>
            <p style="color:#aaa;font-size:13px;margin:0 0 4px;">🎮 ${s(t.juego)} · Premio: Q${parseFloat(t.premio).toFixed(2)} · Cupos: ${t.cupos}</p>
            <p style="color:#555;font-size:12px;margin-bottom:16px;">📅 ${fechaTorneo}</p>

            ${!finalizado ? `
            <div style="display:flex;gap:8px;margin-bottom:8px;">
                <input id="buscarClienteTorneo" placeholder="Buscar jugador para inscribir..." style="flex:1;font-size:13px;" oninput="buscarParaTorneo(${id})">
                <button class="btn" onclick="toggleNuevoJugadorTorneo(${id})" style="font-size:12px;padding:4px 10px;background:#333;white-space:nowrap;">➕ Nuevo jugador</button>
            </div>
            <div id="resultadosBusquedaTorneo" style="margin-bottom:8px;"></div>
            <div id="formNuevoJugadorTorneo" style="display:none;background:#111;border:1px solid #333;border-radius:8px;padding:12px;margin-bottom:16px;">
                <p style="font-size:12px;color:#aaa;margin-bottom:8px;">Crear nuevo jugador e inscribir directamente</p>
                <input id="njNombre" placeholder="Nombre *" style="margin-bottom:6px;">
                <input id="njApodo"  placeholder="Apodo (opcional)" style="margin-bottom:6px;">
                <input id="njTel"    placeholder="Teléfono (opcional)" style="margin-bottom:10px;">
                <button class="btn" onclick="crearYInscribirTorneo(${id})" style="font-size:12px;width:100%;">Crear e inscribir</button>
            </div>` : ""}

            <h4 style="margin:0 0 8px;">👥 Participantes (${participantes.length})</h4>
            <table style="width:100%;border-collapse:collapse;font-size:13px;margin-bottom:16px;">
                <thead><tr style="color:#aaa;border-bottom:1px solid #333;">
                    <th style="padding:7px 6px;text-align:left;">Jugador</th>
                    <th style="padding:7px 6px;text-align:left;">Posición</th>
                    ${!finalizado ? `<th style="padding:7px 6px;">Asignar</th>` : ""}
                </tr></thead>
                <tbody>${listaParticipantes}</tbody>
            </table>

            ${!finalizado && participantes.length > 0 ? `
            <button class="btn" onclick="finalizarTorneo(${id},[${participantes.map(p=>`${p.id_cliente}`).join(",")}])" style="background:#f59e0b;width:100%;">
                🏆 Finalizar torneo y asignar puntos
            </button>` : ""}
            ${finalizado ? `<p style="color:#555;text-align:center;font-size:13px;">✅ Torneo finalizado</p>` : ""}
        `;
    }).catch(() => { cont.innerHTML = `<p style="color:#ef4444;">Error cargando detalle.</p>`; });
}

function toggleNuevoJugadorTorneo(idTorneo) {
    const form = document.getElementById("formNuevoJugadorTorneo");
    if (!form) return;
    form.style.display = form.style.display === "none" ? "block" : "none";
    if (form.style.display === "block") document.getElementById("njNombre")?.focus();
}

function crearYInscribirTorneo(idTorneo) {
    const nombre = document.getElementById("njNombre")?.value?.trim();
    const apodo  = document.getElementById("njApodo")?.value?.trim();
    const tel    = document.getElementById("njTel")?.value?.trim();

    if (!nombre) { mostrarMensaje("❌ El nombre es obligatorio"); return; }

    authFetch(`${API}/clientes`, {
        method: "POST",
        body: JSON.stringify({ nombre, apodo: apodo || "", telefono: tel || "" })
    })
    .then(r => r.json())
    .then(data => {
        if (data.error) { mostrarMensaje("❌ " + data.error); return; }
        const idCliente = data.id || data.id_cliente;
        if (!idCliente) { mostrarMensaje("❌ No se pudo obtener el ID del jugador"); return; }
        return authFetch(`${API}/torneos/${idTorneo}/inscribir?id_cliente=${idCliente}`, { method: "POST" })
            .then(r => r.json())
            .then(d => {
                if (d.error) { mostrarMensaje("❌ " + d.error); return; }
                mostrarMensaje(`✅ ${nombre} creado e inscrito (+2 pts)`);
                verDetalleTorneo(idTorneo);
            });
    })
    .catch(() => mostrarMensaje("❌ Error creando jugador"));
}

function buscarParaTorneo(idTorneo) {
    const q = document.getElementById("buscarClienteTorneo")?.value?.trim();
    const res = document.getElementById("resultadosBusquedaTorneo");
    if (!res) return;
    if (!q || q.length < 2) { res.innerHTML = ""; return; }
    authFetch(`${API}/clientes/buscar?texto=${encodeURIComponent(q)}`)
    .then(r => r.json())
    .then(data => {
        if (!data.length) { res.innerHTML = `<p style="color:#555;font-size:12px;">Sin resultados</p>`; return; }
        res.innerHTML = data.slice(0,5).map(c =>
            `<div onclick="inscribirEnTorneo(${idTorneo},${c.id},'${c.nombre.replace(/'/g,"\\'")}')"
                  style="padding:6px 10px;cursor:pointer;border-radius:4px;font-size:13px;background:#1a1a1a;margin-top:3px;"
                  onmouseover="this.style.background='#2a2a2a'" onmouseout="this.style.background='#1a1a1a'">
                👤 <strong>${s(c.nombre)}</strong>${c.apodo ? ` · ${s(c.apodo)}` : ""}
            </div>`
        ).join("");
    }).catch(() => {});
}

function inscribirEnTorneo(idTorneo, idCliente, nombre) {
    authFetch(`${API}/torneos/${idTorneo}/inscribir?id_cliente=${idCliente}`, { method: "POST" })
    .then(r => r.json())
    .then(data => {
        if (data.error) { mostrarMensaje("❌ " + data.error); return; }
        mostrarMensaje(`✅ ${nombre} inscrito (+2 pts)`);
        verDetalleTorneo(idTorneo);
    })
    .catch(() => mostrarMensaje("❌ Error inscribiendo jugador"));
}

async function finalizarTorneo(idTorneo, idsClientes) {
    const posiciones = idsClientes.map(id => ({
        id_cliente: id,
        posicion: parseInt(document.getElementById(`pos_${id}`)?.value || 0)
    }));
    if (!await confirmarDialog("Finalizar torneo", "¿Finalizar el torneo y asignar puntos? Esta acción no se puede deshacer.")) return;
    authFetch(`${API}/torneos/${idTorneo}/finalizar`, {
        method: "POST",
        body: JSON.stringify(posiciones)
    })
    .then(r => r.json())
    .then(data => {
        if (data.error) { mostrarMensaje("❌ " + data.error); return; }
        mostrarMensaje("🏆 Torneo finalizado y puntos asignados");
        cerrarModalTorneo();
        cargarTorneos();
    })
    .catch(() => mostrarMensaje("❌ Error finalizando torneo"));
}

function cerrarModalTorneo() {
    document.getElementById("modalTorneo").style.display = "none";
}


// ==================================================================
// 🔒 CIERRE DIARIO
// ==================================================================
function cargarCierre(){

    // Resumen (ventas, gastos, balance)
    authFetch(`${API}/cierres/resumen`)

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
    authFetch(`${API}/cierres`)

    .then(r => r.json())

    .then(data => {

        let html = "";

        if(!data || data.length === 0){
            html = `<div class="card">Sin cierres registrados</div>`;
        }

        data.forEach(c => {

            html += `
            <div class="card">
                <h4>${fmtFecha(c.fecha)}</h4>
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

    authFetch(`${API}/cierres`, {

        method: "POST",

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
    const mes = document.getElementById("mesReporte")?.value;
    if (!mes) {
        mostrarMensaje("⚠️ Selecciona un mes primero");
        return;
    }
    window.open(`${API}/exportar/ventas?mes=${mes}&token=${getToken()}`, "_blank");
}

function exportarExcelTodo(){
    window.open(`${API}/exportar/ventas?token=${getToken()}`, "_blank");
}


// ==================================================================
// 🗑️ ELIMINAR PRODUCTO
// (requiere el endpoint DELETE /api/productos/{id} en el backend)
// ==================================================================
async function eliminarProducto(id){

    if (!await confirmarDialog("Eliminar producto", "¿Seguro que deseas eliminar este producto?", "danger")) return;

    authFetch(`${API}/productos/${id}`, {
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
        cargarProductosCatalogo();
    })

    .catch(error => {
        console.log(error);
        mostrarMensaje("❌ " + error.message);
    });
}
// ==================================================================
// 💸 FINANZAS
// ==================================================================

function togglePrecioVenta() { toggleFormProducto(); }

function toggleFormProducto() {
    const registraCosto = document.getElementById("registraCosto")?.checked;
    const ctrlStock     = document.getElementById("controlaStockProducto")?.checked;
    const campoPrecioC  = document.getElementById("precioCompraProducto");
    const campoStock    = document.getElementById("stockProducto");

    // Precio de compra: solo visible si marcó "Registra precio de compra"
    if (campoPrecioC) {
        campoPrecioC.style.display = registraCosto ? "" : "none";
        if (!registraCosto) campoPrecioC.value = "";
    }

    // Stock: visible solo si controla stock
    if (campoStock) {
        campoStock.style.display = ctrlStock ? "" : "none";
        if (!ctrlStock) campoStock.value = "";
    }
}

function cargarFinanzas() {
    const mes = document.getElementById("mesFinanzas")?.value || "";
    const q = mes ? `?mes=${mes}` : "";

    // Resumen
    authFetch(`${API}/gastos/resumen${q}`)
    .then(r => r.json())
    .then(data => {
        const ganancia = parseFloat(data.ganancia || 0);
        document.getElementById("finIngresos").textContent      = `Q${parseFloat(data.ingresos||0).toFixed(2)}`;
        document.getElementById("finIngresosExtra").textContent = `Q${parseFloat(data.ingresos_extra||0).toFixed(2)}`;
        document.getElementById("finGastos").textContent        = `Q${parseFloat(data.gastos||0).toFixed(2)}`;
        const el = document.getElementById("finGanancia");
        el.textContent = `Q${ganancia.toFixed(2)}`;
        el.style.color = ganancia >= 0 ? "#4ade80" : "#ef4444";
    })
    .catch(() => {});

    // Lista de ingresos extra
    authFetch(`${API}/gastos/ingresos-extra${q}`)
    .then(r => r.json())
    .then(data => {
        const lista = document.getElementById("listaIngresosExtra");
        if (!lista) return;
        if (!data.length) {
            lista.innerHTML = '<p style="color:#555;font-size:13px;padding:16px 18px;">Sin ingresos extra este mes.</p>';
            return;
        }
        lista.innerHTML = data.map(g =>
            '<div style="display:flex;align-items:center;gap:10px;padding:11px 18px;border-bottom:1px solid #131313;">'
            + '<div style="flex:1;min-width:0;">'
            +   '<div style="font-size:13px;font-weight:600;color:#e2e2e2;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;">' + s(g.descripcion) + '</div>'
            +   '<div style="font-size:11px;color:#444;margin-top:2px;">' + fmtFecha(g.fecha) + '</div>'
            + '</div>'
            + '<div style="font-size:14px;font-weight:700;color:#60a5fa;white-space:nowrap;">+Q' + parseFloat(g.monto).toFixed(2) + '</div>'
            + '<button onclick="eliminarIngresoExtra(' + g.id + ')" style="background:transparent;border:none;color:#333;cursor:pointer;font-size:15px;padding:2px 4px;line-height:1;" title="Eliminar">✕</button>'
            + '</div>'
        ).join('');
    })
    .catch(() => {});

    // Lista de gastos
    authFetch(`${API}/gastos${q}`)
    .then(r => r.json())
    .then(data => {
        const lista = document.getElementById("listaGastos");
        if (!lista) return;
        if (!data.length) {
            lista.innerHTML = '<p style="color:#555;font-size:13px;padding:16px 18px;">Sin gastos este mes.</p>';
            return;
        }
        lista.innerHTML = data.map(g =>
            '<div style="display:flex;align-items:center;gap:10px;padding:11px 18px;border-bottom:1px solid #131313;">'
            + '<div style="flex:1;min-width:0;">'
            +   '<div style="font-size:13px;font-weight:600;color:#e2e2e2;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;">' + s(g.descripcion) + '</div>'
            +   '<div style="font-size:11px;color:#444;margin-top:2px;">' + fmtFecha(g.fecha) + '</div>'
            + '</div>'
            + '<div style="font-size:14px;font-weight:700;color:#f87171;white-space:nowrap;">-Q' + parseFloat(g.monto).toFixed(2) + '</div>'
            + (esCajero() ? '' : '<button onclick="eliminarGasto(' + g.id + ')" style="background:transparent;border:none;color:#333;cursor:pointer;font-size:15px;padding:2px 4px;line-height:1;" title="Eliminar">✕</button>')
            + '</div>'
        ).join('');
    })
    .catch(() => {});

    // Ingresos por día
    authFetch(`${API}/gastos/ingresos-por-dia${q}`)
    .then(r => r.json())
    .then(data => {
        const lista = document.getElementById("listaIngresosDia");
        if (!lista) return;
        if (!data.length) {
            lista.innerHTML = '<p style="color:#555;font-size:13px;padding:16px 18px;">Sin ventas este mes.</p>';
            return;
        }
        lista.innerHTML = data.map(d =>
            '<div style="display:flex;align-items:center;gap:10px;padding:11px 18px;border-bottom:1px solid #131313;">'
            + '<div style="flex:1;">'
            +   '<div style="font-size:13px;font-weight:600;color:#e2e2e2;">' + fmtFecha(d.dia) + '</div>'
            +   '<div style="font-size:11px;color:#444;margin-top:2px;">' + d.ventas + ' venta' + (d.ventas !== 1 ? 's' : '') + '</div>'
            + '</div>'
            + '<div style="font-size:14px;font-weight:700;color:#4ade80;white-space:nowrap;">+Q' + parseFloat(d.total).toFixed(2) + '</div>'
            + '</div>'
        ).join('');
    })
    .catch(() => {});
}

function exportarFinanzasExcel() {
    const mes = document.getElementById('mesFinanzas')?.value || '';
    if (!mes) { mostrarMensaje('⚠️ Selecciona un mes antes de exportar'); return; }

    const q = '?mes=' + encodeURIComponent(mes);
    const [anio, mesNum] = mes.split('-');
    const nombreMes = ['Enero','Febrero','Marzo','Abril','Mayo','Junio','Julio','Agosto','Septiembre','Octubre','Noviembre','Diciembre'][parseInt(mesNum)-1];

    mostrarMensaje('⏳ Generando Excel...');

    Promise.all([
        authFetch(API + '/gastos/resumen' + q).then(r => r.json()),
        authFetch(API + '/gastos' + q).then(r => r.json()),
        authFetch(API + '/gastos/ingresos-extra' + q).then(r => r.json()),
        authFetch(API + '/gastos/ingresos-por-dia' + q).then(r => r.json())
    ])
    .then(([resumen, gastos, ingresosExtra, ventasDia]) => {

        const ingresos     = parseFloat(resumen.ingresos     || 0);
        const extra        = parseFloat(resumen.ingresos_extra || 0);
        const totalGastos  = parseFloat(resumen.gastos       || 0);
        const ganancia     = parseFloat(resumen.ganancia     || 0);

        // Estilos reutilizables (SpreadsheetML no soporta CSS; usamos colores de fondo/fuente via XML)
        const titulo = (txt) =>
            '<Row><Cell ss:MergeAcross="3" ss:StyleID="sTitulo"><Data ss:Type="String">' + txt + '</Data></Cell></Row>';

        const cabecera = (...cols) =>
            '<Row>' + cols.map(c => '<Cell ss:StyleID="sHead"><Data ss:Type="String">' + c + '</Data></Cell>').join('') + '</Row>';

        const filaStr = (styleId, ...vals) =>
            '<Row>' + vals.map((v, i) =>
                '<Cell ss:StyleID="' + (Array.isArray(styleId) ? styleId[i] : styleId) + '"><Data ss:Type="String">' + v + '</Data></Cell>'
            ).join('') + '</Row>';

        const filaNum = (styleId, str1, str2, num, str3) =>
            '<Row>'
            + '<Cell ss:StyleID="' + styleId + '"><Data ss:Type="String">' + str1 + '</Data></Cell>'
            + '<Cell ss:StyleID="' + styleId + '"><Data ss:Type="String">' + str2 + '</Data></Cell>'
            + '<Cell ss:StyleID="' + styleId + 'N"><Data ss:Type="Number">' + num + '</Data></Cell>'
            + (str3 !== undefined ? '<Cell ss:StyleID="' + styleId + '"><Data ss:Type="String">' + str3 + '</Data></Cell>' : '')
            + '</Row>';

        // ── Filas de resumen ──
        const resumenFilas =
            titulo('💰 RESUMEN — ' + nombreMes + ' ' + anio)
            + cabecera('Concepto', 'Monto (Q)', '', '')
            + filaNum('sVenta', 'Ventas del mes',   '', ingresos,    '')
            + filaNum('sExtra', 'Ingresos extra',   '', extra,       '')
            + filaNum('sGasto', 'Gastos del mes',   '', -totalGastos,'')
            + filaNum('sGanan', 'Ganancia neta',    '', ganancia,    '')
            + '<Row></Row>';

        // ── Gastos ──
        const gastosFilas = !gastos.length ? '' :
            titulo('🔴 GASTOS REGISTRADOS')
            + cabecera('Fecha', 'Descripción', 'Monto (Q)', '')
            + gastos.map(g =>
                '<Row>'
                + '<Cell ss:StyleID="sGasto"><Data ss:Type="String">' + fmtFecha(g.fecha) + '</Data></Cell>'
                + '<Cell ss:StyleID="sGasto"><Data ss:Type="String">' + (g.descripcion || '') + '</Data></Cell>'
                + '<Cell ss:StyleID="sGastoN"><Data ss:Type="Number">' + (-parseFloat(g.monto)) + '</Data></Cell>'
                + '<Cell ss:StyleID="sGasto"><Data ss:Type="String"></Data></Cell>'
                + '</Row>'
            ).join('')
            + '<Row></Row>';

        // ── Ingresos extra ──
        const extraFilas = !ingresosExtra.length ? '' :
            titulo('🔵 INGRESOS EXTRA')
            + cabecera('Fecha', 'Descripción', 'Monto (Q)', '')
            + ingresosExtra.map(g =>
                '<Row>'
                + '<Cell ss:StyleID="sExtra"><Data ss:Type="String">' + fmtFecha(g.fecha) + '</Data></Cell>'
                + '<Cell ss:StyleID="sExtra"><Data ss:Type="String">' + (g.descripcion || '') + '</Data></Cell>'
                + '<Cell ss:StyleID="sExtraN"><Data ss:Type="Number">' + parseFloat(g.monto) + '</Data></Cell>'
                + '<Cell ss:StyleID="sExtra"><Data ss:Type="String"></Data></Cell>'
                + '</Row>'
            ).join('')
            + '<Row></Row>';

        // ── Ventas por día ──
        const ventasFilas = !ventasDia.length ? '' :
            titulo('🟢 VENTAS POR DÍA')
            + cabecera('Día', 'N° Ventas', 'Total (Q)', '')
            + ventasDia.map(d =>
                '<Row>'
                + '<Cell ss:StyleID="sVenta"><Data ss:Type="String">' + fmtFecha(d.dia) + '</Data></Cell>'
                + '<Cell ss:StyleID="sVenta"><Data ss:Type="Number">' + d.ventas + '</Data></Cell>'
                + '<Cell ss:StyleID="sVentaN"><Data ss:Type="Number">' + parseFloat(d.total) + '</Data></Cell>'
                + '<Cell ss:StyleID="sVenta"><Data ss:Type="String"></Data></Cell>'
                + '</Row>'
            ).join('')
            + '<Row></Row>';

        const xml = '<?xml version="1.0" encoding="UTF-8"?>'
            + '<?mso-application progid="Excel.Sheet"?>'
            + '<Workbook xmlns="urn:schemas-microsoft-com:office:spreadsheet"'
            + ' xmlns:ss="urn:schemas-microsoft-com:office:spreadsheet">'

            // Estilos
            + '<Styles>'
            // Título de sección
            + '<Style ss:ID="sTitulo"><Font ss:Bold="1" ss:Size="13" ss:Color="#FFFFFF"/><Interior ss:Color="#1a1a2e" ss:Pattern="Solid"/><Alignment ss:Horizontal="Left" ss:Vertical="Center"/></Style>'
            // Cabecera columnas
            + '<Style ss:ID="sHead"><Font ss:Bold="1" ss:Color="#FFFFFF"/><Interior ss:Color="#374151" ss:Pattern="Solid"/><Borders><Border ss:Position="Bottom" ss:LineStyle="Continuous" ss:Weight="1"/></Borders></Style>'
            // Gasto
            + '<Style ss:ID="sGasto"><Interior ss:Color="#2d0a0a" ss:Pattern="Solid"/><Font ss:Color="#fca5a5"/></Style>'
            + '<Style ss:ID="sGastoN"><Interior ss:Color="#2d0a0a" ss:Pattern="Solid"/><Font ss:Color="#f87171" ss:Bold="1"/><NumberFormat ss:Format=\'#,##0.00\'/></Style>'
            // Ingreso extra
            + '<Style ss:ID="sExtra"><Interior ss:Color="#0a1427" ss:Pattern="Solid"/><Font ss:Color="#93c5fd"/></Style>'
            + '<Style ss:ID="sExtraN"><Interior ss:Color="#0a1427" ss:Pattern="Solid"/><Font ss:Color="#60a5fa" ss:Bold="1"/><NumberFormat ss:Format=\'#,##0.00\'/></Style>'
            // Venta
            + '<Style ss:ID="sVenta"><Interior ss:Color="#0a1f0a" ss:Pattern="Solid"/><Font ss:Color="#86efac"/></Style>'
            + '<Style ss:ID="sVentaN"><Interior ss:Color="#0a1f0a" ss:Pattern="Solid"/><Font ss:Color="#4ade80" ss:Bold="1"/><NumberFormat ss:Format=\'#,##0.00\'/></Style>'
            // Ganancia
            + '<Style ss:ID="sGanan"><Interior ss:Color="#111827" ss:Pattern="Solid"/><Font ss:Color="#e2e2e2" ss:Bold="1"/><NumberFormat ss:Format=\'#,##0.00\'/></Style>'
            + '<Style ss:ID="sGananN"><Interior ss:Color="#111827" ss:Pattern="Solid"/><Font ss:Color="#e2e2e2" ss:Bold="1"/><NumberFormat ss:Format=\'#,##0.00\'/></Style>'
            + '</Styles>'

            + '<Worksheet ss:Name="Finanzas ' + nombreMes + ' ' + anio + '">'
            + '<Table ss:DefaultColumnWidth="140">'
            + resumenFilas
            + gastosFilas
            + extraFilas
            + ventasFilas
            + '</Table></Worksheet></Workbook>';

        const blob = new Blob([xml], { type: 'application/vnd.ms-excel;charset=utf-8' });
        const url  = URL.createObjectURL(blob);
        const a    = document.createElement('a');
        a.href     = url;
        a.download = 'finanzas_' + mes + '.xls';
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
        mostrarMensaje('✅ Excel descargado: finanzas_' + mes + '.xls');
    })
    .catch(() => mostrarMensaje('❌ Error generando el Excel'));
}

function registrarGasto() {
    const desc = document.getElementById("gastoDesc")?.value?.trim();
    const monto = document.getElementById("gastoMonto")?.value;

    if (!desc || !monto || parseFloat(monto) <= 0) {
        mostrarMensaje("❌ Ingresa descripción y monto");
        return;
    }

    authFetch(`${API}/gastos`, {
        method: "POST",
        body: JSON.stringify({ descripcion: desc, monto: parseFloat(monto) })
    })
    .then(r => r.json())
    .then(() => {
        mostrarMensaje("✅ Gasto registrado");
        document.getElementById("gastoDesc").value = "";
        document.getElementById("gastoMonto").value = "";
        cargarFinanzas();
    })
    .catch(() => mostrarMensaje("❌ Error registrando gasto"));
}

async function eliminarGasto(id) {
    if (!await confirmarDialog("Eliminar gasto", "¿Eliminar este gasto? Esta acción no se puede deshacer.", "danger")) return;
    authFetch(`${API}/gastos/${id}`, { method: "DELETE" })
    .then(() => {
        mostrarMensaje("✅ Gasto eliminado");
        cargarFinanzas();
    })
    .catch(() => mostrarMensaje("❌ Error eliminando gasto"));
}

function registrarIngresoExtra() {
    const desc  = document.getElementById("ingresoDesc")?.value?.trim();
    const monto = document.getElementById("ingresoMonto")?.value;

    if (!desc || !monto || parseFloat(monto) <= 0) {
        mostrarMensaje("❌ Ingresa descripción y monto");
        return;
    }

    authFetch(`${API}/gastos/ingresos-extra`, {
        method: "POST",
        body: JSON.stringify({ descripcion: desc, monto: parseFloat(monto) })
    })
    .then(r => r.json())
    .then(() => {
        mostrarMensaje("✅ Ingreso extra registrado");
        document.getElementById("ingresoDesc").value  = "";
        document.getElementById("ingresoMonto").value = "";
        cargarFinanzas();
    })
    .catch(() => mostrarMensaje("❌ Error registrando ingreso"));
}

async function eliminarIngresoExtra(id) {
    if (!await confirmarDialog("Eliminar ingreso", "¿Eliminar este ingreso extra?", "danger")) return;
    authFetch(`${API}/gastos/ingresos-extra/${id}`, { method: "DELETE" })
    .then(() => {
        mostrarMensaje("✅ Ingreso eliminado");
        cargarFinanzas();
    })
    .catch(() => mostrarMensaje("❌ Error eliminando ingreso"));
}
