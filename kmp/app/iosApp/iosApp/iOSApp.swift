import SwiftUI
import UserNotifications
import FirebaseCore
import FirebaseMessaging
import Shared

/// Firebase y avisos push. El resto de la app es Kotlin (MainViewController).
/// Si la app se compila sin GoogleService-Info.plist, todo funciona igual pero sin push.
class AppDelegate: NSObject, UIApplicationDelegate, MessagingDelegate, UNUserNotificationCenterDelegate {

    func application(_ application: UIApplication,
                     didFinishLaunchingWithOptions launchOptions: [UIApplication.LaunchOptionsKey: Any]? = nil) -> Bool {
        let conFirebase = Bundle.main.path(forResource: "GoogleService-Info", ofType: "plist") != nil
        if conFirebase {
            FirebaseApp.configure()
            Messaging.messaging().delegate = self
        }
        PushIos.shared.configurar(activo: conFirebase)
        UNUserNotificationCenter.current().delegate = self
        return true
    }

    func application(_ application: UIApplication, didRegisterForRemoteNotificationsWithDeviceToken deviceToken: Data) {
        guard FirebaseApp.app() != nil else { return }
        Messaging.messaging().apnsToken = deviceToken
    }

    // Token FCM nuevo o renovado: Kotlin lo registra en el servidor.
    func messaging(_ messaging: Messaging, didReceiveRegistrationToken fcmToken: String?) {
        PushIos.shared.recibirToken(token: fcmToken)
    }

    // Aviso con la app abierta: se muestra igual.
    func userNotificationCenter(_ center: UNUserNotificationCenter,
                                willPresent notification: UNNotification) async -> UNNotificationPresentationOptions {
        [.banner, .list, .sound]
    }

    // La persona tocó el aviso: abrir la pantalla que indica "ruta".
    func userNotificationCenter(_ center: UNUserNotificationCenter,
                                didReceive response: UNNotificationResponse) async {
        let ruta = response.notification.request.content.userInfo["ruta"] as? String
        await MainActor.run { PushIos.shared.tocoNotificacion(ruta: ruta) }
    }
}

@main
struct iOSApp: App {
    @UIApplicationDelegateAdaptor(AppDelegate.self) var delegado

    var body: some Scene {
        WindowGroup {
            ContentView()
        }
    }
}
