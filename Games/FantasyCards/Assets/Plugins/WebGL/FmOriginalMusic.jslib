mergeInto(LibraryManager.library, {
  FMOriginalMusic_Message: function (pointer) {
    try {
      var message = JSON.parse(UTF8ToString(pointer));
      if (window.FirstGameOriginalMusic) window.FirstGameOriginalMusic.receive(message);
      else (window.__fmOriginalMusicMessages || (window.__fmOriginalMusicMessages = [])).push(message);
    } catch (error) {
      console.warn("Original music player message could not be delivered.");
    }
  },
  FMOriginalMusic_GetMuted: function () {
    if (!window.FirstGameOriginalMusic) return -1;
    var state = window.FirstGameOriginalMusic.getState();
    return state.hasMutedPreference ? (state.muted ? 1 : 0) : -1;
  },
  FMOriginalMusic_GetVolume: function () {
    if (!window.FirstGameOriginalMusic) return -1;
    var state = window.FirstGameOriginalMusic.getState();
    return state.hasVolumePreference ? state.volume : -1;
  }
});
