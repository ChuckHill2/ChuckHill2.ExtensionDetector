using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace ChuckHill2.ExtensionDetector
{
    /// <summary>
    /// Options for MagicDetector.LibMagic()
    /// </summary>
    public enum LibMagicOptions
    {
        /// <summary>
        /// Returns the mime type. e.g. "text/plain"
        /// </summary>
        MimeType,
        /// <summary>
        /// Returns the encoding for text based files. e.g. "text/plain;utf-8
        /// </summary>
        MimeEncoding,
        /// <summary>
        /// Returns a 1-line free-form description of the file.
        /// </summary>
        Description,
        /// <summary>
        /// Return a slash-separated list of different extensions for this file content or "???" if not known by Libmagic.
        /// </summary>
        Extensions,
        /// <summary>
        /// Version of the Magic database.
        /// </summary>
        Version,
    }

    /// <summary>
    /// Utility to retrieve info based upon a file's content. Otherwise known as LibMagic.
    /// This is based upon  https://github.com/hey-red/Mime on 09/07/2022.
    /// It also ONLY supports Windows x64.
    /// </summary>
    public static class MagicDetector
    {
        private static string LibMagicDirectory;
        private static string LibMagicDataBase;
        private static object InitLibmagic_Lock = new object();
        private const MagicOpenFlags MagicMimeFlagsBase =
                                     MagicOpenFlags.MAGIC_ERROR |
                                     MagicOpenFlags.MAGIC_NO_CHECK_COMPRESS |
                                     MagicOpenFlags.MAGIC_NO_CHECK_ELF |
                                     MagicOpenFlags.MAGIC_NO_CHECK_APPTYPE;

        /// <summary>
        /// Get properties of a file based upon its content.
        /// </summary>
        /// <param name="filename">File to inspect.</param>
        /// <param name="option">The property to retrieve</param>
        /// <returns>The returned info</returns>
        /// <exception cref="ArgumentNullException">filename must not be null.</exception>
        /// <exception cref="FileNotFoundException">filename must exist.</exception>
        /// <exception cref="BadImageFormatException">LibMagic supports 64-bit assemblies only.</exception>
        /// <exception cref="MagicException">File content cannot be read or database is corrupted.</exception>
        public static string LibMagic(string filename, LibMagicOptions option)
        {
            if (string.IsNullOrEmpty(filename)) throw new ArgumentNullException(nameof(filename));
            if (!File.Exists(filename)) throw new FileNotFoundException($"File \"{filename}\" not found.", filename);

            if (LibMagicDirectory == null)
            {
                lock (InitLibmagic_Lock) if (LibMagicDirectory == null) InitLibmagic();
            }

            var flags = MagicOpenFlags.MAGIC_NONE;
            switch (option)
            {
                case LibMagicOptions.MimeType: flags = MagicMimeFlagsBase | MagicOpenFlags.MAGIC_MIME_TYPE; break;
                case LibMagicOptions.MimeEncoding: flags = MagicMimeFlagsBase | MagicOpenFlags.MAGIC_MIME_TYPE | MagicOpenFlags.MAGIC_MIME_ENCODING; break;
                case LibMagicOptions.Description: flags = MagicMimeFlagsBase; break;
                case LibMagicOptions.Extensions: flags = MagicMimeFlagsBase | MagicOpenFlags.MAGIC_EXTENSION; break;
                case LibMagicOptions.Version: return Magic.Version.ToString();
                default: throw new NotSupportedException($"Option '{option}' not supported.");
            }

            using (var magic = new Magic(flags, LibMagicDataBase)) //NOT thread-safe!!!
            {
                var fn = MyGetShortPathName(filename);
                return magic?.Read(fn);
            }
        }

        private static string MyGetShortPathName(string longFileName)
        {
            // LibMagic API are strictly ANSI, so if a filename has unicode chars in it, the Libmagic 
            // binary will throw an exception. We trick it by using the shortname equivalant.

            var sb = new StringBuilder((longFileName.Length+1) *2);
            //File 'longFileName' must exist otherwise GetShortPathName will fail and return a zero-length string.
            var length = GetShortPathName(longFileName, sb, (longFileName.Length+1) *2);
            return sb.ToString();
        }

        //Exclusively used by MyGetShortPathName();
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern uint GetShortPathName(
           [MarshalAs(UnmanagedType.LPTStr)]
           string lpszLongPath,
           [MarshalAs(UnmanagedType.LPTStr)]
           StringBuilder lpszShortPath,
           int cchBuffer);

        private static void InitLibmagic()
        {
            //Binaries from https://github.com/hey-red/Mime, gzip compressed with 7-Zip utility, and embedded into this assmbly.
            var libMagicDirectory = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);

            if (IntPtr.Size != 8) throw new BadImageFormatException("LibMagic supports 64-bit assemblies only.");

            var files = new string[]
            {
                Path.Combine(libMagicDirectory, "libgnurx-0.dll"),
                Path.Combine(libMagicDirectory, "libmagic-1.dll"),
                Path.Combine(libMagicDirectory, "magic.mgc")
            };

            Assembly asm = null;
            string[] resnames = null;

            if (!Directory.Exists(libMagicDirectory)) Directory.CreateDirectory(libMagicDirectory);
            foreach (var f in files)
            {
                if (File.Exists(f)) continue;
                if (asm == null)
                {
                    asm = Assembly.GetExecutingAssembly();
                    resnames = asm.GetManifestResourceNames();
                }

                var name = Path.GetFileName(f) + ".gz";
                var fullname = resnames.FirstOrDefault(s => s.EndsWith("." + name, StringComparison.OrdinalIgnoreCase));
                if (fullname == null) throw new DllNotFoundException($"LibMagic file {name} not found embedded within {Path.GetFileName(asm.CodeBase)}.");

                using (var fs = new FileStream(f, FileMode.Create, FileAccess.Write, FileShare.ReadWrite, 4096, FileOptions.WriteThrough))
                {
                    using (var decompressor = new GZipStream(asm.GetManifestResourceStream(fullname), CompressionMode.Decompress))
                        decompressor.CopyTo(fs);
                }
            }

            LibMagicDataBase = MyGetShortPathName(files[2]);
            LibMagicDirectory = libMagicDirectory;
        }
    }
}
