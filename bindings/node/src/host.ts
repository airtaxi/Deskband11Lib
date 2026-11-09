import { EventEmitter } from 'node:events';

import { loadLibrary, getLastErrorMessage, type HostHandle, type NativeEvent, type NativeLibrary } from './bindings';
import { HostEventKind, ResultCode, TaskbarAlignment, TaskbarEdge, isVerticalTaskbarEdge } from './enums';
import { Deskband11LibError, checkResult } from './errors';
import { toNativeOptions, type TaskbarContentHostOptions } from './options';

const DEFAULT_EVENT_POLL_INTERVAL = 50;

/** A native window handle (HWND), or the buffer returned by BrowserWindow.getNativeWindowHandle(). */
export type WindowHandle = number | bigint | Buffer;

/**
 * An event raised by a TaskbarContentHost.
 *
 * maxWidth, width and height are set for ContentBoundsChanged and are in effective pixels.
 * errorMessage is set when result is negative, for AttachFailed and Error.
 */
export interface HostEvent {
    kind: HostEventKind;
    result: ResultCode;
    taskbarEdge: TaskbarEdge;
    maxWidth: number;
    width: number;
    height: number;
    errorMessage?: string;
}

export interface TaskbarContentHostEventMap {
    /** attachWhenLayoutReady() completed. */
    attached: [event: HostEvent];
    /** attachWhenLayoutReady() failed. The event carries the error message. */
    attachFailed: [event: HostEvent];
    /** Explorer restarted and created the taskbar again. */
    taskbarWindowRecreated: [event: HostEvent];
    /** The taskbar the window sat on disappeared, for example because its monitor was disconnected. */
    taskbarWindowDisappeared: [event: HostEvent];
    /** The system DPI changed. The window has been hidden and detached. */
    taskbarDpiChanged: [event: HostEvent];
    /** Raised together with each of the three events above. One handler covers all of them. */
    taskbarWindowRecreationRequired: [event: HostEvent];
    /** The taskbar moved to another screen edge, including the first layout after attaching. */
    taskbarEdgeChanged: [event: HostEvent];
    /** The window received a new size. */
    contentBoundsChanged: [event: HostEvent];
    /** An unexpected error occurred while the layout was updated. */
    layoutError: [event: HostEvent];
}

type TaskbarContentHostEventName = keyof TaskbarContentHostEventMap;

const EVENT_NAMES: Partial<Record<HostEventKind, TaskbarContentHostEventName>> = {
    [HostEventKind.Attached]: 'attached',
    [HostEventKind.AttachFailed]: 'attachFailed',
    [HostEventKind.TaskbarWindowRecreated]: 'taskbarWindowRecreated',
    [HostEventKind.TaskbarWindowDisappeared]: 'taskbarWindowDisappeared',
    [HostEventKind.TaskbarDpiChanged]: 'taskbarDpiChanged',
    [HostEventKind.TaskbarWindowRecreationRequired]: 'taskbarWindowRecreationRequired',
    [HostEventKind.TaskbarEdgeChanged]: 'taskbarEdgeChanged',
    [HostEventKind.ContentBoundsChanged]: 'contentBoundsChanged',
    [HostEventKind.Error]: 'layoutError',
};

interface PendingAttach {
    resolve(): void;
    reject(error: Error): void;
}

/**
 * Places a top-level window inside the Windows 11 taskbar and keeps it aligned with the taskbar layout.
 *
 * A host belongs to the thread that creates it, and that thread must run a Win32 message loop. The main
 * process of Electron does. Events are read from the native library on a short interval and emitted on the
 * same thread.
 *
 * The window must be per-monitor DPI aware, which Electron windows are. Create it hidden and without a frame,
 * attach it, then show it without activating it. When taskbarWindowRecreationRequired is emitted, close the
 * host, create the window again and create a new host for it.
 */
export class TaskbarContentHost extends EventEmitter<TaskbarContentHostEventMap> {
    readonly #library: NativeLibrary;
    readonly #windowHandle: bigint;
    readonly #pollTimer: NodeJS.Timeout;
    #hostHandle: HostHandle | null;
    #pendingAttaches: PendingAttach[] = [];

