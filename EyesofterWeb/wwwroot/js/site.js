// Convierte a mayúsculas los campos marcados (por ejemplo, la matrícula) mientras se escriben.
document.addEventListener('input', function (e) {
    if (e.target.classList && e.target.classList.contains('campo-mayusculas')) {
        var pos = e.target.selectionStart;
        e.target.value = e.target.value.toUpperCase();
        e.target.setSelectionRange(pos, pos);
    }
});
