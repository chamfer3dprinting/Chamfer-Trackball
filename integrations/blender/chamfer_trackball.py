bl_info = {"name": "Chamfer Trackball", "author": "Chamfer3D", "version": (1, 5, 6), "blender": (4, 2, 0), "location": "3D View", "description": "Cursor pivot and direct camera navigation from Chamfer Trackball", "category": "3D View"}
import bpy
import json
import math
import os
import queue
import threading
import time
import urllib.request
from mathutils import Vector, Matrix
from bpy_extras import view3d_utils

_BASE = "http://127.0.0.1:47613"
_incoming = queue.Queue(maxsize=128)
_outgoing = queue.Queue(maxsize=128)
_stop = threading.Event()
_thread = None
_session = ""
_context = None

def _put(q, value):
    try:
        q.put_nowait(value)
    except queue.Full:
        pass

def _network():
    client = ""
    while not _stop.is_set():
        try:
            with urllib.request.urlopen(_BASE + "/native/poll?app=blender&pid=" + str(os.getpid()), timeout=0.5) as r:
                value = json.loads(r.read(32768))
            client = value["id"]
            for command in value.get("commands", []):
                _put(_incoming, command)
            while not _outgoing.empty():
                message = _outgoing.get_nowait()
                message["id"] = client
                data = json.dumps(message, allow_nan=False).encode()
                request = urllib.request.Request(_BASE + "/native/scene", data=data, headers={"Content-Type": "application/json"})
                with urllib.request.urlopen(request, timeout=0.5) as r:
                    r.read(4096)
        except (OSError, ValueError, KeyError):
            pass
        _stop.wait(0.012)

def _region_at(screen_x, screen_y):
    # All bpy scene/camera operations run on Blender's main thread.
    if os.name != "nt":
        return None
    import ctypes
    from ctypes import wintypes
    user = ctypes.WinDLL("user32", use_last_error=True)
    user.GetForegroundWindow.restype = wintypes.HWND
    hwnd = user.GetForegroundWindow()
    process = wintypes.DWORD()
    user.GetWindowThreadProcessId(hwnd, ctypes.byref(process))
    if process.value != os.getpid():
        return None
    point = wintypes.POINT(int(screen_x), int(screen_y))
    user.ScreenToClient(hwnd, ctypes.byref(point))
    for window in bpy.context.window_manager.windows:
        x, y = point.x, window.height - point.y
        for area in window.screen.areas:
            if area.type != "VIEW_3D":
                continue
            for region in area.regions:
                if region.type == "WINDOW" and region.x <= x < region.x + region.width and region.y <= y < region.y + region.height:
                    return window, area, region, area.spaces.active, (x-region.x, y-region.y)
    return None

def _begin(command):
    global _session, _context
    context = _region_at(command.get("x", 0), command.get("y", 0))
    if not context:
        _session = ""
        _context = None
        return
    window, area, region, space, coord = context
    rv = space.region_3d
    if rv is None or rv.view_perspective == "CAMERA":
        # Camera-view editing is deliberately not allowed to move a model camera.
        return
    _session = command["session"]
    _context = context
    rotation = rv.view_rotation
    target = rv.view_location.copy()
    back = rotation @ Vector((0, 0, 1))
    up = rotation @ Vector((0, 1, 0))
    eye = target + back * max(1e-6, rv.view_distance)
    pivot = target.copy()
    try:
        origin = view3d_utils.region_2d_to_origin_3d(region, rv, coord)
        direction = view3d_utils.region_2d_to_vector_3d(region, rv, coord)
        with bpy.context.temp_override(window=window, area=area, region=region):
            hit, location, normal, index, obj, matrix = bpy.context.scene.ray_cast(bpy.context.evaluated_depsgraph_get(), origin, direction)
        if hit:
            pivot = location
        elif bpy.context.selected_objects:
            obj = bpy.context.selected_objects[0]
            pivot = obj.matrix_world @ (sum((Vector(v) for v in obj.bound_box), Vector()) / 8)
    except (RuntimeError, AttributeError, ValueError):
        pass
    fov = 2 * math.atan(16 / max(1e-6, space.lens) * region.height / max(1, region.width))
    ortho = max(1e-6, 2 / max(1e-9, abs(rv.window_matrix[1][1])))
    _put(_outgoing, {"t": "scene", "session": _session, "eye": list(eye), "target": list(target), "up": list(up), "pivot": list(pivot), "verticalFov": fov, "orthoHeight": ortho, "width": region.width, "height": region.height, "orthographic": rv.view_perspective == "ORTHO"})

def _apply(command):
    if not _context or command.get("session") != _session:
        return
    window, area, region, space, coord = _context
    if area.type != "VIEW_3D":
        return
    eye, target, up = Vector(command["eye"]), Vector(command["target"]), Vector(command["up"])
    if not all(math.isfinite(v) for vector in (eye, target, up) for v in vector):
        return
    back = (eye-target).normalized()
    right = up.cross(back).normalized()
    up = back.cross(right).normalized()
    rv = space.region_3d
    previous_height = max(1e-6, 2 / max(1e-9, abs(rv.window_matrix[1][1])))
    distance = max(1e-6, (eye-target).length)
    if rv.view_perspective == "ORTHO":
        distance = rv.view_distance * float(command["orthoHeight"]) / previous_height
    rv.view_rotation = Matrix((right, up, back)).transposed().to_quaternion()
    rv.view_location = target
    rv.view_distance = max(1e-6, distance)
    area.tag_redraw()

def _tick():
    global _session, _context
    if _stop.is_set():
        return None
    for _ in range(128):
        try:
            command = _incoming.get_nowait()
        except queue.Empty:
            break
        try:
            if command.get("t") == "begin":
                _begin(command)
            elif command.get("t") == "camera":
                _apply(command)
            elif command.get("t") == "end" and command.get("session") == _session:
                _session, _context = "", None
        except (ReferenceError, RuntimeError, ValueError, KeyError, ZeroDivisionError):
            _session, _context = "", None
    return 0.016

def register():
    global _thread
    _stop.clear()
    if not bpy.app.timers.is_registered(_tick):
        bpy.app.timers.register(_tick, persistent=True)
    if _thread is None or not _thread.is_alive():
        _thread = threading.Thread(target=_network, name="Chamfer Trackball", daemon=True)
        _thread.start()

def unregister():
    global _thread, _session, _context
    _stop.set()
    if bpy.app.timers.is_registered(_tick):
        bpy.app.timers.unregister(_tick)
    if _thread:
        _thread.join(timeout=2)
    _thread, _session, _context = None, "", None
