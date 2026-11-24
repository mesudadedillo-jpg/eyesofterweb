package com.example.eyesofterapp

import androidx.compose.runtime.remember
import androidx.compose.ui.window.ComposeUIViewController
import com.example.eyesofterapp.location.IosLocationProvider
import com.example.eyesofterapp.plataforma.PushIos

/** Punto de entrada de iOS (lo llama iosApp/ContentView.swift). */
fun MainViewController() = ComposeUIViewController {
    App(
        locationProvider = remember { IosLocationProvider() },
        push = PushIos,
        rutaExterna = PushIos.ruta,
        onRutaAtendida = { PushIos.ruta.value = null },
    )
}
