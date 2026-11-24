package com.example.eyesofterapp.location

import com.example.eyesofterapp.domain.model.Coordenadas
import kotlinx.cinterop.ExperimentalForeignApi
import kotlinx.cinterop.useContents
import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.withTimeoutOrNull
import platform.CoreLocation.CLAuthorizationStatus
import platform.CoreLocation.CLLocation
import platform.CoreLocation.CLLocationManager
import platform.CoreLocation.CLLocationManagerDelegateProtocol
import platform.CoreLocation.kCLAuthorizationStatusAuthorizedAlways
import platform.CoreLocation.kCLAuthorizationStatusAuthorizedWhenInUse
import platform.CoreLocation.kCLAuthorizationStatusNotDetermined
import platform.CoreLocation.kCLLocationAccuracyHundredMeters
import platform.Foundation.NSError
import platform.darwin.NSObject

/**
 * Ubicación en iPhone con CoreLocation (equivalente de AndroidLocationProvider).
 * Pide permiso "mientras se usa la app" (texto en Info.plist: NSLocationWhenInUseUsageDescription)
 * y una sola lectura; si no llega a tiempo usa la última posición conocida.
 */
@OptIn(ExperimentalForeignApi::class)
class IosLocationProvider(private val timeoutMs: Long = 20_000L) : LocationProvider {

    private val manager = CLLocationManager()

    /** CLLocationManager guarda el delegado como referencia débil: aquí se mantiene vivo. */
    private var delegado: Delegado? = null

    override suspend fun obtenerUbicacion(): ResultadoUbicacion {
        if (!CLLocationManager.locationServicesEnabled()) return ResultadoUbicacion.UbicacionApagada

        val d = Delegado()
        delegado = d
        manager.delegate = d
        manager.desiredAccuracy = kCLLocationAccuracyHundredMeters

        var estado = manager.authorizationStatus
        if (estado == kCLAuthorizationStatusNotDetermined) {
            manager.requestWhenInUseAuthorization()
            estado = withTimeoutOrNull(60_000L) { d.permiso.await() } ?: manager.authorizationStatus
        }
        if (estado != kCLAuthorizationStatusAuthorizedWhenInUse && estado != kCLAuthorizationStatusAuthorizedAlways) {
            return ResultadoUbicacion.SinPermiso
        }

        manager.requestLocation()
        val nueva = withTimeoutOrNull(timeoutMs) { d.lectura.await() }
        if (nueva != null) return exito(nueva, esUltimaConocida = false)

        return manager.location?.let { exito(it, esUltimaConocida = true) } ?: ResultadoUbicacion.SinSenal
    }

    private fun exito(ubicacion: CLLocation, esUltimaConocida: Boolean): ResultadoUbicacion {
        val (lat, lng) = ubicacion.coordinate.useContents { latitude to longitude }
        val precision = ubicacion.horizontalAccuracy
        return ResultadoUbicacion.Exito(
            coordenadas = Coordenadas(lat, lng),
            precisionMetros = if (precision >= 0) precision.toFloat() else null,
            proveedor = "corelocation",
            esUltimaConocida = esUltimaConocida,
        )
    }

    private class Delegado : NSObject(), CLLocationManagerDelegateProtocol {
        val permiso = CompletableDeferred<CLAuthorizationStatus>()
        val lectura = CompletableDeferred<CLLocation?>()

        override fun locationManagerDidChangeAuthorization(manager: CLLocationManager) {
            val estado = manager.authorizationStatus
            if (estado != kCLAuthorizationStatusNotDetermined) permiso.complete(estado)
        }

        override fun locationManager(manager: CLLocationManager, didUpdateLocations: List<*>) {
            lectura.complete(didUpdateLocations.lastOrNull() as? CLLocation)
        }

        override fun locationManager(manager: CLLocationManager, didFailWithError: NSError) {
            lectura.complete(null)
        }
    }
}
