#ifndef DESKBAND11LIB_H
#define DESKBAND11LIB_H

/*
 * Deskband11Lib C API
 *
 * Hosts a top-level window inside the Windows 11 taskbar.
 *
 * Threading
 *   A host belongs to the thread that creates it. Call every db11l_host_* function on that thread,
 *   and keep a Win32 message loop running on it (GetMessage and DispatchMessage, or the loop of a UI
 *   framework). Layout timers, layout animation and event delivery all run from that loop.
 *
 * Events
 *   Events arrive in order from the message loop, never from inside another db11l_* call. Pass a
 *   callback to receive them as they arrive, or pass NULL and call db11l_host_poll_event. Runtimes
 *   that must not be re-entered from the message loop should poll.
 *
 * Errors
 *   Negative results are errors. db11l_get_last_error_message describes the most recent error on the
 *   calling thread, including the error carried by an event with a negative result.
 *
 * Window
 *   The window must be a per-monitor DPI aware top-level window. Create it hidden and without a
 *   frame, attach it, then show it without activating it. When
 *   DB11L_EVENT_TASKBAR_WINDOW_RECREATION_REQUIRED arrives, destroy the host, recreate the window and
 *   create a new host for it.
 */

#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

#ifndef DB11L_API
#define DB11L_API __declspec(dllimport)
#endif

#define DB11L_CALL __cdecl

#define DB11L_ABI_VERSION 1u

typedef struct db11l_host_handle* db11l_host;

typedef enum db11l_result
{
    DB11L_OK = 0,
    DB11L_NO_EVENT = 1,
    DB11L_ERROR_INVALID_ARGUMENT = -1,
    DB11L_ERROR_INVALID_HANDLE = -2,
    DB11L_ERROR_WRONG_THREAD = -3,
    DB11L_ERROR_INVALID_STATE = -4,
    DB11L_ERROR_UNSUPPORTED_DPI_AWARENESS = -5,
    DB11L_ERROR_WIN32 = -6,
    DB11L_ERROR_UNEXPECTED = -100
} db11l_result;

typedef enum db11l_placement
{
    DB11L_PLACEMENT_AUTO = 0,
    DB11L_PLACEMENT_LEFT_EDGE = 1,
    DB11L_PLACEMENT_BEFORE_NOTIFICATION_AREA = 2,
    DB11L_PLACEMENT_BEFORE_START_BUTTON = 3
} db11l_placement;

typedef enum db11l_easing
{
    DB11L_EASING_LINEAR = 0,
    DB11L_EASING_SINE_IN = 1,
    DB11L_EASING_SINE_OUT = 2,
    DB11L_EASING_SINE_IN_OUT = 3,
    DB11L_EASING_QUADRATIC_IN = 4,
    DB11L_EASING_QUADRATIC_OUT = 5,
    DB11L_EASING_QUADRATIC_IN_OUT = 6,
    DB11L_EASING_CUBIC_IN = 7,
    DB11L_EASING_CUBIC_OUT = 8,
    DB11L_EASING_CUBIC_IN_OUT = 9,
    DB11L_EASING_QUARTIC_IN = 10,
    DB11L_EASING_QUARTIC_OUT = 11,
    DB11L_EASING_QUARTIC_IN_OUT = 12,
    DB11L_EASING_QUINTIC_IN = 13,
    DB11L_EASING_QUINTIC_OUT = 14,
    DB11L_EASING_QUINTIC_IN_OUT = 15,
    DB11L_EASING_EXPONENTIAL_IN = 16,
    DB11L_EASING_EXPONENTIAL_OUT = 17,
    DB11L_EASING_EXPONENTIAL_IN_OUT = 18,
    DB11L_EASING_CIRCLE_IN = 19,
    DB11L_EASING_CIRCLE_OUT = 20,
    DB11L_EASING_CIRCLE_IN_OUT = 21
} db11l_easing;

typedef enum db11l_taskbar_edge
{
    DB11L_TASKBAR_EDGE_UNKNOWN = 0,
    DB11L_TASKBAR_EDGE_LEFT = 1,
    DB11L_TASKBAR_EDGE_TOP = 2,
    DB11L_TASKBAR_EDGE_RIGHT = 3,
    DB11L_TASKBAR_EDGE_BOTTOM = 4
} db11l_taskbar_edge;

typedef enum db11l_taskbar_alignment
{
    DB11L_TASKBAR_ALIGNMENT_UNKNOWN = 0,
    DB11L_TASKBAR_ALIGNMENT_LEFT = 1,
    DB11L_TASKBAR_ALIGNMENT_CENTER = 2
} db11l_taskbar_alignment;

typedef enum db11l_event_kind
{
    DB11L_EVENT_NONE = 0,
    DB11L_EVENT_ATTACHED = 1,
    DB11L_EVENT_ATTACH_FAILED = 2,
    DB11L_EVENT_TASKBAR_WINDOW_RECREATED = 3,
    DB11L_EVENT_TASKBAR_WINDOW_DISAPPEARED = 4,
    DB11L_EVENT_TASKBAR_DPI_CHANGED = 5,
    DB11L_EVENT_TASKBAR_WINDOW_RECREATION_REQUIRED = 6,
    DB11L_EVENT_TASKBAR_EDGE_CHANGED = 7,
    DB11L_EVENT_CONTENT_BOUNDS_CHANGED = 8,
    DB11L_EVENT_ERROR = 9
} db11l_event_kind;

