mergeInto(LibraryManager.library, {
    $GDK_WebGLFileSystemState: {
        nextSyncRequestId: 1,
        syncRequests: {},
        nextMiniGameRequestId: 1,
        miniGameRequests: {},

        getMiniGamePlatform: function () {
            if (typeof wx !== 'undefined' && wx && wx.env && wx.env.USER_DATA_PATH &&
                typeof wx.getFileSystemManager === 'function') {
                return wx;
            }

            if (typeof tt !== 'undefined' && tt && tt.env && tt.env.USER_DATA_PATH &&
                typeof tt.getFileSystemManager === 'function') {
                return tt;
            }

            return null;
        },

        getMiniGameStorageRoot: function (platform) {
            return platform.env.USER_DATA_PATH.replace(/\/+$/, '') + '/__GDK_VFS';
        },

        toArrayBuffer: function (bytes) {
            return bytes.buffer.slice(bytes.byteOffset, bytes.byteOffset + bytes.byteLength);
        }
    },

    GDK_WebGLReplaceFile__deps: ['$GDK_WebGLFileSystemState'],
    GDK_WebGLReplaceFile: function (sourcePathPointer, destinationPathPointer) {
        try {
            var sourcePath = UTF8ToString(sourcePathPointer);
            var destinationPath = UTF8ToString(destinationPathPointer);
            FS.rename(sourcePath, destinationPath);
            return 0;
        } catch (error) {
            console.error('[GameDeveloperKit] WebGL file replace failed.', error);
            return -1;
        }
    },

    GDK_WebGLPopulatePersistentFileSystem__deps: ['$GDK_WebGLFileSystemState'],
    GDK_WebGLPopulatePersistentFileSystem: function (rootPathPointer) {
        var platform = GDK_WebGLFileSystemState.getMiniGamePlatform();
        if (!platform) {
            return 0;
        }

        var rootPath = UTF8ToString(rootPathPointer).replace(/\/+$/, '');
        var storageRoot = GDK_WebGLFileSystemState.getMiniGameStorageRoot(platform);
        var fileSystem = platform.getFileSystemManager();
        try {
            try {
                fileSystem.mkdirSync(storageRoot, true);
            } catch (mkdirError) {
                // Existing directories are valid on SDK versions that report EEXIST.
                fileSystem.readdirSync(storageRoot);
            }

            var localEntries = FS.readdir(rootPath);
            for (var localIndex = 0; localIndex < localEntries.length; localIndex++) {
                var localName = localEntries[localIndex];
                if (localName === '.' || localName === '..') {
                    continue;
                }

                var localPath = rootPath + '/' + localName;
                var localStat = FS.stat(localPath);
                if (FS.isFile(localStat.mode)) {
                    FS.unlink(localPath);
                }
            }

            var storedEntries = fileSystem.readdirSync(storageRoot) || [];
            for (var storedIndex = 0; storedIndex < storedEntries.length; storedIndex++) {
                var storedName = storedEntries[storedIndex];
                var storedPath = storageRoot + '/' + storedName;
                var data = fileSystem.readFileSync(storedPath);
                var bytes;
                if (data instanceof ArrayBuffer) {
                    bytes = new Uint8Array(data);
                } else if (ArrayBuffer.isView(data)) {
                    bytes = new Uint8Array(data.buffer, data.byteOffset, data.byteLength);
                } else {
                    throw new Error('Mini-game persistent file is not binary: ' + storedPath);
                }

                FS.writeFile(rootPath + '/' + storedName, bytes);
            }

            return 0;
        } catch (error) {
            console.error('[GameDeveloperKit] Unable to populate the mini-game persistent file system.', error);
            return -1;
        }
    },

    GDK_WebGLBeginPersistentSync__deps: ['$GDK_WebGLFileSystemState'],
    GDK_WebGLBeginPersistentSync: function (rootPathPointer) {
        var requestId = GDK_WebGLFileSystemState.nextSyncRequestId++;
        GDK_WebGLFileSystemState.syncRequests[requestId] = 0;
        var platform = GDK_WebGLFileSystemState.getMiniGamePlatform();
        if (platform) {
            var rootPath = UTF8ToString(rootPathPointer).replace(/\/+$/, '');
            var storageRoot = GDK_WebGLFileSystemState.getMiniGameStorageRoot(platform);
            var fileSystem = platform.getFileSystemManager();
            var finish = function (error) {
                if (GDK_WebGLFileSystemState.syncRequests[requestId] === undefined) {
                    return;
                }

                GDK_WebGLFileSystemState.syncRequests[requestId] = error ? -1 : 1;
                if (error) {
                    console.error('[GameDeveloperKit] Mini-game persistent file system sync failed.', error);
                }
            };

            try {
                try {
                    fileSystem.mkdirSync(storageRoot, true);
                } catch (mkdirError) {
                    fileSystem.readdirSync(storageRoot);
                }

                var localNames = [];
                var entries = FS.readdir(rootPath);
                for (var index = 0; index < entries.length; index++) {
                    var name = entries[index];
                    if (name === '.' || name === '..') {
                        continue;
                    }

                    var path = rootPath + '/' + name;
                    if (FS.isFile(FS.stat(path).mode)) {
                        localNames.push(name);
                    }
                }

                var storedNames = fileSystem.readdirSync(storageRoot) || [];
                var operations = [];
                for (var writeIndex = 0; writeIndex < localNames.length; writeIndex++) {
                    (function (fileName) {
                        operations.push(new Promise(function (resolve, reject) {
                            var bytes = FS.readFile(rootPath + '/' + fileName);
                            fileSystem.writeFile({
                                filePath: storageRoot + '/' + fileName,
                                data: GDK_WebGLFileSystemState.toArrayBuffer(bytes),
                                success: resolve,
                                fail: reject
                            });
                        }));
                    })(localNames[writeIndex]);
                }

                for (var deleteIndex = 0; deleteIndex < storedNames.length; deleteIndex++) {
                    var storedName = storedNames[deleteIndex];
                    if (localNames.indexOf(storedName) >= 0) {
                        continue;
                    }

                    (function (fileName) {
                        operations.push(new Promise(function (resolve, reject) {
                            fileSystem.unlink({
                                filePath: storageRoot + '/' + fileName,
                                success: resolve,
                                fail: reject
                            });
                        }));
                    })(storedName);
                }

                Promise.all(operations).then(function () {
                    finish(null);
                }).catch(finish);
            } catch (error) {
                finish(error);
            }

            return requestId;
        }

        try {
            FS.syncfs(false, function (error) {
                if (GDK_WebGLFileSystemState.syncRequests[requestId] === undefined) {
                    return;
                }

                GDK_WebGLFileSystemState.syncRequests[requestId] = error ? -1 : 1;
                if (error) {
                    console.error('[GameDeveloperKit] WebGL persistent file system sync failed.', error);
                }
            });
        } catch (error) {
            GDK_WebGLFileSystemState.syncRequests[requestId] = -1;
            console.error('[GameDeveloperKit] Unable to start WebGL persistent file system sync.', error);
        }

        return requestId;
    },

    GDK_WebGLPollPersistentSync__deps: ['$GDK_WebGLFileSystemState'],
    GDK_WebGLPollPersistentSync: function (requestId) {
        var status = GDK_WebGLFileSystemState.syncRequests[requestId];
        if (status === undefined) {
            return -1;
        }

        if (status !== 0) {
            delete GDK_WebGLFileSystemState.syncRequests[requestId];
        }

        return status;
    },

    GDK_WebGLBeginMiniGameSaveImage__deps: ['$GDK_WebGLFileSystemState'],
    GDK_WebGLBeginMiniGameSaveImage: function (dataPointer, length, fileNamePointer) {
        var platform = typeof wx !== 'undefined'
            ? wx
            : (typeof tt !== 'undefined' ? tt : null);
        if (!platform || !platform.env || !platform.env.USER_DATA_PATH ||
            typeof platform.getFileSystemManager !== 'function' ||
            typeof platform.saveImageToPhotosAlbum !== 'function') {
            console.error('[GameDeveloperKit] Mini-game file or photo-album API is unavailable.');
            return -1;
        }

        var fileName = UTF8ToString(fileNamePointer);
        var filePath = platform.env.USER_DATA_PATH.replace(/\/+$/, '') + '/' + fileName;
        var bytes = HEAPU8.slice(dataPointer, dataPointer + length);
        var requestId = GDK_WebGLFileSystemState.nextMiniGameRequestId++;
        GDK_WebGLFileSystemState.miniGameRequests[requestId] = 0;
        var fileSystem = platform.getFileSystemManager();
        var cleanup = function () {
            try {
                fileSystem.unlink({ filePath: filePath });
            } catch (error) {
                console.warn('[GameDeveloperKit] Unable to remove temporary mini-game image.', error);
            }
        };

        fileSystem.writeFile({
            filePath: filePath,
            data: bytes.buffer,
            success: function () {
                platform.saveImageToPhotosAlbum({
                    filePath: filePath,
                    success: function () {
                        GDK_WebGLFileSystemState.miniGameRequests[requestId] = 1;
                        cleanup();
                    },
                    fail: function (error) {
                        GDK_WebGLFileSystemState.miniGameRequests[requestId] = -1;
                        console.error('[GameDeveloperKit] Mini-game photo-album save failed.', error);
                        cleanup();
                    }
                });
            },
            fail: function (error) {
                GDK_WebGLFileSystemState.miniGameRequests[requestId] = -1;
                console.error('[GameDeveloperKit] Mini-game temporary image write failed.', error);
                cleanup();
            }
        });
        return requestId;
    },

    GDK_WebGLPollMiniGameFileOperation__deps: ['$GDK_WebGLFileSystemState'],
    GDK_WebGLPollMiniGameFileOperation: function (requestId) {
        var status = GDK_WebGLFileSystemState.miniGameRequests[requestId];
        if (status === undefined) {
            return -1;
        }

        if (status !== 0) {
            delete GDK_WebGLFileSystemState.miniGameRequests[requestId];
        }

        return status;
    },

    GDK_WebGLDownloadFile: function (dataPointer, length, fileNamePointer, mimeTypePointer) {
        var bytes = HEAPU8.slice(dataPointer, dataPointer + length);
        var fileName = UTF8ToString(fileNamePointer);
        var mimeType = UTF8ToString(mimeTypePointer);
        var blob = new Blob([bytes], { type: mimeType });
        var objectUrl = URL.createObjectURL(blob);
        var anchor = document.createElement('a');
        anchor.href = objectUrl;
        anchor.download = fileName;
        anchor.style.display = 'none';
        document.body.appendChild(anchor);
        anchor.click();
        document.body.removeChild(anchor);
        setTimeout(function () {
            URL.revokeObjectURL(objectUrl);
        }, 0);
    },

    GDK_WebGLDownloadUrl: function (urlPointer, fileNamePointer) {
        var anchor = document.createElement('a');
        anchor.href = UTF8ToString(urlPointer);
        anchor.download = UTF8ToString(fileNamePointer);
        anchor.target = '_blank';
        anchor.rel = 'noopener';
        anchor.style.display = 'none';
        document.body.appendChild(anchor);
        anchor.click();
        document.body.removeChild(anchor);
    }
});
