from __future__ import annotations

import logging
from collections.abc import Callable
from dataclasses import dataclass
from typing import Generic, TypeVar

from .enums import HostEventKind, ResultCode, TaskbarEdge

_logger = logging.getLogger("deskband11lib")
_ValueType = TypeVar("_ValueType")


@dataclass(frozen=True)
class HostEvent:
    """An event raised by a TaskbarContentHost.

    max_width, width and height are set for CONTENT_BOUNDS_CHANGED and are in effective pixels.
    error_message is set when result is negative, for ATTACH_FAILED and ERROR.
    """

    kind: HostEventKind | int
    result: ResultCode | int
    taskbar_edge: TaskbarEdge | int
    max_width: float = 0.0
    width: float = 0.0
    height: float = 0.0
    error_message: str | None = None


class EventHook(Generic[_ValueType]):
    """Handlers called in the order they were connected. An exception from one handler is logged and does not stop the others."""

    def __init__(self) -> None:
        self._handlers: list[Callable[[_ValueType], object]] = []

    def connect(self, handler: Callable[[_ValueType], object]) -> Callable[[_ValueType], object]:
        """Adds a handler and returns it, so connect can also be used as a decorator."""
        self._handlers.append(handler)
        return handler

    def disconnect(self, handler: Callable[[_ValueType], object]) -> None:
        self._handlers.remove(handler)

    def emit(self, value: _ValueType) -> None:
        for handler in list(self._handlers):
            try:
                handler(value)
            except Exception:
                _logger.exception("An event handler raised an exception.")
