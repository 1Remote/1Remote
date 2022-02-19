using System;
using System.IO;
using System.Linq;
using System.Timers;
using Shawn.Utils;

namespace PRM.Core.Service
{
    [Flags]
    public enum FileWatcherMode
    {
        ContentChanged = 1,
        CreatedOrDeleted = 2,
        All = ContentChanged | CreatedOrDeleted
    }

    public class PathChangedEventArgs : EventArgs
    {
        public string Directory { get; }
        public string Path { get; }
        public bool PathExists { get; }

        public PathChangedEventArgs(string directory, string path, bool pathExists)
        {
            Directory = directory;
            Path = path;
            PathExists = pathExists;
        }
    }

    //public interface IFileWatcherFactory
    //{
    //    FileWatcher Create(FileWatcherMode mode, string directory, TimeSpan existenceCheckingInterval, string filter = "*.*");
    //}

    //public class FileWatcherFactory : IFileWatcherFactory
    //{

    //    public FileWatcherFactory(IFilesystemProvider filesystem)
    //    {
    //        this.filesystem = filesystem;
    //    }

    //    public FileWatcher Create(FileWatcherMode mode, string directory, TimeSpan existenceCheckingInterval, string filter = "*.*")
    //    {
    //        return new(filesystem, mode, directory, existenceCheckingInterval, filter);
    //    }
    //}

    public class FileWatcher : IDisposable
    {
        private static readonly SimpleLogHelperObject Logger = SimpleLogHelper.StaticInstance;

        private readonly FileWatcherMode _mode;
        private readonly string _filter;
        protected readonly string Directory;
        private readonly Timer _existenceCheckingTimer;

        private FileSystemWatcher _watcher;

        public event EventHandler<PathChangedEventArgs> PathChanged;

        public FileWatcher(FileWatcherMode mode, string directory, TimeSpan existenceCheckingInterval, string filter = "*.*")
        {
            this._mode = mode;
            this._filter = filter;
            Directory = directory.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

            _watcher = TryToCreateWatcher(Directory);

            _existenceCheckingTimer = new Timer()
            {
                AutoReset = true,
                Interval = existenceCheckingInterval.TotalMilliseconds,
                Enabled = true,
            };
            _existenceCheckingTimer.Elapsed += (o, e) => CheckExistence();
        }

        private FileSystemWatcher TryToCreateWatcher(string directory)
        {
            try
            {
                var watcher = new FileSystemWatcher()
                {
                    Path = directory,
                    Filter = _filter,
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
                };

                if (_mode.HasFlag(FileWatcherMode.ContentChanged))
                {
                    watcher.Changed += OnChanged;
                }
                if (_mode.HasFlag(FileWatcherMode.CreatedOrDeleted))
                {
                    watcher.Created += OnCreated;
                    watcher.Deleted += OnDeleted;
                    watcher.Renamed += OnRenamed;
                }

                watcher.EnableRaisingEvents = true;

                return watcher;
            }
            catch (ArgumentException)
            {
                Logger.Warning("Watcher for {0} couldn't be created: path doesn't exist", Directory);
                // The path doesn't exist. That's fine, the existenceCheckingTimer will try and
                // re-create us shortly if needs be
                return null;
            }
            catch (FileNotFoundException e)
            {
                // This can happen if e.g. the user points us towards 'My Documents' on Vista+, and we get an
                // 'Error reading the xxx directory'
                Logger.Warning($"Watcher for {Directory} couldn't be created: {e.Message}", e);
                // We'll try again soon
                return null;
            }
        }

        private void CheckExistence()
        {
            var exists = System.IO.Directory.Exists(Directory);
            if (exists && _watcher == null)
            {
                Logger.Debug("Path {0} appeared. Creating watcher", Directory);
                _watcher = TryToCreateWatcher(Directory);
            }
            else if (!exists && _watcher != null)
            {
                Logger.Debug("Path {0} disappeared. Destroying watcher", Directory);
                _watcher.Dispose();
                _watcher = null;
            }
        }

        private void OnDeleted(object source, FileSystemEventArgs e)
        {
            if (e.FullPath == null)
            {
                Logger.Warning("OnDeleted: e.FullPath is null. Ignoring...");
                return;
            }

            RecordPathChange(e.FullPath, pathExists: false);
        }

