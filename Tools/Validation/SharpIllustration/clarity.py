"""Read-only DOM/WebGL observations for the current shipping canvas.

No Unity method, state, save, camera or quality field is written. A successful
measurement is mechanical evidence and never image/art approval.
"""

CANVAS_OBSERVER = """() => {
    const c = document.querySelector('#unity-canvas');
    if (!(c instanceof HTMLCanvasElement) || c.dataset.unityState !== 'ready')
        throw new Error('Ready shipping canvas required for backing-buffer observation');
    const rect = c.getBoundingClientRect();
    // Unity has already created this WebGL context before its ready marker.
    // Request its existing standard context; never alter its state or viewport.
    const gl = c.getContext('webgl2') || c.getContext('webgl');
    if (!gl) throw new Error('Existing shipping WebGL context unavailable');
    const cfg = typeof config === 'object' && config !== null ? config : null;
    return {
        shippingCanvas: c.isConnected && c === window.canvas && !c.hidden,
        ready: c.dataset.unityState,
        devicePixelRatio: window.devicePixelRatio,
        coarsePointer: matchMedia('(pointer: coarse)').matches,
        configuredDevicePixelRatio: cfg && typeof cfg.devicePixelRatio === 'number' ? cfg.devicePixelRatio : null,
        matchWebGLToCanvasSize: cfg !== null && cfg.matchWebGLToCanvasSize === true,
        css: {width: rect.width, height: rect.height},
        position: {x: rect.x, y: rect.y},
        backing: {width: c.width, height: c.height},
        drawingBuffer: {width: gl.drawingBufferWidth, height: gl.drawingBufferHeight},
        contextLost: gl.isContextLost(),
        dataUrl: c.dataset.dataUrl,
        url: location.href
    };
}"""


def observe(page, *, navigation, reason, checkpoint=None):
    value = page.evaluate(CANVAS_OBSERVER)
    value.update(navigation=navigation, reason=reason)
    if checkpoint is not None:
        value['checkpoint'] = checkpoint
    return value
