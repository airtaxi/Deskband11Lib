from __future__ import annotations

from enum import IntEnum
from typing import TypeVar

_EnumType = TypeVar("_EnumType", bound=IntEnum)


class ResultCode(IntEnum):
    OK = 0
    NO_EVENT = 1
    INVALID_ARGUMENT = -1
    INVALID_HANDLE = -2
    WRONG_THREAD = -3
    INVALID_STATE = -4
    UNSUPPORTED_DPI_AWARENESS = -5
    WIN32_ERROR = -6
    UNEXPECTED = -100


class TaskbarContentPlacement(IntEnum):
    AUTO = 0
    LEFT_EDGE = 1
    BEFORE_NOTIFICATION_AREA = 2
    BEFORE_START_BUTTON = 3


class Easing(IntEnum):
    LINEAR = 0
    SINE_IN = 1
    SINE_OUT = 2
    SINE_IN_OUT = 3
    QUADRATIC_IN = 4
    QUADRATIC_OUT = 5
    QUADRATIC_IN_OUT = 6
    CUBIC_IN = 7
    CUBIC_OUT = 8
    CUBIC_IN_OUT = 9
    QUARTIC_IN = 10
    QUARTIC_OUT = 11
    QUARTIC_IN_OUT = 12
    QUINTIC_IN = 13
    QUINTIC_OUT = 14
    QUINTIC_IN_OUT = 15
    EXPONENTIAL_IN = 16
    EXPONENTIAL_OUT = 17
    EXPONENTIAL_IN_OUT = 18
    CIRCLE_IN = 19
    CIRCLE_OUT = 20
    CIRCLE_IN_OUT = 21


class TaskbarEdge(IntEnum):
    UNKNOWN = 0
    LEFT = 1
    TOP = 2
    RIGHT = 3
    BOTTOM = 4

    @property
    def is_vertical(self) -> bool:
        return self in (TaskbarEdge.LEFT, TaskbarEdge.RIGHT)


class TaskbarAlignment(IntEnum):
    UNKNOWN = 0
    LEFT = 1
    CENTER = 2


class HostEventKind(IntEnum):
    NONE = 0
    ATTACHED = 1
    ATTACH_FAILED = 2
    TASKBAR_WINDOW_RECREATED = 3
    TASKBAR_WINDOW_DISAPPEARED = 4
    TASKBAR_DPI_CHANGED = 5
    TASKBAR_WINDOW_RECREATION_REQUIRED = 6
    TASKBAR_EDGE_CHANGED = 7
    CONTENT_BOUNDS_CHANGED = 8
    ERROR = 9


def to_enum(enum_type: type[_EnumType], value: int, default: _EnumType | int) -> _EnumType | int:
    """Converts a value reported by the native library, keeping values added by newer library versions usable."""
    try:
        return enum_type(value)
    except ValueError:
        return default
