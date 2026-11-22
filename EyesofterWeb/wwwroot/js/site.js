// Convierte a mayúsculas los campos marcados (por ejemplo, la matrícula) mientras se escriben.
document.addEventListener('input', function (e) {
    if (e.target.classList && e.target.classList.contains('campo-mayusculas')) {
        var pos = e.target.selectionStart;
        e.target.value = e.target.value.toUpperCase();
        e.target.setSelectionRange(pos, pos);
    }
});

// W-RFNN-01: la sesión expira tras N minutos sin actividad (lo decide el servidor con la cookie).
// Aquí solo se avisa un minuto antes y, al vencer, se manda al inicio de sesión.
(function () {
    var minutos = parseInt(document.body.dataset.inactividad || '0', 10);
    if (!minutos) return;
    var aviso = document.getElementById('avisoInactividad');
    var temporizadorAviso, temporizadorFin;

    function reiniciar() {
        clearTimeout(temporizadorAviso);
        clearTimeout(temporizadorFin);
        if (aviso) aviso.classList.add('d-none');
        temporizadorAviso = setTimeout(function () {
            if (aviso) { aviso.classList.remove('d-none'); document.getElementById('seguirConectado').focus(); }
        }, (minutos - 1) * 60000);
        temporizadorFin = setTimeout(function () {
            window.location.href = '/Cuenta/Login?expirada=true';
        }, minutos * 60000);
    }

    var boton = document.getElementById('seguirConectado');
    if (boton) boton.addEventListener('click', function () {
        // Una petición al servidor renueva la cookie (expiración deslizante).
        fetch('/Home/Mantener', { credentials: 'same-origin' }).then(reiniciar);
    });
    reiniciar();
})();

// Confirmación antes de acciones irreversibles: <form data-confirmar="¿Seguro?">
document.addEventListener('submit', function (e) {
    var mensaje = e.target.dataset && e.target.dataset.confirmar;
    if (mensaje && !window.confirm(mensaje)) e.preventDefault();
});

// Filtros que se aplican al cambiar la opción: <select data-auto-enviar>. (La CSP no permite onchange en línea.)
document.addEventListener('change', function (e) {
    if (e.target.matches && e.target.matches('[data-auto-enviar]') && e.target.form) e.target.form.submit();
});
