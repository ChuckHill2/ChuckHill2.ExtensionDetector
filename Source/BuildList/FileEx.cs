using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace Chuckhill2.Utilities
{
    public static class FileEx
    {
        /// <summary>
        /// The maximum size of a fully qualified (not relative) filename.
        /// </summary>
        /// <remarks>
        ///    The traditional maximum path has been 260 (256 + "\\?\") but as of Win10 this limit may be disabled and allow 
        ///    the true NTFS limit of 32767. To disable this limit within an application the registry flag HKEY_LOCAL_MACHINE
        ///    \SYSTEM\CurrentControlSet\Control\FileSystem\LongPathsEnabled [DWORD] must be set to 1, OS rebooted, 
        ///    AND enabled in the assembly manifest of all assemblies that use this variable.
        /// </remarks>
        public static readonly int MAX_PATH = (int)(Registry.GetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\FileSystem", "LongPathsEnabled", 0) ?? 0) == 1 ? short.MaxValue : 256;

        #region Win32
        private const int IMAGE_FILE_DLL = 0x2000;
        private const int IMAGE_FILE_EXECUTABLE_IMAGE = 0x0002;
        private const int IMAGE_DOS_SIGNATURE = 0x5A4D;  // 'MZ'
        private const int IMAGE_NT_SIGNATURE = 0x00004550;  // 'PE00'
        private const int MIN_EXE_SIZE = 1024;

        private static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto, Pack = 4)]
        private struct WIN32_FIND_DATA
        {
            public FileAttributes dwFileAttributes;
            public ulong ftCreationTime;
            public ulong ftLastAccessTime;
            public ulong ftLastWriteTime;
            public uint nFileSizeHigh;
            public uint nFileSizeLow;
            public uint dwReserved0;
            public uint dwReserved1;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string cFileName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
            public string cAlternateFileName;
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr FindFirstFile(string lpFileName, out WIN32_FIND_DATA lpFindFileData);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool FindNextFile(IntPtr hFindFile, out WIN32_FIND_DATA lpFindFileData);
        private const int ERROR_NO_MORE_FILES = 18;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FindClose(IntPtr hFindFile);

        /// <summary>
        /// This is a low-level alternative to:
        ///    ??? System.IO.File.GetCreationTime()
        ///    ??? System.IO.File.GetLastWriteTime()
        ///    ??? System.IO.File.GetLastAccessTime()
        ///    and
        ///    ??? System.IO.File.SetCreationTime()
        ///    ??? System.IO.File.SetLastWriteTime()
        ///    ??? System.IO.File.SetLastAccessTime()
        /// The reason is sometimes some fields do not get set properly. File open/close 3 times in rapid succession?
        /// </summary>
        [DllImport("kernel32.dll")] private static extern bool SetFileTime(IntPtr hFile, ref long creationTime, ref long lastAccessTime, ref long lastWriteTime);
        [DllImport("kernel32.dll")] private static extern bool SetFileTime(IntPtr hFile, IntPtr creationTime, ref long lastAccessTime, ref long lastWriteTime);
        [DllImport("kernel32.dll")] private static extern bool SetFileTime(IntPtr hFile, ref long creationTime, IntPtr lastAccessTime, ref long lastWriteTime);
        [DllImport("kernel32.dll")] private static extern bool SetFileTime(IntPtr hFile, ref long creationTime, ref long lastAccessTime, IntPtr lastWriteTime);
        [DllImport("kernel32.dll")] private static extern bool SetFileTime(IntPtr hFile, IntPtr creationTime, IntPtr lastAccessTime, ref long lastWriteTime);
        [DllImport("kernel32.dll")] private static extern bool SetFileTime(IntPtr hFile, ref long creationTime, IntPtr lastAccessTime, IntPtr lastWriteTime);
        [DllImport("kernel32.dll")] private static extern bool SetFileTime(IntPtr hFile, IntPtr creationTime, ref long lastAccessTime, IntPtr lastWriteTime);

        [DllImport("kernel32.dll", SetLastError = true, BestFitMapping = false)]
        private static extern bool GetFileTime(IntPtr hFile, out long creationTime, out long lastAccessTime, out long lastWriteTime);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true, BestFitMapping = false)]
        private static extern IntPtr CreateFile(string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true, BestFitMapping = false)]
        private static extern bool CloseHandle(IntPtr hFile);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true, BestFitMapping = false)]
        private static extern bool RemoveDirectory(string path);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true, BestFitMapping = false)]
        private static extern bool DeleteFile(string path);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true, BestFitMapping = false)]
        private static extern bool CopyFile(string srcfile, string dstfile, bool failIfExists);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true, BestFitMapping = false)]
        private static extern bool MoveFileEx(string src, string dst, int dwFlags);

        /// <summary>
        /// Get the attributes flag for the specified file.
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns>The current file attributes</returns>
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true, BestFitMapping = false)]
        public static extern FileAttributes GetFileAttributes(string fileName);

        /// <summary>
        /// Set the attributes flag on on the specified file.
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="attr"></param>
        /// <returns>True if successful</returns>
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true, BestFitMapping = false)]
        public static extern bool SetFileAttributes(string fileName, FileAttributes attr);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true, BestFitMapping = false)]
        private static extern bool GetFileAttributesEx(string lpFileName, int flags, out WIN32_FILE_ATTRIBUTE_DATA fileData);

        [DllImport("shell32.dll", CharSet = CharSet.Auto, SetLastError = true, ThrowOnUnmappableChar = true)]
        static extern int SHFileOperation(ref SHFILEOPSTRUCT32 lpFileOp);

        [DllImport("shell32.dll", CharSet = CharSet.Auto, SetLastError = true, ThrowOnUnmappableChar = true)]
        static extern int SHFileOperation(ref SHFILEOPSTRUCT64 lpFileOp);

        [StructLayout(LayoutKind.Sequential)]
        private struct WIN32_FILE_ATTRIBUTE_DATA
        {
            public FileAttributes dwFileAttributes;
            public long ftCreationTime;
            public long ftLastAccessTime;
            public long ftLastWriteTime;
            public long nFileSize;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Auto)]  //32bit App packs struct on 1-byte boundries otherwise the same.
        private struct SHFILEOPSTRUCT32
        {
            public IntPtr hwnd;
            public uint wFunc;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string pFrom;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string pTo;
            public ushort fFlags;
            public bool fAnyOperationsAborted;
            public IntPtr hNameMappings;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string lpszProgressTitle;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct SHFILEOPSTRUCT64
        {
            public IntPtr hwnd;
            public uint wFunc;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string pFrom;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string pTo;
            public ushort fFlags;
            public bool fAnyOperationsAborted;
            public IntPtr hNameMappings;
            [MarshalAs(UnmanagedType.LPTStr)]
            public string lpszProgressTitle;
        }
        #endregion

        public static event Action<string> Log;

        static FileEx()
        {
            Log += s => Debug.WriteLine(s);
        }

        /// <summary>
        /// Determine if file is an executable image file.
        /// </summary>
        /// <param name="filename">
        ///    Any executable image file (not necessarily .net assembly) to retrieve build date from.
        /// </param>
        /// <param name="isDll">Returns true if this is a DLL otherwise an EXE</param>
        /// <returns>True if this is an executable image file.</returns>
        public static bool IsExecutable(string filename, out bool isDll)
        {
            isDll = false;
            using (var stream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, FileOptions.SequentialScan))
            {
                if (stream.Length < MIN_EXE_SIZE) return false;
                var reader = new BinaryReader(stream);

                stream.Position = 0;  //starts with struct IMAGE_DOS_HEADER 
                if (reader.ReadInt16() != IMAGE_DOS_SIGNATURE) return false;

                stream.Seek(64 - 4, SeekOrigin.Begin); //read last field IMAGE_DOS_HEADER.e_lfanew. This is the offset where the IMAGE_NT_HEADER begins
                int offset = reader.ReadInt32();
                stream.Seek(offset, SeekOrigin.Begin);
                if (offset + 4 + 18 > stream.Length) return false; //must be an old DOS ".com"  executable.
                if (reader.ReadInt32() != IMAGE_NT_SIGNATURE) return false;

                stream.Seek(18, SeekOrigin.Current); //point to last word of IMAGE_FILE_HEADER
                short characteristics = reader.ReadInt16();
                isDll = (characteristics & IMAGE_FILE_DLL) == IMAGE_FILE_DLL;
            }

            return true;
        }

        /// <summary>
        /// Parse filename into component parts. 
        /// Name may contain wildcards.
        /// Will throw exception if directory part has any invalid chars.
        /// File does not need to exist.
        /// </summary>
        /// <param name="file">Source filename to parse.</param>
        /// <param name="dir">Returned directory part. Expands directory into absolute path. Does not include trailing slash.</param>
        /// <param name="name">Returned file name part</param>
        /// <param name="ext">Returned file extension. Includes leading '.'</param>
        public static void GetPathParts(string file, out string dir, out string name, out string ext)
        {
            int i = file.LastIndexOf('\\');
            dir = i == -1 ? "" : file.Substring(0, i);
            var nameext = i == -1 ? file : file.Substring(i + 1);

            i = nameext.LastIndexOf('.');
            name = i == -1 ? nameext : nameext.Substring(0, i);
            ext = i == -1 ? "" : nameext.Substring(i);

            if (dir == "") dir = ".";
            dir = GetFullPath(dir);
        }

        /// <summary>
        /// Detect if this binary file is a .NET assembly.
        /// </summary>
        /// <param name="filename">Name of file to test</param>
        /// <returns>True if this is a .NET assembly.</returns>
        /// https://social.msdn.microsoft.com/Forums/en-US/827c985c-9d12-48ad-9b45-1eca90702983/determining-whether-a-file-is-an-assembly?forum=csharpgeneral
        public static bool IsAssembly(string filename)
        {
            using (Stream fs = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, FileOptions.SequentialScan))
            {
                if (fs.Length < 512) return false; //Round up from 368 -- file is not big enough
                BinaryReader reader = new BinaryReader(fs);

                fs.Position = 0;  //starts with struct IMAGE_DOS_HEADER 
                if (reader.ReadInt16() != IMAGE_DOS_SIGNATURE) return false;

                fs.Seek(64 - 4, SeekOrigin.Begin); //read last field IMAGE_DOS_HEADER.e_lfanew. This is the offset where the IMAGE_NT_HEADER begins
                int offset = reader.ReadInt32();
                fs.Seek(offset, SeekOrigin.Begin);
                if (offset + 4 + 18 > fs.Length) return false; //must be an old DOS ".com"  executable.
                if (reader.ReadInt32() != IMAGE_NT_SIGNATURE) return false;

                //We can also show all these value, but we will be       
                //limiting to the CLI header test.

                var machine = reader.ReadUInt16();
                var sections = reader.ReadUInt16();
                var timestamp = reader.ReadUInt32();
                var pSymbolTable = reader.ReadUInt32();
                var noOfSymbol = reader.ReadUInt32();
                var optionalHeaderSize = reader.ReadUInt16();
                var characteristics = reader.ReadUInt16();

                //Now we are at the end of the PE Header and from here, the
                //PE Optional Headers starts...
                //To go directly to the datadictionary, we'll increase the      
                //stream???s current position to with 96 (0x60). 96 because,
                //   28 for Standard fields
                //   68 for NT-specific fields
                //From here DataDictionary starts...and its of total 128 bytes. DataDictionay has 16 directories in total,
                //doing simple maths 128/16 = 8.
                //So each directory is of 8 bytes.
                //In this 8 bytes, 4 bytes is of RVA and 4 bytes of Size.
                //btw, the 15th directory consist of CLR header! if its 0, its not a CLR file :)

                ushort dataDictionaryStart = Convert.ToUInt16(Convert.ToUInt16(fs.Position) + 0x60); //96
                uint[] dataDictionaryRVA = new uint[16];
                uint[] dataDictionarySize = new uint[16];

                fs.Position = dataDictionaryStart;
                for (int i = 0; i < 15; i++)
                {
                    dataDictionaryRVA[i] = reader.ReadUInt32();
                    dataDictionarySize[i] = reader.ReadUInt32();
                }

                return (dataDictionaryRVA[14] != 0);
            }
        }

        /// <summary>
        /// Gets the build/link timestamp from the specified executable file header.
        /// </summary>
        /// <remarks>
        ///    WARNING: When compiled in a .netcore application/library, the PE timestamp 
        ///    is NOT set with the the application link time. It contains some other non-
        ///    timestamp (hash?) value. To force the .netcore linker to embed the true 
        ///    timestamp as previously, add or set the csproj property 
        ///    "&lt;Deterministic&gt;false&lt;/Deterministic&gt;".
        /// </remarks>
        /// <param name="filePath">
        ///    Any executable image file (not necessarily .net assembly) to retrieve build date
        ///    from. If file is not an executable image file, the file creation date is returned.
        /// </param>
        /// <returns>The local DateTime that the specified executable image file was built.</returns>
        public static DateTime ExecutableTimestamp(string filePath)
        {
            uint TimeDateStamp = 0;
            using (FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                if (stream.Length < MIN_EXE_SIZE) return File.GetCreationTime(filePath);
                var reader = new BinaryReader(stream);
                stream.Position = 0;  //starts with struct IMAGE_DOS_HEADER 
                if (reader.ReadInt16() != IMAGE_DOS_SIGNATURE) return File.GetCreationTime(filePath);

                stream.Seek(64 - 4, SeekOrigin.Begin); //read last field IMAGE_DOS_HEADER.e_lfanew. This is the offset where the IMAGE_NT_HEADER begins
                int offset = reader.ReadInt32();
                stream.Seek(offset, SeekOrigin.Begin);
                if (reader.ReadInt32() != IMAGE_NT_SIGNATURE) return File.GetCreationTime(filePath);

                stream.Position += 4; //offset of IMAGE_FILE_HEADER.TimeDateStamp
                TimeDateStamp = reader.ReadUInt32(); //unix-style time_t value
            }

            DateTime returnValue = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(TimeDateStamp);
            returnValue = returnValue.ToLocalTime();

            if (returnValue < new DateTime(2000, 1, 1) || returnValue > DateTime.Now)
            {
                //PEHeader link timestamp field is random junk because csproj property "Deterministic" == true
                //so we just return the 2nd best "build" time (iffy, unreliable).
                return File.GetCreationTime(filePath);
            }

            return returnValue;
        }

        /// <summary>
        /// Returns an enumerable collection of file names that match a search pattern in a specified path.
        /// Same as System.IO.Directory.EnumerateFiles(), except 42% faster on a SSD.
        /// To filter list, use this.Where(m=>m.something(m)) linq clause.
        /// </summary>
        /// <remarks>Does not throw exceptions. Error messages are written to debug output window.</remarks>
        /// <param name="folder">The relative or absolute path to the directory to search. This string is not case-sensitive.</param>
        /// <param name="searchOption">One of the enumeration values that specifies whether the search operation should include only the current directory or should include all subdirectories. The default value is System.IO.SearchOption.TopDirectoryOnly.</param>
        /// <returns>An enumerable collection of all the full names (including paths) for the files in the root directory specified by 'folder'.</returns>
        public static IEnumerable<string> EnumerateFiles(string folder, SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            WIN32_FIND_DATA fd = new WIN32_FIND_DATA();
            IntPtr hFind = FindFirstFile(Path.Combine(folder, "*"), out fd);
            if (hFind == INVALID_HANDLE_VALUE)
            {
                Log($"EnumerateFiles(\"{folder}\"): {new Win32Exception().Message}");
                yield break;
            }

            do
            {
                if (fd.cFileName == "." || fd.cFileName == "..") continue;   //pseudo-directory
                string path = Path.Combine(folder, fd.cFileName);
                if (path.Length > MAX_PATH) continue;
                if ((fd.dwFileAttributes & FileAttributes.Directory) != 0)
                {
                    if (searchOption == SearchOption.AllDirectories)
                    {
                        if ((fd.dwFileAttributes & FileAttributes.ReparsePoint) != 0) continue; //don't dive down into file links. they may be recursive!
                        foreach (var x in EnumerateFiles(path, searchOption))
                        {
                            yield return x;
                        }
                    }

                    continue;
                }

                yield return path;

            } while (FindNextFile(hFind, out fd));
            var lastErr = Marshal.GetLastWin32Error();
            if (lastErr != 0 && lastErr != ERROR_NO_MORE_FILES) Log($"EnumerateFiles(\"{folder}\"): {new Win32Exception(lastErr).Message}");

            if (!FindClose(hFind)) Log($"EnumerateFiles(\"{folder}\"): {new Win32Exception().Message}"); ;

            yield break;
        }

        /// <summary>
        /// Returns an enumerable collection of file names that match a search pattern in a specified path.
        /// Same as System.IO.Directory.EnumerateFiles(), except 42% faster on a SSD.
        /// To filter list, use this.Where(m=>m.something(m)) linq clause.
        /// </summary>
        /// <remarks>Does not throw exceptions. Error messages are written to debug output window.</remarks>
        /// <param name="folder">The relative or absolute path to the directory to search. This string is not case-sensitive.</param>
        /// <param name="searchOption">One of the enumeration values that specifies whether the search operation should include only the current directory or should include all subdirectories. The default value is System.IO.SearchOption.TopDirectoryOnly.</param>
        /// <returns>An enumerable collection of all the full directory paths in the root directory specified by 'folder'.</returns>
        public static IEnumerable<string> EnumerateFolders(string folder, SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            WIN32_FIND_DATA fd = new WIN32_FIND_DATA();
            IntPtr hFind = FindFirstFile(Path.Combine(folder, "*"), out fd);
            if (hFind == INVALID_HANDLE_VALUE)
            {
                Log($"EnumerateFolders(\"{folder}\"): {new Win32Exception().Message}");
                yield break;
            }

            do
            {
                if (fd.cFileName == "." || fd.cFileName == "..") continue;   //pseudo-directory
                string path = Path.Combine(folder, fd.cFileName);
                if (path.Length > MAX_PATH) continue;
                if ((fd.dwFileAttributes & FileAttributes.Directory) != 0)
                {
                    yield return path;

                    if (searchOption == SearchOption.AllDirectories)
                    {
                        if ((fd.dwFileAttributes & FileAttributes.ReparsePoint) != 0) continue; //don't dive down into file links. they may be recursive!
                        foreach (var x in EnumerateFolders(path, searchOption))
                        {
                            yield return x;
                        }
                    }

                    continue;
                }

                yield return path;

            } while (FindNextFile(hFind, out fd));
            var lastErr = Marshal.GetLastWin32Error();
            if (lastErr != 0 && lastErr != ERROR_NO_MORE_FILES) Log($"EnumerateFolders(\"{folder}\"): {new Win32Exception(lastErr).Message}");

            if (!FindClose(hFind)) Log($"EnumerateFolders(\"{folder}\"): {new Win32Exception().Message}");

            yield break;
        }

        /// <summary>
        /// Delete a folder and everything in it.
        /// </summary>
        /// <remarks>
        ///    Does not throw exceptions. Error messages are written to debug output window.
        ///    Even deletes files and folders that have any System, Hidden, or Readonly attributes.
        /// </remarks>
        /// <param name="folder">folder tree to delete</param>
        /// <param name="force">True to continue deleting whatever files and sub-folders that it can even though not all files have been deleted.</param>
        /// <returns>True if successful or if 'force' is true</returns>
        public static bool DeleteDirectoryTree(string folder, bool force = false)
        {
            WIN32_FIND_DATA fd = new WIN32_FIND_DATA();
            IntPtr hFind = FindFirstFile(Path.Combine(folder, "*"), out fd);
            if (hFind == INVALID_HANDLE_VALUE) { Log($"{nameof(DeleteDirectoryTree)}(\"{folder}\"): {new Win32Exception().Message}"); return force; }

            try
            {
                do
                {
                    if (fd.cFileName == "." || fd.cFileName == "..") continue;   //pseudo-directory
                    string path = Path.Combine(folder, fd.cFileName);
                    if (path.Length > MAX_PATH) continue;

                    if ((fd.dwFileAttributes & FileAttributes.ReparsePoint) != 0)
                    {
                        //don't dive down into file links. they may be recursive!
                        if (!Delete(path) && force) return false;
                    }

                    if ((fd.dwFileAttributes & (FileAttributes.ReadOnly | FileAttributes.Hidden | FileAttributes.System)) != 0)
                    {
                        SetFileAttributes(path, ~(~fd.dwFileAttributes | (FileAttributes.ReadOnly | FileAttributes.Hidden | FileAttributes.System)));
                    }

                    if ((fd.dwFileAttributes & FileAttributes.Directory) != 0)
                    {
                        if (!DeleteDirectoryTree(path, force)) return false;
                        continue;
                    }

                    if (!Delete(path) && force) return false;

                } while (FindNextFile(hFind, out fd));
            }
            finally
            {
                FindClose(hFind);
            }

            if (!DeleteDirectory(folder) && force) return false;
            return true;
        }

        /// <summary>
        /// Compute unique MD5 hash of file contents.
        /// DO NOT USE for security encryption.
        /// </summary>
        /// <param name="filename">File content to generate hash from.</param>
        /// <returns>Guid hash. Upon error (null, file not found, file locked, invalid permissions, etc) returns empty guid.</returns>
        public static Guid GetHash(string filename)
        {
            if (string.IsNullOrEmpty(filename)) return Guid.Empty;
            try
            {
                using (var fs = new FileStream(filename, FileMode.Open, System.Security.AccessControl.FileSystemRights.Read, FileShare.ReadWrite, 1024 * 1024, FileOptions.SequentialScan))
                {
                    var md5 = new MD5CryptoServiceProvider(); //to be multi-threaded compliant, this must not be a static variable.
                    var result = new Guid(md5.ComputeHash(fs));
                    md5.Dispose();
                    return result;
                }
            }
            catch
            {
                return Guid.Empty;
            }
        }

        /// <summary>
        /// Get earliest file or directory datetime.
        /// Empirically, it appears that the LastAccess or LastWrite times can be 
        /// earlier than the Creation time! For consistency, this method just 
        /// returns the earliest of these three file datetimes.
        /// </summary>
        /// <param name="filename">Full directory or filepath</param>
        /// <returns>DateTime</returns>
        public static DateTime GetCreationDate(string filename)
        {
            GetFileTime(filename, out long creationTime, out long lastAccessTime, out long lastWriteTime);
            long timeMin = creationTime;
            if (lastAccessTime < timeMin) timeMin = lastAccessTime;
            if (lastWriteTime < timeMin) timeMin = lastWriteTime;
            var dtMin = DateTime.FromFileTime(timeMin);

            //Forget hi-precision and DateTimeKind. It just complicates comparisons. This is more than good enough.
            return new DateTime(dtMin.Year, dtMin.Month, dtMin.Day, dtMin.Hour, dtMin.Minute, 0);
        }

        /// <summary>
        /// Check if file is read/copyable
        /// </summary>
        /// <param name="filename">Full path of file to test/</param>
        /// <returns>True if readable.</returns>
        public static bool IsReadable(string filename)
        {
            const uint GENERIC_READ = 0x80000000;
            const int FILE_FLAG_NO_BUFFERING = 0x20000000; //not supported in enum FileOptions

            //try { using (var x = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, (FileOptions)FILE_FLAG_NO_BUFFERING)) { } }
            //catch { return false;  }
            //return true;

            var handle = CreateFile(filename, GENERIC_READ, (uint)FileShare.ReadWrite, IntPtr.Zero, (uint)FileMode.Open, FILE_FLAG_NO_BUFFERING, IntPtr.Zero);
            if (handle == INVALID_HANDLE_VALUE) return false;
            CloseHandle(handle);
            return true;
        }

        /// <summary>
        /// Delete the specified file.
        /// </summary>
        /// <param name="filename">Full name of file to delete.</param>
        /// <returns>True if successfully deleted</returns>
        /// <remarks>Does not throw exceptions.</remarks>
        public static bool Delete(string filename)
        {
            var success = DeleteFile(filename);
            if (!success)
            {
                var lastErr = Marshal.GetLastWin32Error();
                Log($"FileEx.Delete(\"{filename}\"): {(lastErr == 0 ? "Failed" : new Win32Exception(lastErr).Message)}");
            }
            return success;
        }

        /// <summary>
        /// Delete the specified folder.
        /// </summary>
        /// <param name="folder">Full name of folder to delete.</param>
        /// <returns>True if successfully deleted</returns>
        /// <remarks>Does not throw exceptions. Error messages are written to debug output window. Folder must be empty.</remarks>
        public static bool DeleteDirectory(string folder)
        {
            var success = RemoveDirectory(folder);
            if (!success) Log($"FileEx.DeleteDirectory(\"{folder}\"): {new Win32Exception().Message}");
            if (!success)
            {
                var lastErr = Marshal.GetLastWin32Error();
                Log($"FileEx.DeleteDirectory(\"{folder}\"): {(lastErr == 0 ? "Failed" : new Win32Exception(lastErr).Message)}");
            }
            return success;
        }

        /// <summary>
        ///  Copy a file to a new filename.
        /// </summary>
        /// <param name="srcfile">File name of source file</param>
        /// <param name="dstFile">File name of destination file</param>
        /// <param name="failIfExists"></param>
        /// <returns>True if successful</returns>
        /// <remarks>Does not throw exceptions. Error messages are written to debug output window.</remarks>
        public static bool Copy(string srcfile, string dstFile, bool failIfExists = false)
        {
            var success = CopyFile(srcfile, dstFile, failIfExists);
            if (!success)
            {
                var lastErr = Marshal.GetLastWin32Error();
                Log($"FileEx.Copy(\"{srcfile}\", \"{dstFile}\"): {(lastErr==0?"Failed":new Win32Exception(lastErr).Message)}");
            }
            return success;
        }

        /// <summary>
        /// Move a file to a new destination.
        /// </summary>
        /// <param name="srcfile">File name of source file</param>
        /// <param name="dstFile">File name of destination file</param>
        /// <returns>True if successful</returns>
        /// <remarks>
        /// Does not throw exceptions. Error messages are written to debug output window.
        /// A pre-existing destination file is overwritten.
        /// May move files across drives.
        /// </remarks>
        public static bool Move(string srcfile, string dstFile)
        {
            const int MOVEFILE_COPY_ALLOWED = 0x02; //If the file is to be moved to a different volume, the function simulates the move by using the CopyFile and DeleteFile functions.
            const int MOVEFILE_REPLACE_EXISTING = 0x01; //If a file named dstFile exists, the function replaces its contents with the contents of the srcfile file.

            var success = MoveFileEx(srcfile, dstFile, MOVEFILE_COPY_ALLOWED | MOVEFILE_REPLACE_EXISTING);
            if (!success)
            {
                var lastErr = Marshal.GetLastWin32Error();
                Log($"FileEx.Move(\"{srcfile}\", \"{dstFile}\"): {(lastErr == 0 ? "Failed" : new Win32Exception(lastErr).Message)}");
            }
            return success;
        }

        /// <summary>
        /// Get length of specified file 
        /// </summary>
        /// <param name="filename"></param>
        /// <returns>File length or -1 upon error.</returns>
        /// <remarks>Does not throw exceptions.</remarks>
        public static long Length(string filename)
        {
            bool success = GetFileAttributesEx(filename, 0, out WIN32_FILE_ATTRIBUTE_DATA fileData);
            if (!success) return -1L;
            return fileData.nFileSize;
        }

        /// <summary>
        /// Check if a file exists.
        /// </summary>
        /// <param name="filename"></param>
        /// <returns>True if file exists.</returns>
        /// <remarks>Does not throw exceptions.</remarks>
        public static bool Exists(string filename) => (int)GetFileAttributes(filename) != -1;

        /// <summary>
        /// Get all 3 datetime fields for a given file in FileTime (64-bit) format.
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="creationTime"></param>
        /// <param name="lastAccessTime"></param>
        /// <param name="lastWriteTime"></param>
        /// <returns>True if successful</returns>
        public static bool GetFileTime(string filename, out long creationTime, out long lastAccessTime, out long lastWriteTime)
        {
            creationTime = lastAccessTime = lastWriteTime = 0;

            var hFile = CreateFile(filename, 0x0080, 0x00000003, IntPtr.Zero, 3, 0x80, IntPtr.Zero);
            if (hFile == INVALID_HANDLE_VALUE) return false;
            bool success = GetFileTime(hFile, out creationTime, out lastAccessTime, out lastWriteTime);
            CloseHandle(hFile);
            return success;
        }

        /// <summary>
        /// Set datetime fields for a given file in FileTime (64-bit) format. Time field value 0 == not modified.
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="creationTime"></param>
        /// <param name="lastAccessTime"></param>
        /// <param name="lastWriteTime"></param>
        /// <returns>True if successful</returns>
        public static bool SetFileTime(string filename, long creationTime, long lastAccessTime, long lastWriteTime)
        {
            bool success;
            var hFile = CreateFile(filename, 0x0100, 0x00000003, IntPtr.Zero, 3, 0x80, IntPtr.Zero);
            if (hFile == INVALID_HANDLE_VALUE) return false;

            var fields = (creationTime == 0 ? 0 : 1) | (lastAccessTime == 0 ? 0 : 2) | (lastWriteTime == 0 ? 0 : 4);

            switch (fields)
            {
                case 0x01: success = SetFileTime(hFile, ref creationTime, IntPtr.Zero, IntPtr.Zero); break;
                case 0x02: success = SetFileTime(hFile, IntPtr.Zero, ref lastAccessTime, IntPtr.Zero); break;
                case 0x03: success = SetFileTime(hFile, ref creationTime, ref lastAccessTime, IntPtr.Zero); break;
                case 0x04: success = SetFileTime(hFile, IntPtr.Zero, IntPtr.Zero, ref lastWriteTime); break;
                case 0x05: success = SetFileTime(hFile, ref creationTime, IntPtr.Zero, ref lastWriteTime); break;
                case 0x06: success = SetFileTime(hFile, IntPtr.Zero, ref lastAccessTime, ref lastWriteTime); break;
                case 0x07: success = SetFileTime(hFile, ref creationTime, ref lastAccessTime, ref lastWriteTime); break;
                default: success = false; break;
            }

            CloseHandle(hFile);
            return success;
        }

        /// <summary>
        ///  Securely find an unused filename in a multi-threaded environment.
        /// </summary>
        /// <param name="srcFilename">Suggested filename</param>
        /// <returns>Returns a (maybe new) zero-length filename as a placeholder.</returns>
        private static string GetUniqueFilename(string srcFilename)
        {
            srcFilename = GetFullPath(srcFilename);
            if (srcFilename==null) return null;

            lock (GetUniqueFilename_Lock)
            {
                string pathFormat = null;
                string newFilename = srcFilename;
                int index = 1;

                string dir = Path.GetDirectoryName(srcFilename);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                while (FileEx.Exists(newFilename))
                {
                    if (pathFormat == null)
                    {
                        string path = Path.Combine(dir, Path.GetFileNameWithoutExtension(srcFilename));
                        if (path[path.Length - 1] == ')')
                        {
                            int i = path.LastIndexOf('(');
                            if (i > 0) path = path.Substring(0, i);
                        }
                        pathFormat = path + "({0:00})" + Path.GetExtension(srcFilename);
                    }
                    newFilename = string.Format(pathFormat, index++);
                }

                File.Create(newFilename).Dispose();  //create place-holder file.
                return newFilename;
            }

        }
        private static readonly Object GetUniqueFilename_Lock = new Object();

        /// <summary>
        /// Return valid full path name or null if invalid.
        /// </summary>
        /// <remarks>Does not throw exceptions.</remarks>
        /// <param name="path">path name to test</param>
        /// <returns>full path name or null if invalid</returns>
        public static string GetFullPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            try
            {
                return Path.GetFullPath(path);
            }
            catch
            {
                return null;
            }
        }
    }
}
