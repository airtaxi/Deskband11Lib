"""Tests the Qt controller against the taskbar of the current session. Set DESKBAND11LIB_TASKBAR_TESTS=1 to run them.

DESKBAND11LIB_QT_BINDING selects PySide6 (the default) or PyQt6. The two bindings cannot share a process.
"""

import ctypes
import ctypes.wintypes as wintypes
import os
import time
from collections.abc import Callable

import pytest

QtWidgets = pytest.importorskip(os.environ.get("DESKBAND11LIB_QT_BINDING", "PySide6") + ".QtWidgets")

from deskband11lib import HostEvent, HostEventKind, ResultCode, TaskbarContentHostOptions, TaskbarEdge  # noqa: E402
from deskband11lib.qt import QtTaskbarWidgetController  # noqa: E402

pytestmark = pytest.mark.skipif(os.environ.get("DESKBAND11LIB_TASKBAR_TESTS") != "1", reason="Attaches windows to the taskbar of the current session.")

user32 = ctypes.WinDLL("user32")
user32.GetParent.argtypes = [wintypes.HWND]
user32.GetParent.restype = wintypes.HWND
user32.FindWindowW.argtypes = [wintypes.LPCWSTR, wintypes.LPCWSTR]
user32.FindWindowW.restype = wintypes.HWND


def process_events_until(condition: Callable[[], bool], timeout: float = 20.0) -> bool:
    deadline = time.monotonic() + timeout
    while not condition():
        if time.monotonic() > deadline:
            return False
        QtWidgets.QApplication.processEvents()
        time.sleep(0.01)
    return True


def test_controller_attaches_and_recreates_the_widget() -> None:
    application = QtWidgets.QApplication.instance() or QtWidgets.QApplication([])
    controller = QtTaskbarWidgetController(lambda: QtWidgets.QLabel("deskband11lib"), TaskbarContentHostOptions(preferred_width=120), recreation_delay=100)
    attached_events: list[HostEvent] = []
    controller.attached.connect(attached_events.append)
    taskbar_window = user32.FindWindowW("Shell_TrayWnd", None)

    controller.start()
    try:
        assert process_events_until(lambda: len(attached_events) == 1)
        first_widget = controller.widget
        assert first_widget.isVisible()
        assert user32.GetParent(int(first_widget.winId())) == taskbar_window
        assert not application.quitOnLastWindowClosed()

        # Raising the hook the native library raises after Explorer restarts drives the same recreation path.
        controller.host.taskbar_window_recreation_required.emit(HostEvent(HostEventKind.TASKBAR_WINDOW_RECREATION_REQUIRED, ResultCode.OK, TaskbarEdge.BOTTOM))
        assert controller.widget is None
        assert process_events_until(lambda: len(attached_events) == 2)
        assert controller.widget is not first_widget
        assert user32.GetParent(int(controller.widget.winId())) == taskbar_window
    finally:
        controller.stop()

    assert controller.widget is None
    assert controller.host is None
