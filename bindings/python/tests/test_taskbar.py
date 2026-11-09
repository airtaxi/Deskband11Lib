"""Tests that attach real windows to the taskbar of the current session. Set DESKBAND11LIB_TASKBAR_TESTS=1 to run them."""

import ctypes
import ctypes.wintypes as wintypes
import os
import threading
import time
from collections.abc import Callable, Iterator

import pytest

from deskband11lib import Deskband11LibError, HostEvent, ResultCode, TaskbarContentHost, TaskbarContentHostOptions, TaskbarEdge, enable_per_monitor_dpi_awareness

pytestmark = pytest.mark.skipif(os.environ.get("DESKBAND11LIB_TASKBAR_TESTS") != "1", reason="Attaches windows to the taskbar of the current session.")

user32 = ctypes.WinDLL("user32", use_last_error=True)
user32.CreateWindowExW.argtypes = [wintypes.DWORD, wintypes.LPCWSTR, wintypes.LPCWSTR, wintypes.DWORD, ctypes.c_int, ctypes.c_int, ctypes.c_int, ctypes.c_int, wintypes.HWND, wintypes.HMENU, wintypes.HINSTANCE, wintypes.LPVOID]
user32.CreateWindowExW.restype = wintypes.HWND
user32.DestroyWindow.argtypes = [wintypes.HWND]
user32.GetParent.argtypes = [wintypes.HWND]
user32.GetParent.restype = wintypes.HWND
user32.FindWindowW.argtypes = [wintypes.LPCWSTR, wintypes.LPCWSTR]
user32.FindWindowW.restype = wintypes.HWND
user32.MsgWaitForMultipleObjects.argtypes = [wintypes.DWORD, ctypes.c_void_p, wintypes.BOOL, wintypes.DWORD, wintypes.DWORD]
user32.PeekMessageW.argtypes = [ctypes.POINTER(wintypes.MSG), wintypes.HWND, wintypes.UINT, wintypes.UINT, wintypes.UINT]
user32.TranslateMessage.argtypes = [ctypes.POINTER(wintypes.MSG)]
user32.DispatchMessageW.argtypes = [ctypes.POINTER(wintypes.MSG)]

POPUP_WINDOW_STYLE = 0x80000000
TOOL_WINDOW_EXTENDED_STYLE = 0x00000080
NO_ACTIVATE_EXTENDED_STYLE = 0x08000000
ALL_INPUT_QUEUE_STATUS = 0x04FF
REMOVE_MESSAGE = 0x0001


def pump_messages_until(condition: Callable[[], bool], timeout: float = 20.0) -> bool:
    message = wintypes.MSG()
    deadline = time.monotonic() + timeout
    while not condition():
        remaining = deadline - time.monotonic()
        if remaining <= 0:
            return False
        user32.MsgWaitForMultipleObjects(0, None, False, max(1, int(remaining * 1000)), ALL_INPUT_QUEUE_STATUS)
        while user32.PeekMessageW(ctypes.byref(message), None, 0, 0, REMOVE_MESSAGE):
            user32.TranslateMessage(ctypes.byref(message))
            user32.DispatchMessageW(ctypes.byref(message))
    return True


@pytest.fixture
def window_handle() -> Iterator[int]:
    enable_per_monitor_dpi_awareness()
    handle = user32.CreateWindowExW(NO_ACTIVATE_EXTENDED_STYLE | TOOL_WINDOW_EXTENDED_STYLE, "STATIC", "deskband11lib", POPUP_WINDOW_STYLE, 0, 0, 120, 40, None, None, None, None)
    assert handle
    yield handle
    user32.DestroyWindow(handle)


def taskbar_window() -> int:
    return user32.FindWindowW("Shell_TrayWnd", None)


def test_attach_when_layout_ready_completes_on_the_host_thread(window_handle: int) -> None:
    with TaskbarContentHost(window_handle, TaskbarContentHostOptions(preferred_width=120)) as host:
        event_threads: list[int] = []
        edge_events: list[HostEvent] = []
        host.taskbar_edge_changed.connect(edge_events.append)
        host.attached.connect(lambda _: event_threads.append(threading.get_ident()))

        future = host.attach_when_layout_ready()
        assert pump_messages_until(future.done)

        assert future.result() is None
        assert event_threads == [threading.get_ident()]
        assert edge_events and edge_events[0].taskbar_edge is not TaskbarEdge.UNKNOWN
        assert host.is_attached
        assert user32.GetParent(window_handle) == taskbar_window()

    assert host.is_closed
    assert not user32.GetParent(window_handle)


def test_methods_reject_other_threads(window_handle: int) -> None:
    with TaskbarContentHost(window_handle) as host:
        errors: list[Deskband11LibError] = []

        def refresh_layout() -> None:
            try:
                host.refresh_layout()
            except Deskband11LibError as error:
                errors.append(error)

        worker = threading.Thread(target=refresh_layout)
        worker.start()
        worker.join()

        assert [error.result for error in errors] == [ResultCode.WRONG_THREAD]


def test_host_can_be_closed_from_an_event_handler(window_handle: int) -> None:
    host = TaskbarContentHost(window_handle, TaskbarContentHostOptions(preferred_width=120))
    host.content_bounds_changed.connect(lambda _: host.close())
    host.attach()

    assert pump_messages_until(lambda: host.is_closed)
    assert not user32.GetParent(window_handle)
