package com.example.eyesofterapp

import androidx.compose.runtime.remember
import androidx.compose.ui.window.ComposeUIViewController
import com.example.eyesofterapp.location.IosLocationProvider

/** Punto de entrada de iOS (lo llama iosApp/ContentView.swift). */
fun MainViewController() = ComposeUIViewController {
    App(locationProvider = remember { IosLocationProvider() })
}
