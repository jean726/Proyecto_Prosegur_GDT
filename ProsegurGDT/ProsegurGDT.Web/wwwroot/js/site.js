// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.


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