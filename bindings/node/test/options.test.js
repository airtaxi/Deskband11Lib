const assert = require('node:assert/strict');
const { test } = require('node:test');

const { Deskband11LibError, ResultCode, STRETCH, TaskbarContentHost } = require('..');
const { loadLibrary } = require('../dist/bindings.js');
const { toWindowHandle } = require('../dist/host.js');
const { toNativeOptions } = require('../dist/options.js');

test('the options structure matches the size declared in the header', () => {
    assert.equal(loadLibrary().optionsSize, 72);
});

test('options that are left out keep the native defaults', () => {
    const nativeOptions = toNativeOptions({});

    assert.equal(nativeOptions.struct_size, 72);
    assert.equal(nativeOptions.preferred_width, 360);
    assert.equal(nativeOptions.preferred_height, 48);
    assert.equal(nativeOptions.layout_animation_easing, 20);
    assert.equal(nativeOptions.manual_slot_priority, 65535);
    assert.equal(nativeOptions.layout_refresh_interval, 500);
    assert.equal(nativeOptions.animate_layout_changes, 1);
});

test('options that are set replace the native defaults', () => {
    const nativeOptions = toNativeOptions({ preferredWidth: STRETCH, highRefreshRateMode: false, manualSlotPriority: 10 });

    assert.equal(nativeOptions.preferred_width, Number.MAX_VALUE);
    assert.equal(nativeOptions.high_refresh_rate_mode, 0);
    assert.equal(nativeOptions.manual_slot_priority, 10);
    assert.equal(nativeOptions.preferred_height, 48);
});

test('an invalid option is reported with its name', () => {
    assert.throws(() => new TaskbarContentHost(0, { preferredWidth: 0 }), error => error instanceof Deskband11LibError && error.result === ResultCode.InvalidArgument && error.message.includes('preferred_width'));
});

test('a missing window is rejected', () => {
    assert.throws(() => new TaskbarContentHost(0), error => error instanceof Deskband11LibError && error.result === ResultCode.InvalidArgument);
});

test('manualSlotPriority is range checked', () => {
    assert.throws(() => toNativeOptions({ manualSlotPriority: 70000 }), RangeError);
});

test('window handle buffers are read as little-endian integers', () => {
    assert.equal(toWindowHandle(Buffer.from([0x34, 0x12, 0, 0, 0, 0, 0, 0])), 0x1234n);
    assert.equal(toWindowHandle(Buffer.from([0x78, 0x56, 0, 0])), 0x5678n);
    assert.equal(toWindowHandle(0x9abc), 0x9abcn);
});
