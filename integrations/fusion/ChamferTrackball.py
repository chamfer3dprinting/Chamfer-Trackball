import adsk.core
import adsk.fusion
import json
import math
import os
import queue
import threading
import urllib.request

_BASE = "http://127.0.0.1:47613"
_EVENT = "ChamferTrackballCamera156"
_incoming = queue.Queue(maxsize=128)
_outgoing = queue.Queue(maxsize=128)
_stop = threading.Event()
_handlers = []
_thread = None
_app = None
_session = ""

def _put(q, value):
    try:
        q.put_nowait(value)
    except queue.Full:
        pass

def _network():
    while not _stop.is_set():
        try:
            with urllib.request.urlopen(_BASE + "/native/poll?app=fusion&pid=" + str(os.getpid()), timeout=0.5) as r:
                value = json.loads(r.read(32768))
            for command in value.get("commands", []):
                _put(_incoming, command)
            if not _incoming.empty():
                _app.fireCustomEvent(_EVENT)
            while not _outgoing.empty():
                message = _outgoing.get_nowait()
                message["id"] = value["id"]
                request = urllib.request.Request(_BASE + "/native/scene", data=json.dumps(message, allow_nan=False).encode(), headers={"Content-Type": "application/json"})
                with urllib.request.urlopen(request, timeout=0.5) as r:
                    r.read(4096)
        except (OSError, ValueError, KeyError, RuntimeError):
            pass
        _stop.wait(0.012)

def _point(values):
    return adsk.core.Point3D.create(*[float(x) for x in values])

def _vector(values):
    return adsk.core.Vector3D.create(*[float(x) for x in values])

def _begin(command):
    global _session
    viewport = _app.activeViewport
    if not viewport or not _app.activeDocument:
        return
    point = viewport.screenToView(adsk.core.Point2D.create(command.get("x", 0), command.get("y", 0)))
    if point.x < 0 or point.y < 0 or point.x >= viewport.width or point.y >= viewport.height:
        return
    camera = viewport.camera
    eye, target, up = camera.eye, camera.target, camera.upVector
    pivot = target
    ortho = camera.cameraType == adsk.core.CameraTypes.OrthographicCameraType
    try:
        sample = viewport.viewToModelSpace(point)
        direction = eye.vectorTo(target) if ortho else eye.vectorTo(sample)
        direction.normalize()
        origin = sample.copy() if ortho else eye.copy()
        if ortho:
            back = target.vectorTo(eye)
            back.normalize()
            distance = sample.vectorTo(eye).dotProduct(back)
            back.scaleBy(distance)
            origin.translateBy(back)
        design = adsk.fusion.Design.cast(_app.activeProduct)
        if design:
            hits = adsk.core.ObjectCollection.create()
            design.rootComponent.findBRepUsingRay(origin, direction, adsk.fusion.BRepEntityTypes.BRepFaceEntityType, -1.0, True, hits)
            if hits.count:
                pivot = hits.item(0)
            else:
                ui = _app.userInterface
                if ui.activeSelections.count:
                    entity = ui.activeSelections.item(0).entity
                    if hasattr(entity, "boundingBox"):
                        box = entity.boundingBox
                        pivot = adsk.core.Point3D.create((box.minPoint.x+box.maxPoint.x)/2, (box.minPoint.y+box.maxPoint.y)/2, (box.minPoint.z+box.maxPoint.z)/2)
    except (RuntimeError, AttributeError, TypeError):
        pass
    height = max(1e-6, eye.distanceTo(target))
    if ortho:
        try:
            height = camera.getExtents()[-1]
        except (AttributeError, RuntimeError, TypeError):
            height = max(1e-6, camera.viewExtents*2)
    fov = float(camera.perspectiveAngle) if not ortho else 0.785398
    if not 0.01 < fov < 3.12:
        fov = 0.785398
    _session = command["session"]
    _put(_outgoing, {"t": "scene", "session": _session, "eye": eye.asArray(), "target": target.asArray(), "up": up.asArray(), "pivot": pivot.asArray(), "verticalFov": fov, "orthoHeight": height, "width": viewport.width, "height": viewport.height, "orthographic": ortho})

def _apply(command):
    if command.get("session") != _session or not _app.activeViewport:
        return
    if not all(math.isfinite(float(x)) for name in ("eye", "target", "up") for x in command[name]):
        return
    viewport = _app.activeViewport
    camera = viewport.camera
    camera.eye = _point(command["eye"])
    camera.target = _point(command["target"])
    camera.upVector = _vector(command["up"])
    camera.isSmoothTransition = False
    if command.get("orthographic"):
        height = max(1e-6, float(command["orthoHeight"]))
        camera.setExtents(height*viewport.width/max(1, viewport.height), height)
    viewport.camera = camera
    viewport.refresh()

class _Handler(adsk.core.CustomEventHandler):
    def notify(self, args):
        global _session
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
                    _session = ""
            except (RuntimeError, ValueError, KeyError, AttributeError, TypeError):
                _session = ""

def run(context):
    global _app, _thread
    _app = adsk.core.Application.get()
    _stop.clear()
    event = _app.registerCustomEvent(_EVENT)
    handler = _Handler()
    event.add(handler)
    _handlers.append(handler)
    _thread = threading.Thread(target=_network, name="Chamfer Trackball", daemon=True)
    _thread.start()

def stop(context):
    global _thread, _session
    _stop.set()
    if _thread:
        _thread.join(timeout=2)
    if _app:
        _app.unregisterCustomEvent(_EVENT)
    _handlers.clear()
    _thread, _session = None, ""
