import { EventEmitter } from 'node:events';
import { app, type BrowserWindow, type BrowserWindowConstructorOptions } from 'electron';

import { TaskbarContentHost, toWindowHandle, type HostEvent } from './host';
import type { TaskbarContentHostOptions } from './options';

const DEFAULT_RECREATION_DELAY = 1000;
const OFF_SCREEN_POSITION = -32000;

/** Returns the native window handle (HWND) of a BrowserWindow. */
export function getWindowHandle(window: BrowserWindow): bigint {
    return toWindowHandle(window.getNativeWindowHandle());
}

/**
 * Adds the BrowserWindow options a taskbar window needs: hidden until attached, no frame, no taskbar button
 * or Alt+Tab entry, and no resizing, snapping or rounded corners. Pass the result to new BrowserWindow().
 */
export function createTaskbarWindowOptions(options: BrowserWindowConstructorOptions = {}): BrowserWindowConstructorOptions {
    return {
        ...options,
        type: 'toolbar',
        show: false,
        frame: false,
        thickFrame: false,
        skipTaskbar: true,
        resizable: false,
        minimizable: false,
        maximizable: false,
        fullscreenable: false,
        roundedCorners: false,
        hasShadow: false,
    };
}

export interface ElectronTaskbarWindowControllerOptions extends TaskbarContentHostOptions {
    /** Milliseconds to wait for the taskbar before the window is created again. Defaults to 1000. */
    recreationDelay?: number;
}

export interface ElectronTaskbarWindowControllerEventMap {
    /** A window was created, before it is attached. */
    windowCreated: [window: BrowserWindow];
    /** The window was attached to the taskbar. */
    attached: [event: HostEvent];
    /** The window could not be attached. It has been hidden. */
    attachFailed: [event: HostEvent];
    /** The taskbar moved to another screen edge, including the first layout after attaching. */
    taskbarEdgeChanged: [event: HostEvent];
    layoutError: [event: HostEvent];
}

/**
 * Creates a BrowserWindow, hosts it in the taskbar, and creates it again whenever the taskbar requires it.
 *
 * The taskbar destroys the hosted window when Explorer restarts or when the monitor it sits on is
 * disconnected, and the window is detached when the system DPI changes. In each case the controller releases
 * the host, waits recreationDelay milliseconds for the taskbar to settle, then creates a new window and host.
 *
 * createWindow must return a new, hidden BrowserWindow built with createTaskbarWindowOptions(). Do not show,
 * move or resize the window yourself. While the controller runs, it handles window-all-closed so that Electron
 * does not quit when the taskbar destroys the window.
 */
export class ElectronTaskbarWindowController extends EventEmitter<ElectronTaskbarWindowControllerEventMap> {
    readonly #createWindow: () => BrowserWindow;
    readonly #options: ElectronTaskbarWindowControllerOptions;
    #window: BrowserWindow | null = null;
    #host: TaskbarContentHost | null = null;
    #recreationTimer: NodeJS.Timeout | null = null;
    #isRunning = false;

    constructor(createWindow: () => BrowserWindow, options: ElectronTaskbarWindowControllerOptions = {}) {
        super();
        this.#createWindow = createWindow;
        this.#options = options;
    }

    /** The current window, or null while it is being created again. */
    get window(): BrowserWindow | null {
        return this.#window;
    }

    get host(): TaskbarContentHost | null {
        return this.#host;
    }

    /** True between the release of a window and the creation of its replacement. */
    get isRecreating(): boolean {
        return this.#recreationTimer !== null;
    }

    start(): void {
        if (this.#isRunning) return;

        this.#isRunning = true;
        app.on('window-all-closed', keepApplicationRunning);
        this.#create();
    }

    stop(): void {
        this.#isRunning = false;
        if (this.#recreationTimer !== null) clearTimeout(this.#recreationTimer);
        this.#recreationTimer = null;
        app.removeListener('window-all-closed', keepApplicationRunning);
        destroyWindow(this.#release());
    }

    #create(): void {
        const window = this.#createWindow();
        let host: TaskbarContentHost;
        try {
            host = new TaskbarContentHost(getWindowHandle(window), this.#options);
        } catch (error) {
            destroyWindow(window);
            throw error;
        }

        host.on('attached', event => this.emit('attached', event));
        host.on('attachFailed', event => this.#onHostAttachFailed(window, event));
        host.on('taskbarEdgeChanged', event => this.emit('taskbarEdgeChanged', event));
        host.on('layoutError', event => this.emit('layoutError', event));
        host.on('taskbarWindowRecreationRequired', () => this.#scheduleRecreation());
        this.#window = window;
        this.#host = host;
        this.emit('windowCreated', window);
        void this.#showAndAttach(window, host);
    }

    // Chromium sets up the surface a window renders into when the window is first shown. A window first shown
    // after it became a child of the taskbar displays only its background color, and a transparent window
    // shown after attaching is moved back to the screen position Chromium remembers. The window is therefore
    // shown off-screen while it is still a top-level window, and attached afterwards.
    async #showAndAttach(window: BrowserWindow, host: TaskbarContentHost): Promise<void> {
        window.setPosition(OFF_SCREEN_POSITION, OFF_SCREEN_POSITION);
        window.showInactive();

        // A failure is reported through attachFailed.
        await host.attachWhenLayoutReady().catch(() => undefined);
    }

    #onHostAttachFailed(window: BrowserWindow, event: HostEvent): void {
        if (!window.isDestroyed()) window.hide();
        this.emit('attachFailed', event);
    }

    #scheduleRecreation(): void {
        const previousWindow = this.#release();
        this.#recreationTimer = setTimeout(() => {
            this.#recreationTimer = null;
            if (this.#isRunning && this.#host === null) this.#create();
            destroyWindow(previousWindow);
        }, this.#options.recreationDelay ?? DEFAULT_RECREATION_DELAY);
    }

    #release(): BrowserWindow | null {
        const host = this.#host;
        const window = this.#window;
        this.#host = null;
        this.#window = null;
        // Hiding first keeps the detached window from appearing as a top-level window before it is destroyed.
        if (window !== null && !window.isDestroyed()) window.hide();
        host?.close();
        return window;
    }
}

function keepApplicationRunning(): void {}

function destroyWindow(window: BrowserWindow | null): void {
    if (window !== null && !window.isDestroyed()) window.destroy();
}
