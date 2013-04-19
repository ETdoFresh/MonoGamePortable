﻿using Microsoft.Xna.Framework.Content;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;


//Non Core assemblies
#if WINRT
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Search;
#elif WINDOWS_PHONE
using System.IO.IsolatedStorage;
#endif

namespace Microsoft.Xna.Framework.Utilities
{
    public static class Helper_File
    {
        #region internal properties
#if WINDOWS_PHONE
        //If no storage file supplied, use the default isolated storage folder
        static IsolatedStorageFile storage = IsolatedStorageFile.GetUserStoreForApplication();
#elif ANDROID
		// Keep this static so we only call Game.Activity.Assets.List() once
		// No need to call it for each file if the list will never change.
		// We do need one file list per folder though.
		static Dictionary<string, string[]> filesInFolders = new Dictionary<string,string[]>();
#endif
        #endregion

        #region File Handlers

        internal static Stream FileOpen(string filePath, string fileMode, string fileAccess, string fileShare)
        {
            return FileOpen(filePath, (FileMode)Enum.Parse(typeof(FileMode), fileMode, true), (FileAccess)Enum.Parse(typeof(FileAccess), fileAccess, false), (FileShare)Enum.Parse(typeof(FileShare), fileShare, false));
        }

        public static Stream FileOpen(string filePath, FileMode fileMode, FileAccess fileAccess, FileShare fileShare)
        {
#if WINDOWS_STOREAPP
            var folder = ApplicationData.Current.LocalFolder;
            if (fileMode == FileMode.Create || fileMode == FileMode.CreateNew)
            {
                return folder.OpenStreamForWriteAsync(filePath, CreationCollisionOption.ReplaceExisting).GetAwaiter().GetResult();
            }
            else if (fileMode == FileMode.OpenOrCreate)
            {
                if (fileAccess == FileAccess.Read)
                    return folder.OpenStreamForReadAsync(filePath).GetAwaiter().GetResult();
                else
                {
                    // Not using OpenStreamForReadAsync because the stream position is placed at the end of the file, instead of the beginning
                    var f = folder.CreateFileAsync(filePath, CreationCollisionOption.OpenIfExists).AsTask().GetAwaiter().GetResult();
                    return f.OpenAsync(FileAccessMode.ReadWrite).AsTask().GetAwaiter().GetResult().AsStream();
                }
            }
            else if (fileMode == FileMode.Truncate)
            {
                return folder.OpenStreamForWriteAsync(filePath, CreationCollisionOption.ReplaceExisting).GetAwaiter().GetResult();
            }
            else
            {
                //if (fileMode == FileMode.Append)
                // Not using OpenStreamForReadAsync because the stream position is placed at the end of the file, instead of the beginning
                folder.CreateFileAsync(filePath, CreationCollisionOption.OpenIfExists).AsTask().GetAwaiter().GetResult().OpenAsync(FileAccessMode.ReadWrite).AsTask().GetAwaiter().GetResult().AsStream();
                var f = folder.CreateFileAsync(filePath, CreationCollisionOption.OpenIfExists).AsTask().GetAwaiter().GetResult();
                return f.OpenAsync(FileAccessMode.ReadWrite).AsTask().GetAwaiter().GetResult().AsStream();
            }
#else
            return File.Open(filePath, fileMode, fileAccess, fileShare);
#endif
        }

        internal static Stream FileOpenRead(string Location, string safeName)
        {
#if WINRT
            var stream = Task.Run( () => Helper_File.OpenStreamAsync(safeName).Result ).Result;
            if (stream == null)
                throw new FileNotFoundException(safeName);

            return stream;
#elif ANDROID
            return Game.Activity.Assets.Open(safeName);
#elif IOS
            var absolutePath = Path.Combine(Location, safeName);
            if (SupportRetina)
            {
                // Insert the @2x immediately prior to the extension. If this file exists
                // and we are on a Retina device, return this file instead.
                var absolutePath2x = Path.Combine(Path.GetDirectoryName(absolutePath),
                                                  Path.GetFileNameWithoutExtension(absolutePath)
                                                  + "@2x" + Path.GetExtension(absolutePath));
                if (File.Exists(absolutePath2x))
                    return File.OpenRead(absolutePath2x);
            }
            return File.OpenRead(absolutePath);
#elif WINDOWS_PHONE
            return storage.OpenFile(safeName, FileMode.Open, FileAccess.Read);
#else
            var absolutePath = Path.Combine(Location, safeName);
            return File.OpenRead(absolutePath);
#endif
        }

