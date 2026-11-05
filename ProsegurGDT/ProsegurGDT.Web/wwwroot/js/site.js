// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.


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