"""Host Python UI windows inside the Windows 11 taskbar."""

from importlib.metadata import PackageNotFoundError, version

from .dpi import enable_per_monitor_dpi_awareness
from .enums import Easing, HostEventKind, ResultCode, TaskbarAlignment, TaskbarContentPlacement, TaskbarEdge
from .errors import Deskband11LibError
from .events import EventHook, HostEvent
from .host import TaskbarContentHost, is_window_alive
from .options import STRETCH, TaskbarContentHostOptions

try:
    __version__ = version("deskband11lib")
except PackageNotFoundError:
    __version__ = "0.0.0"

__all__ = [
    "STRETCH",
    "Deskband11LibError",
    "Easing",
    "EventHook",
    "HostEvent",
    "HostEventKind",
    "ResultCode",
    "TaskbarAlignment",
    "TaskbarContentHost",
    "TaskbarContentHostOptions",
    "TaskbarContentPlacement",
    "TaskbarEdge",
    "enable_per_monitor_dpi_awareness",
    "is_window_alive",
]
