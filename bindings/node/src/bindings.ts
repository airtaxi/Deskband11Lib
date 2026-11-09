// koffi declarations for deskband11lib.dll, mirroring include/deskband11lib.h.
import * as fs from 'node:fs';
import * as path from 'node:path';
import * as koffi from 'koffi';

export const ABI_VERSION = 1;
export const LIBRARY_PATH_ENVIRONMENT_VARIABLE = 'DESKBAND11LIB_LIBRARY_PATH';

export type HostHandle = number | bigint;

export interface NativeOptions {
    struct_size: number;
    animate_layout_changes: number;
    high_refresh_rate_mode: number;
    layout_animation_easing: number;
    track_taskbar_buttons: number;
    track_notification_area: number;
    preferred_monitor_identity: number;
    allow_fixed_slot_resize: number;
    placement: number;
    manual_slot_priority: number;
    layout_animation_duration: number;
    preferred_width: number;
    preferred_height: number;
    layout_refresh_interval: number;
}

export interface NativeEvent {
    kind: number;
    result: number;
    taskbar_edge: number;
    reserved: number;
    max_width: number;
    width: number;
    height: number;
}

export interface NativeLibrary {
    readonly optionsSize: number;
    getAbiVersion(): number;
    getLastErrorMessage(buffer: Buffer | null, bufferSize: number): number;
    initializeOptions(options: NativeOptions, optionsSize: number): number;
    createHost(windowHandle: bigint, options: NativeOptions, callback: null, userData: null, hostHandle: [HostHandle]): number;
    destroyHost(hostHandle: HostHandle): number;
    attachHost(hostHandle: HostHandle): number;
    attachHostWhenLayoutReady(hostHandle: HostHandle): number;
    detachHost(hostHandle: HostHandle): number;
    refreshHostLayout(hostHandle: HostHandle): number;
    pollHostEvent(hostHandle: HostHandle, nativeEvent: NativeEvent): number;
    isHostAttached(hostHandle: HostHandle, value: [number]): number;
    getHostTaskbarEdge(hostHandle: HostHandle, value: [number]): number;
    getHostTaskbarAlignment(hostHandle: HostHandle, value: [number]): number;
    isWindowAlive(windowHandle: bigint): number;
}

// Anonymous structs keep koffi's global type registry free of names that a second copy of this package would collide with.
const nativeOptionsType = koffi.struct({
    struct_size: 'uint32_t',
    animate_layout_changes: 'int32_t',
    high_refresh_rate_mode: 'int32_t',
    layout_animation_easing: 'int32_t',
    track_taskbar_buttons: 'int32_t',
    track_notification_area: 'int32_t',
    preferred_monitor_identity: 'int32_t',
    allow_fixed_slot_resize: 'int32_t',
    placement: 'int32_t',
    manual_slot_priority: 'uint32_t',
    layout_animation_duration: 'double',
    preferred_width: 'double',
    preferred_height: 'double',
    layout_refresh_interval: 'double',
});

const nativeEventType = koffi.struct({
    kind: 'int32_t',
    result: 'int32_t',
    taskbar_edge: 'int32_t',
    reserved: 'int32_t',
    max_width: 'double',
    width: 'double',
    height: 'double',
});

let cachedLibrary: NativeLibrary | undefined;

export function resolveLibraryPath(): string {
    const overridePath = process.env[LIBRARY_PATH_ENVIRONMENT_VARIABLE];
    if (overridePath) return overridePath;
    if (process.platform !== 'win32') throw new Error(`deskband11lib supports Windows only, not ${process.platform}.`);

    const libraryPath = path.join(__dirname, '..', 'bin', `win32-${process.arch}`, 'deskband11lib.dll');
    // Windows cannot load a library from inside an asar archive, so packaged Electron apps load the unpacked copy.
    return libraryPath.replace(/\.asar([\\/])/, '.asar.unpacked$1');
}

export function loadLibrary(): NativeLibrary {
    if (cachedLibrary) return cachedLibrary;

    const libraryPath = resolveLibraryPath();
    if (!fs.existsSync(libraryPath)) throw new Error(`The deskband11lib native library was not found at ${libraryPath}.`);

    const library = koffi.load(libraryPath);
    const int32Output = koffi.out(koffi.pointer('int32_t'));
    const loadedLibrary: NativeLibrary = {
        optionsSize: koffi.sizeof(nativeOptionsType),
        getAbiVersion: library.func('db11l_get_abi_version', 'uint32_t', []),
        getLastErrorMessage: library.func('db11l_get_last_error_message', 'int32_t', ['uint8_t *', 'int32_t']),
        initializeOptions: library.func('db11l_options_init', 'int32_t', [koffi.out(koffi.pointer(nativeOptionsType)), 'uint32_t']),
        createHost: library.func('db11l_host_create', 'int32_t', ['intptr_t', koffi.pointer(nativeOptionsType), 'void *', 'void *', koffi.out(koffi.pointer('intptr_t'))]),
        destroyHost: library.func('db11l_host_destroy', 'int32_t', ['intptr_t']),
        attachHost: library.func('db11l_host_attach', 'int32_t', ['intptr_t']),
        attachHostWhenLayoutReady: library.func('db11l_host_attach_when_layout_ready', 'int32_t', ['intptr_t']),
        detachHost: library.func('db11l_host_detach', 'int32_t', ['intptr_t']),
        refreshHostLayout: library.func('db11l_host_refresh_layout', 'int32_t', ['intptr_t']),
        pollHostEvent: library.func('db11l_host_poll_event', 'int32_t', ['intptr_t', koffi.out(koffi.pointer(nativeEventType))]),
        isHostAttached: library.func('db11l_host_is_attached', 'int32_t', ['intptr_t', int32Output]),
        getHostTaskbarEdge: library.func('db11l_host_get_taskbar_edge', 'int32_t', ['intptr_t', int32Output]),
        getHostTaskbarAlignment: library.func('db11l_host_get_taskbar_alignment', 'int32_t', ['intptr_t', int32Output]),
        isWindowAlive: library.func('db11l_is_window_alive', 'int32_t', ['intptr_t']),
    };

    const abiVersion = loadedLibrary.getAbiVersion();
    if (abiVersion !== ABI_VERSION) throw new Error(`${libraryPath} implements ABI version ${abiVersion}, but this package requires version ${ABI_VERSION}.`);

    cachedLibrary = loadedLibrary;
    return loadedLibrary;
}

export function getLastErrorMessage(): string {
    const library = loadLibrary();
    const size = library.getLastErrorMessage(null, 0);
    const buffer = Buffer.alloc(size);
    library.getLastErrorMessage(buffer, size);
    const terminatorIndex = buffer.indexOf(0);
    return buffer.toString('utf8', 0, terminatorIndex < 0 ? buffer.length : terminatorIndex);
}
