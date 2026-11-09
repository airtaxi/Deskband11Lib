from __future__ import annotations

from ._bindings import get_last_error_message
from .enums import ResultCode, to_enum


class Deskband11LibError(RuntimeError):
    """Raised when the native library reports an error. result holds the ResultCode of the failure."""

    def __init__(self, result: ResultCode | int, message: str) -> None:
        super().__init__(message)
        self.result = result


def check_result(result: int) -> int:
    if result < 0:
        raise Deskband11LibError(to_enum(ResultCode, result, result), get_last_error_message() or f"The native call failed with result {result}.")
    return result
