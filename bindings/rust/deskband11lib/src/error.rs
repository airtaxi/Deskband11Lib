use std::fmt;

use crate::enums::ResultCode;

/// An error from deskband11lib.
#[derive(Clone, Debug, PartialEq, Eq)]
#[non_exhaustive]
pub enum Error {
    /// The native library reported an error. Invalid options name the option in the message.
    Native { result: ResultCode, message: String },
    /// The native library could not be found or loaded.
    LibraryLoad(String),
    /// The host was already closed.
    Closed,
}

impl fmt::Display for Error {
    fn fmt(&self, formatter: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::Native { message, .. } => formatter.write_str(message),
            Self::LibraryLoad(message) => formatter.write_str(message),
            Self::Closed => formatter.write_str("The host is closed."),
        }
    }
}

impl std::error::Error for Error {}

pub type Result<T> = std::result::Result<T, Error>;
