// Declarations for deskband11lib.dll, mirroring include/deskband11lib.h.
use std::ffi::{c_char, c_void};
use std::path::{Path, PathBuf};
use std::sync::OnceLock;

use libloading::Library;

use crate::enums::ResultCode;
use crate::error::{Error, Result};

pub(crate) const ABI_VERSION: u32 = 1;
pub(crate) const LIBRARY_PATH_ENVIRONMENT_VARIABLE: &str = "DESKBAND11LIB_LIBRARY_PATH";
const LIBRARY_FILE_NAME: &str = "deskband11lib.dll";

#[repr(C)]
#[derive(Clone, Copy, Debug, Default)]
pub(crate) struct NativeOptions {
    pub struct_size: u32,
    pub animate_layout_changes: i32,
    pub high_refresh_rate_mode: i32,
    pub layout_animation_easing: i32,
    pub track_taskbar_buttons: i32,
    pub track_notification_area: i32,
    pub preferred_monitor_identity: i32,
    pub allow_fixed_slot_resize: i32,
    pub placement: i32,
    pub manual_slot_priority: u32,
    pub layout_animation_duration: f64,
    pub preferred_width: f64,
    pub preferred_height: f64,
    pub layout_refresh_interval: f64,
}

#[repr(C)]
#[derive(Clone, Copy, Debug, Default)]
pub(crate) struct NativeEvent {
    pub kind: i32,
    pub result: i32,
    pub taskbar_edge: i32,
    pub reserved: i32,
    pub max_width: f64,
    pub width: f64,
    pub height: f64,
}

pub(crate) type EventCallback =
    unsafe extern "C" fn(user_data: *mut c_void, event: *const NativeEvent);

pub(crate) struct NativeLibrary {
    _library: Library,
    pub get_last_error_message: unsafe extern "C" fn(buffer: *mut c_char, buffer_size: i32) -> i32,
    pub initialize_options:
        unsafe extern "C" fn(options: *mut NativeOptions, options_size: u32) -> i32,
    pub create_host: unsafe extern "C" fn(
        window_handle: isize,
        options: *const NativeOptions,
        callback: Option<EventCallback>,
        user_data: *mut c_void,
        host_handle: *mut isize,
    ) -> i32,
    pub destroy_host: unsafe extern "C" fn(host_handle: isize) -> i32,
    pub attach_host: unsafe extern "C" fn(host_handle: isize) -> i32,
    pub attach_host_when_layout_ready: unsafe extern "C" fn(host_handle: isize) -> i32,
    pub detach_host: unsafe extern "C" fn(host_handle: isize) -> i32,
    pub refresh_host_layout: unsafe extern "C" fn(host_handle: isize) -> i32,
    pub is_host_attached: unsafe extern "C" fn(host_handle: isize, value: *mut i32) -> i32,
    pub get_host_taskbar_edge: unsafe extern "C" fn(host_handle: isize, value: *mut i32) -> i32,
    pub get_host_taskbar_alignment:
        unsafe extern "C" fn(host_handle: isize, value: *mut i32) -> i32,
    pub is_window_alive: unsafe extern "C" fn(window_handle: isize) -> i32,
}

static LIBRARY: OnceLock<std::result::Result<NativeLibrary, String>> = OnceLock::new();

/// Loads the native library once per process.
///
/// The library is searched in the path given by `DESKBAND11LIB_LIBRARY_PATH`, then next to the executable.
/// Debug builds also fall back to the copy bundled with this crate.
pub(crate) fn load_library() -> Result<&'static NativeLibrary> {
    LIBRARY
        .get_or_init(load)
        .as_ref()
        .map_err(|message| Error::LibraryLoad(message.clone()))
}

pub(crate) fn check(library: &NativeLibrary, result: i32) -> Result<i32> {
    if result >= 0 {
        return Ok(result);
    }

    let message = last_error_message(library);
    let message = if message.is_empty() {
        format!("The native call failed with result {result}.")
    } else {
        message
    };
    Err(Error::Native {
        result: ResultCode::from_native(result),
        message,
    })
}