    constructor(windowHandle: WindowHandle, options: TaskbarContentHostOptions = {}) {
        super();
        this.#library = loadLibrary();
        this.#windowHandle = toWindowHandle(windowHandle);

        const hostHandle: [HostHandle] = [0];
        checkResult(this.#library.createHost(this.#windowHandle, toNativeOptions(options), null, null, hostHandle));
        this.#hostHandle = hostHandle[0];

        this.#pollTimer = setInterval(() => this.#pollEvents(), options.eventPollInterval ?? DEFAULT_EVENT_POLL_INTERVAL);
        this.#pollTimer.unref();
    }

    get windowHandle(): bigint {
        return this.#windowHandle;
    }

    get isClosed(): boolean {
        return this.#hostHandle === null;
    }

    get isAttached(): boolean {
        return this.#readValue(this.#library.isHostAttached) !== 0;
    }

    /** The edge the taskbar sits on. Unknown until the first layout. */
    get taskbarEdge(): TaskbarEdge {
        return this.#readValue(this.#library.getHostTaskbarEdge);
    }

    get isTaskbarVertical(): boolean {
        return isVerticalTaskbarEdge(this.taskbarEdge);
    }

    get taskbarAlignment(): TaskbarAlignment {
        return this.#readValue(this.#library.getHostTaskbarAlignment);
    }

    /** Attaches the window immediately. Throws Deskband11LibError when the taskbar window cannot be found. */
    attach(): void {
        checkResult(this.#library.attachHost(this.#requireHandle()));
    }

    /** Starts attaching once the taskbar layout can be measured. The promise settles when attached or attachFailed is emitted. */
    attachWhenLayoutReady(): Promise<void> {
        const hostHandle = this.#requireHandle();
        return new Promise((resolve, reject) => {
            const pendingAttach: PendingAttach = { resolve, reject };
            this.#pendingAttaches.push(pendingAttach);
            try {
                checkResult(this.#library.attachHostWhenLayoutReady(hostHandle));
            } catch (error) {
                this.#pendingAttaches = this.#pendingAttaches.filter(candidate => candidate !== pendingAttach);
                reject(error);
            }
        });
    }

    /** Restores the original parent and style of the window. */
    detach(): void {
        checkResult(this.#library.detachHost(this.#requireHandle()));
    }

    /** Recalculates the layout immediately instead of waiting for the next refresh. */
    refreshLayout(): void {
        checkResult(this.#library.refreshHostLayout(this.#requireHandle()));
    }

    /** Detaches the window when attached and releases the host. Can be called from an event listener. */
    close(): void {
        if (this.#hostHandle === null) return;

        checkResult(this.#library.destroyHost(this.#hostHandle));
        this.#hostHandle = null;
        clearInterval(this.#pollTimer);
        this.#settlePendingAttaches(new Deskband11LibError(ResultCode.InvalidHandle, 'The host was closed before it attached.'));
    }

    #requireHandle(): HostHandle {
        if (this.#hostHandle === null) throw new Deskband11LibError(ResultCode.InvalidHandle, 'The host is closed.');
        return this.#hostHandle;
    }

    #readValue(readNativeValue: (hostHandle: HostHandle, value: [number]) => number): number {
        const value: [number] = [0];
        checkResult(readNativeValue(this.#requireHandle(), value));
        return value[0];
    }

    // Every pending event is emitted in one pass, so a burst does not wait for later intervals.
    #pollEvents(): void {
        while (this.#hostHandle !== null) {
            const nativeEvent = {} as NativeEvent;
            if (this.#library.pollHostEvent(this.#hostHandle, nativeEvent) !== ResultCode.Ok) return;
            this.#dispatchEvent(createHostEvent(nativeEvent));
        }
    }

    #dispatchEvent(event: HostEvent): void {
        const eventName = EVENT_NAMES[event.kind];
        if (eventName !== undefined) (this.emit as (eventName: TaskbarContentHostEventName, event: HostEvent) => boolean)(eventName, event);

        if (event.kind === HostEventKind.Attached) this.#settlePendingAttaches();
        else if (event.kind === HostEventKind.AttachFailed) this.#settlePendingAttaches(new Deskband11LibError(event.result, event.errorMessage ?? 'Attaching to the taskbar failed.'));
    }

    #settlePendingAttaches(error?: Error): void {
        const pendingAttaches = this.#pendingAttaches;
        this.#pendingAttaches = [];
        for (const pendingAttach of pendingAttaches) {
            if (error) pendingAttach.reject(error);
            else pendingAttach.resolve();
        }
    }
}

/** Returns whether windowHandle still refers to an existing window, for example after Explorer restarted. */
export function isWindowAlive(windowHandle: WindowHandle): boolean {
    return loadLibrary().isWindowAlive(toWindowHandle(windowHandle)) !== 0;
}

export function toWindowHandle(windowHandle: WindowHandle): bigint {
    if (!Buffer.isBuffer(windowHandle)) return BigInt(windowHandle);
    return windowHandle.length >= 8 ? windowHandle.readBigUInt64LE(0) : BigInt(windowHandle.readUInt32LE(0));
}

function createHostEvent(nativeEvent: NativeEvent): HostEvent {
    const event: HostEvent = { kind: nativeEvent.kind, result: nativeEvent.result, taskbarEdge: nativeEvent.taskbar_edge, maxWidth: nativeEvent.max_width, width: nativeEvent.width, height: nativeEvent.height };
    if (nativeEvent.result < 0) event.errorMessage = getLastErrorMessage();
    return event;
}
