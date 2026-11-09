use std::cell::{Cell, RefCell};
use std::rc::{Rc, Weak};
use std::sync::atomic::{AtomicU64, Ordering};
use std::time::Duration;

use deskband11lib::{HostEvent, TaskbarContentHost, TaskbarContentHostOptions, TaskbarEdge};
use serde::Serialize;
use tauri::plugin::{Builder, TauriPlugin};
use tauri::{
    AppHandle, Emitter, Manager, PhysicalPosition, RunEvent, Runtime, WebviewUrl, WebviewWindow,
    WebviewWindowBuilder,
};

use crate::error::Result;

/// The event emitted to a hosted window when the taskbar moves to another screen edge, including the first
/// layout after attaching. The payload is `{ taskbarEdge: "left" | "top" | "right" | "bottom", isVertical: boolean }`.
pub const TASKBAR_EDGE_CHANGED_EVENT: &str = "deskband11lib://taskbar-edge-changed";

const DEFAULT_RECREATION_DELAY: Duration = Duration::from_secs(1);
const OFF_SCREEN_POSITION: i32 = -32000;

static NEXT_CONTROLLER_IDENTIFIER: AtomicU64 = AtomicU64::new(1);

thread_local! {
    // Hosts belong to the main thread, so running controllers are kept there.
    static RUNNING_CONTROLLERS: RefCell<Vec<Rc<dyn RunningController>>> = const { RefCell::new(Vec::new()) };
}

/// Creates the plugin.
///
/// While a [`TaskbarWindowController`] runs, the plugin keeps Tauri from exiting when its last window closes,
/// because the taskbar destroys the hosted window before the controller creates it again. Exit the
/// application explicitly, for example with `AppHandle::exit`. Running controllers stop when the application exits.
pub fn init<R: Runtime>() -> TauriPlugin<R> {
    Builder::new("deskband11lib")
        .on_event(|_, event| match event {
            RunEvent::ExitRequested {
                code: None, api, ..
            } if has_running_controllers() => api.prevent_exit(),
            RunEvent::Exit => stop_all_controllers(),
            _ => {}
        })
        .build()
}

/// Returns a window builder with what a taskbar window needs: hidden until attached, not focused, no
/// decorations, taskbar button, resizing or shadow. Add your own settings, such as `transparent(true)`, and build it.
pub fn taskbar_window_builder<'a, R: Runtime, M: Manager<R>>(
    manager: &'a M,
    label: impl Into<String>,
    url: WebviewUrl,
) -> WebviewWindowBuilder<'a, R, M> {
    WebviewWindowBuilder::new(manager, label, url)
        .visible(false)
        .focused(false)
        .decorations(false)
        .skip_taskbar(true)
        .resizable(false)
        .maximizable(false)
        .minimizable(false)
        .shadow(false)
}

type CreateWindow<R> = Box<dyn Fn(&AppHandle<R>) -> tauri::Result<WebviewWindow<R>>>;
type EventHandler<R> = Box<dyn Fn(&WebviewWindow<R>, &HostEvent)>;

/// Creates a webview window, hosts it in the taskbar, and creates it again whenever the taskbar requires it.
///
/// The taskbar destroys the hosted window when Explorer restarts or when the monitor it sits on is
/// disconnected, and the window is detached when the system DPI changes. In each case the controller releases
/// the host, destroys the window, waits for the taskbar to settle, then calls the factory again.
///
/// The factory must build a new, hidden window, preferably from [`taskbar_window_builder`]. Do not show, move
/// or resize the window yourself. Start the controller on the main thread, for example from the setup hook.
pub struct TaskbarWindowController<R: Runtime> {
    app: AppHandle<R>,
    create_window: CreateWindow<R>,
    options: TaskbarContentHostOptions,
    recreation_delay: Duration,
    event_handler: Option<EventHandler<R>>,
}

