// A clock hosted in the Windows 11 taskbar with Tauri. Right-click the clock to exit.
#![windows_subsystem = "windows"]

use tauri::menu::{Menu, MenuItem};
use tauri::{AppHandle, Manager, WebviewUrl, WebviewWindow, WindowEvent};
use tauri_plugin_deskband11lib::{
    TaskbarContentHostOptions, TaskbarWindowController, taskbar_window_builder,
};

const CLOCK_WINDOW_LABEL: &str = "clock";
const EXIT_MENU_ITEM_IDENTIFIER: &str = "exit";
const PERSONALIZE_KEY_PATH: &str = r"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

fn main() {
    tauri::Builder::default()
        .plugin(tauri_plugin_deskband11lib::init())
        .invoke_handler(tauri::generate_handler![show_context_menu])
        .on_menu_event(|app, event| {
            if event.id() == EXIT_MENU_ITEM_IDENTIFIER {
                app.exit(0);
            }
        })
        .setup(|app| {
            TaskbarWindowController::new(app.handle(), create_clock_window)
                .options(TaskbarContentHostOptions {
                    preferred_width: Some(150.0),
                    ..Default::default()
                })
                .start()?;
            Ok(())
        })
        .run(tauri::generate_context!())
        .expect("The clock failed to run.");
}

fn create_clock_window(app: &AppHandle) -> tauri::Result<WebviewWindow> {
    let window = taskbar_window_builder(
        app,
        CLOCK_WINDOW_LABEL,
        WebviewUrl::App("index.html".into()),
    )
    .inner_size(150.0, 48.0)
    .transparent(true)
    .on_page_load(|window, _| apply_taskbar_theme(&window))
    .build()?;

    let theme_window = window.clone();
    window.on_window_event(move |event| {
        if let WindowEvent::ThemeChanged(_) = event {
            apply_taskbar_theme(&theme_window);
        }
    });
    Ok(window)
}

// The taskbar follows the Windows mode, which can differ from the app mode the page sees through prefers-color-scheme.
fn apply_taskbar_theme(window: &WebviewWindow) {
    let theme = if is_taskbar_light_theme() {
        "light"
    } else {
        "dark"
    };
    let _ = window.eval(format!(
        "document.documentElement.dataset.theme = '{theme}';"
    ));
}

fn is_taskbar_light_theme() -> bool {
    windows_registry::CURRENT_USER
        .open(PERSONALIZE_KEY_PATH)
        .and_then(|key| key.get_u32("SystemUsesLightTheme"))
        .is_ok_and(|value| value != 0)
}

#[tauri::command]
fn show_context_menu(app: AppHandle) -> tauri::Result<()> {
    let Some(window) = app.get_webview_window(CLOCK_WINDOW_LABEL) else {
        return Ok(());
    };

    let exit_item = MenuItem::with_id(&app, EXIT_MENU_ITEM_IDENTIFIER, "Exit", true, None::<&str>)?;
    let menu = Menu::with_items(&app, &[&exit_item])?;
    window.popup_menu(&menu)
}