/*
 * Host options. Initialize with db11l_options_init before changing fields.
 * Boolean fields use 0 and 1. Sizes are in effective pixels.
 */
typedef struct db11l_options
{
    uint32_t struct_size;               /* Size of this structure in bytes. Set by db11l_options_init. */
    int32_t animate_layout_changes;     /* Animates position and size changes. */
    int32_t high_refresh_rate_mode;     /* Matches the animation timer to the monitor refresh rate. */
    int32_t layout_animation_easing;    /* db11l_easing. */
    int32_t track_taskbar_buttons;      /* Measures taskbar buttons through UI Automation. */
    int32_t track_notification_area;    /* Keeps content away from the notification area. */
    int32_t preferred_monitor_identity; /* 0 is the primary taskbar. 1 and above are secondary taskbars from left to right. */
    int32_t allow_fixed_slot_resize;    /* Lets fixed slots shrink proportionally when space runs out. */
    int32_t placement;                  /* db11l_placement. */
    uint32_t manual_slot_priority;      /* 0 to 65535. Lower values are placed first. 65535 orders by arrival. */
    double layout_animation_duration;   /* Milliseconds. */
    double preferred_width;             /* Length along the taskbar. DBL_MAX fills the space left by fixed slots. */
    double preferred_height;            /* Size across the taskbar, capped by its thickness. */
    double layout_refresh_interval;     /* Milliseconds between layout refreshes. */
} db11l_options;

typedef struct db11l_event
{
    int32_t kind;           /* db11l_event_kind. */
    int32_t result;         /* db11l_result. Negative for DB11L_EVENT_ATTACH_FAILED and DB11L_EVENT_ERROR. */
    int32_t taskbar_edge;   /* db11l_taskbar_edge when the event was raised. */
    int32_t reserved;
    double max_width;       /* DB11L_EVENT_CONTENT_BOUNDS_CHANGED: content bounds in effective pixels. */
    double width;
    double height;
} db11l_event;

typedef void (DB11L_CALL* db11l_event_callback)(void* user_data, const db11l_event* event);

/* Returns the ABI version of the loaded library. Compare it with DB11L_ABI_VERSION. */
DB11L_API uint32_t DB11L_CALL db11l_get_abi_version(void);

/*
 * Copies the UTF-8 message of the most recent error on the calling thread, including the terminating
 * zero, when buffer_size is large enough. Returns the required buffer size in bytes.
 */
DB11L_API int32_t DB11L_CALL db11l_get_last_error_message(char* buffer, int32_t buffer_size);

/* Fills options with default values. Pass sizeof(db11l_options) as options_size. */
DB11L_API int32_t DB11L_CALL db11l_options_init(db11l_options* options, uint32_t options_size);

/*
 * Creates a host for window_handle (HWND) on the calling thread. options may be NULL for default
 * values. callback may be NULL to receive events through db11l_host_poll_event instead.
 */
DB11L_API int32_t DB11L_CALL db11l_host_create(void* window_handle, const db11l_options* options, db11l_event_callback callback, void* user_data, db11l_host* host);

/* Detaches the window when attached and releases the host. Can be called from an event callback. */
DB11L_API int32_t DB11L_CALL db11l_host_destroy(db11l_host host);

/* Attaches the window immediately. Fails when the taskbar window cannot be found. */
DB11L_API int32_t DB11L_CALL db11l_host_attach(db11l_host host);

/*
 * Starts attaching once the taskbar layout can be measured and returns immediately.
 * The outcome arrives as DB11L_EVENT_ATTACHED or DB11L_EVENT_ATTACH_FAILED.
 */
DB11L_API int32_t DB11L_CALL db11l_host_attach_when_layout_ready(db11l_host host);

/* Restores the original parent and style of the window. */
DB11L_API int32_t DB11L_CALL db11l_host_detach(db11l_host host);

/* Recalculates the layout immediately instead of waiting for the next refresh. */
DB11L_API int32_t DB11L_CALL db11l_host_refresh_layout(db11l_host host);

/* Returns DB11L_OK and fills event, or DB11L_NO_EVENT when no event is pending. */
DB11L_API int32_t DB11L_CALL db11l_host_poll_event(db11l_host host, db11l_event* event);

DB11L_API int32_t DB11L_CALL db11l_host_is_attached(db11l_host host, int32_t* is_attached);

DB11L_API int32_t DB11L_CALL db11l_host_get_taskbar_edge(db11l_host host, int32_t* taskbar_edge);

DB11L_API int32_t DB11L_CALL db11l_host_get_taskbar_alignment(db11l_host host, int32_t* taskbar_alignment);

/* Returns 1 when window_handle refers to an existing window, otherwise 0. */
DB11L_API int32_t DB11L_CALL db11l_is_window_alive(void* window_handle);

#ifdef __cplusplus
}
#endif

#endif
