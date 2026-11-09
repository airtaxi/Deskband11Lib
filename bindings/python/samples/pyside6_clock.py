"""A clock hosted in the Windows 11 taskbar with PySide6. Right-click the clock to exit."""

from __future__ import annotations

import sys
import winreg

from PySide6.QtCore import QDateTime, Qt, QTimer
from PySide6.QtGui import QCursor
from PySide6.QtWidgets import QApplication, QBoxLayout, QLabel, QMenu, QWidget

from deskband11lib import HostEvent, TaskbarContentHostOptions
from deskband11lib.qt import QtTaskbarWidgetController

PERSONALIZE_KEY_PATH = r"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"
TWO_LINE_MINIMUM_HEIGHT = 40


def is_taskbar_light_theme() -> bool:
    try:
        with winreg.OpenKey(winreg.HKEY_CURRENT_USER, PERSONALIZE_KEY_PATH) as key:
            return winreg.QueryValueEx(key, "SystemUsesLightTheme")[0] != 0
    except OSError:
        return False


class ClockWidget(QWidget):
    def __init__(self) -> None:
        super().__init__()
        self.setAttribute(Qt.WidgetAttribute.WA_TranslucentBackground, True)
        self._is_taskbar_vertical = False
        self._is_light_theme: bool | None = None

        self._time_label = QLabel(alignment=Qt.AlignmentFlag.AlignCenter)
        self._time_label.setObjectName("time")
        self._date_label = QLabel(alignment=Qt.AlignmentFlag.AlignCenter)
        self._layout = QBoxLayout(QBoxLayout.Direction.TopToBottom, self)
        self._layout.setContentsMargins(6, 0, 6, 0)
        self._layout.addStretch()
        self._layout.addWidget(self._time_label)
        self._layout.addWidget(self._date_label)
        self._layout.addStretch()

        self._timer = QTimer(self)
        self._timer.timeout.connect(self._refresh)
        self._timer.start(1000)
        self._update_layout()
        self._refresh()

    def apply_taskbar_orientation(self, is_vertical: bool) -> None:
        self._is_taskbar_vertical = is_vertical
        self._update_layout()
        self._refresh()

    def resizeEvent(self, event) -> None:
        super().resizeEvent(event)
        self._update_layout()

    def contextMenuEvent(self, event) -> None:
        menu = QMenu(self)
        menu.addAction("Exit", QApplication.quit)
        # Qt does not know that the widget lives inside the taskbar, so the menu opens at the cursor instead of a mapped position.
        menu.exec(QCursor.pos())

    # A thin horizontal taskbar fits a single line only.
    def _update_layout(self) -> None:
        is_single_line = not self._is_taskbar_vertical and self.height() < TWO_LINE_MINIMUM_HEIGHT
        self._layout.setDirection(QBoxLayout.Direction.LeftToRight if is_single_line else QBoxLayout.Direction.TopToBottom)
        self._layout.setSpacing(8 if is_single_line else 0)

    def _refresh(self) -> None:
        now = QDateTime.currentDateTime()
        self._time_label.setText(now.toString("HH:mm" if self._is_taskbar_vertical else "HH:mm:ss"))
        self._date_label.setText(now.toString("MM-dd" if self._is_taskbar_vertical else "yyyy-MM-dd"))

        is_light_theme = is_taskbar_light_theme()
        if is_light_theme == self._is_light_theme:
            return

        self._is_light_theme = is_light_theme
        text_color = "#1A1A1A" if is_light_theme else "#FFFFFF"
        self.setStyleSheet(f"QLabel {{ color: {text_color}; font-family: 'Segoe UI Variable Text', 'Segoe UI'; font-size: 12px; }} QLabel#time {{ font-weight: 600; }}")


def main() -> int:
    application = QApplication(sys.argv)
    controller = QtTaskbarWidgetController(ClockWidget, TaskbarContentHostOptions(preferred_width=150))

    @controller.taskbar_edge_changed.connect
    def on_taskbar_edge_changed(event: HostEvent) -> None:
        controller.widget.apply_taskbar_orientation(event.taskbar_edge.is_vertical)

    @controller.attach_failed.connect
    def on_attach_failed(event: HostEvent) -> None:
        print(f"The clock could not be attached to the taskbar: {event.error_message}", file=sys.stderr)

    controller.start()
    exit_code = application.exec()
    controller.stop()
    return exit_code


if __name__ == "__main__":
    sys.exit(main())
