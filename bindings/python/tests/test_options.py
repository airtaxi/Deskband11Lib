import ctypes

import pytest

from deskband11lib import Deskband11LibError, ResultCode, TaskbarContentHost, TaskbarContentHostOptions
from deskband11lib._bindings import NativeOptions, load_library


def test_native_options_size_matches_header() -> None:
    assert ctypes.sizeof(NativeOptions) == 72


def test_default_options_match_native_defaults() -> None:
    native_defaults = NativeOptions()
    assert load_library().db11l_options_init(ctypes.byref(native_defaults), ctypes.sizeof(NativeOptions)) == 0

    converted_options = TaskbarContentHostOptions().to_native()
    for field_name, _ in NativeOptions._fields_:
        assert getattr(converted_options, field_name) == getattr(native_defaults, field_name), field_name


def test_invalid_option_is_reported_with_its_name() -> None:
    with pytest.raises(Deskband11LibError) as error_information:
        TaskbarContentHost(0, TaskbarContentHostOptions(preferred_width=0))

    assert error_information.value.result is ResultCode.INVALID_ARGUMENT
    assert "preferred_width" in str(error_information.value)


def test_missing_window_is_rejected() -> None:
    with pytest.raises(Deskband11LibError) as error_information:
        TaskbarContentHost(0)

    assert error_information.value.result is ResultCode.INVALID_ARGUMENT


def test_manual_slot_priority_is_range_checked() -> None:
    with pytest.raises(ValueError):
        TaskbarContentHostOptions(manual_slot_priority=70000).to_native()