        private void OnChanged(object source, FileSystemEventArgs e)
        {
            if (e.FullPath == null)
            {
                Logger.Warning("OnChnaged: e.FullPath is null. Ignoring...");
                return;
            }

            // We don't want to raise Changed events on directories - those are file creations or deletions.
            // Creations will pop up in OnCreated, deletions in OnDeletion.
            // We do however want to handle file changes

            try
            {
                if (File.Exists(e.FullPath))
                    RecordPathChange(e.FullPath, pathExists: true);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, $"Failed to see whether file/dir {e.FullPath} exists");
            }
        }

        private void OnCreated(object source, FileSystemEventArgs e)
        {
            if (e.FullPath == null)
            {
                Logger.Warning("OnCreated: e.FullPath is null. Ignoring...");
                return;
            }

            RecordPathChange(e.FullPath, pathExists: true);
        }

        private void OnRenamed(object source, RenamedEventArgs e)
        {
            // Apparently e.FullPath or e.OldName can be null, see #112
            // Not sure why this might be, but let's work around it...
            if (e.FullPath == null)
            {
                Logger.Warning("OnRenamed: e.FullPath is null. Ignoring...");
                return;
            }

            RecordPathChange(e.FullPath, pathExists: true);
            // Irritatingly, e.OldFullPath will throw an exception if the path is longer than the windows max
            // (but e.FullPath is fine).
            // So, construct it from e.FullPath and e.OldName.
            // Note that we're using Pri.LongPath to get a Path.GetDirectoryName implementation that can handle
            // long paths

            if (e.OldName == null)
            {
                Logger.Warning("OnRenamed: e.OldName is null. Not sure why");
                return;
            }

            // Note that e.FullPath could be a file or a directory. If it's a directory, it could be a drive
            // root. If it's a drive root, Path.GetDirectoryName will return null. I'm not sure *how* it could
            // be a drive root though... Record a change to the drive root if so.
            var oldFullPathDirectory = Path.GetDirectoryName(e.FullPath);
            if (oldFullPathDirectory == null)
            {
                RecordPathChange(e.FullPath, pathExists: true);
            }
            else
            {
                var oldFileName = Path.GetFileName(e.OldName);
                var oldFullPath = Path.Combine(oldFullPathDirectory, oldFileName);

                RecordPathChange(oldFullPath, pathExists: false);
            }
        }

        private void RecordPathChange(string path, bool pathExists)
        {
            //// First, we need to convert to a long path, just in case anyone's using the short path
            //// We can't do this if we don't expect the file to exist any more...
            //// There's also a chance that the file no longer exists. Catch that exception.
            //// If a short path is renamed or deleted, then we do our best with it in a bit, by removing the short bits
            //// If short path segments are used in the base directory path in this case, tough.
            //if (pathExists && path.Contains("~"))
            //    path = GetLongPathName(path);

            // https://msdn.microsoft.com/en-us/library/dd465121.aspx
            if (!path.StartsWith(Directory, StringComparison.OrdinalIgnoreCase))
            {
                Logger.Warning($"Ignoring change to {path}, as it isn't in {Directory}");
                return;
            }

            var subPath = path.Substring(Directory.Length);

            // If it contains a tilde, then it's a short path that squeezed through GetLongPath above
            // (e.g. because it was a deletion), then strip it back to the first component without an ~
            subPath = StripShortPathSegments(subPath);

            OnPathChanged(subPath, pathExists);
        }

        public virtual void OnPathChanged(string path, bool pathExists)
        {
            PathChanged?.Invoke(this, new PathChangedEventArgs(Directory, path, pathExists));
        }

        //private string GetLongPathName(string path)
        //{
        //    try
        //    {
        //        path = PathEx.GetLongPathName(path);
        //    }
        //    catch (FileNotFoundException)
        //    {
        //        logger.Warning($"Path {path} changed, but it doesn't exist any more");
        //    }
        //    return path;
        //}

        private string StripShortPathSegments(string path)
        {
            if (!path.Contains('~'))
                return path;

            var parts = path.Split(new[] { Path.DirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
            var filteredPaths = parts.TakeWhile(x => !x.Contains('~'));
            return String.Join(Path.DirectorySeparatorChar.ToString(), filteredPaths);
        }

        public virtual void Dispose()
        {
            if (_watcher != null)
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Dispose();
            }

            _existenceCheckingTimer.Stop();
            _existenceCheckingTimer.Dispose();
        }
    }
}