        internal static string GetFilename(string name)
        {
#if WINRT
            // Replace non-windows seperators.
            name = name.Replace('/', '\\');
#else
            // Replace Windows path separators with local path separators
            name = name.Replace('\\', Path.DirectorySeparatorChar);
#endif
            return name;
        }

        internal static bool FileExists(string fileName)
        {
#if WINRT
            var result = Task.Run(async () =>
            {
                try
                {
                    var file = await Windows.ApplicationModel.Package.Current.InstalledLocation.GetFileAsync(fileName);
                    return file == null ? false : true;
                }
                catch (FileNotFoundException)
                {
                    return false;
                }
            }).Result;

            if (result)
            {
                return true;
            }

#elif ANDROID
            int index = fileName.LastIndexOf(Path.DirectorySeparatorChar);
            string path = string.Empty;
            string file = fileName;
            if (index >= 0)
            {
                file = fileName.Substring(index + 1, fileName.Length - index - 1);
                path = fileName.Substring(0, index);
            }

            // Only read the assets file list once
            string[] files = DirectoryGetFiles(path);

            if (files.Any(s => s.ToLower() == file.ToLower()))
                return true;
#elif WINDOWS_PHONE
            if(FileExists(storage, fileName))
                return true;
#else
            if (File.Exists(fileName))
                return true;
#endif
            return false;
        }

        internal static Stream FileCreate(string filePath)
        {
#if WINDOWS_STOREAPP
            var folder = ApplicationData.Current.LocalFolder;
            var awaiter = folder.OpenStreamForWriteAsync(filePath, CreationCollisionOption.ReplaceExisting).GetAwaiter();
            return awaiter.GetResult();
#elif WINDOWS_PHONE
            return storage.CreateFile(filePath);
#else
            // return A new file with read/write access.
            return File.Create(filePath);
#endif
        }

        internal static void FileDelete(string filePath)
        {
#if WINDOWS_STOREAPP
            var folder = ApplicationData.Current.LocalFolder;
            var deleteFile = folder.GetFileAsync(filePath).AsTask().GetAwaiter().GetResult();
            deleteFile.DeleteAsync().AsTask().Wait();
#elif WINDOWS_PHONE
            storage.DeleteFile(filePath);
#else
            // Now let's try to delete it
            File.Delete(filePath);
#endif
        }

        internal static string NormalizeFilename(string fileName, string[] extensions)
        {

            if (FileExists(fileName))
            	return fileName;

            // Check the file extension
            if (!string.IsNullOrEmpty(Path.GetExtension(fileName)))
                return null;

            foreach (string ext in extensions)
            {
                // Concat the file name with valid extensions
                string fileNamePlusExt = fileName + ext;

                if (FileExists(fileNamePlusExt))
                    return fileNamePlusExt;
            }

            return null;
        }


        #region File Handler reciprocal overloads

        internal static void FileOpenRead(string Location, string safeName, out Stream fileStream)
        {
            fileStream = FileOpenRead(Location, safeName);
        }

        internal static void FileOpenRead(object storageFile, string Location, string safeName, out Stream fileStream)
        {
            if (storageFile == null)
            {
                throw new NullReferenceException("Must supply a storageFile reference");
            }

#if WINDOWS_PHONE
            storage = (IsolatedStorageFile)storageFile;
#endif

            fileStream = FileOpenRead(Location, safeName);

        }

        internal static Stream FileOpenRead(object storageFile, string Location, string safeName)
        {
            if (storageFile == null)
            {
                throw new NullReferenceException("Must supply a storageFile reference");
            }

#if WINDOWS_PHONE
            storage = (IsolatedStorageFile)storageFile;
#endif

            return FileOpenRead(Location, safeName);

        }

        internal static bool FileExists(object storageFile, string fileName)
        {
            if (storageFile == null)
            {
                throw new NullReferenceException("Must supply a storageFile reference");
            }

#if WINDOWS_PHONE
            storage = (IsolatedStorageFile)storageFile;
#endif

            return FileExists(fileName);
        }