impl<R: Runtime> TaskbarWindowController<R> {
    pub fn new(
        app: &AppHandle<R>,
        create_window: impl Fn(&AppHandle<R>) -> tauri::Result<WebviewWindow<R>> + 'static,
    ) -> Self {
        Self {
            app: app.clone(),
            create_window: Box::new(create_window),
            options: TaskbarContentHostOptions::default(),
            recreation_delay: DEFAULT_RECREATION_DELAY,
            event_handler: None,
        }
    }

    /// Sets the layout options of the host.
    pub fn options(mut self, options: TaskbarContentHostOptions) -> Self {
        self.options = options;
        self
    }

    /// Sets how long the controller waits for the taskbar before it creates the window again. Defaults to one second.
    pub fn recreation_delay(mut self, recreation_delay: Duration) -> Self {
        self.recreation_delay = recreation_delay;
        self
    }

    /// Sets a handler that receives every host event together with the window it concerns.
    pub fn on_event(mut self, handler: impl Fn(&WebviewWindow<R>, &HostEvent) + 'static) -> Self {
        self.event_handler = Some(Box::new(handler));
        self
    }

    /// Creates the first window and starts attaching it. Call this on the main thread.
    pub fn start(self) -> Result<TaskbarWindowHandle<R>> {
        let running = Rc::new(Running {
            identifier: NEXT_CONTROLLER_IDENTIFIER.fetch_add(1, Ordering::Relaxed),
            configuration: self,
            state: RefCell::new(RunningState::default()),
            is_running: Cell::new(true),
        });
        running.create()?;
        RUNNING_CONTROLLERS.with_borrow_mut(|controllers| controllers.push(running.clone()));
        Ok(TaskbarWindowHandle {
            running: Rc::downgrade(&running),
        })
    }
}

/// A started [`TaskbarWindowController`]. Use it on the main thread.
pub struct TaskbarWindowHandle<R: Runtime> {
    running: Weak<Running<R>>,
}

impl<R: Runtime> TaskbarWindowHandle<R> {
    /// The current window, or `None` while it is being created again or after the controller stopped.
    pub fn window(&self) -> Option<WebviewWindow<R>> {
        self.running.upgrade()?.state.borrow().window.clone()
    }

    /// Destroys the window and creates it again through the factory after the recreation delay, as after an Explorer restart.
    pub fn recreate(&self) {
        if let Some(running) = self.running.upgrade() {
            running.schedule_recreation();
        }
    }

    /// Detaches and destroys the window and stops creating it again.
    pub fn stop(&self) {
        if let Some(running) = self.running.upgrade() {
            RUNNING_CONTROLLERS.with_borrow_mut(|controllers| {
                controllers.retain(|controller| controller.identifier() != running.identifier)
            });
            running.stop();
        }
    }
}

trait RunningController {
    fn identifier(&self) -> u64;
    fn recreate(self: Rc<Self>);
    fn stop(&self);
}

struct Running<R: Runtime> {
    identifier: u64,
    configuration: TaskbarWindowController<R>,
    state: RefCell<RunningState<R>>,
    is_running: Cell<bool>,
}

struct RunningState<R: Runtime> {
    window: Option<WebviewWindow<R>>,
    host: Option<TaskbarContentHost>,
    is_recreating: bool,
}

impl<R: Runtime> Default for RunningState<R> {
    fn default() -> Self {
        Self {
            window: None,
            host: None,
            is_recreating: false,
        }
    }
}

#[derive(Clone, Serialize)]
#[serde(rename_all = "camelCase")]
struct TaskbarEdgeChangedPayload {
    taskbar_edge: &'static str,
    is_vertical: bool,
}

impl<R: Runtime> Running<R> {
    fn create(self: &Rc<Self>) -> Result<()> {
        let window = (self.configuration.create_window)(&self.configuration.app)?;
        let host = match create_host(&window, &self.configuration.options) {
            Ok(host) => host,
            Err(error) => {
                let _ = window.destroy();
                return Err(error);
            }
        };

        let weak_running = Rc::downgrade(self);
        let event_window = window.clone();
        host.set_event_handler(move |event| {
            if let Some(running) = weak_running.upgrade() {
                running.on_host_event(&event_window, event);
            }
        });

        // A webview sets up the surface it renders into when its window is first shown. A window first shown after it
        // became a child of the taskbar displays only its background, so it is shown off-screen while it is still a
        // top-level window and attached afterwards.
        window.set_position(PhysicalPosition::new(
            OFF_SCREEN_POSITION,
            OFF_SCREEN_POSITION,
        ))?;
        window.show()?;
        host.attach_when_layout_ready()?;

        let mut state = self.state.borrow_mut();
        state.window = Some(window);
        state.host = Some(host);
        Ok(())
    }

