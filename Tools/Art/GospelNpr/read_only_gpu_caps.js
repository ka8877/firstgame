// Evaluate only after the actual Unity preview has created its WebGL2 context.
// This returns capabilities. It does not bind a program/texture/framebuffer,
// intercept draws, inject shader code, clear errors, or change any GL state.
(() => {
  const canvas = document.querySelector('#unity-canvas') || document.querySelector('canvas');
  if (!canvas) throw new Error('Actual Unity canvas is required.');
  const gl = canvas.getContext('webgl2');
  if (!gl) throw new Error('The existing Unity WebGL2 context is required.');
  const names = [
    'VERSION', 'SHADING_LANGUAGE_VERSION', 'VENDOR', 'RENDERER',
    'MAX_TEXTURE_IMAGE_UNITS', 'MAX_VERTEX_TEXTURE_IMAGE_UNITS',
    'MAX_COMBINED_TEXTURE_IMAGE_UNITS', 'MAX_VERTEX_UNIFORM_VECTORS',
    'MAX_FRAGMENT_UNIFORM_VECTORS', 'MAX_UNIFORM_BLOCK_SIZE',
    'MAX_VERTEX_UNIFORM_BLOCKS', 'MAX_FRAGMENT_UNIFORM_BLOCKS',
    'MAX_UNIFORM_BUFFER_BINDINGS', 'MAX_VARYING_COMPONENTS',
    'MAX_VERTEX_OUTPUT_COMPONENTS', 'MAX_FRAGMENT_INPUT_COMPONENTS'
  ];
  const parameters = Object.fromEntries(names.map(name => [name,
    typeof gl[name] === 'number' ? gl.getParameter(gl[name]) : {available: false}
  ]));
  const highFloat = gl.getShaderPrecisionFormat(gl.FRAGMENT_SHADER, gl.HIGH_FLOAT);
  return {
    kind: 'read-only-actual-preview-webgl2-capabilities',
    observedUtc: new Date().toISOString(),
    parameters,
    fragmentHighFloat: highFloat ? {rangeMin: highFloat.rangeMin, rangeMax: highFloat.rangeMax, precision: highFloat.precision} : null,
    drawingBuffer: {width: gl.drawingBufferWidth, height: gl.drawingBufferHeight},
    extensions: gl.getSupportedExtensions()
  };
})();
