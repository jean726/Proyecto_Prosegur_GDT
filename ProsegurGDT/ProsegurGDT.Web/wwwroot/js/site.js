// Scripts comunes de la plataforma. La política de contenido (CSP) no admite manejadores en línea
// (onclick, onsubmit): el comportamiento se declara con atributos data-* y se conecta aquí.

// Confirmación antes de una acción irreversible: <form data-confirmar="…"> o <button data-confirmar="…">
document.addEventListener('submit', function (e) {
    var f = e.target;
    var boton = e.submitter;
    var mensaje = (boton && boton.getAttribute('data-confirmar')) || f.getAttribute('data-confirmar');
    if (mensaje && !window.confirm(mensaje)) e.preventDefault();
}, true);

// Botones de las vistas de impresión: data-accion="imprimir" | "cerrar"
document.addEventListener('click', function (e) {
    var b = e.target.closest ? e.target.closest('[data-accion]') : null;
    if (!b) return;
    var accion = b.getAttribute('data-accion');
    if (accion === 'imprimir') window.print();
    else if (accion === 'cerrar') window.close();
});

// Imágenes opcionales (logotipo): si no cargan, se ocultan
function pgOcultarSiFalla(img) { img.style.display = 'none'; }
document.addEventListener('error', function (e) {
    var t = e.target;
    if (t && t.tagName === 'IMG' && t.hasAttribute('data-ocultar-si-falla')) pgOcultarSiFalla(t);
}, true);
document.addEventListener('DOMContentLoaded', function () {
    document.querySelectorAll('img[data-ocultar-si-falla]').forEach(function (img) {
        if (img.complete && img.naturalWidth === 0) pgOcultarSiFalla(img);
    });
});

// Fase 11: conteo ascendente en los dashboards («Torre de control»)
document.addEventListener('DOMContentLoaded', function () {
    var nodos = document.querySelectorAll('[data-pg-contar]');
    if (!nodos.length) return;
    if (window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches) return;
    var duracion = 1200;
    var inicio = performance.now();
    var datos = Array.prototype.map.call(nodos, function (n) {
        return { nodo: n, destino: parseInt(n.getAttribute('data-pg-contar'), 10) || 0 };
    });
    datos.forEach(function (d) { if (d.destino > 0) d.nodo.textContent = '0'; });
    function paso(t) {
        var p = Math.min(1, (t - inicio) / duracion);
        var suave = 1 - Math.pow(1 - p, 3);
        datos.forEach(function (d) { d.nodo.textContent = Math.round(d.destino * suave); });
        if (p < 1) requestAnimationFrame(paso);
    }
    requestAnimationFrame(paso);
});

// Fase D5: bloquear doble envío
document.addEventListener('submit', function (e) {
    var f = e.target;
    if (e.defaultPrevented || f.hasAttribute('data-no-bloquear')) return;
    setTimeout(function () {
        f.querySelectorAll('[type=submit]').forEach(function (b) {
            b.disabled = true;
            b.classList.add('pg-enviando');
        });
    }, 0);
});
window.addEventListener('pageshow', function (e) {
    if (!e.persisted) return;
    document.querySelectorAll('.pg-enviando').forEach(function (b) {
        b.disabled = false;
        b.classList.remove('pg-enviando');
    });
});