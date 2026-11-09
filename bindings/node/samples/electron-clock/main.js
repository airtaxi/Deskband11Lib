// A clock hosted in the Windows 11 taskbar with Electron. Right-click the clock to exit.
const path = require('node:path');
const { app, BrowserWindow, Menu, nativeTheme } = require('electron');

const { ElectronTaskbarWindowController, createTaskbarWindowOptions } = require('deskband11lib/electron');

function createClockWindow() {
    const window = new BrowserWindow(createTaskbarWindowOptions({ width: 150, height: 48, transparent: true, backgroundColor: '#00000000' }));
    window.webContents.on('did-finish-load', () => applyTaskbarTheme(window));
    window.webContents.on('context-menu', () => Menu.buildFromTemplate([{ label: 'Exit', click: () => app.quit() }]).popup({ window }));
    window.loadFile(path.join(__dirname, 'index.html'));
    return window;
}

// The taskbar follows the Windows mode, which can differ from the app mode the page sees through prefers-color-scheme.
function applyTaskbarTheme(window) {
    const theme = nativeTheme.shouldUseDarkColorsForSystemIntegratedUI ? 'dark' : 'light';
    window.webContents.executeJavaScript(`document.documentElement.dataset.theme = ${JSON.stringify(theme)};`);
}

app.whenReady().then(() => {
    const controller = new ElectronTaskbarWindowController(createClockWindow, { preferredWidth: 150 });
    controller.on('attachFailed', event => console.error(`The clock could not be attached to the taskbar: ${event.errorMessage}`));
    nativeTheme.on('updated', () => {
        if (controller.window !== null) applyTaskbarTheme(controller.window);
    });
    controller.start();
});
