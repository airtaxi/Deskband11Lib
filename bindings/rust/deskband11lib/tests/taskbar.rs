//! Attaches a real window to the taskbar of the current session. Set DESKBAND11LIB_TASKBAR_TESTS=1 to run it.
#![cfg(windows)]

use std::cell::RefCell;
use std::ffi::c_void;
use std::rc::Rc;
use std::time::{Duration, Instant};

use deskband11lib::{HostEvent, TaskbarContentHost, TaskbarContentHostOptions, TaskbarEdge};

const PER_MONITOR_AWARE_V2: isize = -4;
const POPUP_WINDOW_STYLE: u32 = 0x8000_0000;
const TOOL_WINDOW_EXTENDED_STYLE: u32 = 0x0000_0080;
const NO_ACTIVATE_EXTENDED_STYLE: u32 = 0x0800_0000;
const ALL_INPUT_QUEUE_STATUS: u32 = 0x04FF;
const REMOVE_MESSAGE: u32 = 0x0001;

#[repr(C)]
#[derive(Default)]
struct Message {
    window_handle: isize,
    message: u32,
    w_parameter: usize,
    l_parameter: isize,
    time: u32,
    point_x: i32,
    point_y: i32,
}

#[link(name = "user32")]
unsafe extern "system" {
    fn SetProcessDpiAwarenessContext(value: isize) -> i32;
    fn CreateWindowExW(
        extended_style: u32,
        class_name: *const u16,
        window_name: *const u16,
        style: u32,
        x: i32,
        y: i32,
        width: i32,
        height: i32,
        parent: isize,
        menu: isize,
        instance: isize,
        parameter: *mut c_void,
    ) -> isize;
    fn DestroyWindow(window_handle: isize) -> i32;
    fn GetParent(window_handle: isize) -> isize;
    fn FindWindowW(class_name: *const u16, window_name: *const u16) -> isize;
    fn MsgWaitForMultipleObjects(
        count: u32,
        handles: *const isize,
        wait_all: i32,
        milliseconds: u32,
        wake_mask: u32,
    ) -> u32;
    fn PeekMessageW(
        message: *mut Message,
        window_handle: isize,
        minimum_message: u32,
        maximum_message: u32,
        remove: u32,
    ) -> i32;
    fn TranslateMessage(message: *const Message) -> i32;
    fn DispatchMessageW(message: *const Message) -> isize;
}

fn wide(text: &str) -> Vec<u16> {
    text.encode_utf16().chain(Some(0)).collect()
}

fn pump_messages_until(mut condition: impl FnMut() -> bool) -> bool {
    let deadline = Instant::now() + Duration::from_secs(20);
    let mut message = Message::default();
    while !condition() {
        let Some(remaining) = deadline.checked_duration_since(Instant::now()) else {
            return false;
        };
        unsafe {
            MsgWaitForMultipleObjects(
                0,
                std::ptr::null(),
                0,
                remaining.as_millis().max(1) as u32,
                ALL_INPUT_QUEUE_STATUS,
            );
            while PeekMessageW(&mut message, 0, 0, 0, REMOVE_MESSAGE) != 0 {
                TranslateMessage(&message);
                DispatchMessageW(&message);
            }
        }
    }
    true
}

#[test]
fn a_window_attaches_and_can_be_released_from_an_event_handler() {
    if std::env::var("DESKBAND11LIB_TASKBAR_TESTS").as_deref() != Ok("1") {
        eprintln!("Skipped: attaches a window to the taskbar of the current session.");
        return;
    }

    unsafe { SetProcessDpiAwarenessContext(PER_MONITOR_AWARE_V2) };
    let window_handle = unsafe {
        CreateWindowExW(
            NO_ACTIVATE_EXTENDED_STYLE | TOOL_WINDOW_EXTENDED_STYLE,
            wide("STATIC").as_ptr(),
            wide("deskband11lib").as_ptr(),
            POPUP_WINDOW_STYLE,
            0,
            0,
            120,
            40,
            0,
            0,
            0,
            std::ptr::null_mut(),
        )
    };
    assert_ne!(window_handle, 0);
    let taskbar_window = unsafe { FindWindowW(wide("Shell_TrayWnd").as_ptr(), std::ptr::null()) };
    let options = TaskbarContentHostOptions {
        preferred_width: Some(120.0),
        ..Default::default()
    };

    let events = Rc::new(RefCell::new(Vec::new()));
    let host = TaskbarContentHost::new(window_handle, &options).unwrap();
    let recorded_events = Rc::clone(&events);
    host.set_event_handler(move |event| recorded_events.borrow_mut().push(event.clone()));
    host.attach_when_layout_ready().unwrap();

    assert!(pump_messages_until(|| events
        .borrow()
        .contains(&HostEvent::Attached)));
    assert!(host.is_attached().unwrap());
    assert_ne!(host.taskbar_edge().unwrap(), TaskbarEdge::Unknown);
    assert!(
        events
            .borrow()
            .iter()
            .any(|event| matches!(event, HostEvent::TaskbarEdgeChanged(_)))
    );
    assert_eq!(unsafe { GetParent(window_handle) }, taskbar_window);

    host.close().unwrap();
    assert!(host.is_closed());
    assert_eq!(unsafe { GetParent(window_handle) }, 0);

    // Dropping a host from its own event handler must be safe.
    let host_slot = Rc::new(RefCell::new(Some(
        TaskbarContentHost::new(window_handle, &options).unwrap(),
    )));
    let handler_slot = Rc::clone(&host_slot);
    host_slot
        .borrow()
        .as_ref()
        .unwrap()
        .set_event_handler(move |event| {
            if matches!(event, HostEvent::ContentBoundsChanged { .. }) {
                handler_slot.borrow_mut().take();
            }
        });
    host_slot.borrow().as_ref().unwrap().attach().unwrap();

    assert!(pump_messages_until(|| host_slot.borrow().is_none()));
    assert_eq!(unsafe { GetParent(window_handle) }, 0);
    unsafe { DestroyWindow(window_handle) };
}