    fn on_host_event(self: &Rc<Self>, window: &WebviewWindow<R>, event: &HostEvent) {
        match event {
            HostEvent::TaskbarEdgeChanged(edge) => {
                let payload = TaskbarEdgeChangedPayload {
                    taskbar_edge: edge_name(*edge),
                    is_vertical: edge.is_vertical(),
                };
                let _ = window.emit_to(window.label(), TASKBAR_EDGE_CHANGED_EVENT, payload);
            }
            HostEvent::AttachFailed(_) => {
                let _ = window.hide();
            }
            _ => {}
        }

        if let Some(handler) = &self.configuration.event_handler {
            handler(window, event);
        }

        if *event == HostEvent::TaskbarWindowRecreationRequired {
            self.schedule_recreation();
        }
    }

    fn schedule_recreation(self: &Rc<Self>) {
        if self.state.borrow().is_recreating {
            return;
        }

        self.state.borrow_mut().is_recreating = true;
        self.release();

        // The delay runs off the main thread; only the identifier of the controller crosses back.
        let app = self.configuration.app.clone();
        let identifier = self.identifier;
        let recreation_delay = self.configuration.recreation_delay;
        std::thread::spawn(move || {
            std::thread::sleep(recreation_delay);
            let _ = app.run_on_main_thread(move || recreate_controller(identifier));
        });
    }

    // The window is destroyed before its replacement is created, because both use the same label.
    fn release(&self) {
        let (host, window) = {
            let mut state = self.state.borrow_mut();
            (state.host.take(), state.window.take())
        };

        // Hiding first keeps the detached window from appearing as a top-level window before it is destroyed.
        if let Some(window) = &window {
            let _ = window.hide();
        }
        drop(host);
        if let Some(window) = window {
            let _ = window.destroy();
        }
    }
}

impl<R: Runtime> RunningController for Running<R> {
    fn identifier(&self) -> u64 {
        self.identifier
    }

    fn recreate(self: Rc<Self>) {
        self.state.borrow_mut().is_recreating = false;
        if !self.is_running.get() {
            return;
        }

        if let Err(error) = self.create() {
            log::error!("The taskbar window could not be created again: {error}");
            self.schedule_recreation();
        }
    }

    fn stop(&self) {
        self.is_running.set(false);
        self.release();
    }
}

fn create_host<R: Runtime>(
    window: &WebviewWindow<R>,
    options: &TaskbarContentHostOptions,
) -> Result<TaskbarContentHost> {
    let window_handle = window.hwnd()?.0 as isize;
    Ok(TaskbarContentHost::new(window_handle, options)?)
}

fn edge_name(edge: TaskbarEdge) -> &'static str {
    match edge {
        TaskbarEdge::Left => "left",
        TaskbarEdge::Top => "top",
        TaskbarEdge::Right => "right",
        TaskbarEdge::Bottom => "bottom",
        _ => "unknown",
    }
}

fn has_running_controllers() -> bool {
    RUNNING_CONTROLLERS.with_borrow(|controllers| !controllers.is_empty())
}

fn recreate_controller(identifier: u64) {
    let controller = RUNNING_CONTROLLERS.with_borrow(|controllers| {
        controllers
            .iter()
            .find(|controller| controller.identifier() == identifier)
            .cloned()
    });
    if let Some(controller) = controller {
        controller.recreate();
    }
}

fn stop_all_controllers() {
    let controllers = RUNNING_CONTROLLERS.with_borrow_mut(std::mem::take);
    for controller in controllers {
        controller.stop();
    }
}