        internal static Stream FileCreate(object storageFile, string filePath)
        {
            if (storageFile == null)
            {
                throw new NullReferenceException("Must supply a storageFile reference");
            }

#if WINDOWS_PHONE
            storage = (IsolatedStorageFile)storageFile;
#endif

            return FileCreate(filePath);
        }

        internal static void FileCreate(string filePath, out Stream fileStream)
        {
            fileStream = FileCreate(filePath);
        }

        internal static void FileCreate(object storageFile, string filePath, out Stream fileStream)
        {
            if (storageFile == null)
            {
                throw new NullReferenceException("Must supply a storageFile reference");
            }

#if WINDOWS_PHONE
            storage = (IsolatedStorageFile)storageFile;
#endif

            fileStream = FileCreate(filePath);
        }

        internal static void FileDelete(object storageFile, string filePath)
        {
            if (storageFile == null)
            {
                throw new NullReferenceException("Must supply a storageFile reference");
            }

#if WINDOWS_PHONE
            storage = (IsolatedStorageFile)storageFile;
#endif

            FileDelete(filePath);
        }

        #endregion

        #endregion

        #region Directory Handlers

        internal static string[] DirectoryGetFiles(string storagePath)
        {
#if WINDOWS_STOREAPP
            var folder = ApplicationData.Current.LocalFolder;
            var results = folder.GetFilesAsync().AsTask().GetAwaiter().GetResult();
            return results.Select<StorageFile, string>(e => e.Name).ToArray();
#elif WINDOWS_PHONE
            return storage.GetFileNames(storagePath);
#elif ANDROID
            string[] files = null;
            if (!filesInFolders.TryGetValue(storagePath, out files))
            {
                files = Game.Activity.Assets.List(storagePath);
                filesInFolders[storagePath] = files;
            }
            return filesInFolders[storagePath];
#else
            return Directory.GetFiles(storagePath);
#endif
        }

        internal static string[] DirectoryGetFiles(string storagePath, string searchPattern)
        {
            if (string.IsNullOrEmpty(searchPattern))
                throw new ArgumentNullException("Parameter searchPattern must contain a value.");

#if WINDOWS_STOREAPP
            var folder = ApplicationData.Current.LocalFolder;
            var options = new QueryOptions( CommonFileQuery.DefaultQuery, new [] { searchPattern } );
            var query = folder.CreateFileQueryWithOptions(options);
            var files = query.GetFilesAsync().AsTask().GetAwaiter().GetResult();
            return files.Select<StorageFile, string>(e => e.Name).ToArray();
#else
            return Directory.GetFiles(storagePath, searchPattern);
#endif
        }

        internal static bool DirectoryExists(string dirPath)
        {
#if WINDOWS_STOREAPP
            var folder = ApplicationData.Current.LocalFolder;

            try
            {
                var result = folder.GetFolderAsync(dirPath).GetResults();
            return result != null;
            }
            catch
            {
                return false;
            }
#elif WINDOWS_PHONE
            return storage.DirectoryExists(dirPath);
#else
            return Directory.Exists(dirPath);
#endif
        }

        internal static string[] DirectoryGetDirectories(string storagePath)
        {
#if WINDOWS_STOREAPP
            var folder = ApplicationData.Current.LocalFolder;
            var results = folder.GetFoldersAsync().AsTask().GetAwaiter().GetResult();
            return results.Select<StorageFolder, string>(e => e.Name).ToArray();
#else
            return Directory.GetDirectories(storagePath);
#endif
        }

        internal static string[] DirectoryGetDirectories(string storagePath, string searchPattern)
        {
            throw new NotImplementedException();
        }

        internal static void DirectoryCreate(string directory)
        {
            if (string.IsNullOrEmpty(directory))
                throw new ArgumentNullException("Parameter directory must contain a value.");

            // Now let's try to create it
#if WINDOWS_STOREAPP
            var folder = ApplicationData.Current.LocalFolder;
            var task = folder.CreateFolderAsync(directory, CreationCollisionOption.OpenIfExists);
            task.AsTask().Wait();
#else
            Directory.CreateDirectory(directory);
#endif
        }

        /// <summary>
        /// Creates a new directory in the storage-container.
        /// </summary>
        /// <param name="directory">Relative path of the directory to be created.</param>
        internal static void DirectoryCreate(string directory, string storagePath)
        {
            if (string.IsNullOrEmpty(directory))
                throw new ArgumentNullException("Parameter directory must contain a value.");

            // relative so combine with our path
            var dirPath = Path.Combine(storagePath, directory);

            DirectoryCreate(dirPath);
        }

