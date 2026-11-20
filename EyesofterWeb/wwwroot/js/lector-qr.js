// Lee el QR del pase con la cámara del navegador (W-RI-02). El QR contiene la dirección
// .../Pases/Validar?codigo=XXXX; se toma el código y se abre esa página.
(function () {
    var boton = document.getElementById('iniciarCamara');
    var estado = document.getElementById('estadoCamara');
    if (!boton || typeof Html5Qrcode === 'undefined') return;
    var lector = new Html5Qrcode('lectorQr');

    function extraerCodigo(texto) {
        try {
            var url = new URL(texto);
            return url.searchParams.get('codigo');
        } catch (e) {
            return texto; // QR con solo el código
        }
    }

    boton.addEventListener('click', function () {
        estado.textContent = 'Pidiendo permiso para usar la cámara...';
        lector.start({ facingMode: 'environment' }, { fps: 10, qrbox: 240 }, function (texto) {
            var codigo = extraerCodigo(texto);
            if (!codigo) return;
            lector.stop();
            estado.textContent = 'Código leído. Validando...';
            window.location.href = '/Pases/Validar?codigo=' + encodeURIComponent(codigo);
        }).then(function () {
            boton.classList.add('d-none');
            estado.textContent = 'Cámara encendida. Centra el código QR en el recuadro.';
        }).catch(function () {
            estado.textContent = 'No se pudo usar la cámara. Revisa el permiso del navegador o escribe el código a mano.';
        });
    });
})();
