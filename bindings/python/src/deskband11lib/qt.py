"""Hosts PySide6 or PyQt6 widgets in the Windows 11 taskbar."""

from __future__ import annotations

import importlib
from collections.abc import Callable
from types import ModuleType
from typing import Any

from .events import EventHook, HostEvent
from .host import TaskbarContentHost
from .options import TaskbarContentHostOptions

_QT_BINDINGS = ("PySide6", "PyQt6")


def prepare_widget(widget: Any) -> int:
    """Turns a top-level widget into a frameless tool window and returns its native window handle.

    The taskbar layout decides the size of the widget, so the minimum size of the widget and of its layout is
    cleared; otherwise Qt would resize the window back whenever the taskbar gives it less room.

    Call this before the widget is shown. Qt creates a new native window when window flags change, so the
    flags must not change after the widget is attached.
    """
    qt = _import_qt_module(widget, "QtCore").Qt
    widget.setWindowFlags(widget.windowFlags() | qt.WindowType.FramelessWindowHint | qt.WindowType.Tool)
    widget.setAttribute(qt.WidgetAttribute.WA_ShowWithoutActivating, True)
    layout = widget.layout()
    if layout is not None:
        layout.setSizeConstraint(_import_qt_module(widget, "QtWidgets").QLayout.SizeConstraint.SetNoConstraint)
    widget.setMinimumSize(0, 0)
    return int(widget.winId())


class QtTaskbarWidgetController:
    """Creates a widget, hosts it in the taskbar, and creates it again whenever the taskbar requires it.

    The taskbar destroys the hosted window when Explorer restarts or when the monitor it sits on is
    disconnected, and the window is detached when the system DPI changes. In each case the controller
    releases the host, waits recreation_delay milliseconds for the taskbar to settle, then creates a new
    widget and host. The widget is shown once it is attached.

    start() turns off QGuiApplication.quitOnLastWindowClosed, because the widget is closed and created again
    while the taskbar recovers. Call start() and stop() on the Qt GUI thread.
    """

    def __init__(self, create_widget: Callable[[], Any], options: TaskbarContentHostOptions | None = None, recreation_delay: int = 1000) -> None:
        self._create_widget = create_widget
        self._options = options
        self._recreation_delay = recreation_delay
        self._widget: Any = None
        self._host: TaskbarContentHost | None = None
        self._content_size: tuple[int, int] | None = None
        self._is_running = False

        self.widget_created: EventHook[Any] = EventHook()
        self.attached: EventHook[HostEvent] = EventHook()
        self.attach_failed: EventHook[HostEvent] = EventHook()
        self.taskbar_edge_changed: EventHook[HostEvent] = EventHook()
        self.error: EventHook[HostEvent] = EventHook()

    @property
    def widget(self) -> Any:
        """The current widget, or None while it is being created again."""
        return self._widget

    @property
    def host(self) -> TaskbarContentHost | None:
        return self._host

    def start(self) -> None:
        if self._is_running:
            return

        self._is_running = True
        self._create()

    def stop(self) -> None:
        self._is_running = False
        _dispose_widget(self._release())

    def _create(self) -> None:
        widget = self._create_widget()
        try:
            _import_qt_module(widget, "QtGui").QGuiApplication.setQuitOnLastWindowClosed(False)
            host = TaskbarContentHost(prepare_widget(widget), self._options)
        except BaseException:
            _dispose_widget(widget)
            raise

        self._content_size = None
        host.content_bounds_changed.connect(self._on_host_content_bounds_changed)
        host.attached.connect(self._on_host_attached)
        host.attach_failed.connect(self.attach_failed.emit)
        host.taskbar_edge_changed.connect(self.taskbar_edge_changed.emit)
        host.error.connect(self.error.emit)
        host.taskbar_window_recreation_required.connect(self._on_host_taskbar_window_recreation_required)
        self._widget = widget
        self._host = host
        self.widget_created.emit(widget)
        host.attach_when_layout_ready()

    def _release(self) -> Any:
        host, widget = self._host, self._widget
        self._host = None
        self._widget = None
        # Hiding first keeps the detached window from appearing as a top-level window before it is closed.
        if widget is not None:
            widget.hide()
        if host is not None:
            host.close()
        return widget

    def _on_host_content_bounds_changed(self, event: HostEvent) -> None:
        self._content_size = (round(event.width), round(event.height))

    def _on_host_attached(self, event: HostEvent) -> None:
        widget = self._widget
        if widget is not None:
            # Qt fits a top-level widget that was never resized to its size hint when it is first shown, which would undo the size the taskbar layout applied.
            if self._content_size is not None:
                widget.resize(*self._content_size)
            widget.show()
        self.attached.emit(event)

    def _on_host_taskbar_window_recreation_required(self, _event: HostEvent) -> None:
        previous_widget = self._release()
        timer = _import_qt_module(previous_widget, "QtCore").QTimer
        timer.singleShot(self._recreation_delay, lambda: self._recreate(previous_widget))

    def _recreate(self, previous_widget: Any) -> None:
        if self._is_running and self._host is None:
            self._create()
        _dispose_widget(previous_widget)


def _dispose_widget(widget: Any) -> None:
    if widget is None:
        return

    widget.close()
    widget.deleteLater()


def _import_qt_module(widget: Any, module_name: str) -> ModuleType:
    for widget_type in type(widget).__mro__:
        binding = widget_type.__module__.split(".", 1)[0]
        if binding in _QT_BINDINGS:
            return importlib.import_module(f"{binding}.{module_name}")
    raise TypeError("The widget must be a PySide6 or PyQt6 QWidget.")
