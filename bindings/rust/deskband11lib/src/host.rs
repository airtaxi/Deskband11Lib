use std::cell::{Cell, RefCell};
use std::ffi::c_void;
use std::panic::{self, AssertUnwindSafe};
use std::rc::Rc;

use crate::bindings::{NativeEvent, NativeLibrary, check, last_error_message, load_library};
use crate::enums::{ResultCode, TaskbarAlignment, TaskbarEdge};
use crate::error::{Error, Result};
use crate::options::TaskbarContentHostOptions;

/// An event raised by a [`TaskbarContentHost`].
#[derive(Clone, Debug, PartialEq)]
#[non_exhaustive]
pub enum HostEvent {
    /// [`TaskbarContentHost::attach_when_layout_ready`] completed.
    Attached,
    /// [`TaskbarContentHost::attach_when_layout_ready`] failed.
    AttachFailed(Error),
    /// Explorer restarted and created the taskbar again.
    TaskbarWindowRecreated,
    /// The taskbar the window sat on disappeared, for example because its monitor was disconnected.
    TaskbarWindowDisappeared,
    /// The system DPI changed. The window has been hidden and detached.
    TaskbarDpiChanged,
    /// Raised together with each of the three events above. Close the host, create the window again and create a new host for it.
    TaskbarWindowRecreationRequired,
    /// The taskbar moved to another screen edge, including the first layout after attaching.
    TaskbarEdgeChanged(TaskbarEdge),
    /// The window received a new size, in effective pixels.
    ContentBoundsChanged {
        max_width: f64,
        width: f64,
        height: f64,
    },
    /// An unexpected error occurred while the layout was updated.
    LayoutError(Error),
}

/// Places a top-level window inside the Windows 11 taskbar and keeps it aligned with the taskbar layout.
///
/// A host belongs to the thread that creates it, and that thread must run a Win32 message loop, such as the
/// main thread of a Tauri application. Events are delivered on the same thread from the message loop, never
/// from inside another call to the host.
///
/// The window must be per-monitor DPI aware. Create it hidden and without a frame, attach it, then show it
/// without activating it. When [`HostEvent::TaskbarWindowRecreationRequired`] is raised, close the host,
/// create the window again and create a new host for it. Dropping the host closes it.
pub struct TaskbarContentHost {
    inner: Rc<HostInner>,
}

type EventHandler = Box<dyn FnMut(&HostEvent)>;

struct HostInner {
    library: &'static NativeLibrary,
    window_handle: isize,
    host_handle: Cell<Option<isize>>,
    event_handler: RefCell<Option<EventHandler>>,
}

impl TaskbarContentHost {
    /// Creates a host for a native window handle (HWND) on the calling thread.
    pub fn new(window_handle: isize, options: &TaskbarContentHostOptions) -> Result<Self> {
        let library = load_library()?;
        let native_options = options.to_native(library)?;
        let inner = Rc::new(HostInner {
            library,
            window_handle,
            host_handle: Cell::new(None),
            event_handler: RefCell::new(None),
        });

        // The native library keeps one reference as the callback user data until the host is destroyed.
        let user_data = Rc::into_raw(Rc::clone(&inner)).cast_mut().cast::<c_void>();
        let mut host_handle = 0_isize;
        let result = unsafe {
            (library.create_host)(
                window_handle,
                &native_options,
                Some(on_native_event),
                user_data,
                &mut host_handle,
            )
        };
        if let Err(error) = check(library, result) {
            unsafe { drop(Rc::from_raw(user_data.cast::<HostInner>())) };
            return Err(error);
        }

        inner.host_handle.set(Some(host_handle));
        Ok(Self { inner })
    }

    /// Sets the handler that receives the events of this host, replacing the previous one.
    pub fn set_event_handler(&self, handler: impl FnMut(&HostEvent) + 'static) {
        *self.inner.event_handler.borrow_mut() = Some(Box::new(handler));
    }

    pub fn window_handle(&self) -> isize {
        self.inner.window_handle
    }

    pub fn is_closed(&self) -> bool {
        self.inner.host_handle.get().is_none()
    }

    pub fn is_attached(&self) -> Result<bool> {
        Ok(self.inner.read_value(self.inner.library.is_host_attached)? != 0)
    }

    /// The edge the taskbar sits on. `Unknown` until the first layout.
    pub fn taskbar_edge(&self) -> Result<TaskbarEdge> {
        Ok(TaskbarEdge::from_native(
            self.inner
                .read_value(self.inner.library.get_host_taskbar_edge)?,
        ))
    }

    pub fn is_taskbar_vertical(&self) -> Result<bool> {
        Ok(self.taskbar_edge()?.is_vertical())
    }