        internal static void DirectoryDelete(string dirPath)
        {
#if WINDOWS_STOREAPP
            var folder = ApplicationData.Current.LocalFolder;
            var deleteFolder = folder.GetFolderAsync(dirPath).AsTask().GetAwaiter().GetResult();
            deleteFolder.DeleteAsync().AsTask().Wait();
#else
            Directory.Delete(dirPath);
#endif
        }

        #region Directory Handler reciprocal overloads

        internal static string[] DirectoryGetFiles(object storageFile, string storagePath)
        {
            if (storageFile == null)
            {
                throw new NullReferenceException("Must supply a storageFile reference");
            }

#if WINDOWS_PHONE
            storage = (IsolatedStorageFile)storageFile;
#endif

            return DirectoryGetFiles(storagePath);
        }

        internal static void DirectoryGetFiles(string storagePath, out string[] files)
        {
            files = DirectoryGetFiles(storagePath);
        }

        internal static void DirectoryGetFiles(object storageFile, string storagePath, out string[] files)
        {
            if (storageFile == null)
            {
                throw new NullReferenceException("Must supply a storageFile reference");
            }

#if WINDOWS_PHONE
            storage = (IsolatedStorageFile)storageFile;
#endif

            DirectoryGetFiles(storagePath, out files);
        }

        internal static bool DirectoryExists(string dirPath, out object storageFile)
        {

#if WINDOWS_PHONE
            storageFile = storage;
            if (storage == null)
            {
                return false;
            }
#else
            storageFile = null;
#endif

            return DirectoryExists(dirPath);
        }

        #endregion

        #endregion

        #region Stream Handlers

        internal static Stream OpenStream(string rootDirectory, string assetName, string extension)
        {
            Stream stream;
            try
            {
                string assetPath = Path.Combine(rootDirectory, assetName) + extension;
                stream = TitleContainer.OpenStream(assetPath);
#if ANDROID
                SeekStreamtoStart(stream, 0);
#else
                SeekStreamtoStart(stream);
#endif
            }
            catch (FileNotFoundException fileNotFound)
            {
                throw new ContentLoadException("The content file was not found.", fileNotFound);
            }
#if !WINRT
            catch (DirectoryNotFoundException directoryNotFound)
            {
                throw new ContentLoadException("The directory was not found.", directoryNotFound);
            }
#endif
            catch (Exception exception)
            {
                throw new ContentLoadException("Opening stream error.", exception);
            }
            return stream;
        }

        internal static Stream SeekStreamtoStart(Stream stream, long StartPos, out long pos)
        {
#if ANDROID
                // Android native stream does not support the Position property. LzxDecoder.Decompress also uses
                // Seek.  So we read the entirity of the stream into a memory stream and replace stream with the
                // memory stream.
                MemoryStream memStream = new MemoryStream();
                stream.CopyTo(memStream);
                memStream.Seek(0, SeekOrigin.Begin);
                stream.Dispose();
                stream = memStream;
                // Position is at the start of the MemoryStream as Stream.CopyTo copies from current position
                pos = 0;
#else
                pos = StartPos;
#endif
                return stream;
        }

        internal static void StreamClose(Stream stream)
        {
#if !WINRT
            stream.Close();
#endif
        }

#if WINRT

        public static async Task<Stream> OpenStreamAsync(string name)
        {
            var package = Windows.ApplicationModel.Package.Current;

            try
            {
                var storageFile = await package.InstalledLocation.GetFileAsync(name);
                var randomAccessStream = await storageFile.OpenReadAsync();
                return randomAccessStream.AsStreamForRead();
            }
            catch (IOException)
            {
                // The file must not exist... return a null stream.
                return null;
            }
        }

#endif

        #region Stream Handler reciprocal overloads

        internal static Stream SeekStreamtoStart(Stream stream)
        {
            long StartPos = stream.Position;
            return SeekStreamtoStart(stream, StartPos, out StartPos);
        }

        internal static Stream SeekStreamtoStart(Stream stream, long StartPos)
        {
            return SeekStreamtoStart(stream, StartPos, out StartPos);
        }


        #endregion

        #endregion

    }
}
