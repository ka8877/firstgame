mergeInto(LibraryManager.library, {
  PathOfDawn_HasTouchInterface: function () {
    // Require real touch capability as well as a coarse pointer. "any-pointer"
    // retains touch controls for hybrid devices that also have a mouse attached.
    if (typeof navigator === "undefined" || !(navigator.maxTouchPoints > 0) ||
        typeof window === "undefined" || typeof window.matchMedia !== "function") {
      return 0;
    }
    return window.matchMedia("(pointer: coarse)").matches ||
      window.matchMedia("(any-pointer: coarse)").matches ? 1 : 0;
  }
});
