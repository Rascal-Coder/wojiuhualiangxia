// Targeted fixes for Tuanjie 1.10.4's GLES initialization on WeChat WebGL2.
// Keep GL errors and console output intact for every other operation.
mergeInto(LibraryManager.library, {
    glGetIntegerv__deps: ['$GL', '$emscriptenWebGLGet'],
    glGetIntegerv: function (name, ptr) {
        if (name === 33310) {
            // GL_CONTEXT_FLAGS is a desktop GLES query, absent in WebGL2.
            // WebGL exposes no native debug/forward-compatible context flags.
            if (ptr) HEAP32[ptr >> 2] = 0;
            else GL.recordError(1281);
            return;
        }
        emscriptenWebGLGet(name, ptr, 0);
    },
    glTexImage3D__deps: ['$GL', '$heapObjectForWebGLType', '$heapAccessShiftForWebGLHeap'],
    glTexImage3D: function (target, level, internalFormat, width, height, depth, border, format, type, pixels) {
        if (GLctx.currentPixelUnpackBufferBinding) {
            GLctx.texImage3D(target, level, internalFormat, width, height, depth, border, format, type, pixels);
        } else if (pixels) {
            var heap = heapObjectForWebGLType(type);
            // Supply the source view directly instead of the optional srcOffset
            // overload. The view includes all depth slices and shares WASM bytes.
            var view = heap.subarray(pixels >> heapAccessShiftForWebGLHeap(heap));
            GLctx.texImage3D(target, level, internalFormat, width, height, depth, border, format, type, view);
        } else {
            GLctx.texImage3D(target, level, internalFormat, width, height, depth, border, format, type, null);
        }
    }
});