    pub fn taskbar_alignment(&self) -> Result<TaskbarAlignment> {
        Ok(TaskbarAlignment::from_native(self.inner.read_value(
            self.inner.library.get_host_taskbar_alignment,
        )?))
    }

    /// Attaches the window immediately. Fails when the taskbar window cannot be found.
    pub fn attach(&self) -> Result<()> {
        self.inner.call(self.inner.library.attach_host)
    }

    /// Starts attaching once the taskbar layout can be measured and returns immediately.
    /// The outcome arrives as [`HostEvent::Attached`] or [`HostEvent::AttachFailed`].
    pub fn attach_when_layout_ready(&self) -> Result<()> {
        self.inner
            .call(self.inner.library.attach_host_when_layout_ready)
    }

    /// Restores the original parent and style of the window.
    pub fn detach(&self) -> Result<()> {
        self.inner.call(self.inner.library.detach_host)
    }

    /// Recalculates the layout immediately instead of waiting for the next refresh.
    pub fn refresh_layout(&self) -> Result<()> {
        self.inner.call(self.inner.library.refresh_host_layout)
    }

    /// Detaches the window when attached and releases the host. Can be called from the event handler.
    pub fn close(&self) -> Result<()> {
        self.inner.close()
    }
}

impl Drop for TaskbarContentHost {
    fn drop(&mut self) {
        let _ = self.inner.close();
    }
}

impl HostInner {
    fn require_handle(&self) -> Result<isize> {
        self.host_handle.get().ok_or(Error::Closed)
    }

    fn call(&self, function: unsafe extern "C" fn(isize) -> i32) -> Result<()> {
        let host_handle = self.require_handle()?;
        check(self.library, unsafe { function(host_handle) }).map(drop)
    }

    fn read_value(&self, function: unsafe extern "C" fn(isize, *mut i32) -> i32) -> Result<i32> {
        let host_handle = self.require_handle()?;
        let mut value = 0;
        check(self.library, unsafe { function(host_handle, &mut value) })?;
        Ok(value)
    }

    fn close(self: &Rc<Self>) -> Result<()> {
        let Some(host_handle) = self.host_handle.get() else {
            return Ok(());
        };

        check(self.library, unsafe {
            (self.library.destroy_host)(host_handle)
        })?;
        self.host_handle.set(None);

        // No event is delivered after the native host is destroyed, so the reference held as user data is released.
        unsafe { Rc::decrement_strong_count(Rc::as_ptr(self)) };
        Ok(())
    }

    fn dispatch(&self, native_event: &NativeEvent) {
        let Some(event) = self.create_event(native_event) else {
            return;
        };

        // The handler is taken out while it runs, so it can replace itself or close the host.
        let handler = self.event_handler.borrow_mut().take();
        if let Some(mut handler) = handler {
            handler(&event);
            let mut slot = self.event_handler.borrow_mut();
            if slot.is_none() {
                *slot = Some(handler);
            }
        }
    }

    fn create_event(&self, native_event: &NativeEvent) -> Option<HostEvent> {
        let error = || Error::Native {
            result: ResultCode::from_native(native_event.result),
            message: last_error_message(self.library),
        };
        Some(match native_event.kind {
            1 => HostEvent::Attached,
            2 => HostEvent::AttachFailed(error()),
            3 => HostEvent::TaskbarWindowRecreated,
            4 => HostEvent::TaskbarWindowDisappeared,
            5 => HostEvent::TaskbarDpiChanged,
            6 => HostEvent::TaskbarWindowRecreationRequired,
            7 => HostEvent::TaskbarEdgeChanged(TaskbarEdge::from_native(native_event.taskbar_edge)),
            8 => HostEvent::ContentBoundsChanged {
                max_width: native_event.max_width,
                width: native_event.width,
                height: native_event.height,
            },
            9 => HostEvent::LayoutError(error()),
            _ => return None,
        })
    }
}

unsafe extern "C" fn on_native_event(user_data: *mut c_void, native_event: *const NativeEvent) {
    if user_data.is_null() || native_event.is_null() {
        return;
    }

    // A reference is held for the duration of the call, so a handler that drops the host does not free the state it runs on.
    let inner = unsafe {
        Rc::increment_strong_count(user_data.cast::<HostInner>());
        Rc::from_raw(user_data.cast::<HostInner>())
    };
    let native_event = unsafe { *native_event };

    // A panic must not unwind into the native caller. The panic hook has already reported it.
    let _ = panic::catch_unwind(AssertUnwindSafe(|| inner.dispatch(&native_event)));
}

/// Returns whether `window_handle` still refers to an existing window, for example after Explorer restarted.
pub fn is_window_alive(window_handle: isize) -> Result<bool> {
    let library = load_library()?;
    Ok(unsafe { (library.is_window_alive)(window_handle) } != 0)
}
