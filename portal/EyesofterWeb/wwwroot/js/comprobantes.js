// Motivos frecuentes de rechazo: un clic llena el campo.
document.querySelectorAll('[data-motivo]').forEach(function (b) {
    b.addEventListener('click', function () { document.getElementById('motivo').value = b.dataset.motivo; });
});
