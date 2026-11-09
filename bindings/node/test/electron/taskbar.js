// Attaches real windows to the taskbar of the current session. Run with: npm run test:taskbar
const assert = require('node:assert/strict');
const { app, BrowserWindow } = require('electron');
const koffi = require('koffi');

const { HostEventKind, ResultCode, TaskbarEdge } = require('deskband11lib');
const { ElectronTaskbarWindowController, createTaskbarWindowOptions, getWindowHandle } = require('deskband11lib/electron');

const user32 = koffi.load('user32.dll');
const rectangleType = koffi.struct({ left: 'int32_t', top: 'int32_t', right: 'int32_t', bottom: 'int32_t' });
const findWindow = user32.func('FindWindowW', 'intptr_t', ['str16', 'str16']);
const getParent = user32.func('GetParent', 'intptr_t', ['intptr_t']);
const getWindowRect = user32.func('GetWindowRect', 'int32_t', ['intptr_t', koffi.out(koffi.pointer(rectangleType))]);

function readWindowRectangle(windowHandle) {
    const rectangle = {};
    getWindowRect(windowHandle, rectangle);
    return rectangle;
}

function waitFor(condition, timeout = 20000) {
    const deadline = Date.now() + timeout;
    return new Promise((resolve, reject) => {
        const check = () => {
            if (condition()) resolve();
            else if (Date.now() > deadline) reject(new Error('Timed out.'));
            else setTimeout(check, 20);
        };
        check();
    });
}

// A transparent window, because Chromium repositions transparent windows when they are shown.
function createTestWindow() {
    const window = new BrowserWindow(createTaskbarWindowOptions({ width: 120, height: 40, transparent: true, backgroundColor: '#00000000' }));
    window.loadURL('data:text/html,<body style="margin:0;background:%232f6f4f;color:white;font:12px sans-serif">deskband11lib</body>');
    return window;
}

async function testControllerAttachesAndRecreatesTheWindow() {
    const taskbarWindow = BigInt(findWindow('Shell_TrayWnd', null));
    const attachedEvents = [];
    const controller = new ElectronTaskbarWindowController(createTestWindow, { preferredWidth: 120, recreationDelay: 100 });
    controller.on('attached', event => attachedEvents.push(event));
    controller.start();
    try {
        await waitFor(() => attachedEvents.length === 1);
        const firstWindow = controller.window;
        const firstWindowHandle = getWindowHandle(firstWindow);
        assert.ok(firstWindow.isVisible());
        assert.equal(BigInt(getParent(firstWindowHandle)), taskbarWindow);

        // Showing the window must keep the size the taskbar layout applied, so the window stays inside the taskbar.
        await new Promise(resolve => setTimeout(resolve, 300));
        const windowRectangle = readWindowRectangle(firstWindowHandle);
        const taskbarRectangle = readWindowRectangle(taskbarWindow);
        console.log('window', windowRectangle, 'taskbar', taskbarRectangle);
        assert.ok(windowRectangle.top >= taskbarRectangle.top && windowRectangle.bottom <= taskbarRectangle.bottom, 'The window extends beyond the taskbar.');

        // Emitting the event the native library raises after Explorer restarts drives the same recreation path.
        controller.host.emit('taskbarWindowRecreationRequired', { kind: HostEventKind.TaskbarWindowRecreationRequired, result: ResultCode.Ok, taskbarEdge: TaskbarEdge.Bottom, maxWidth: 0, width: 0, height: 0 });
        assert.equal(controller.window, null);
        assert.ok(controller.isRecreating);

        await waitFor(() => attachedEvents.length === 2);
        assert.notEqual(controller.window, firstWindow);
        assert.ok(firstWindow.isDestroyed());
        assert.equal(BigInt(getParent(getWindowHandle(controller.window))), taskbarWindow);
    } finally {
        controller.stop();
    }

    assert.equal(controller.window, null);
    assert.equal(controller.host, null);
}

app.whenReady()
    .then(testControllerAttachesAndRecreatesTheWindow)
    .then(() => {
        console.log('PASS controller attaches and recreates the window');
        app.exit(0);
    }, error => {
        console.error(error);
        app.exit(1);
    });
