mergeInto(LibraryManager.library, {
  FmClientCssPixelRatio: function () {
    var canvas = Module["canvas"];
    if (!canvas || !canvas.getBoundingClientRect) return 1;
    var height = canvas.getBoundingClientRect().height;
    var ratio = height > 0 ? canvas.height / height : 1;
    return Number.isFinite(ratio) && ratio > 0 ? ratio : 1;
  }
});