pub(crate) fn last_error_message(library: &NativeLibrary) -> String {
    let size = unsafe { (library.get_last_error_message)(std::ptr::null_mut(), 0) };
    if size <= 1 {
        return String::new();
    }

    let mut buffer = vec![0_u8; size as usize];
    unsafe { (library.get_last_error_message)(buffer.as_mut_ptr().cast(), size) };
    let end = buffer
        .iter()
        .position(|&byte| byte == 0)
        .unwrap_or(buffer.len());
    String::from_utf8_lossy(&buffer[..end]).into_owned()
}

fn load() -> std::result::Result<NativeLibrary, String> {
    let candidate_paths = candidate_paths();
    let mut failures = Vec::new();
    for path in &candidate_paths {
        if !path.is_file() {
            continue;
        }

        match unsafe { Library::new(path) } {
            Ok(library) => return bind(library, path),
            Err(error) => failures.push(format!("{}: {error}", path.display())),
        }
    }

    if failures.is_empty() {
        let searched_paths: Vec<String> = candidate_paths
            .iter()
            .map(|path| path.display().to_string())
            .collect();
        return Err(format!(
            "The deskband11lib native library was not found. Searched: {}.",
            searched_paths.join(", ")
        ));
    }
    Err(format!(
        "The deskband11lib native library could not be loaded. {}",
        failures.join(" ")
    ))
}

fn candidate_paths() -> Vec<PathBuf> {
    let mut paths = Vec::new();
    if let Some(path) = std::env::var_os(LIBRARY_PATH_ENVIRONMENT_VARIABLE) {
        paths.push(PathBuf::from(path));
    }
    if let Ok(executable_path) = std::env::current_exe()
        && let Some(executable_directory) = executable_path.parent()
    {
        paths.push(executable_directory.join(LIBRARY_FILE_NAME));
    }
    #[cfg(debug_assertions)]
    if let Some(path) = option_env!("DESKBAND11LIB_BUNDLED_LIBRARY_PATH") {
        paths.push(PathBuf::from(path));
    }
    paths
}

macro_rules! symbol {
    ($library:expr, $name:literal) => {
        *unsafe { $library.get(concat!($name, "\0").as_bytes()) }
            .map_err(|error| format!("{} is missing {}: {error}", LIBRARY_FILE_NAME, $name))?
    };
}

fn bind(library: Library, path: &Path) -> std::result::Result<NativeLibrary, String> {
    let get_abi_version: unsafe extern "C" fn() -> u32 = symbol!(library, "db11l_get_abi_version");
    let abi_version = unsafe { get_abi_version() };
    if abi_version != ABI_VERSION {
        return Err(format!(
            "{} implements ABI version {abi_version}, but this crate requires version {ABI_VERSION}.",
            path.display()
        ));
    }

    Ok(NativeLibrary {
        get_last_error_message: symbol!(library, "db11l_get_last_error_message"),
        initialize_options: symbol!(library, "db11l_options_init"),
        create_host: symbol!(library, "db11l_host_create"),
        destroy_host: symbol!(library, "db11l_host_destroy"),
        attach_host: symbol!(library, "db11l_host_attach"),
        attach_host_when_layout_ready: symbol!(library, "db11l_host_attach_when_layout_ready"),
        detach_host: symbol!(library, "db11l_host_detach"),
        refresh_host_layout: symbol!(library, "db11l_host_refresh_layout"),
        is_host_attached: symbol!(library, "db11l_host_is_attached"),
        get_host_taskbar_edge: symbol!(library, "db11l_host_get_taskbar_edge"),
        get_host_taskbar_alignment: symbol!(library, "db11l_host_get_taskbar_alignment"),
        is_window_alive: symbol!(library, "db11l_is_window_alive"),
        _library: library,
    })
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn structures_match_the_header() {
        assert_eq!(size_of::<NativeOptions>(), 72);
        assert_eq!(size_of::<NativeEvent>(), 40);
    }
}
