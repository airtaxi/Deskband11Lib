"""ctypes declarations for deskband11lib.dll, mirroring include/deskband11lib.h."""

from __future__ import annotations

import ctypes
import functools
import os
import sysconfig
from pathlib import Path

ABI_VERSION = 1
LIBRARY_PATH_ENVIRONMENT_VARIABLE = "DESKBAND11LIB_LIBRARY_PATH"
_RUNTIME_IDENTIFIERS = {"win-amd64": "win-x64", "win-arm64": "win-arm64", "win32": "win-x86"}


class NativeOptions(ctypes.Structure):
    _fields_ = [
        ("struct_size", ctypes.c_uint32),
        ("animate_layout_changes", ctypes.c_int32),
        ("high_refresh_rate_mode", ctypes.c_int32),
        ("layout_animation_easing", ctypes.c_int32),
        ("track_taskbar_buttons", ctypes.c_int32),
        ("track_notification_area", ctypes.c_int32),
        ("preferred_monitor_identity", ctypes.c_int32),
        ("allow_fixed_slot_resize", ctypes.c_int32),
        ("placement", ctypes.c_int32),
        ("manual_slot_priority", ctypes.c_uint32),
        ("layout_animation_duration", ctypes.c_double),
        ("preferred_width", ctypes.c_double),
        ("preferred_height", ctypes.c_double),
        ("layout_refresh_interval", ctypes.c_double),
    ]


class NativeEvent(ctypes.Structure):
    _fields_ = [
        ("kind", ctypes.c_int32),
        ("result", ctypes.c_int32),
        ("taskbar_edge", ctypes.c_int32),
        ("reserved", ctypes.c_int32),
        ("max_width", ctypes.c_double),
        ("width", ctypes.c_double),
        ("height", ctypes.c_double),
    ]


EventCallback = ctypes.CFUNCTYPE(None, ctypes.c_void_p, ctypes.POINTER(NativeEvent))


def resolve_library_path() -> Path:
    override = os.environ.get(LIBRARY_PATH_ENVIRONMENT_VARIABLE)
    if override:
        return Path(override)

    platform = sysconfig.get_platform()
    runtime_identifier = _RUNTIME_IDENTIFIERS.get(platform)
    if runtime_identifier is None:
        raise OSError(f"deskband11lib does not support the {platform} platform.")
    return Path(__file__).resolve().parent / "bin" / runtime_identifier / "deskband11lib.dll"


@functools.cache
def load_library() -> ctypes.CDLL:
    path = resolve_library_path()
    if not path.is_file():
        raise OSError(f"The deskband11lib native library was not found at {path}.")

    library = ctypes.CDLL(str(path))
    _declare_functions(library)
    abi_version = library.db11l_get_abi_version()
    if abi_version != ABI_VERSION:
        raise OSError(f"{path} implements ABI version {abi_version}, but this package requires version {ABI_VERSION}.")
    return library


def get_last_error_message() -> str:
    library = load_library()
    size = library.db11l_get_last_error_message(None, 0)
    buffer = ctypes.create_string_buffer(size)
    library.db11l_get_last_error_message(buffer, size)
    return buffer.value.decode("utf-8", "replace")


def _declare_functions(library: ctypes.CDLL) -> None:
    host = ctypes.c_void_p
    int32_pointer = ctypes.POINTER(ctypes.c_int32)
    signatures = {
        "db11l_get_abi_version": (ctypes.c_uint32, []),
        "db11l_get_last_error_message": (ctypes.c_int32, [ctypes.c_char_p, ctypes.c_int32]),
        "db11l_options_init": (ctypes.c_int32, [ctypes.POINTER(NativeOptions), ctypes.c_uint32]),
        "db11l_host_create": (ctypes.c_int32, [ctypes.c_void_p, ctypes.POINTER(NativeOptions), EventCallback, ctypes.c_void_p, ctypes.POINTER(host)]),
        "db11l_host_destroy": (ctypes.c_int32, [host]),
        "db11l_host_attach": (ctypes.c_int32, [host]),
        "db11l_host_attach_when_layout_ready": (ctypes.c_int32, [host]),
        "db11l_host_detach": (ctypes.c_int32, [host]),
        "db11l_host_refresh_layout": (ctypes.c_int32, [host]),
        "db11l_host_poll_event": (ctypes.c_int32, [host, ctypes.POINTER(NativeEvent)]),
        "db11l_host_is_attached": (ctypes.c_int32, [host, int32_pointer]),
        "db11l_host_get_taskbar_edge": (ctypes.c_int32, [host, int32_pointer]),
        "db11l_host_get_taskbar_alignment": (ctypes.c_int32, [host, int32_pointer]),
        "db11l_is_window_alive": (ctypes.c_int32, [ctypes.c_void_p]),
    }
    for name, (result_type, argument_types) in signatures.items():
        function = getattr(library, name)
        function.restype = result_type
        function.argtypes = argument_types
