from __future__ import annotations

import ctypes
import dataclasses
import logging
from collections.abc import Callable
from concurrent.futures import Future

from ._bindings import EventCallback, NativeEvent, get_last_error_message, load_library
from .enums import HostEventKind, ResultCode, TaskbarAlignment, TaskbarEdge, to_enum
from .errors import Deskband11LibError, check_result
from .events import EventHook, HostEvent
from .options import TaskbarContentHostOptions

_logger = logging.getLogger("deskband11lib")


class TaskbarContentHost:
    """Places a top-level window inside the Windows 11 taskbar and keeps it aligned with the taskbar layout.

    A host belongs to the thread that creates it, and that thread must run a Win32 message loop, such as the
    main loop of Qt, Tk or wxPython. Call every method on that thread. Events are raised on the same thread
    from the message loop, never from inside another call to the host.

    The window must be per-monitor DPI aware. Create it hidden and without a frame, attach it, then show it
    without activating it. When taskbar_window_recreation_required is raised, close the host, create the
    window again and create a new host for it.

    Events:
        attached: attach_when_layout_ready() completed.
        attach_failed: attach_when_layout_ready() failed. The event carries the error message.
        taskbar_window_recreated: Explorer restarted and recreated the taskbar.
        taskbar_window_disappeared: The taskbar the window was placed on disappeared, for example because its monitor was disconnected.
        taskbar_dpi_changed: The system DPI changed. The window has been hidden and detached.
        taskbar_window_recreation_required: Raised together with each of the three events above.
        taskbar_edge_changed: The taskbar moved to another screen edge, including the first layout after attaching.
        content_bounds_changed: The window was given a new size.
        error: An unexpected error occurred while the host updated the layout.
    """

    def __init__(self, window_handle: int, options: TaskbarContentHostOptions | None = None) -> None:
        self._library = load_library()
        self._window_handle = int(window_handle)
        self._options = options if options is not None else TaskbarContentHostOptions()
        self._host_handle: int | None = None
        self._pending_attaches: list[Future[None]] = []
        self._event_callback = EventCallback(self._on_native_event)

        self.attached: EventHook[HostEvent] = EventHook()
        self.attach_failed: EventHook[HostEvent] = EventHook()
        self.taskbar_window_recreated: EventHook[HostEvent] = EventHook()
        self.taskbar_window_disappeared: EventHook[HostEvent] = EventHook()
        self.taskbar_dpi_changed: EventHook[HostEvent] = EventHook()
        self.taskbar_window_recreation_required: EventHook[HostEvent] = EventHook()
        self.taskbar_edge_changed: EventHook[HostEvent] = EventHook()
        self.content_bounds_changed: EventHook[HostEvent] = EventHook()
        self.error: EventHook[HostEvent] = EventHook()
        self._event_hooks = {
            HostEventKind.ATTACHED: self.attached,
            HostEventKind.ATTACH_FAILED: self.attach_failed,
            HostEventKind.TASKBAR_WINDOW_RECREATED: self.taskbar_window_recreated,
            HostEventKind.TASKBAR_WINDOW_DISAPPEARED: self.taskbar_window_disappeared,
            HostEventKind.TASKBAR_DPI_CHANGED: self.taskbar_dpi_changed,
            HostEventKind.TASKBAR_WINDOW_RECREATION_REQUIRED: self.taskbar_window_recreation_required,
            HostEventKind.TASKBAR_EDGE_CHANGED: self.taskbar_edge_changed,
            HostEventKind.CONTENT_BOUNDS_CHANGED: self.content_bounds_changed,
            HostEventKind.ERROR: self.error,
        }

        native_options = self._options.to_native()
        host_handle = ctypes.c_void_p()
        check_result(self._library.db11l_host_create(self._window_handle, ctypes.byref(native_options), self._event_callback, None, ctypes.byref(host_handle)))
        self._host_handle = host_handle.value

    def __enter__(self) -> TaskbarContentHost:
        return self

    def __exit__(self, *_: object) -> None:
        self.close()

    @property
    def window_handle(self) -> int:
        return self._window_handle

    @property
    def options(self) -> TaskbarContentHostOptions:
        return dataclasses.replace(self._options)

    @property
    def is_closed(self) -> bool:
        return self._host_handle is None

    @property
    def is_attached(self) -> bool:
        return self._read_value(self._library.db11l_host_is_attached) != 0

    @property
    def taskbar_edge(self) -> TaskbarEdge | int:
        """The edge the taskbar sits on. UNKNOWN until the first layout."""
        return to_enum(TaskbarEdge, self._read_value(self._library.db11l_host_get_taskbar_edge), TaskbarEdge.UNKNOWN)

    @property
    def is_taskbar_vertical(self) -> bool:
        return self.taskbar_edge in (TaskbarEdge.LEFT, TaskbarEdge.RIGHT)

    @property
    def taskbar_alignment(self) -> TaskbarAlignment | int:
        return to_enum(TaskbarAlignment, self._read_value(self._library.db11l_host_get_taskbar_alignment), TaskbarAlignment.UNKNOWN)

    def attach(self) -> None:
        """Attaches the window immediately. Raises Deskband11LibError when the taskbar window cannot be found."""
        check_result(self._library.db11l_host_attach(self._require_handle()))

    def attach_when_layout_ready(self) -> Future[None]:
        """Starts attaching once the taskbar layout can be measured and returns immediately.

        The returned future completes on the host thread when attached or attach_failed is raised.
        """
        host_handle = self._require_handle()
        future: Future[None] = Future()
        self._pending_attaches.append(future)
        try:
            check_result(self._library.db11l_host_attach_when_layout_ready(host_handle))
        except BaseException:
            self._pending_attaches.remove(future)
            raise
        return future

    def detach(self) -> None:
        """Restores the original parent and style of the window."""
        check_result(self._library.db11l_host_detach(self._require_handle()))

    def refresh_layout(self) -> None:
        """Recalculates the layout immediately instead of waiting for the next refresh."""
        check_result(self._library.db11l_host_refresh_layout(self._require_handle()))

    def close(self) -> None:
        """Detaches the window when attached and releases the host. Can be called from an event handler."""
        if self._host_handle is None:
            return

        check_result(self._library.db11l_host_destroy(self._host_handle))
        self._host_handle = None
        pending_attaches, self._pending_attaches = self._pending_attaches, []
        for future in pending_attaches:
            future.cancel()

    def _require_handle(self) -> int:
        if self._host_handle is None:
            raise Deskband11LibError(ResultCode.INVALID_HANDLE, "The host is closed.")
        return self._host_handle

    def _read_value(self, function: Callable[..., int]) -> int:
        value = ctypes.c_int32()
        check_result(function(self._require_handle(), ctypes.byref(value)))
        return value.value

    def _on_native_event(self, _user_data: object, native_event_pointer: ctypes._Pointer[NativeEvent]) -> None:
        # The native caller cannot receive Python exceptions, so failures are logged here.
        try:
            self._dispatch_event(self._create_event(native_event_pointer.contents))
        except Exception:
            _logger.exception("Failed to dispatch a taskbar host event.")

    @staticmethod
    def _create_event(native_event: NativeEvent) -> HostEvent:
        return HostEvent(
            kind=to_enum(HostEventKind, native_event.kind, native_event.kind),
            result=to_enum(ResultCode, native_event.result, native_event.result),
            taskbar_edge=to_enum(TaskbarEdge, native_event.taskbar_edge, TaskbarEdge.UNKNOWN),
            max_width=native_event.max_width,
            width=native_event.width,
            height=native_event.height,
            error_message=get_last_error_message() if native_event.result < 0 else None,
        )

    def _dispatch_event(self, event: HostEvent) -> None:
        event_hook = self._event_hooks.get(event.kind)
        if event_hook is not None:
            event_hook.emit(event)

        if event.kind not in (HostEventKind.ATTACHED, HostEventKind.ATTACH_FAILED):
            return

        pending_attaches, self._pending_attaches = self._pending_attaches, []
        for future in pending_attaches:
            if future.done():
                continue
            if event.kind == HostEventKind.ATTACHED:
                future.set_result(None)
            else:
                future.set_exception(Deskband11LibError(event.result, event.error_message or "Attaching to the taskbar failed."))


def is_window_alive(window_handle: int) -> bool:
    """Returns whether window_handle still refers to an existing window, for example after Explorer restarted."""
    return load_library().db11l_is_window_alive(int(window_handle)) != 0
