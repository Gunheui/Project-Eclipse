// WebGL 세이브 동기화 — File.WriteAllText가 남긴 IDBFS 메모리 캐시를 IndexedDB로 내린다.
// SaveService.SyncWebGLFileSystem()이 매 저장 직후 호출한다.
mergeInto(LibraryManager.library, {
  EclipseSyncFs: function () {
    FS.syncfs(false, function (err) {
      if (err) console.error('EclipseSyncFs failed:', err);
    });
  }
});
