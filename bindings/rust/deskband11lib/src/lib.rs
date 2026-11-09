//! Hosts native windows inside the Windows 11 taskbar.
//!
//! [`TaskbarContentHost`] finds the free space between the taskbar buttons and the notification area, moves a
//! window there, and keeps it aligned when the taskbar layout changes, when the taskbar moves to another
//! screen edge, and when other hosted windows share the same area.
//!
//! The crate loads `deskband11lib.dll`, which it bundles for x64 and ARM64 and copies next to the executables
//! of the build. Ship the library next to your executable.

mod bindings;
mod enums;
mod error;
mod host;
mod options;

pub use enums::{Easing, ResultCode, TaskbarAlignment, TaskbarContentPlacement, TaskbarEdge};
pub use error::{Error, Result};
pub use host::{HostEvent, TaskbarContentHost, is_window_alive};
pub use options::{STRETCH, TaskbarContentHostOptions};
