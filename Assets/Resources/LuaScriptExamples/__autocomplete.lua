tool.startPosition = nil
tool.endPosition = nil
tool.vector = nil
unityMathf.deg2Rad = nil
unityMathf.epsilon = nil
unityMathf.infinity = nil
unityMathf.negativeInfinity = nil
unityMathf.pI = nil
unityMathf.rad2Deg = nil
function unityMathf.abs(f) end
function unityMathf.acos(f) end
function unityMathf.approximately(a, b) end
function unityMathf.asin(f) end
function unityMathf.atan(f) end
function unityMathf.atan2(y, x) end
function unityMathf.ceil(f) end
function unityMathf.clamp(value, min, max) end
function unityMathf.clamp01(value) end
function unityMathf.closestPowerOfTwo(value) end
function unityMathf.cos(f) end
function unityMathf.deltaAngle(current, target) end
function unityMathf.exp(power) end
function unityMathf.floor(f) end
function unityMathf.inverseLerp(a, b, value) end
function unityMathf.isPowerOfTwo(value) end
function unityMathf.lerp(a, b, t) end
function unityMathf.lerpAngle(a, b, t) end
function unityMathf.lerpUnclamped(a, b, t) end
function unityMathf.log(f, p) end
function unityMathf.log10(f) end
function unityMathf.max(a, b) end
function unityMathf.max(values) end
function unityMathf.min(a, b) end
function unityMathf.min(values) end
function unityMathf.moveTowards(current, target, maxDelta) end
function unityMathf.nextPowerOfTwo(value) end
function unityMathf.perlinNoise(x, y) end
function unityMathf.pingPong(t, length) end
function unityMathf.pow(f, p) end
function unityMathf.repeater(t, length) end
function unityMathf.round(f) end
function unityMathf.sign(f) end
function unityMathf.sin(f) end
function unityMathf.sqrt(f) end
function unityMathf.smoothStep(from, to, t) end
function unityMathf.tan(f) end
unityQuaternion.identity = nil
unityQuaternion.kEpsilon = nil
function unityQuaternion.angle(a, b) end
function unityQuaternion.angleAxis(angle, axis) end
function unityQuaternion.dot(a, b) end
function unityQuaternion.fromToRotation(from, to) end
function unityQuaternion.inverse(a) end
function unityQuaternion.lerp(a, b, t) end
function unityQuaternion.lerpUnclamped(a, b, t) end
function unityQuaternion.lookRotation(forward, up) end
function unityQuaternion.normalize(a) end
function unityQuaternion.rotateTowards(from, to, maxDegreesDelta) end
function unityQuaternion.slerp(a, b, t) end
function unityQuaternion.slerpUnclamped(a, b, t) end
unityVector3.back = nil
unityVector3.down = nil
unityVector3.forward = nil
unityVector3.left = nil
unityVector3.negativeInfinity = nil
unityVector3.one = nil
unityVector3.positiveInfinity = nil
unityVector3.right = nil
unityVector3.up = nil
unityVector3.zero = nil
function unityVector3.angle(a, b) end
function unityVector3.clampMagnitude(v, maxLength) end
function unityVector3.cross(a, b) end
function unityVector3.distance(a, b) end
function unityVector3.magnitude(a) end
function unityVector3.sqrMagnitude(a) end
function unityVector3.dot(a, b) end
function unityVector3.lerp(a, b, t) end
function unityVector3.lerpUnclamped(a, b, t) end
function unityVector3.max(a, b) end
function unityVector3.min(a, b) end
function unityVector3.moveTowards(current, target, maxDistanceDelta) end
function unityVector3.normalize(a) end
function unityVector3.project(a, b) end
function unityVector3.projectOnPlane(vector, planeNormal) end
function unityVector3.reflect(a, b) end
function unityVector3.rotateTowards(current, target, maxRadiansDelta, maxMagnitudeDelta) end
function unityVector3.scale(a, b) end
function unityVector3.signedAngle(from, to, axis) end
function unityVector3.slerp(a, b, t) end
function unityVector3.slerpUnclamped(a, b, t) end
app.time = nil
app.frames = nil
app.currentScale = nil
app.lastSelectedStroke = nil
app.lastStroke = nil
function app.undo() end
function app.redo() end
function app.addListener(a) end
function app.resetPanels() end
function app.showScriptsFolder() end
function app.showExportFolder() end
function app.showSketchesFolder(a) end
function app.straightEdge(a) end
function app.autoOrient(a) end
function app.viewOnly(a) end
function app.autoSimplify(a) end
function app.disco(a) end
function app.profiling(a) end
function app.postProcessing(a) end
function app.draftingVisible() end
function app.draftingTransparent() end
function app.draftingHidden() end
function app.setEnvironment(environmentName) end
function app.watermark(a) end
brush.timeSincePressed = nil
brush.timeSinceReleased = nil
brush.triggerIsPressed = nil
brush.triggerIsPressedThisFrame = nil
brush.distanceMoved = nil
brush.distanceDrawn = nil
brush.position = nil
brush.rotation = nil
brush.direction = nil
brush.size = nil
brush.size01 = nil
brush.pressure = nil
brush.name = nil
brush.speed = nil
brush.color = nil
brush.lastColorPicked = nil
brush.colorHsv = nil
brush.lastColorPickedHsv = nil
function brush.type(brushName) end
function brush.pastPosition(back) end
function brush.pastRotation(back) end
function brush.sizeSet(amount) end
function brush.sizeAdd(amount) end
function brush.forcePaintingOn(active) end
function brush.forcePaintingOff(active) end
function camerapath.render() end
function camerapath.toggleVisuals() end
function camerapath.togglePreview() end
function camerapath.delete() end
function camerapath.record() end
function color.addHsv(color) end
function color.addRgb(color) end
function color.setRgb(color) end
function color.setHsv(color) end
function color.setHtml(color) end
function color.jitter() end
function draw.path(path) end
function draw.paths(paths) end
function draw.polygon(sides, radius, angle) end
function draw.text(text) end
function draw.svg(svg) end
function draw.cameraPath(index) end
function guides.addCube(tr) end
function guides.addSphere(tr) end
function guides.addCapsule(tr) end
function guides.addCone(tr) end
function guides.addEllipsoid(tr) end
function guides.select(index) end
function guides.moveTo(index, position) end
function guides.scale(index, scale) end
function guides.toggle() end
function headset.pastPosition(count) end
function headset.pastRotation(count) end
function image.import(location) end
function image.select(index) end
function image.moveTo(index, position) end
layer.active = nil
function layer.transform(index) end
function layer.position(index) end
function layer.rotation(index) end
function layer.moveTo(index, position) end
function layer.moveBy(index, distance) end
function layer.add() end
function layer.clear(index) end
function layer.delete(index) end
function layer.squash(sourceLayer, destinationLayer) end
function layer.activate(index) end
function layer.show(index) end
function layer.hide(index) end
function layer.toggle(index) end
function layer._MoveTo(index, pos) end
function layer._MoveBy(index, distance) end
function layer._TransformLayer(index, tr) end
function model.select(index) end
function model.moveTo(index, position) end
function path.fromSvg(svg) end
function path.fromSvgMultiple(svg) end
function path.transform(path, transform) end
function path.translate(path, amount) end
function path.rotate(path, amount) end
function path.scale(path, amount) end
function selection.duplicate() end
function selection.group() end
function selection.invert() end
function selection.flip() end
function selection.recolor() end
function selection.rebrush() end
function selection.resize() end
function selection.trim(count) end
function selection.selectAll() end
function sketch.open(name) end
function sketch.save(overwrite) end
function sketch.export() end
function sketch.newSketch() end
function spectator.moveTo(position) end
function spectator.moveBy(distance) end
function spectator.turn(angle) end
function spectator.turnX(angle) end
function spectator.turnZ(angle) end
function spectator.direction(direction) end
function spectator.lookAt(position) end
function spectator.mode(mode) end
function spectator.show(type) end
function spectator.hide(type) end
function spectator.toggle() end
function spectator.on() end
function spectator.off() end
strokes.count = nil
function strokes.delete(index) end
function strokes.select(index) end
function strokes.selectMultiple(from, to) end
function strokes.join(from, to) end
function strokes.joinPrevious() end
function strokes.import(name) end
symmetry.position = nil
symmetry.rotation = nil
symmetry.direction = nil
function symmetry.mirror() end
function symmetry.doubleMirror() end
function symmetry.twoHandeded() end
function symmetry.setPosition(position) end
function symmetry.setRotation(rotation) end
function symmetry.setTransform(position, rotation) end
function symmetry.summonWidget() end
function symmetry.spin(rot) end
turtle.transform = nil
turtle.position = nil
turtle.rotation = nil
function turtle.moveTo(position) end
function turtle.moveBy(amount) end
function turtle.move(amount) end
function turtle.draw(amount) end
function turtle.turnY(angle) end
function turtle.turnX(angle) end
function turtle.turnZ(angle) end
function turtle.lookAt(amount) end
function turtle.lookForwards() end
function turtle.lookUp() end
function turtle.lookDown() end
function turtle.lookLeft() end
function turtle.lookRight() end
function turtle.lookBackwards() end
function turtle.homeReset() end
function turtle.homeSet() end
function turtle.transformPush() end
function turtle.transformPop() end
function user.moveTo(position) end
function user.moveBy(distance) end
wand.position = nil
wand.rotation = nil
wand.direction = nil
wand.pressure = nil
wand.speed = nil
function wand.pastPosition(back) end
function wand.pastRotation(back) end
