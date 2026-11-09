use std::fmt;

/// An error from the taskbar window controller.
#[derive(Debug)]
#[non_exhaustive]
pub enum Error {
    /// The taskbar host reported an error.
    Host(deskband11lib::Error),
    /// Tauri could not create or change the window.
    Tauri(tauri::Error),
}

impl fmt::Display for Error {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::Host(error) => error.fmt(formatter),
            Self::Tauri(error) => error.fmt(formatter),
        }
    }
}

impl std::error::Error for Error {
    fn source(&self) -> Option<&(dyn std::error::Error + 'static)> {
        match self {
            Self::Host(error) => Some(error),
            Self::Tauri(error) => Some(error),
        }
    }
}

impl From<deskband11lib::Error> for Error {
    fn from(error: deskband11lib::Error) -> Self {
        Self::Host(error)
    }
}

impl From<tauri::Error> for Error {
    fn from(error: tauri::Error) -> Self {
        Self::Tauri(error)
    }
}

pub type Result<T> = std::result::Result<T, Error>;
