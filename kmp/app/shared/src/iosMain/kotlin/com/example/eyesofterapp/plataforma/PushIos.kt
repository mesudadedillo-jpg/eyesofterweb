package com.example.eyesofterapp.plataforma

import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.withTimeoutOrNull
import platform.UIKit.UIApplication
import platform.UserNotifications.UNAuthorizationOptionAlert
import platform.UserNotifications.UNAuthorizationOptionBadge
import platform.UserNotifications.UNAuthorizationOptionSound
import platform.UserNotifications.UNUserNotificationCenter
import platform.darwin.dispatch_async
import platform.darwin.dispatch_get_main_queue

/**
 * Push en iPhone con Firebase Cloud Messaging (el servidor no cambia: FCM reenvía a APNs).
 *
 * La parte de Firebase vive en Swift (iosApp/iOSApp.swift), que llama:
 *  - [configurar] al arrancar: si la app trae GoogleService-Info.plist o no.
 *  - [recibirToken] cuando Firebase entrega o renueva el token FCM.
 *  - [tocoNotificacion] cuando la persona toca un aviso (M-RC-02: abre la pantalla correcta).
 */
object PushIos : Push {
    override val plataforma: String = "IOS"

    /** Ruta del aviso tocado ("tamizaje/12"...); App() la atiende y la limpia. */
    val ruta = MutableStateFlow<String?>(null)

    private var activo = false
    private var tokenActual: String? = null
    private var esperando: CompletableDeferred<String?>? = null

    fun configurar(activo: Boolean) {
        this.activo = activo
    }

    fun recibirToken(token: String?) {
        if (token.isNullOrBlank()) return
        tokenActual = token
        esperando?.complete(token)
    }

    fun tocoNotificacion(ruta: String?) {
        if (!ruta.isNullOrBlank()) this.ruta.value = ruta
    }

    /** Pide permiso (si no se ha pedido) y devuelve el token FCM, o null si no hay Firebase. */
    override suspend fun token(): String? {
        if (!activo) return null
        tokenActual?.let { return it }
        val espera = CompletableDeferred<String?>()
        esperando = espera
        pedirPermisoYRegistrar()
        return withTimeoutOrNull(15_000L) { espera.await() } ?: tokenActual
    }

    private fun pedirPermisoYRegistrar() {
        val opciones = UNAuthorizationOptionAlert or UNAuthorizationOptionSound or UNAuthorizationOptionBadge
        UNUserNotificationCenter.currentNotificationCenter().requestAuthorizationWithOptions(opciones) { _, _ ->
            // Igual que en Android: aunque niegue el permiso se registra; si después lo concede, ya recibe avisos.
            dispatch_async(dispatch_get_main_queue()) {
                UIApplication.sharedApplication.registerForRemoteNotifications()
            }
        }
    }
}
