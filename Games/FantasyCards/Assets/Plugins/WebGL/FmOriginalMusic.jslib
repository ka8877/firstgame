mergeInto(LibraryManager.library, {
  FMOriginalMusic_Message: function (pointer) {
    try {
      var message = JSON.parse(UTF8ToString(pointer));
      if (window.FirstGameOriginalMusic) window.FirstGameOriginalMusic.receive(message);
    } catch (error) {
      console.warn("Original music player message could not be delivered.");
    }
  }
});
