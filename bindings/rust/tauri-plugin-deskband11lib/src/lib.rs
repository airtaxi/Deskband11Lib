//! Hosts Tauri webview windows inside the Windows 11 taskbar.
//!
//! Register the plugin with [`init`], then start a [`TaskbarWindowController`] from the setup hook. The
//! controller creates the window through your factory, attaches it to the taskbar, and creates it again
//! whenever the taskbar requires it.
//!
//! ```ignore
//! use tauri_plugin_deskband11lib::{TaskbarContentHostOptions, TaskbarWindowController, taskbar_window_builder};
//!
//! tauri::Builder::default()
//!     .plugin(tauri_plugin_deskband11lib::init())
//!     .setup(|app| {
//!         TaskbarWindowController::new(app.handle(), |app| taskbar_window_builder(app, "widget", tauri::WebviewUrl::App("index.html".into())).transparent(true).build())
//!             .options(TaskbarContentHostOptions { preferred_width: Some(200.0), ..Default::default() })
//!             .start()?;
//!         Ok(())
//!     })
//!     .run(tauri::generate_context!())
//!     .expect("The application failed to run.");
//! ```

#[cfg(windows)]
mod controller;
mod error;

#[cfg(windows)]
pub use controller::{
    TASKBAR_EDGE_CHANGED_EVENT, TaskbarWindowController, TaskbarWindowHandle, init,
    taskbar_window_builder,
};
pub use deskband11lib::{
    Easing, HostEvent, ResultCode, STRETCH, TaskbarAlignment, TaskbarContentHostOptions,
    TaskbarContentPlacement, TaskbarEdge,
};
pub use error::{Error, Result};
