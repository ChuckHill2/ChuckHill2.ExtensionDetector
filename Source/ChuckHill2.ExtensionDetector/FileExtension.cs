using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace ChuckHill2.ExtensionDetector
{
    /// <summary>
    /// Various static methods to retrieve the extension of a file.
    /// </summary>
    public static class FileExtension
    {
        #region Debugging
        /// <summary>
        ///Emit tab-delimited verbose list of debugging files DebugLog.txt and DebugLogSummary.txt.  
        ///Importable into Excel (if double-quote escapes disabled). Default is false.
        ///Set this property to false to flush results at will. It will automatically flush upon exit.
        /// </summary>
        public static bool EnableDebug 
        {
            get => __enableDebug;
            set
            {
                if (value == __enableDebug) return;
                __enableDebug = value;
                if (__enableDebug)
                {
                    AppDomain.CurrentDomain.DomainUnload += CurrentDomain_DomainUnload;
                    AppDomain.CurrentDomain.ProcessExit += CurrentDomain_DomainUnload;
                    DebugDict = new Dictionary<string, int>(StringComparer.Ordinal);
                    SR = new StreamWriter("DebugLog.temp") { AutoFlush = true };
                    SR.WriteLine("Line Number\tOld Ext\tNew Ext\tMime Type\tDescription\tFilename");
                }
                else
                {
                    SR.Close();
                    if (DebugDict.Count != 0)
                    {
                        using (var sw2 = new StreamWriter("DebugLogSummary.txt"))
                        {
                            sw2.WriteLine("Dest Ext\tCall Count");
                            foreach (var kv in DebugDict.OrderBy(m => m.Value).ThenBy(n => n.Value))
                            {
                                sw2.WriteLine($"{kv.Key}\t{kv.Value}");
                            }
                        }

                        File.Delete("DebugLog.txt");
                        File.Move("DebugLog.temp", "DebugLog.txt");
                    }
                    else File.Delete("DebugLog.temp");

                    DebugDict.Clear();
                    DebugDict = null;
                    SR = null;
                }
            }
        }
        private static void CurrentDomain_DomainUnload(object sender, EventArgs e)
        {
            EnableDebug = false;
            AppDomain.CurrentDomain.DomainUnload -= CurrentDomain_DomainUnload;
            AppDomain.CurrentDomain.ProcessExit -= CurrentDomain_DomainUnload;
        }

        private static bool __enableDebug = false;
        private static Dictionary<string, int> DebugDict;
        private static StreamWriter SR;

        private static string DebugLog(string fn, string mime, string newext, string desc = null, [CallerLineNumber] int lineNumber = 0)
        {
            if (EnableDebug)
            {
                lock (SR)
                {
                    if (!DebugDict.TryGetValue(newext, out int kount)) kount = 0;
                    DebugDict[newext] = ++kount;
                    SR.WriteLine($"{lineNumber}\t{Path.GetExtension(fn)}\t{newext}\t{mime}\t{desc ?? " "}\t{fn}");
                }
            }
            return newext;
        }
        #endregion Debugging

        /// <summary>Returns the extension of the specified path string.</summary>
        /// <param name="path">The path string from which to get the extension.</param>
        /// <returns>The extension of the specified path (including the period "."), or <see langword="null" />, or <see cref="F:System.String.Empty" />. If <paramref name="path" /> is <see langword="null" />, <see cref="M:System.IO.Path.GetExtension(System.String)" /> returns <see langword="null" />. If <paramref name="path" /> does not have extension information, <see cref="M:System.IO.Path.GetExtension(System.String)" /> returns <see cref="F:System.String.Empty" />.</returns>
        /// <exception cref="T:System.ArgumentException"><paramref name="path" /> contains one or more of the invalid characters defined in <see cref="M:System.IO.Path.GetInvalidPathChars" />.</exception>
        /// <remarks>This is identical to System.IO.Path.GetExtension</remarks>
        public static string ByName(string path) => Path.GetExtension(path);

        /// <summary>
        /// For a given MIME (Multipurpose Internet Mail Extension) Type, returns an equivalant Windows extension.
        /// </summary>
        /// <param name="mimeType"></param>
        /// <returns>A Windows extension with a leading "." or NULL if there is no match.</returns>
        public static string ByMimetype(string mimeType)
        {
            if (string.IsNullOrEmpty(mimeType)) return null;

            string ext = null;

            if (!_mime2Ext.TryGetTarget(out var dict))
            {
                dict = InitMime2ExtDictionary();
                _mime2Ext.SetTarget(dict);
            }

            if (dict.TryGetValue(mimeType, out ext)) return ext;

            //If all goes well, we should never get here.
            ext = Registry.GetValue(@"HKEY_CLASSES_ROOT\MIME\Database\Content Type\" + mimeType, "Extension", string.Empty)?.ToString();
            if (!string.IsNullOrEmpty(ext)) return ext;

            if (string.IsNullOrEmpty(ext) && mimeType.ContainsEx("/x-"))  //this hack works!
            {
                ext = Registry.GetValue(@"HKEY_CLASSES_ROOT\MIME\Database\Content Type\" + mimeType.Replace("x-", ""), "Extension", string.Empty)?.ToString();
                if (!string.IsNullOrEmpty(ext)) return ext;
            }

            return null;
        }
        private static readonly WeakReference<Dictionary<string, string>> _mime2Ext = new WeakReference<Dictionary<string, string>>(null);
        private static Dictionary<string, string> InitMime2ExtDictionary()
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "application/andrew-inset", ".ez" },
                { "application/applixware", ".aw" },
                { "application/atom+xml", ".atom" },
                { "application/atomcat+xml", ".atomcat" },
                { "application/atomsvc+xml", ".atomsvc" },
                { "application/ccxml+xml", ".ccxml" },
                { "application/CDFV2", ".cdfv2" },
                { "application/cdmi-capability", ".cdmia" },
                { "application/cdmi-container", ".cdmic" },
                { "application/cdmi-domain", ".cdmid" },
                { "application/cdmi-object", ".cdmio" },
                { "application/cdmi-queue", ".cdmiq" },
                { "application/csv", ".csv" },
                { "application/cu-seeme", ".cu" },
                { "application/davmount+xml", ".davmount" },
                { "application/docbook+xml", ".dbk" },
                { "application/dssc+der", ".dssc" },
                { "application/dssc+xml", ".xdssc" },
                { "application/ecmascript", ".ecma" },
                { "application/emma+xml", ".emma" },
                { "application/epub+zip", ".epub" },
                { "application/exi", ".exi" },
                { "application/font-tdpfr", ".pfr" },
                { "application/fractals", ".fif" },
                { "application/gml+xml", ".gml" },
                { "application/gpx+xml", ".gpx" },
                { "application/gxf", ".gxf" },
                { "application/gzip", ".gz" },
                { "application/hta", ".hta" },
                { "application/hyperstudio", ".stk" },
                { "application/inkml+xml", ".inkml" },
                { "application/ipfix", ".ipfix" },
                { "application/java-archive", ".jar" },
                { "application/java-serialized-object", ".ser" },
                { "application/java-vm", ".class" },
                { "application/javascript", ".js" },
                { "application/json", ".json" },
                { "application/jsonml+json", ".jsonml" },
                { "application/lost+xml", ".lostxml" },
                { "application/mac-binhex40", ".hqx" },
                { "application/mac-compactpro", ".cpt" },
                { "application/mads+xml", ".mads" },
                { "application/marc", ".mrc" },
                { "application/marcxml+xml", ".mrcx" },
                { "application/mathematica", ".ma" },
                { "application/mathml+xml", ".mathml" },
                { "application/mbox", ".mbox" },
                { "application/mediaservercontrol+xml", ".mscml" },
                { "application/metalink+xml", ".metalink" },
                { "application/metalink4+xml", ".meta4" },
                { "application/mets+xml", ".mets" },
                { "application/mods+xml", ".mods" },
                { "application/mp21", ".mp21" },
                { "application/mp4", ".mp4" },
                { "application/msaccess", ".accdb" },
                { "application/msaccess.addin", ".accda" },
                { "application/msaccess.cab", ".accdc" },
                { "application/msaccess.exec", ".accde" },
                { "application/msaccess.ftemplate", ".accft" },
                { "application/msaccess.runtime", ".accdr" },
                { "application/msaccess.template", ".accdt" },
                { "application/msaccess.webapplication", ".accdw" },
                { "application/msword", ".doc" },
                { "application/mxf", ".mxf" },
                { "application/octet-stream", ".bin" },
                { "application/oda", ".oda" },
                { "application/oebps-package+xml", ".opf" },
                { "application/ogg", ".ogx" },
                { "application/omdoc+xml", ".omdoc" },
                { "application/onenote", ".onepkg" },
                { "application/opensearchdescription+xml", ".osdx" },
                { "application/oxps", ".oxps" },
                { "application/patch-ops-error+xml", ".xer" },
                { "application/pdf", ".pdf" },
                { "application/pgp-encrypted", ".pgp" },
                { "application/pgp-keys", ".key" },
                { "application/pgp-signature", ".sig" },
                { "application/pics-rules", ".prf" },
                { "application/pkcs10", ".p10" },
                { "application/pkcs7-mime", ".p7c" },
                { "application/pkcs7-signature", ".p7s" },
                { "application/pkcs8", ".p8" },
                { "application/pkix-attr-cert", ".ac" },
                { "application/pkix-cert", ".cer" },
                { "application/pkix-crl", ".crl" },
                { "application/pkix-pkipath", ".pkipath" },
                { "application/pkixcmp", ".pki" },
                { "application/pls+xml", ".pls" },
                { "application/postscript", ".ps" },
                { "application/prs.cww", ".cww" },
                { "application/pskc+xml", ".pskcxml" },
                { "application/rdf+xml", ".rdf" },
                { "application/reginfo+xml", ".rif" },
                { "application/relax-ng-compact-syntax", ".rnc" },
                { "application/resource-lists+xml", ".rl" },
                { "application/resource-lists-diff+xml", ".rld" },
                { "application/rls-services+xml", ".rs" },
                { "application/rpki-ghostbusters", ".gbr" },
                { "application/rpki-manifest", ".mft" },
                { "application/rpki-roa", ".roa" },
                { "application/rsd+xml", ".rsd" },
                { "application/rss+xml", ".rss" },
                { "application/rtf", ".rtf" },
                { "application/sbml+xml", ".sbml" },
                { "application/scvp-cv-request", ".scq" },
                { "application/scvp-cv-response", ".scs" },
                { "application/scvp-vp-request", ".spq" },
                { "application/scvp-vp-response", ".spp" },
                { "application/sdp", ".sdp" },
                { "application/set-payment-initiation", ".setpay" },
                { "application/set-registration-initiation", ".setreg" },
                { "application/shf+xml", ".shf" },
                { "application/smil+xml", ".smi" },
                { "application/sparql-query", ".rq" },
                { "application/sparql-results+xml", ".srx" },
                { "application/srgs", ".gram" },
                { "application/srgs+xml", ".grxml" },
                { "application/sru+xml", ".sru" },
                { "application/ssdl+xml", ".ssdl" },
                { "application/ssml+xml", ".ssml" },
                { "application/tei+xml", ".tei" },
                { "application/thraud+xml", ".tfi" },
                { "application/timestamped-data", ".tsd" },
                { "application/vnd.3gpp.pic-bw-large", ".plb" },
                { "application/vnd.3gpp.pic-bw-small", ".psb" },
                { "application/vnd.3gpp.pic-bw-var", ".pvb" },
                { "application/vnd.3gpp2.tcap", ".tcap" },
                { "application/vnd.3m.post-it-notes", ".pwn" },
                { "application/vnd.accpac.simply.aso", ".aso" },
                { "application/vnd.accpac.simply.imp", ".imp" },
                { "application/vnd.acucobol", ".acu" },
                { "application/vnd.acucorp", ".atc" },
                { "application/vnd.adobe.acrobat-security-settings", ".acrobatsecuritysettings" },
                { "application/vnd.adobe.acrobat.aaui+xml", ".aaui" },
                { "application/vnd.adobe.air-application-installer-package+zip", ".air" },
                { "application/vnd.adobe.formscentral.fcdt", ".fcdt" },
                { "application/vnd.adobe.fxp", ".fxp" },
                { "application/vnd.adobe.pdfxml", ".pdfxml" },
                { "application/vnd.adobe.pdx", ".pdx" },
                { "application/vnd.adobe.rmf", ".rmf" },
                { "application/vnd.adobe.xdp+xml", ".xdp" },
                { "application/vnd.adobe.xfd+xml", ".xfd" },
                { "application/vnd.adobe.xfdf", ".xfdf" },
                { "application/vnd.ahead.space", ".ahead" },
                { "application/vnd.airzip.filesecure.azf", ".azf" },
                { "application/vnd.airzip.filesecure.azs", ".azs" },
                { "application/vnd.amazon.ebook", ".azw" },
                { "application/vnd.americandynamics.acc", ".acc" },
                { "application/vnd.amiga.ami", ".ami" },
                { "application/vnd.android.package-archive", ".apk" },
                { "application/vnd.anser-web-certificate-issue-initiation", ".cii" },
                { "application/vnd.anser-web-funds-transfer-initiation", ".fti" },
                { "application/vnd.antix.game-component", ".atx" },
                { "application/vnd.apple.installer+xml", ".mpkg" },
                { "application/vnd.apple.mpegurl", ".m3u8" },
                { "application/vnd.aristanetworks.swi", ".swi" },
                { "application/vnd.astraea-software.iota", ".iota" },
                { "application/vnd.audiograph", ".aep" },
                { "application/vnd.blueice.multipass", ".mpm" },
                { "application/vnd.bmi", ".bmi" },
                { "application/vnd.businessobjects", ".rep" },
                { "application/vnd.chemdraw+xml", ".cdxml" },
                { "application/vnd.chipnuts.karaoke-mmd", ".mmd" },
                { "application/vnd.cinderella", ".cdy" },
                { "application/vnd.claymore", ".cla" },
                { "application/vnd.cloanto.rp9", ".rp9" },
                { "application/vnd.clonk.c4group", ".c4g" },
                { "application/vnd.cluetrust.cartomobile-config", ".c11amc" },
                { "application/vnd.cluetrust.cartomobile-config-pkg", ".c11amz" },
                { "application/vnd.commonspace", ".csp" },
                { "application/vnd.contact.cmsg", ".cdbcmsg" },
                { "application/vnd.cosmocaller", ".cmc" },
                { "application/vnd.crick.clicker", ".clkx" },
                { "application/vnd.crick.clicker.keyboard", ".clkk" },
                { "application/vnd.crick.clicker.palette", ".clkp" },
                { "application/vnd.crick.clicker.template", ".clkt" },
                { "application/vnd.crick.clicker.wordbank", ".clkw" },
                { "application/vnd.criticaltools.wbs+xml", ".wbs" },
                { "application/vnd.ctc-posml", ".pml" },
                { "application/vnd.cups-ppd", ".ppd" },
                { "application/vnd.curl.car", ".car" },
                { "application/vnd.curl.pcurl", ".pcurl" },
                { "application/vnd.dart", ".dart" },
                { "application/vnd.data-vision.rdz", ".rdz" },
                { "application/vnd.dece.data", ".uvd" },
                { "application/vnd.dece.ttml+xml", ".uvt" },
                { "application/vnd.dece.unspecified", ".uvx" },
                { "application/vnd.dece.zip", ".uvz" },
                { "application/vnd.denovo.fcselayout-link", ".fe_launch" },
                { "application/vnd.dna", ".dna" },
                { "application/vnd.dolby.mlp", ".mlp" },
                { "application/vnd.dpgraph", ".dpg" },
                { "application/vnd.dreamfactory", ".dfac" },
                { "application/vnd.ds-keypoint", ".kpxx" },
                { "application/vnd.dvb.ait", ".ait" },
                { "application/vnd.dvb.service", ".svc" },
                { "application/vnd.dynageo", ".geo" },
                { "application/vnd.ecowin.chart", ".mag" },
                { "application/vnd.enliven", ".nml" },
                { "application/vnd.epson.esf", ".esf" },
                { "application/vnd.epson.msf", ".msf" },
                { "application/vnd.epson.quickanime", ".qam" },
                { "application/vnd.epson.salt", ".slt" },
                { "application/vnd.epson.ssf", ".ssf" },
                { "application/vnd.eszigno3+xml", ".es3" },
                { "application/vnd.ezpix-album", ".ez2" },
                { "application/vnd.ezpix-package", ".ez3" },
                { "application/vnd.fdf", ".fdf" },
                { "application/vnd.fdsn.mseed", ".mseed" },
                { "application/vnd.fdsn.seed", ".seed" },
                { "application/vnd.flographit", ".gph" },
                { "application/vnd.fluxtime.clip", ".ftc" },
                { "application/vnd.framemaker", ".frame" },
                { "application/vnd.frogans.fnc", ".fnc" },
                { "application/vnd.frogans.ltf", ".ltf" },
                { "application/vnd.fsc.weblaunch", ".fsc" },
                { "application/vnd.fujitsu.oasys", ".oas" },
                { "application/vnd.fujitsu.oasys2", ".oa2" },
                { "application/vnd.fujitsu.oasys3", ".oa3" },
                { "application/vnd.fujitsu.oasysgp", ".fg5" },
                { "application/vnd.fujitsu.oasysprs", ".bh2" },
                { "application/vnd.fujixerox.ddd", ".ddd" },
                { "application/vnd.fujixerox.docuworks", ".xdw" },
                { "application/vnd.fujixerox.docuworks.binder", ".xbd" },
                { "application/vnd.fuzzysheet", ".fzs" },
                { "application/vnd.genomatix.tuxedo", ".txd" },
                { "application/vnd.geogebra.file", ".ggb" },
                { "application/vnd.geogebra.tool", ".ggt" },
                { "application/vnd.geometry-explorer", ".gex" },
                { "application/vnd.geonext", ".gxt" },
                { "application/vnd.geoplan", ".g2w" },
                { "application/vnd.geospace", ".g3w" },
                { "application/vnd.gmx", ".gmx" },
                { "application/vnd.google-earth.kml+xml", ".kml" },
                { "application/vnd.google-earth.kmz", ".kmz" },
                { "application/vnd.grafeq", ".gqf" },
                { "application/vnd.groove-account", ".gac" },
                { "application/vnd.groove-help", ".ghf" },
                { "application/vnd.groove-identity-message", ".gim" },
                { "application/vnd.groove-injector", ".grv" },
                { "application/vnd.groove-tool-message", ".gtm" },
                { "application/vnd.groove-tool-template", ".tpl" },
                { "application/vnd.groove-vcard", ".vcg" },
                { "application/vnd.hal+xml", ".hal" },
                { "application/vnd.handheld-entertainment+xml", ".zmm" },
                { "application/vnd.hbci", ".hbci" },
                { "application/vnd.hhe.lesson-player", ".les" },
                { "application/vnd.hp-hpgl", ".hpgl" },
                { "application/vnd.hp-hpid", ".hpid" },
                { "application/vnd.hp-hps", ".hps" },
                { "application/vnd.hp-jlyt", ".jlt" },
                { "application/vnd.hp-pcl", ".pcl" },
                { "application/vnd.hp-pclxl", ".pclxl" },
                { "application/vnd.hydrostatix.sof-data", ".sfd-hdstx" },
                { "application/vnd.ibm.minipay", ".mpy" },
                { "application/vnd.ibm.modcap", ".afp" },
                { "application/vnd.ibm.rights-management", ".irm" },
                { "application/vnd.ibm.secure-container", ".sc" },
                { "application/vnd.iccprofile", ".icc" },
                { "application/vnd.igloader", ".igl" },
                { "application/vnd.immervision-ivp", ".ivp" },
                { "application/vnd.immervision-ivu", ".ivu" },
                { "application/vnd.insors.igm", ".igm" },
                { "application/vnd.intercon.formnet", ".xpx" },
                { "application/vnd.intergeo", ".i2g" },
                { "application/vnd.intu.qbo", ".qbo" },
                { "application/vnd.intu.qfx", ".qfx" },
                { "application/vnd.ipunplugged.rcprofile", ".rcprofile" },
                { "application/vnd.irepository.package+xml", ".irp" },
                { "application/vnd.is-xpr", ".xpr" },
                { "application/vnd.isac.fcs", ".fcs" },
                { "application/vnd.jam", ".jam" },
                { "application/vnd.jcp.javame.midlet-rms", ".rms" },
                { "application/vnd.jisp", ".jisp" },
                { "application/vnd.joost.joda-archive", ".joda" },
                { "application/vnd.kahootz", ".ktz" },
                { "application/vnd.kde.karbon", ".karbon" },
                { "application/vnd.kde.kchart", ".chrt" },
                { "application/vnd.kde.kformula", ".kfo" },
                { "application/vnd.kde.kivio", ".flw" },
                { "application/vnd.kde.kontour", ".kon" },
                { "application/vnd.kde.kpresenter", ".kpr" },
                { "application/vnd.kde.kspread", ".ksp" },
                { "application/vnd.kde.kword", ".kwd" },
                { "application/vnd.kenameaapp", ".htke" },
                { "application/vnd.kidspiration", ".kia" },
                { "application/vnd.kinar", ".knp" },
                { "application/vnd.koan", ".skd" },
                { "application/vnd.kodak-descriptor", ".sse" },
                { "application/vnd.las.las+xml", ".lasxml" },
                { "application/vnd.llamagraphics.life-balance.desktop", ".lbd" },
                { "application/vnd.llamagraphics.life-balance.exchange+xml", ".lbe" },
                { "application/vnd.lotus-1-2-3", ".123" },
                { "application/vnd.lotus-approach", ".apr" },
                { "application/vnd.lotus-freelance", ".pre" },
                { "application/vnd.lotus-notes", ".nsf" },
                { "application/vnd.lotus-organizer", ".org" },
                { "application/vnd.lotus-screencam", ".scm" },
                { "application/vnd.lotus-wordpro", ".lwp" },
                { "application/vnd.macports.portpkg", ".portpkg" },
                { "application/vnd.mcd", ".mcd" },
                { "application/vnd.medcalcdata", ".mc1" },
                { "application/vnd.mediastation.cdkey", ".cdkey" },
                { "application/vnd.mfer", ".mwf" },
                { "application/vnd.mfmp", ".mfm" },
                { "application/vnd.micrografx.flo", ".flo" },
                { "application/vnd.micrografx.igx", ".igx" },
                { "application/vnd.mif", ".mif" },
                { "application/vnd.mobius.daf", ".daf" },
                { "application/vnd.mobius.dis", ".dis" },
                { "application/vnd.mobius.mbk", ".mbk" },
                { "application/vnd.mobius.mqy", ".mqy" },
                { "application/vnd.mobius.msl", ".msl" },
                { "application/vnd.mobius.plc", ".plc" },
                { "application/vnd.mobius.txf", ".txf" },
                { "application/vnd.mophun.application", ".mpn" },
                { "application/vnd.mophun.certificate", ".mpc" },
                { "application/vnd.mozilla.xul+xml", ".xul" },
                { "application/vnd.ms-artgalry", ".cil" },
                { "application/vnd.ms-cab-compressed", ".cab" },
                { "application/vnd.ms-excel", ".xls" },
                { "application/vnd.ms-excel.12", ".xlsx" },
                { "application/vnd.ms-excel.addin.macroEnabled.12", ".xlam" },
                { "application/vnd.ms-excel.sheet.binary.macroEnabled.12", ".xlsb" },
                { "application/vnd.ms-excel.sheet.macroEnabled.12", ".xlsm" },
                { "application/vnd.ms-excel.template.macroEnabled.12", ".xltm" },
                { "application/vnd.ms-fontobject", ".eot" },
                { "application/vnd.ms-htmlhelp", ".chm" },
                { "application/vnd.ms-ims", ".ims" },
                { "application/vnd.ms-lrm", ".lrm" },
                { "application/vnd.ms-msi", ".msi" },
                { "application/vnd.ms-office", ".pub" },
                { "application/vnd.ms-officetheme", ".thmx" },
                { "application/vnd.ms-opentype", ".otf" },
                { "application/vnd.ms-outlook", ".oft" },
                { "application/vnd.ms-pki.certstore", ".sst" },
                { "application/vnd.ms-pki.pko", ".pko" },
                { "application/vnd.ms-pki.seccat", ".cat" },
                { "application/vnd.ms-pki.stl", ".stl" },
                { "application/vnd.ms-powerpoint", ".ppt" },
                { "application/vnd.ms-powerpoint.12", ".pptx" },
                { "application/vnd.ms-powerpoint.addin.macroEnabled.12", ".ppam" },
                { "application/vnd.ms-powerpoint.presentation.macroEnabled.12", ".pptm" },
                { "application/vnd.ms-powerpoint.slide.macroEnabled.12", ".sldm" },
                { "application/vnd.ms-powerpoint.slideshow.macroEnabled.12", ".ppsm" },
                { "application/vnd.ms-powerpoint.template.macroEnabled.12", ".potm" },
                { "application/vnd.ms-project", ".mpp" },
                { "application/vnd.ms-publisher", ".pub" },
                { "application/vnd.ms-visio.viewer", ".vsd" },
                { "application/vnd.ms-word.document.12", ".docx" },
                { "application/vnd.ms-word.document.macroEnabled.12", ".docm" },
                { "application/vnd.ms-word.template.12", ".dotx" },
                { "application/vnd.ms-word.template.macroEnabled.12", ".dotm" },
                { "application/vnd.ms-works", ".wks" },
                { "application/vnd.ms-wpl", ".wpl" },
                { "application/vnd.ms-xpsdocument", ".xps" },
                { "application/vnd.mseq", ".mseq" },
                { "application/vnd.musician", ".mus" },
                { "application/vnd.muvee.style", ".msty" },
                { "application/vnd.mynfc", ".taglet" },
                { "application/vnd.neurolanguage.nlu", ".nlu" },
                { "application/vnd.nitf", ".ntf" },
                { "application/vnd.noblenet-directory", ".nnd" },
                { "application/vnd.noblenet-sealer", ".nns" },
                { "application/vnd.noblenet-web", ".nnw" },
                { "application/vnd.nokia.n-gage.data", ".ngdat" },
                { "application/vnd.nokia.n-gage.symbian.install", ".n-gage" },
                { "application/vnd.nokia.radio-preset", ".rpst" },
                { "application/vnd.nokia.radio-presets", ".rpss" },
                { "application/vnd.novadigm.edm", ".edm" },
                { "application/vnd.novadigm.edx", ".edx" },
                { "application/vnd.novadigm.ext", ".ext" },
                { "application/vnd.oasis.opendocument.chart", ".odc" },
                { "application/vnd.oasis.opendocument.chart-template", ".otc" },
                { "application/vnd.oasis.opendocument.database", ".odb" },
                { "application/vnd.oasis.opendocument.formula", ".odf" },
                { "application/vnd.oasis.opendocument.formula-template", ".odft" },
                { "application/vnd.oasis.opendocument.graphics", ".odg" },
                { "application/vnd.oasis.opendocument.graphics-template", ".otg" },
                { "application/vnd.oasis.opendocument.image", ".odi" },
                { "application/vnd.oasis.opendocument.image-template", ".oti" },
                { "application/vnd.oasis.opendocument.presentation", ".odp" },
                { "application/vnd.oasis.opendocument.presentation-template", ".otp" },
                { "application/vnd.oasis.opendocument.spreadsheet", ".ods" },
                { "application/vnd.oasis.opendocument.spreadsheet-template", ".ots" },
                { "application/vnd.oasis.opendocument.text", ".odt" },
                { "application/vnd.oasis.opendocument.text-master", ".odm" },
                { "application/vnd.oasis.opendocument.text-template", ".ott" },
                { "application/vnd.oasis.opendocument.text-web", ".oth" },
                { "application/vnd.olpc-sugar", ".xo" },
                { "application/vnd.oma.dd2+xml", ".dd2" },
                { "application/vnd.openofficeorg.extension", ".oxt" },
                { "application/vnd.openxmlformats-officedocument.presentationml.presentation", ".pptx" },
                { "application/vnd.openxmlformats-officedocument.presentationml.slide", ".sldx" },
                { "application/vnd.openxmlformats-officedocument.presentationml.slideshow", ".ppsx" },
                { "application/vnd.openxmlformats-officedocument.presentationml.template", ".potx" },
                { "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", ".xlsx" },
                { "application/vnd.openxmlformats-officedocument.spreadsheetml.template", ".xltx" },
                { "application/vnd.openxmlformats-officedocument.wordprocessingml.document", ".docx" },
                { "application/vnd.openxmlformats-officedocument.wordprocessingml.template", ".dotx" },
                { "application/vnd.osgeo.mapguide.package", ".mgp" },
                { "application/vnd.osgi.dp", ".dp" },
                { "application/vnd.osgi.subsystem", ".esa" },
                { "application/vnd.palm", ".pqa" },
                { "application/vnd.pawaafile", ".paw" },
                { "application/vnd.pg.format", ".str" },
                { "application/vnd.pg.osasli", ".ei6" },
                { "application/vnd.picsel", ".efif" },
                { "application/vnd.pmi.widget", ".wg" },
                { "application/vnd.pocketlearn", ".plf" },
                { "application/vnd.powerbuilder6", ".pbd" },
                { "application/vnd.previewsystems.box", ".box" },
                { "application/vnd.proteus.magazine", ".mgz" },
                { "application/vnd.publishare-delta-tree", ".qps" },
                { "application/vnd.pvi.ptid1", ".ptid" },
                { "application/vnd.quark.quarkxpress", ".qxt" },
                { "application/vnd.realvnc.bed", ".bed" },
                { "application/vnd.recordare.musicxml", ".mxl" },
                { "application/vnd.recordare.musicxml+xml", ".musicxml" },
                { "application/vnd.rig.cryptonote", ".cryptonote" },
                { "application/vnd.rim.cod", ".cod" },
                { "application/vnd.rn-realmedia", ".rm" },
                { "application/vnd.rn-realmedia-vbr", ".rmvb" },
                { "application/vnd.route66.link66+xml", ".link66" },
                { "application/vnd.sailingtracker.track", ".st" },
                { "application/vnd.seemail", ".see" },
                { "application/vnd.sema", ".sema" },
                { "application/vnd.semd", ".semd" },
                { "application/vnd.semf", ".semf" },
                { "application/vnd.shana.informed.formdata", ".ifm" },
                { "application/vnd.shana.informed.formtemplate", ".itp" },
                { "application/vnd.shana.informed.interchange", ".iif" },
                { "application/vnd.shana.informed.package", ".ipk" },
                { "application/vnd.simtech-mindmapper", ".twd" },
                { "application/vnd.smaf", ".mmf" },
                { "application/vnd.smart.teacher", ".teacher" },
                { "application/vnd.solent.sdkm+xml", ".sdkm" },
                { "application/vnd.spotfire.dxp", ".dxp" },
                { "application/vnd.spotfire.sfs", ".sfs" },
                { "application/vnd.sqlite3", ".sqlite" },
                { "application/vnd.stardivision.calc", ".sdc" },
                { "application/vnd.stardivision.draw", ".sda" },
                { "application/vnd.stardivision.impress", ".sdd" },
                { "application/vnd.stardivision.math", ".smf" },
                { "application/vnd.stardivision.writer", ".sdw" },
                { "application/vnd.stardivision.writer-global", ".sgl" },
                { "application/vnd.stepmania.package", ".smzip" },
                { "application/vnd.stepmania.stepchart", ".sm" },
                { "application/vnd.sun.xml.calc", ".sxc" },
                { "application/vnd.sun.xml.calc.template", ".stc" },
                { "application/vnd.sun.xml.draw", ".sxd" },
                { "application/vnd.sun.xml.draw.template", ".std" },
                { "application/vnd.sun.xml.impress", ".sxi" },
                { "application/vnd.sun.xml.impress.template", ".sti" },
                { "application/vnd.sun.xml.math", ".sxm" },
                { "application/vnd.sun.xml.writer", ".sxw" },
                { "application/vnd.sun.xml.writer.global", ".sxg" },
                { "application/vnd.sun.xml.writer.template", ".stw" },
                { "application/vnd.sus-calendar", ".sus" },
                { "application/vnd.svd", ".svd" },
                { "application/vnd.symbian.install", ".sis" },
                { "application/vnd.syncml+xml", ".xsm" },
                { "application/vnd.syncml.dm+wbxml", ".bdm" },
                { "application/vnd.syncml.dm+xml", ".xdm" },
                { "application/vnd.tao.intent-module-archive", ".tao" },
                { "application/vnd.tcpdump.pcap", ".pcap" },
                { "application/vnd.tmobile-livetv", ".tmo" },
                { "application/vnd.trid.tpt", ".tpt" },
                { "application/vnd.triscape.mxs", ".mxs" },
                { "application/vnd.trueapp", ".tra" },
                { "application/vnd.ufdl", ".ufd" },
                { "application/vnd.uiq.theme", ".utz" },
                { "application/vnd.umajin", ".umj" },
                { "application/vnd.unity", ".unityweb" },
                { "application/vnd.uoml+xml", ".uoml" },
                { "application/vnd.vcx", ".vcx" },
                { "application/vnd.visio", ".vsd" },
                { "application/vnd.visionary", ".vis" },
                { "application/vnd.vsf", ".vsf" },
                { "application/vnd.wap.wbxml", ".wbxml" },
                { "application/vnd.wap.wmlc", ".wmlc" },
                { "application/vnd.wap.wmlscriptc", ".wmlsc" },
                { "application/vnd.webturbo", ".wtb" },
                { "application/vnd.wolfram.player", ".nbp" },
                { "application/vnd.wordperfect", ".wpd" },
                { "application/vnd.wqd", ".wqd" },
                { "application/vnd.wt.stf", ".stf" },
                { "application/vnd.xara", ".xar" },
                { "application/vnd.xfdl", ".xfdl" },
                { "application/vnd.yamaha.hv-dic", ".hvd" },
                { "application/vnd.yamaha.hv-script", ".hvs" },
                { "application/vnd.yamaha.hv-voice", ".hvp" },
                { "application/vnd.yamaha.openscoreformat", ".osf" },
                { "application/vnd.yamaha.openscoreformat.osfpvg+xml", ".osfpvg" },
                { "application/vnd.yamaha.smaf-audio", ".saf" },
                { "application/vnd.yamaha.smaf-phrase", ".spf" },
                { "application/vnd.yellowriver-custom-menu", ".cmp" },
                { "application/vnd.zul", ".zir" },
                { "application/vnd.zzazz.deck+xml", ".zaz" },
                { "application/voicexml+xml", ".vxml" },
                { "application/widget", ".wgt" },
                { "application/windows-appcontent+xml", ".appcontent-ms" },
                { "application/winhelp", ".hlp" },
                { "application/winhlp", ".hlp" },
                { "application/wsdl+xml", ".wsdl" },
                { "application/wspolicy+xml", ".wspolicy" },
                { "application/x-7z-compressed", ".7z" },
                { "application/x-abiword", ".abw" },
                { "application/x-ace-compressed", ".ace" },
                { "application/x-apple-diskimage", ".dmg" },
                { "application/x-arc", ".arc" },
                { "application/x-archive", ".a" },
                { "application/x-authorware-bin", ".aab" },
                { "application/x-bcpio", ".bcpio" },
                { "application/x-bittorrent", ".torrent" },
                { "application/x-bittorrent-app", ".btapp" },
                { "application/x-bittorrent-appinst", ".btinstall" },
                { "application/x-bittorrent-key", ".btkey" },
                { "application/x-bittorrent-skin", ".btskin" },
                { "application/x-bittorrentsearchdescription+xml", ".btsearch" },
                { "application/x-blorb", ".blorb" },
                { "application/x-bridge-url", ".adobebridge" },
                { "application/x-bytecode.python", ".pyc" },
                { "application/x-bzip", ".bz" },
                { "application/x-bzip2", ".bz2" },
                { "application/x-cbr", ".cbr" },
                { "application/x-cdf", ".cdf" },
                { "application/x-cdlink", ".vcd" },
                { "application/x-cfs-compressed", ".cfs" },
                { "application/x-chat", ".chat" },
                { "application/x-chess-pgn", ".pgn" },
                { "application/x-chrome-extension", ".crx" },
                { "application/x-coff", ".o" },
                { "application/x-compress", ".z" },
                { "application/x-compress-ttcomp", ".ttcomp" },
                { "application/x-compressed", ".tgz" },
                { "application/x-conference", ".nsc" },
                { "application/x-cpio", ".cpio" },
                { "application/x-csh", ".csh" },
                { "application/x-dbase-index", ".cdx" },
                { "application/x-dbt", ".dbt" },
                { "application/x-debian-package", ".deb" },
                { "application/x-dgc-compressed", ".dgc" },
                { "application/x-director", ".dir" },
                { "application/x-dmp", ".dmp" },
                { "application/x-doom", ".wad" },
                { "application/x-dosdriver", ".sys" },
                { "application/x-dosexec", ".exe" },
                { "application/x-dtbncx+xml", ".ncx" },
                { "application/x-dtbook+xml", ".dtb" },
                { "application/x-dtbresource+xml", ".res" },
                { "application/x-dtcp1", ".dtcp-ip" },
                { "application/x-dvi", ".dvi" },
                { "application/x-dzip", ".dz" },
                { "application/x-envoy", ".evy" },
                { "application/x-eva", ".eva" },
                { "application/x-executable", ".nexe" },
                { "application/x-font-bdf", ".bdf" },
                { "application/x-font-ghostscript", ".gsf" },
                { "application/x-font-linux-psf", ".psf" },
                { "application/x-font-pcf", ".pcf" },
                { "application/x-font-pfm", ".pfm" },
                { "application/x-font-snf", ".snf" },
                { "application/x-font-type1", ".pfm" },
                { "application/x-fpt", ".fpt" },
                { "application/x-freearc", ".arc" },
                { "application/x-futuresplash", ".spl" },
                { "application/x-gca-compressed", ".gca" },
                { "application/x-gettext-translation", ".mo" },
                { "application/x-glulx", ".ulx" },
                { "application/x-gnumeric", ".gnumeric" },
                { "application/x-google-ab", ".ab" },
                { "application/x-gramps-xml", ".gramps" },
                { "application/x-gtar", ".gtar" },
                { "application/x-gzip", ".gz" },
                { "application/x-hdf", ".hdf" },
                { "application/x-hdf5", ".hdf" },
                { "application/x-ima", ".ima" },
                { "application/x-innosetup", ".dat" },
                { "application/x-innosetup-msg", ".msg" },
                { "application/x-install-instructions", ".install" },
                { "application/x-intel-aml", ".aml" },
                { "application/x-iso9660-image", ".iso" },
                { "application/x-java-applet", ".cl" },
                { "application/x-java-jnlp-file", ".jnlp" },
                { "application/x-latex", ".latex" },
                { "application/x-lz4+json", ".jsonlz4" },
                { "application/x-lzh-compressed", ".lzh" },
                { "application/x-lzma", ".lzma" },
                { "application/x-mach-binary", ".dylib" },
                { "application/x-matlab-data", ".mat" },
                { "application/x-mie", ".mie" },
                { "application/x-mix-transfer", ".nix" },
                { "application/x-mobipocket-ebook", ".prc" },
                { "application/x-mplayer2", ".asx" },
                { "application/x-ms-application", ".application" },
                { "application/x-ms-dat", ".dat" },
                { "application/x-ms-ese", ".edb" },
                { "application/x-ms-pdb", ".pdb" },
                { "application/x-ms-reader", ".its" },
                { "application/x-ms-sdb", ".sdb" },
                { "application/x-ms-sdi", ".sdi" },
                { "application/x-ms-shortcut", ".lnk" },
                { "application/x-ms-vsto", ".vsto" },
                { "application/x-ms-wim", ".wim" },
                { "application/x-ms-wmd", ".wmd" },
                { "application/x-ms-wmz", ".wmz" },
                { "application/x-ms-xbap", ".xbap" },
                { "application/x-msaccess", ".mdb" },
                { "application/x-msbinder", ".obd" },
                { "application/x-mscardfile", ".crd" },
                { "application/x-msclip", ".clp" },
                { "application/x-msi", ".msi" },
                { "application/x-msmediaview", ".mvb" },
                { "application/x-msmetafile", ".emf" },
                { "application/x-msmoney", ".mny" },
                { "application/x-mspublisher", ".pub" },
                { "application/x-msschedule", ".scd" },
                { "application/x-msterminal", ".trm" },
                { "application/x-mswebsite", ".website" },
                { "application/x-mswrite", ".wri" },
                { "application/x-navi-animation", ".ani" },
                { "application/x-netcdf", ".cdf" },
                { "application/x-nzb", ".nzb" },
                { "application/x-object", ".o" },
                { "application/x-pkcs12", ".pfx" },
                { "application/x-pkcs7-certificates", ".p7b" },
                { "application/x-pkcs7-certreqresp", ".p7r" },
                { "application/x-pnf", ".pnf" },
                { "application/x-rar", ".rar" },
                { "application/x-rar-compressed", ".rar" },
                { "application/x-research-info-systems", ".ris" },
                { "application/x-riff", ".pal" },
                { "application/x-setupscript", ".inf" },
                { "application/x-sh", ".sh" },
                { "application/x-shar", ".shar" },
                { "application/x-sharedlib", ".so" },
                { "application/x-shockwave-flash", ".swf" },
                { "application/x-silverlight-app", ".xap" },
                { "application/x-snappy-framed", ".sz" },
                { "application/x-sql", ".sql" },
                { "application/x-sqlite3", ".sqlite" },
                { "application/x-stargallery-thm", ".thm" },
                { "application/x-stuffit", ".sit" },
                { "application/x-stuffitx", ".sitx" },
                { "application/x-subrip", ".srt" },
                { "application/x-sv4cpio", ".sv4cpio" },
                { "application/x-sv4crc", ".sv4crc" },
                { "application/x-t3vm-image", ".t3" },
                { "application/x-tads", ".gam" },
                { "application/x-tar", ".tar" },
                { "application/x-tcl", ".tcl" },
                { "application/x-terminfo", ".term" },
                { "application/x-terminfo2", ".term" },
                { "application/x-tex", ".tex" },
                { "application/x-tex-tfm", ".tfm" },
                { "application/x-texinfo", ".texinfo" },
                { "application/x-tgif", ".obj" },
                { "application/x-tplink-bin", ".bin" },
                { "application/x-troff-man", ".man" },
                { "application/x-ustar", ".ustar" },
                { "application/x-wais-source", ".src" },
                { "application/x-wine-extension-ini", ".ini" },
                { "application/x-winhelp", ".gid" },
                { "application/x-wmplayer", ".asx" },
                { "application/x-x509-ca-cert", ".crt" },
                { "application/x-xfig", ".fig" },
                { "application/x-xliff+xml", ".xlf" },
                { "application/x-xpinstall", ".xpi" },
                { "application/x-xz", ".xz" },
                { "application/x-zip-compressed", ".zip" },
                { "application/x-zmachine", ".z1" },
                { "application/x-zoommtg-launcher", ".zoommtg" },
                { "application/xaml+xml", ".xaml" },
                { "application/xcap-diff+xml", ".xdf" },
                { "application/xenc+xml", ".xenc" },
                { "application/xhtml+xml", ".xhtml" },
                { "application/xml", ".xml" },
                { "application/xml-dtd", ".dtd" },
                { "application/xop+xml", ".xop" },
                { "application/xproc+xml", ".xpl" },
                { "application/xslt+xml", ".xslt" },
                { "application/xspf+xml", ".xspf" },
                { "application/xv+xml", ".xhvml" },
                { "application/yang", ".yang" },
                { "application/yin+xml", ".yin" },
                { "application/zip", ".zip" },
                { "application/zlib", ".zlib" },
                { "audio/3gpp", ".3gp" },
                { "audio/3gpp2", ".3g2" },
                { "audio/aac", ".aac" },
                { "audio/ac3", ".ac3" },
                { "audio/adpcm", ".adp" },
                { "audio/aiff", ".aiff" },
                { "audio/amr", ".amr" },
                { "audio/basic", ".au" },
                { "audio/ec3", ".ec3" },
                { "audio/l16", ".lpcm" },
                { "audio/mid", ".mid" },
                { "audio/midi", ".midi" },
                { "audio/mp3", ".mp3" },
                { "audio/mp4", ".m4a" },
                { "audio/mp4a-latm", ".m4a" },
                { "audio/mpeg", ".mp3" },
                { "audio/mpegurl", ".m3u" },
                { "audio/mpg", ".mp3" },
                { "audio/ogg", ".ogg" },
                { "audio/s3m", ".s3m" },
                { "audio/scpls", ".pls" },
                { "audio/silk", ".sil" },
                { "audio/vnd.dece.audio", ".uva" },
                { "audio/vnd.digital-winds", ".eol" },
                { "audio/vnd.dlna.adts", ".adts" },
                { "audio/vnd.dolby.dd-raw", ".ac3" },
                { "audio/vnd.dra", ".dra" },
                { "audio/vnd.dts", ".dts" },
                { "audio/vnd.dts.hd", ".dtshd" },
                { "audio/vnd.lucent.voice", ".lvp" },
                { "audio/vnd.ms-playready.media.pya", ".pya" },
                { "audio/vnd.nuera.ecelp4800", ".ecelp4800" },
                { "audio/vnd.nuera.ecelp7470", ".ecelp7470" },
                { "audio/vnd.nuera.ecelp9600", ".ecelp9600" },
                { "audio/vnd.rip", ".rip" },
                { "audio/wav", ".wav" },
                { "audio/webm", ".weba" },
                { "audio/x-aac", ".aac" },
                { "audio/x-aiff", ".aif" },
                { "audio/x-caf", ".caf" },
                { "audio/x-flac", ".flac" },
                { "audio/x-hx-aac-adts", ".exe" },
                { "audio/x-m4a", ".m4a" },
                { "audio/x-m4r", ".m4r" },
                { "audio/x-matroska", ".mka" },
                { "audio/x-mid", ".mid" },
                { "audio/x-midi", ".mid" },
                { "audio/x-mp3", ".mp3" },
                { "audio/x-mpeg", ".mp3" },
                { "audio/x-mp4a-latm", ".m4a" },
                { "audio/x-mpegurl", ".m3u" },
                { "audio/x-mpg", ".mp3" },
                { "audio/x-ms-wax", ".wax" },
                { "audio/x-ms-wma", ".wma" },
                { "audio/x-pn-realaudio", ".ra" },
                { "audio/x-pn-realaudio-plugin", ".rmp" },
                { "audio/x-scpls", ".pls" },
                { "audio/x-wav", ".wav" },
                { "audio/xm", ".xm" },
                { "chemical/x-cdx", ".cdx" },
                { "chemical/x-cif", ".cif" },
                { "chemical/x-cmdf", ".cmdf" },
                { "chemical/x-cml", ".cml" },
                { "chemical/x-csml", ".csml" },
                { "chemical/x-xyz", ".xyz" },
                { "font/collection", ".ttc" },
                { "font/otf", ".otf" },
                { "font/sfnt", ".ttf" },
                { "font/ttf", ".ttc" },
                { "font/woff", ".woff" },
                { "font/woff2", ".woff2" },
                { "font/x-amiga-font", ".fon" },
                { "image/avci", ".avci" },
                { "image/avcs", ".avcs" },
                { "image/avif", ".avif" },
                { "image/avif-sequence", ".avifs" },
                { "image/bmp", ".bmp" },
                { "image/cgm", ".cgm" },
                { "image/g3fax", ".g3" },
                { "image/gif", ".gif" },
                { "image/heic", ".heic" },
                { "image/heic-sequence", ".heics" },
                { "image/heif", ".heif" },
                { "image/heif-sequence", ".heifs" },
                { "image/ief", ".ief" },
                { "image/jp2", ".jp2" },
                { "image/jpeg", ".jpg" },
                { "image/jxl", ".jxl" },
                { "image/jxr", ".jxr" },
                { "image/ktx", ".ktx" },
                { "image/pjpeg", ".jpg" },
                { "image/png", ".png" },
                { "image/prs.btif", ".btif" },
                { "image/sgi", ".sgi" },
                { "image/svg+xml", ".svg" },
                { "image/tiff", ".tif" },
                { "image/vnd.adobe.photoshop", ".psd" },
                { "image/vnd.dece.graphic", ".uvg" },
                { "image/vnd.djvu", ".djvu" },
                { "image/vnd.dvb.subtitle", ".sub" },
                { "image/vnd.dwg", ".dwg" },
                { "image/vnd.dxf", ".dxf" },
                { "image/vnd.fastbidsheet", ".fbs" },
                { "image/vnd.fpx", ".fpx" },
                { "image/vnd.fst", ".fst" },
                { "image/vnd.fujixerox.edmics-mmr", ".mmr" },
                { "image/vnd.fujixerox.edmics-rlc", ".rlc" },
                { "image/vnd.microsoft.icon", ".ico" },
                { "image/vnd.ms-dds", ".dds" },
                { "image/vnd.ms-modi", ".mdi" },
                { "image/vnd.ms-photo", ".wdp" },
                { "image/vnd.net-fpx", ".npx" },
                { "image/vnd.wap.wbmp", ".wbmp" },
                { "image/vnd.xiff", ".xif" },
                { "image/webp", ".webp" },
                { "image/wmf", ".wmf" },
                { "image/x-3ds", ".3ds" },
                { "image/x-award-bioslogo", ".epa" },
                { "image/x-cmu-raster", ".ras" },
                { "image/x-cmx", ".cmx" },
                { "image/x-emf", ".emf" },
                { "image/x-freehand", ".fh" },
                { "image/x-icns", ".icns" },
                { "image/x-icon", ".ico" },
                { "image/x-mrsid-image", ".sid" },
                { "image/x-ms-bmp", ".dib" },
                { "image/x-paintnet", ".pdn" },
                { "image/x-pcx", ".pcx" },
                { "image/x-pict", ".pct" },
                { "image/x-png", ".png" },
                { "image/x-portable-anymap", ".pnm" },
                { "image/x-portable-bitmap", ".pbm" },
                { "image/x-portable-graymap", ".pgm" },
                { "image/x-portable-pixmap", ".ppm" },
                { "image/x-rgb", ".rgb" },
                { "image/x-sony-tim", ".tim" },
                { "image/x-tga", ".tga" },
                { "image/x-win-bitmap", ".cur" },
                { "image/x-wmf", ".wmf" },
                { "image/x-xbitmap", ".xbm" },
                { "image/x-xwindowdump", ".xwd" },
                { "inode/blockdevice", ".edb" },
                { "inode/x-empty", ".bin" },
                { "interface/x-winamp-lang", ".wlz" },
                { "interface/x-winamp-skin", ".wsz" },
                { "interface/x-winamp3-skin", ".wal" },
                { "message/rfc822", ".eml" },
                { "midi/mid", ".mid" },
                { "model/gltf-binary", ".glb" },
                { "model/iges", ".iges" },
                { "model/mesh", ".mesh" },
                { "model/vnd.collada+xml", ".dae" },
                { "model/vnd.dwf", ".dwf" },
                { "model/vnd.gdl", ".gdl" },
                { "model/vnd.gtw", ".gtw" },
                { "model/vnd.mts", ".mts" },
                { "model/vnd.vtu", ".vtu" },
                { "model/vrml", ".vrml" },
                { "model/x3d+binary", ".x3db" },
                { "model/x3d+vrml", ".x3dv" },
                { "model/x3d+xml", ".x3d" },
                { "pkcs10", ".p10" },
                { "pkcs7-mime", ".p7m" },
                { "pkcs7-signature", ".p7s" },
                { "pkix-cert", ".cer" },
                { "pkix-crl", ".crl" },
                { "text/cache-manifest", ".appcache" },
                { "text/calendar", ".ics" },
                { "text/css", ".css" },
                { "text/csv", ".csv" },
                { "text/directory", ".vcf" },
                { "text/directory;profile=vCard", ".vcf" },
                { "text/html", ".htm" },
                { "text/n3", ".n3" },
                { "text/PGP", ".pgp" },
                { "text/plain", ".txt" },
                { "text/prs.lines.tag", ".dsc" },
                { "text/richtext", ".rtx" },
                { "text/rtf", ".rtf" },
                { "text/scriptlet", ".wsc" },
                { "text/sgml", ".sgml" },
                { "text/tab-separated-values", ".tsv" },
                { "text/troff", ".man" },
                { "text/turtle", ".ttl" },
                { "text/uri-list", ".uris" },
                { "text/vcard", ".vcf" },
                { "text/vnd.curl", ".curl" },
                { "text/vnd.curl.dcurl", ".dcurl" },
                { "text/vnd.curl.mcurl", ".mcurl" },
                { "text/vnd.curl.scurl", ".scurl" },
                { "text/vnd.fly", ".fly" },
                { "text/vnd.fmi.flexstor", ".flx" },
                { "text/vnd.graphviz", ".gv" },
                { "text/vnd.in3d.3dml", ".3dml" },
                { "text/vnd.in3d.spot", ".spot" },
                { "text/vnd.sun.j2me.app-descriptor", ".jad" },
                { "text/vnd.wap.wml", ".wml" },
                { "text/vnd.wap.wmlscript", ".wmls" },
                { "text/x-Algol68", ".alg" },
                { "text/x-asm", ".asm" },
                { "text/x-c", ".c" },
                { "text/x-c++", ".cpp" },
                { "text/x-component", ".htc" },
                { "text/x-diff", ".diff" },
                { "text/x-forth", ".4th" },
                { "text/x-fortran", ".for" },
                { "text/x-gawk", ".awk" },
                { "text/x-gimp-gpl", ".gpl" },
                { "text/x-java", ".idl" },
                { "text/x-java-source", ".java" },
                { "text/x-lisp", ".lsp" },
                { "text/x-m4", ".m4" },
                { "text/x-makefile", ".mak" },
                { "text/x-ms-contact", ".contact" },
                { "text/x-ms-iqy", ".iqy" },
                { "text/x-ms-odc", ".odc" },
                { "text/x-ms-regedit", ".reg" },
                { "text/x-ms-rqy", ".rqy" },
                { "text/x-msdos-batch", ".bat" },
                { "text/x-nfo", ".nfo" },
                { "text/x-objective-c", ".c" },
                { "text/x-opml", ".opml" },
                { "text/x-pascal", ".pas" },
                { "text/x-perl", ".pl" },
                { "text/x-php", ".php" },
                { "text/x-po", ".po" },
                { "text/x-python", ".py" },
                { "text/x-ruby", ".rb" },
                { "text/x-script.python", ".py" },
                { "text/x-setext", ".etx" },
                { "text/x-sfv", ".sfv" },
                { "text/x-shellscript", ".sh" },
                { "text/x-tex", ".tex" },
                { "text/x-uuencode", ".uu" },
                { "text/x-vcalendar", ".vcs" },
                { "text/x-vcard", ".vcf" },
                { "text/xml", ".xml" },
                { "video/3gpp", ".3gp" },
                { "video/3gpp2", ".3g2" },
                { "video/asx", ".asx" },
                { "video/avi", ".avi" },
                { "video/h261", ".h261" },
                { "video/h263", ".h263" },
                { "video/h264", ".h264" },
                { "video/jpeg", ".jpgv" },
                { "video/jpm", ".jpgm" },
                { "video/mj2", ".mj2" },
                { "video/mp4", ".mp4" },
                { "video/mpeg", ".mpg" },
                { "video/msvideo", ".avi" },
                { "video/ogg", ".ogv" },
                { "video/quicktime", ".mov" },
                { "video/vnd.dece.hd", ".uvh" },
                { "video/vnd.dece.mobile", ".uvm" },
                { "video/vnd.dece.mp4", ".uvu" },
                { "video/vnd.dece.pd", ".uvp" },
                { "video/vnd.dece.sd", ".uvs" },
                { "video/vnd.dece.video", ".uvv" },
                { "video/vnd.dlna.mpeg-tts", ".tts" },
                { "video/vnd.dvb.file", ".dvb" },
                { "video/vnd.fvt", ".fvt" },
                { "video/vnd.mpegurl", ".m4u" },
                { "video/vnd.ms-playready.media.pyv", ".pyv" },
                { "video/vnd.uvvu.mp4", ".uvu" },
                { "video/vnd.vivo", ".viv" },
                { "video/webm", ".webm" },
                { "video/wtv", ".wtv" },
                { "video/x-asx", ".asx" },
                { "video/x-f4v", ".f4v" },
                { "video/x-fli", ".fli" },
                { "video/x-flv", ".flv" },
                { "video/x-m4v", ".m4v" },
                { "video/x-matroska", ".mkv" },
                { "video/x-mng", ".mng" },
                { "video/x-mpeg", ".mpg" },
                { "video/x-mpeg2a", ".mpg" },
                { "video/x-ms-asf", ".asx" },
                { "video/x-ms-asf-plugin", ".asx" },
                { "video/x-ms-dvr", ".dvr-ms" },
                { "video/x-ms-vob", ".vob" },
                { "video/x-ms-wm", ".wm" },
                { "video/x-ms-wmv", ".wmv" },
                { "video/x-ms-wmx", ".wmx" },
                { "video/x-ms-wvx", ".wvx" },
                { "video/x-msvideo", ".avi" },
                { "video/x-sgi-movie", ".movie" },
                { "video/x-smv", ".smv" },
                { "vnd.ms-pki.certstore", ".sst" },
                { "vnd.ms-pki.pko", ".pko" },
                { "vnd.ms-pki.seccat", ".cat" },
                { "x-conference/x-cooltalk", ".ice" },
                { "x-pkcs12", ".p12" },
                { "x-pkcs7-certificates", ".p7b" },
                { "x-pkcs7-certreqresp", ".p7r" },
                { "x-x509-ca-cert", ".cer" }
            };
        }

        /// <summary>
        /// Determine file extension by the file content.
        /// </summary>
        /// <param name="filename">Name of file to analyze</param>
        /// <returns>A Windows extension with a leading "." or NULL if there is no match.</returns>
        /// <remarks>
        ///  This works pretty decently but it is not 100% accurate.
        /// </remarks>
        public static string ByContent(string filename)
        {
            string ext = null;
            string nuExt = null;
            string desc = null;
            string content = null;

            var mime = MagicDetector.LibMagic(filename, LibMagicOptions.MimeType);
            ext = ByMimetype(mime);
            if (ext == null) return null;

            //Within a given mimetype, there are many different extensions, so we dig into first the Libmagic description and then the file content itself.

            if (mime == "application/octet-stream")  //.bin
            {
                desc = MagicDetector.LibMagic(filename, LibMagicOptions.Description);
                nuExt = ApplicationOctetStream2Ext(desc);
                if (nuExt != null) return DebugLog(filename, mime, nuExt, desc);

                content = ReadText(filename);
                if (string.IsNullOrEmpty(content)) return ext;

                if (reIsCSharp.Value.IsMatch(content)) return DebugLog(filename,mime,".cs", desc);
                if (reIsIDL.Value.IsMatch(content)) return DebugLog(filename,mime,".idl", desc);
                if (content.ContainsExI("<!DOCTYPE html>")) return DebugLog(filename,mime,".htm", desc);
                if (content.ContainsEx("<?xml ")) return DebugLog(filename,mime,".xml", desc);
                if (content.ContainsEx("=pod ")) return DebugLog(filename,mime,".pod", desc);   
                if (reIsJavaScript.Value.IsMatch(content)) return DebugLog(filename,mime,".js", desc);
                return ".txt";
            }
            else if (mime == "text/plain")  //.txt
            {
                desc = MagicDetector.LibMagic(filename, LibMagicOptions.Description);
                nuExt = TextPlain2Ext(desc);
                if (nuExt != null) return DebugLog(filename, mime, nuExt, desc);

                content = ReadText(filename);
                if (string.IsNullOrEmpty(content)) return ext;

                nuExt = GetTextXmlType(content, mime, content);
                if (nuExt != null) return DebugLog(filename, mime, nuExt, desc);

                if (reIsTypeScript.Value.IsMatch(content)) return DebugLog(filename,mime,".ts", desc);
                if (reIsCSharp.Value.IsMatch(content)) return DebugLog(filename,mime,".cs", desc);
                if (reIsJavaScript.Value.IsMatch(content)) return DebugLog(filename,mime,".js", desc);
                if (content.StartsWith("Microsoft Visual Studio Solution File")) return DebugLog(filename, mime, ".sln", desc);
                try { if (reIsCSS.Value.Matches(content + "}").Count > 3) return DebugLog(filename, mime, ".css", desc); } catch { DebugLog(filename, mime, ".css(failed)", desc); }
                //if (reXmlNamespaces.Value.IsMatch(content)) return DebugLog(filename, mime, ".xml", desc);
                if (reIsIDL.Value.IsMatch(content)) return DebugLog(filename, mime, ".idl", desc);
                if (reIsBase64.Value.IsMatch(content))
                {
                    if (content.Length == 64) return DebugLog(filename, mime, ".sha256");
                    if (content.Length == 88) return DebugLog(filename, mime, ".sha512");
                    return DebugLog(filename, mime, ".base64");
                }
                if (IsASM(filename)) return DebugLog(filename, mime, ".asm");

                return ext;
            }
            else if (mime == "text/html")  //.htm
            {
                content = ReadText(filename);
                if (string.IsNullOrEmpty(content)) return ext;

                nuExt = GetTextXmlType(content, mime, content);
                if (nuExt != null) return DebugLog(filename, mime, nuExt, desc);

                if (content.ContainsEx("<?php ")) return DebugLog(filename,mime,".php");
                if (content.ContainsEx("=pod ")) return DebugLog(filename,mime,".pod");
                if (content.ContainsEx("=head1 ")) return DebugLog(filename, mime, ".pod");
                if (content.ContainsEx("HTML Help Workshop")) return DebugLog(filename, mime, ".hhc"); //could also be .hhk but cant tell the difference...

                if (reIsCSharp.Value.IsMatch(content)) return DebugLog(filename,mime,".cs");
                if (reIsJavaScript.Value.IsMatch(content)) return DebugLog(filename,mime,".js");
                try { if (reIsCSS.Value.Matches(content + "}").Count > 3) return DebugLog(filename, mime, ".css", desc); } catch { DebugLog(filename, mime, ".css(failed)", desc); }
                if (!reIsTextXmlType.Value.IsMatch(content)) return DebugLog(filename, mime, ".txt"); //markdown is the usual (not always) culprit.
                //if (IsASM(filename)) return DebugLog(filename,mime,".asm");
                return ext;
            }
            else if (mime == "application/x-wine-extension-ini")  //.ini
            {
                desc = MagicDetector.LibMagic(filename, LibMagicOptions.Description);
                if (desc.EndsWith("[InternetShortcut]")) DebugLog(filename, mime, ".url",desc);
                if (desc.EndsWith("[FUNC]")) return DebugLog(filename, mime, ".asm", desc);
                if (desc.EndsWith("[)]")) return DebugLog(filename, mime, ".mof", desc);
                if (desc.EndsWith("[]")) return DebugLog(filename, mime, ".idl", desc);
                if (desc.EndsWith("[Exchange Client Compatibility]")) return DebugLog(filename, mime, ".ecf", desc);
                if (desc.EndsWith("[File Transfer]")) return DebugLog(filename, mime, ".iss", desc);
                if (desc.EndsWith("[InternetShortcut]")) return DebugLog(filename, mime, ".url", desc);
                return ext;
            }
            else if (mime == "application/postscript")  //.ps
            {
                desc = MagicDetector.LibMagic(filename, LibMagicOptions.Description);
                if (desc.EndsWith("type EPS")) return DebugLog(filename, mime, ".eps", desc);
                return ext;
            }
            else if (mime == "application/x-setupscript")  //.inf
            {
                content = ReadText(filename);
                if (string.IsNullOrEmpty(content)) return ext;
                if (content.StartsWith("typedef interface ")) return DebugLog(filename, mime, ".h");
                if (content.StartsWith("extern 'C'{ ")) return DebugLog(filename, mime, ".h");
                return ext;
            }
            else if (mime == "text/x-Algol68")  // .alg
            {
                content = ReadText(filename);
                if (string.IsNullOrEmpty(content)) return ext;
                if (reIsCSharp.Value.IsMatch(content)) return DebugLog(filename, mime, ".cs");
                if (reIsJavaScript.Value.IsMatch(content)) return DebugLog(filename, mime, ".js");
                return ".txt";
            }
            else if (mime == "text/x-asm")  // .asm
            {
                content = ReadText(filename,1024);
                if (string.IsNullOrEmpty(content)) return ext;
                if (reIsJavaScript.Value.IsMatch(content)) return DebugLog(filename,mime,".js");
                try { if (reIsCSS.Value.Matches(content + "}").Count > 3) return DebugLog(filename, mime, ".css"); } catch { DebugLog(filename, mime, ".css(failed)"); }
                return ext;
            }
            else if (mime == "text/x-c")  // .c
            {
                content = ReadText(filename);
                if (string.IsNullOrEmpty(content)) return ext;
                if (content.StartsWith("<duixml>")) return DebugLog(filename,mime,".duixml");
                if (reIsCSharp.Value.IsMatch(content)) return DebugLog(filename,mime,".cs");
                if (IsASM(filename)) return DebugLog(filename, mime, ".asm");
                return ext;
            }
            else if (mime == "text/xml")  // .xml
            {
                content = ReadText(filename,2048);
                if (string.IsNullOrEmpty(content)) return ext;

                nuExt = GetTextXmlType(filename, mime, content);
                if (nuExt != null) return DebugLog(filename, mime, nuExt);

                return ext;
            }
            else if (mime == "text/x-forth")  // .4th
            {
                content = ReadText(filename);
                if (string.IsNullOrEmpty(content)) return ext;
                nuExt = GetTextXmlType(content, mime, content);
                if (nuExt != null) return DebugLog(filename, mime, nuExt, desc);
                return ext;
            }
            else if (mime == "image/jp2")  // .jp2
            {
                content = ReadText(filename);
                if (string.IsNullOrEmpty(content)) return ext;
                if (reIsBase64.Value.IsMatch(content))
                {
                    if (content.Length == 64) return DebugLog(filename, mime, ".sha256");
                    if (content.Length == 88) return DebugLog(filename, mime, ".sha512");
                    return DebugLog(filename, mime, ".base64");
                }
                return ext;
            }
            else if (mime == "application/x-dosexec")  //.exe
            {
                desc = MagicDetector.LibMagic(filename, LibMagicOptions.Description);
                nuExt = ApplicationXDosexec2Ext(desc);
                if (nuExt != null) return DebugLog(filename, mime, nuExt);
                return ".exe";
            }
            return ext;
        }

        private static readonly TimeSpan reTimeout = new TimeSpan(0,0,0,5);

        //.ini validation?: (^;.*$)*^\s*\[(\w+)]\s*$((^;.*$)*\s*(\w+)=(.*))+
        private static readonly Lazy<Regex> reProjectType = new Lazy<Regex>(() => new Regex(@"<Compile Include='.+?\.(?<EXT>[a-z]+)'\s*/?>", RegexOptions.Compiled, reTimeout), true);
        private static readonly Lazy<Regex> reIsIDL = new Lazy<Regex>(() => new Regex(@"\bimport '[0-9A-Za-z\._]+.idl'", RegexOptions.Compiled | RegexOptions.Singleline, reTimeout), true);
        private static readonly Lazy<Regex> reIsTypeScript = new Lazy<Regex>(() => new Regex(@"\bdeclare namespace [0-9A-Za-z$\._]+ { ", RegexOptions.Compiled | RegexOptions.Singleline, reTimeout), true);
        private static readonly Lazy<Regex> reIsCSharp = new Lazy<Regex>(() => new Regex(@"\bnamespace [0-9A-Za-z$\._]+ { ", RegexOptions.Compiled | RegexOptions.Singleline, reTimeout), true);
        private static readonly Lazy<Regex> reXmlNamespaces = new Lazy<Regex>(() => new Regex(@"\bxmlns(:(?<NS>[a-z]+))?='(?<URL>[^']+)", RegexOptions.Compiled | RegexOptions.ExplicitCapture, reTimeout), true);
        private static readonly Lazy<Regex> reIsCSS = new Lazy<Regex>(() => new Regex(@"([\.a-zA-Z0-9#*""', >+~\[\]=|\^\$:() -]+)\s*{\s*(([a-zA-Z-]+)\s*:\s*([^;}]+;?)\s*)+}", RegexOptions.Compiled | RegexOptions.ExplicitCapture, reTimeout), true); //https://www.anycodings.com/1questions/4822064/check-string-for-valid-css-using-regex
        private static readonly Lazy<Regex> reIsJavaScript = new Lazy<Regex>(() => new Regex(@"'use strict'|[=:;]\s*require\('[^']+'\)|\bfunction\s*([[A-Za-z0-9_]+)?\([$A-Za-z0-9_, ]+\)", RegexOptions.Compiled | RegexOptions.ExplicitCapture, reTimeout), true); //https://www.anycodings.com/1questions/4822064/check-string-for-valid-css-using-regex
        private static readonly Lazy<Regex> reIsBase64 = new Lazy<Regex>(() => new Regex(@"^[a-zA-Z0-9/+\s]+={0,2}$", RegexOptions.Compiled | RegexOptions.ExplicitCapture, reTimeout), true);
        private static readonly Lazy<Regex> reIsTextXmlType = new Lazy<Regex>(() => new Regex(@"^[^<]?<(\?xml[^\?]+\?>\s*)?<?(?<V1>[@%?/\$!:A-Za-z0-9_]+)(?:\s+(?<V2>[a-zA-Z0-9='://\.-]+))?(?:.+?(?<X>xmlns='[^']+'))?", RegexOptions.Compiled | RegexOptions.ExplicitCapture, reTimeout), true); //https://www.anycodings.com/1questions/4822064/check-string-for-valid-css-using-regex

        private static string GetTextXmlType(string filename, string mime, string content)
        {
            if (content.Length < 3) return null; //e.g. "<p>"
            if (!(content[0] == '<' || (content[1] == '<' && content[0] != '<'))) return null; //Starts with < or '<. Go figure.

            if (content.ContainsEx(" manifestVersion='1.0'")) return ".manifest"; //this could be anywhere in the header.

            var m = reIsTextXmlType.Value.Match(content);
            if (!m.Success) return null;
            var v1 = m.Groups["V1"].Value;
            if (v1 == "") return ".xml";

            if (!_getXmlTypeDict.TryGetTarget(out var dict))
            {
                dict = InitGetXmlType_Dictionary();
                _getXmlTypeDict.SetTarget(dict);
            }

            if (dict.TryGetValue(v1, out var ext))
            {
                if (ext == "RETRY")
                {
                    var v2 = m.Groups["V2"].Value;
                    if (v2.Equals("html",StringComparison.OrdinalIgnoreCase))
                    {
                        if (content.ContainsEx("<?php")) return ".php";
                    }

                    return dict.TryGetValue(string.Concat(v1,"|",v2), out ext) ? ext : ".xml";
                }
                else if (ext == "ROOT")
                {
                    if (content.ContainsEx("<value>text/microsoft-resx</value>")) return ".resx";
                    return ".xml";
                }
                else if (ext == "PROJ")
                {
                    var v2 = m.Groups["V2"].Value;
                    if (v2 == "") return ".proj";
                    else return ".vsproj"; //hard to detect which type of visual studio build project this is, so we just give them a generics name "Visual studio project"
                }
                else if (ext == "CONFIG")
                {
                    var v2 = m.Groups["V2"].Value;
                    if (v2 == "xmlns:xdt='http://schemas.microsoft.com/XML-Document-Transform'") return ".xslt";
                    else return ".config";
                }
                else return ext;
            }

            return ".xml";
        }
        private static readonly WeakReference<Dictionary<string, string>> _getXmlTypeDict = new WeakReference<Dictionary<string, string>>(null);
        private static Dictionary<string, string> InitGetXmlType_Dictionary()
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            d.Add("!DOCTYPE", "RETRY");
            d.Add("!DOCTYPE|boost", ".dbrush");
            d.Add("!DOCTYPE|document", ".DTD");
            d.Add("!DOCTYPE|HelpCollection", ".HxC");
            d.Add("!DOCTYPE|HelpIndex", ".HxK");
            d.Add("!DOCTYPE|HelpTOC", ".HXT");
            d.Add("!DOCTYPE|html", ".html");
            d.Add("!DOCTYPE|plist", ".plist");
            d.Add("!DOCTYPE|Project", ".vpj");
            d.Add("!DOCTYPE|providers", ".mftx");
            d.Add("!DOCTYPE|SETemplate", ".setemplate");
            d.Add("!DOCTYPE|Templates", ".vpt");
            d.Add("!DOCTYPE|Version", ".tbr");
            d.Add("!DOCTYPE|Workspace", ".vpw");
            d.Add("!DOCTYPE|xsd:schema", ".xsd");
            d.Add("!DOCTYPE|xsl:stylesheet", ".xsl");
            d.Add("!ELEMENT", ".dtd");
            d.Add("!ENTITY", ".dtd");
            d.Add("$if$", ".config");
            d.Add("%", "RETRY");
            d.Add("%|", ".asp");
            d.Add("%|Set", ".asp");
            d.Add("%|var", ".inc");
            d.Add("%@", "RETRY");
            d.Add("%@|Application", ".asax");
            d.Add("%@|Control", ".ascx");
            d.Add("%@|Master", ".master");
            d.Add("%@|Page", ".aspx");
            d.Add("%@|WebHandler", ".ashx");
            d.Add("%@|WebService", ".asmx");
            d.Add("?rsa", ".sig");
            d.Add("?vlc", ".vlcx");
            d.Add("?xfa", ".xdc");
            d.Add("AcrobatUI", ".aaui");
            d.Add("actions", ".uaq");
            d.Add("Activity", ".xaml");
            d.Add("addin", ".Addin");
            d.Add("AFX_RIBBON", ".mfcribbon-ms");
            //d.Add("application", ".android");
            d.Add("Application", ".xaml");
            d.Add("ApplicationInsights", ".config");
            d.Add("ApplicationRuntime", ".uar");
            d.Add("asmv1:assembly", ".manifest");
            d.Add("assembly", "RETRY");
            d.Add("assembly|", ".manifest");
            d.Add("assembly|alias='System.Drawing'", ".resx");
            d.Add("assembly|description = 'Windows", ".manifest");
            d.Add("assembly|manifestVersion='1.0'", ".manifest");
            d.Add("assembly|xmlns='urn:schemas-microsoft-com:asm.v1'", ".manifest");
            d.Add("assembly|xmlns='urn:schemas-microsoft-com:asm.v3'", ".manifest");
            d.Add("AssemblyFoldersConfig", ".config");
            d.Add("AssocInfo", ".sip");
            d.Add("AutoVisualizer", ".natvis");
            d.Add("batch", ".tis");
            d.Add("book", ".devhelp2");
            d.Add("bootmedia", ".config");
            d.Add("browsers", ".browser");
            d.Add("bundles", ".config");
            d.Add("cam:ColorAppearanceModel", ".camp");
            d.Add("cdm:ColorDeviceModel", ".cdmp");
            d.Add("cep_report", ".ctr");
            d.Add("cfoutput", ".cfml");
            d.Add("ClassDiagram", ".cd");
            d.Add("CodeAnalysisPlugIn", ".caplugin");
            d.Add("CodeAnnotationData", ".sca");
            d.Add("CodeCoverage", ".config");
            d.Add("CodeSnippet", ".snippet");
            d.Add("CodeSnippets", ".snippet");
            d.Add("CommandTable", ".vsct");
            d.Add("CompatibilityList", ".cache");
            d.Add("Config", "CONFIG");
            d.Add("ContentPage", ".xaml");
            d.Add("ContentView", ".xaml");
            d.Add("CustomCapabilityDescriptor", ".sccd");
            d.Add("DataSetUISetting", ".xsc");
            d.Add("dcmPS:DiagnosticPackage", ".diagpkg");
            d.Add("DevFabricConfig", ".config");
            d.Add("device", ".def");
            d.Add("DiagramLayout", ".xss");
            d.Add("Dim", ".tt");
            //d.Add("DirectedGraph", ".dgsl");
            d.Add("DirectedGraph", ".dgml");
            d.Add("document", "RETRY");
            d.Add("document|type='com.apple.InterfaceBuilder.AppleTV.Storyboard'", ".storyboard");
            d.Add("document|type='com.apple.InterfaceBuilder.WatchKit.Storyboard'", ".storyboard");
            d.Add("document|type='com.apple.InterfaceBuilder3.CocoaTouch.Storyboard.XIB'", ".storyboard");
            d.Add("document|type='com.apple.InterfaceBuilder.AppleTV.XIB'", ".xib");
            d.Add("document|type='com.apple.InterfaceBuilder3.CocoaTouch.XIB'", ".xib");
            d.Add("duixml", ".duixml");
            d.Add("E2ETraceEvent", ".svclog");
            //d.Add("edmx:Edmx", ".diagram");
            d.Add("edmx:Edmx", ".edmx");
            d.Add("FlowDocument", ".xaml");
            d.Add("FlyoutPage", ".xaml");
            d.Add("Folder", ".wmdb");
            d.Add("FontFamily", ".CompositeFont");
            d.Add("FontFamilyCollection", ".CompositeFont");
            d.Add("form", ".html");
            d.Add("forms", ".forms");
            d.Add("Framework", ".frameworkxml");
            d.Add("GenericObjectDataSource", ".datasource");
            d.Add("gmm:GamutMapModel", ".gmmp");
            d.Add("grammar", ".grxml");
            d.Add("GrammarCollection", ".cache");
            d.Add("Grid", ".xaml");
            d.Add("gtob_config", ".cfg");
            d.Add("helpcfg", ".helpcfg");
            d.Add("HelpCollection", ".HXC");
            d.Add("HelpIndex", ".HXK");
            d.Add("html", ".html");
            d.Add("ImageManifest", ".imagemanifest");
            d.Add("Implementatation", ".psm1");
            d.Add("InstrumentationEngineConfiguration", ".config");
            d.Add("instrumentationManifest", ".manifest");
            d.Add("isolation", ".manifest");
            d.Add("its:rules", ".its");
            d.Add("jDownloader", ".mth");
            d.Add("KeyboardList", ".bin");
            d.Add("KeyFile", ".keyx");
            d.Add("Keys", ".keys");
            d.Add("Layouts", ".bin");
            d.Add("LCX", ".lce");
            d.Add("libraryDescription", ".library-ms");
            d.Add("Licenses", ".lic");
            d.Add("LinearLayout", ".axml");
            d.Add("locatingRules", ".loc");
            d.Add("LoggerInfo", ".tmp");
            d.Add("LoginRequest", ".rsrc");
            d.Add("look", ".look");
            d.Add("magicmap", ".mgk");
            d.Add("Manifest", ".manifest");
            d.Add("MasterDetailPage", ".xaml");
            d.Add("migration", ".dat");
            d.Add("MMC_ConsoleFile", ".msc");
            d.Add("mms", ".config");
            d.Add("MobileCompatTable", ".bin");
            d.Add("module", ".iml");
            d.Add("modulemap", ".mgk");
            d.Add("mso:customUI", ".officeUI");
            d.Add("MvcTextTemplateHost", ".tt");
            d.Add("mx:Application", ".mxml");
            d.Add("MyApplicationData", ".myapp");
            d.Add("Network", ".network");
            d.Add("nlog", ".nlog");
            d.Add("NonUserCode", ".natjmc");
            d.Add("NUnitProject", ".nunit");
            d.Add("nvi", ".nvi");
            d.Add("office:color", ".soc");
            d.Add("oobe", ".html");
            d.Add("OrderedTest", ".orderedtest");
            d.Add("package", "RETRY");
            d.Add("package|xmlns='http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd'", ".nuspec");
            d.Add("package_installation_info", ".pimx");
            d.Add("PackageLanguagePackManifest", ".vsixlangpack");
            d.Add("PackageManifest", ".vsixmanifest");
            d.Add("packages", ".config");
            d.Add("Page", ".xaml");
            d.Add("PageFunction", ".xaml");
            d.Add("para", ".docbook");
            d.Add("Patch", ".cache");
            d.Add("pdfpreflight", ".kfp");
            d.Add("persistedQuery", ".search-ms");
            d.Add("persistent", ".set");
            d.Add("playlist", ".xspf");
            d.Add("policyComments", ".cmtx");
            d.Add("policyDefinitionResources", ".adml");
            d.Add("policyDefinitions", ".admx");
            d.Add("PowerShellMetadata", ".cdxml");
            d.Add("Preferences", ".dat");
            d.Add("PremiereData", ".epr");
            d.Add("presentations", ".cfg");
            d.Add("Project", "PROJ");  //Special Handling by caller
            d.Add("ProjectSchemaDefinitions", ".xaml");
            d.Add("Properties", ".config");
            d.Add("providermanifest", ".manifest");
            d.Add("ProvideToolboxControl", ".vb");
            d.Add("PRX", ".prx");
            d.Add("PSConsoleFile", ".psc1");
            d.Add("r:license", ".xrm-ms");
            d.Add("RCC", ".qrc");
            d.Add("RDF", ".rdf");
            d.Add("rdf:Description", ".rdf");
            d.Add("RecentActions", ".dat");
            d.Add("RecentFiles", ".dat");
            d.Add("Relationships", ".rels");
            d.Add("Report", ".rdlc");
            d.Add("repositories", ".config");
            d.Add("ResourceDictionary", ".xaml");
            d.Add("resources", ".template");
            d.Add("rg:licenseGroup", ".xrm-ms");
            d.Add("RoleModule", ".csplugin");
            d.Add("root", "ROOT");
            d.Add("RS_AudioServiceResponse", ".ps1");
            d.Add("rss", ".rss");
            d.Add("Rule", ".xaml");
            d.Add("RuleSet", ".ruleset");
            d.Add("RunSettings", ".runsettings");
            d.Add("schema", ".xsd");
            d.Add("scriptlet", ".sct");
            d.Add("sect1", ".docbook");
            d.Add("server_list", ".config");
            d.Add("ServiceConfiguration", ".cscfg");
            d.Add("ServiceDefinition", ".csdef");
            d.Add("setting", ".setting");
            d.Add("settings", ".settings");
            d.Add("SettingsFile", ".settings");
            d.Add("SettingsToTranscode", ".preset");
            d.Add("setup", ".cfg");
            d.Add("Signature", ".psdsxs");
            d.Add("siteMap", ".sitemap");
            d.Add("SLCInfo", ".slc");
            d.Add("Snippets", ".ps1xml");
            d.Add("software_identification_tag", ".swidtag");
            d.Add("StackPanel", ".xaml");
            d.Add("StepFilter", ".natstepfilter");
            d.Add("strings", ".strings");
            d.Add("StringTable", ".strings");
            d.Add("StyleCopSettings", ".StyleCop");
            d.Add("svg", ".svg");
            d.Add("swid:software_identification_tag", ".swidtag");
            d.Add("System", ".vb");
            d.Add("TabbedPage", ".xaml");
            d.Add("tagfile", ".tag");
            d.Add("tags", ".vpj");
            d.Add("template", ".vue");
            d.Add("TemplateDir", ".vstdir");
            d.Add("TestLists", ".vsmdi");
            d.Add("TestRunner", ".tdnet");
            d.Add("TestSettings", ".testsettings");
            d.Add("TestTypes", ".testtype");
            d.Add("TextView", ".android");
            d.Add("theme", ".theme");
            d.Add("tile", ".rsrc");
            d.Add("toast", ".rsrc");
            d.Add("tr", ".html");
            d.Add("Types", ".ps1xml");
            d.Add("ui", ".ui");
            d.Add("UITest", ".uitest");
            d.Add("UserControl", ".xaml");
            d.Add("UserSettings", ".vssettings");
            d.Add("var", ".tt");
            d.Add("VBMyExtensionTemplate", ".customdata");
            d.Add("ViewCell", ".xaml");
            d.Add("ViewerConfig", ".xml");
            d.Add("VisualStudioProject", "VSPROJ");
            d.Add("VisualWebDeveloper", ".webinfo");
            d.Add("Vsix", ".vsixmanifest");
            d.Add("VsixLanguagePack", ".vsixlangpack");
            d.Add("VSPerformanceSession", ".psess");
            d.Add("VSTemplate", ".vstemplate");
            d.Add("VSTemplateManifest", ".vstman");
            d.Add("w:styles", ".bin");
            d.Add("wap", ".provxml");
            d.Add("Window", ".xaml");
            d.Add("WindowProfile", ".winprf");
            d.Add("WindowsPerformanceRecorder", ".wprp");
            d.Add("WordBreakerRules", ".dat");
            d.Add("Workflow", ".sequ");
            //d.Add("workspace", ".workspace");
            d.Add("workspace", ".ws");
            d.Add("wpf:ResourceDictionary", ".wpf");
            d.Add("x:package", ".dalp");
            d.Add("x:stylesheet", ".xsl");
            d.Add("x:xmpmeta", ".xmp");
            d.Add("xliff", ".xlf");
            d.Add("XrML", ".cc");
            d.Add("xs:complexType", ".xsd");
            d.Add("xs:schema", ".xsd");
            d.Add("XSDDesignerLayout", ".xsx");
            d.Add("xsl:stylesheet", ".xsl");
            d.Add("xsl:transform", ".bin");

            return d;
        }

        private static bool IsASM(string filename)
        {
            var content = ReadText(filename, 1024, true);
            if (string.IsNullOrEmpty(content)) return false;

            string[] instructions = new string[]
            {
//Registers
",cl ",
" eax,",
"eax ",
" edx,",
"edx ",
//Instructions
" add ",
" addi ",
" addis ",
" align ",
//" and ", --too common in other non asm files
" ands ",
" asr ",
" b ",
" bcl ",
" bctr ",
" beq ",
" bgt ",
" bhi ",
" bl ",
" blo ",
" blr",
" bne ",
" bswap ",
" bt ",
" bts ",
" call ",
" cbnz ",
" cbz ",
" cld ",
" cmeq ",
" cmp ",
" cmpi ",
" cmpsb ",
" cmpwi ",
" dcb ",
" dec ",
" div ",
" ends ",
" eor ",
" equ ",
" extsb ",
" fmov ",
" imul ",
" inc ",
" int ",
" ja ",
" jae ",
" jb ",
" jbe ",
" jc ",
" je ",
" jecxz ",
" jge ",
" jmp ",
" jnb ",
" jnc ",
" jne ",
" jns ",
" jnz ",
" jz ",
" lbz ",
" ld1 ",
" ldr ",
" ldrb ",
" lds ",
" lea ",
" les ",
" lfd ",
" lfs ",
" lg ",
" lha ",
" lhz ",
" lwz ",
" lwzu",
" mflr ",
" mov ",
" movapd ",
" movaps ",
" movd ",
" movdqa ",
" movdqu ",
" movntps ",
" movq ",
" movups ",
" movzx ",
" mr ",
" mtctr ",
" mtlr ",
" mul ",
" neg ",
" nop ",
" nop",
" not ",
" or ",
" pop ",
" push ",
" rcr ",
" rep ",
" repe ",
" repne ",
" ret ",
" sar ",
" sbb ",
" sg ",
" sgu ",
" shl ",
" shld ",
" shr ",
" shrd ",
" slwi ",
" srdi ",
" std ",
" stfd ",
" sub ",
" subs ",
" test ",
" umaxv ",
" uminv ",
" xchg ",
" xor "
            };

            // Debugging....
            // var k = 0;
            // foreach(var inst in instructions)
            // {
            //     var b = content.ContainsEx(inst);
            //     if (b)
            //     {
            //         Debug.WriteLine(inst);
            //         k++;
            //     }
            // }

            int kount = 0;
            return instructions.Any(i => content.ContainsEx(i) && ++kount > 5); //stops searching after 6 matches
            //return instructions.Count(m => content.ContainsEx(m)) > 5; //searches entire list

        }

        internal static string BuildListXmlRegex(string content) //for BuildList exploration
        {
            if (content == null) return " "; //space because of Excel formatting.
            if (content.Length < 9) return " ";// because "[Binary]" length==8. See BuildList.
            if (!(content[0] == '<' || (content[1] == '<' && content[0] != '<'))) return " ";
            var m = reIsTextXmlType.Value.Match(content);
            if (!m.Success) return "(xml noMatch)";
            var v1 = m.Groups["V1"].Value;
            return string.Concat("(\"", m.Groups["V1"].Value, "\", \"", m.Groups["V2"].Value, "\")", content.ContainsEx(" manifestVersion='1.0'") ? " IsManifest" : "");
        }
        internal static string BuildListNamespaces(string content) //for BuildList exploration
        {
            var ms = reXmlNamespaces.Value.Matches(content);
            if (ms.Count == 0) return " ";
            return string.Join("\t", ms.Cast<Match>().Select(m => m.Groups["URL"].Value).Where(m => !string.IsNullOrEmpty(m)).OrderBy(m => m, StringComparer.OrdinalIgnoreCase));
        }

        #region Funcions to get extension by LibMagic description
        private static string ApplicationOctetStream2Ext(string desc)
        {
            var i = desc.IndexOfAny(new char[] { ',', '(' });
            if (i != -1) desc = desc.Substring(0, i);

            if (!_applicationOctetStream2ExtDict.TryGetTarget(out var dict))
            {
                dict = InitApplicationOctetStream2Ext_Dictionary();
                _applicationOctetStream2ExtDict.SetTarget(dict);
            }
            if (dict.TryGetValue(desc, out var ext)) return ext;

            return null;
        }
        private static readonly WeakReference<Dictionary<string, string>> _applicationOctetStream2ExtDict = new WeakReference<Dictionary<string, string>>(null);
        private static Dictionary<string, string> InitApplicationOctetStream2Ext_Dictionary()
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "64-bit XCOFF executable or object module", ".dll" },
                { "ASCII font metrics", ".afm" },
                { "Adobe Multiple Master font", ".mmm" },
                { "Apple binary property list", ".sks" },
                { "AppleScript compiled", ".scpt" },
                { "Blender3D", ".blend" },
                { "Certificate", ".cer" },
                { "DER Encoded Key Pair", ".der" },
                { "DIY-Thermocam raw data ", ".dat" },
                { "DIY-Thermocam raw data (Lepton 2.x)", ".abr" },
                { "DOS/MBR boot sector; partition 1 : ID=0xee", ".vmgs" },
                { "Embedded OpenType (EOT)", ".eot" },
                { "GDSII Stream file version 56.66", ".abr" },
                { "Hermes JavaScript bytecode", ".bundle" },
                { "InstallShield CAB", ".cab" },
                { "JPEG 2000 codestream", ".jpc" },
                { "Keepass password database 2.x KDBX", ".kdbx" },
                { "Lua bytecode", ".luac" },
                { "MS Windows HtmlHelp Data", ".chm" },
                { "MS Windows Vista Event Log", ".evtx" },
                { "MS Windows registry file", ".hve" },
                { "MS Windows shortcut", ".lnk" },
                { "MSVC .res", ".res" },
                { "Microsoft Cabinet archive data", ".cab" },
                { "Microsoft DirectDraw Surface ", ".dds" },
                { "Microsoft Disk Image eXtended", ".vhdx" },
                { "Microsoft OOXML", ".ooxml" },
                { "Microsoft Roslyn C# debugging symbols version 1.0", ".pdb" },
                { "OpenPGP Public Key Version 2", ".pgp" },
                { "OpenPGP Public Key Version 4", ".pgp" },
                { "OpenPGP Public Key", ".pgp" },
                { "OpenPGP Secret Key", ".pgp" },
                { "PGP Secret Sub-key -", ".pgp" },
                { "PGP symmetric key encrypted data - Plaintext or unencrypted data salted -", ".pgp" },
                { "PGP symmetric key encrypted data - Plaintext or unencrypted data", ".pgp" },
                { "PGP symmetric key encrypted data - salted & iterated -", ".pgp" },
                { "PGP symmetric key encrypted data -", ".pgp" },
                { "PostScript Type 1 font program data ", ".pfb" },
                { "Qt Translation file", ".qm" },
                { "RAGE Package Format (RPF)", ".pol" },
                { "SQLite Write-Ahead Log", ".sqlite-wal" },
                { "WebAssembly ", ".wasm" },
                { "Web Open Font Format ", ".woff2" }, //Web Open Font Format (Version 2), TrueType
                { "Web Open Font Format", ".woff" }, //Web Open Font Format, TrueType, length 15000, version 1.0
                { "Winamp EQ library filev1.1", ".q1" },
                { "Winamp plug in", ".avs" },
                { "Windows Enhanced Metafile ", ".emf" },
                { "WordPerfect graphic image", ".wpg" },
                { "magic binary file for file", ".mgc" },
            };
        }

        private static string TextPlain2Ext(string desc)
        {
            var i = desc.IndexOfAny(new char[] { ',', '(' });
            if (i != -1) desc = desc.Substring(0, i);

            if (!_textPlain2ExtDict.TryGetTarget(out var dict))
            {
                dict = InitTextPlain2Ext_Dictionary();
                _textPlain2ExtDict.SetTarget(dict);
            }
            if (dict.TryGetValue(desc, out var ext)) return ext;

            return null;
        }
        private static readonly WeakReference<Dictionary<string, string>> _textPlain2ExtDict = new WeakReference<Dictionary<string, string>>(null);
        private static Dictionary<string, string> InitTextPlain2Ext_Dictionary()
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "a  /usr/bin/env node script", ".js" },
                { "a /usr/bin/env node script", ".js" },
                { "a /usr/bin/env ./node_modules/.bin/coffee script", ".coffee" },
                { "a /usr/bin/env wish script", ".tcl" },
                { "a /usr/bin/osascript script", ".scpt" },
                { "a perl script", ".pl" },
                { "a perl -w script", ".pl" },
                { "awk or perl script", ".awk" },
                { "DCL command file", ".vms" },
                { "LCOV coverage tracefile", ".info" },
                { "M3U playlist", ".m3u" },
                { "Microsoft HTML Help Project", ".hhp" },
                { "MS Windows 95 Internet shortcut text ", ".url" },
                { "MS Windows help file Content", ".cnt" },
                { "MS-DOS CONFIG.SYS", ".ini" },
                { "OpenPGP Secret Key", ".pem" },
                { "PEM certificate", ".pem" },
                { "PEM RSA private key", ".pem" },
                { "OS/2 REXX batch file", ".rexx" },
                { "Perl POD document", ".pod" },
                { "Perl5 module source", ".pm" },
                { "PLS playlist", ".pls" },
                { "PPD file", ".ppd" },
                { "Python script", ".py" },
                { "Tcl script", ".tcl" },
                { "Windows codepage translator", ".cpx" },
                { "xbm image ", ".xbm" },
            };
        }

        private static string ApplicationXDosexec2Ext(string desc)
        {
            if (!_applicationXDosexec2ExtDict.TryGetTarget(out var dict))
            {
                dict = InitApplicationXDosexec2Ext_Dictionary();
                _applicationXDosexec2ExtDict.SetTarget(dict);
            }
            if (dict.TryGetValue(desc, out var ext)) return ext;

            return null;
        }
        private static readonly WeakReference<Dictionary<string, string>> _applicationXDosexec2ExtDict = new WeakReference<Dictionary<string, string>>(null);
        private static Dictionary<string, string> InitApplicationXDosexec2Ext_Dictionary()
        {
            return new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "COM executable for DOS", ".com" },
                { "COM executable for MS-DOS", ".com" },
                { "DOS executable (COM)", ".com" },
                { "DOS executable (COM, 0x8C-variant)", ".com" },
                { "FREE-DOS executable (COM), UPX compressed, uncompressed 5626 bytes", ".com" },
                { "MS-DOS executable PE32 executable (DLL) (console) Intel 80386 (stripped to external PDB), for MS Windows, MZ for MS-DOS", ".exe" },
                { "MS-DOS executable PE32 executable (DLL) (console) Intel 80386 Mono/.Net assembly, for MS Windows", ".exe" },
                { "MS-DOS executable PE32 executable (DLL) (GUI) Intel 80386 Mono/.Net assembly, for MS Windows", ".exe" },
                { "MS-DOS executable PE32 executable (DLL) Intel 80386, for MS Windows", ".exe" },
                { "MS-DOS executable PE32+ executable (DLL) (console) x86-64 Mono/.Net assembly, for MS Windows", ".exe" },
                { "MS-DOS executable PE32+ executable (DLL) (GUI) x86-64 Mono/.Net assembly, for MS Windows", ".exe" },
                { "MS-DOS executable", ".exe" },
                { "MS-DOS executable, MZ for MS-DOS", ".exe" },
                { "MS-DOS executable, NE for MS Windows 3.x (DLL or font)", ".dll" },
                { "MS-DOS executable, NE for MS Windows 3.x (EXE)", ".exe" },
                { "PE32 executable (console) ARMv7 Thumb Mono/.Net assembly, for MS Windows", ".exe" },
                { "PE32 executable (console) ARMv7 Thumb, for MS Windows", ".exe" },
                { "PE32 executable (console) Intel 80386 (stripped to external PDB), for MS Windows", ".exe" },
                { "PE32 executable (console) Intel 80386 Mono/.Net assembly, for MS Windows", ".exe" },
                { "PE32 executable (console) Intel 80386, for MS Windows", ".exe" },
                { "PE32 executable (console) Intel 80386, for MS Windows, Nullsoft Installer self-extracting archive", ".exe" },
                { "PE32 executable (console) Intel 80386, for MS Windows, PECompact2 compressed", ".exe" },
                { "PE32 executable (console) Intel 80386, for MS Windows, UPX compressed", ".exe" },
                { "PE32 executable (DLL) (console) ARMv7 Thumb Mono/.Net assembly, for MS Windows", ".dll" },
                { "PE32 executable (DLL) (console) ARMv7 Thumb, for MS Windows", ".dll" },
                { "PE32 executable (DLL) (console) Intel 80386 (stripped to external PDB), for MS Windows", ".dll" },
                { "PE32 executable (DLL) (console) Intel 80386 (stripped to external PDB), for MS Windows, UPX compressed", ".dll" },
                { "PE32 executable (DLL) (console) Intel 80386 Mono/.Net assembly, for MS Windows", ".dll" },
                { "PE32 executable (DLL) (console) Intel 80386, for MS Windows", ".dll" },
                { "PE32 executable (DLL) (console) Intel 80386, for MS Windows, UPX compressed", ".dll" },
                { "PE32 executable (DLL) (EFI application) Intel 80386, for MS Windows", ".efi" },
                { "PE32 executable (DLL) (GUI) ARMv7 Thumb, for MS Windows", ".dll" },
                { "PE32 executable (DLL) (GUI) Intel 80386 (stripped to external PDB), for MS Windows", ".dll" },
                { "PE32 executable (DLL) (GUI) Intel 80386 (stripped to external PDB), for MS Windows, UPX compressed", ".dll" },
                { "PE32 executable (DLL) (GUI) Intel 80386 Mono/.Net assembly, for MS Windows", ".dll" },
                { "PE32 executable (DLL) (GUI) Intel 80386, for MS Windows", ".dll" },
                { "PE32 executable (DLL) (GUI) Intel 80386, for MS Windows, PECompact2 compressed", ".dll" },
                { "PE32 executable (DLL) (GUI) Intel 80386, for MS Windows, UPX compressed", ".dll" },
                { "PE32 executable (DLL) (native) Intel 80386, for MS Windows", ".sys" },
                { "PE32 executable (DLL) Intel 80386, for MS Windows", ".dll" },
                { "PE32 executable (GUI) ARMv7 Thumb Mono/.Net assembly, for MS Windows", ".exe" },
                { "PE32 executable (GUI) ARMv7 Thumb, for MS Windows", ".exe" },
                { "PE32 executable (GUI) Intel 80386 (stripped to external PDB), for MS Windows", ".exe" },
                { "PE32 executable (GUI) Intel 80386 (stripped to external PDB), for MS Windows, MS CAB-Installer self-extracting archive", ".exe" },
                { "PE32 executable (GUI) Intel 80386 (stripped to external PDB), for MS Windows, Nullsoft Installer self-extracting archive", ".exe" },
                { "PE32 executable (GUI) Intel 80386 (stripped to external PDB), for MS Windows, UPX compressed", ".exe" },
                { "PE32 executable (GUI) Intel 80386 Mono/.Net assembly, for MS Windows", ".exe" },
                { "PE32 executable (GUI) Intel 80386, for MS Windows", ".exe" },
                { "PE32 executable (GUI) Intel 80386, for MS Windows, InstallShield self-extracting archive", ".exe" },
                { "PE32 executable (GUI) Intel 80386, for MS Windows, MS CAB-Installer self-extracting archive", ".exe" },
                { "PE32 executable (GUI) Intel 80386, for MS Windows, Nullsoft Installer self-extracting archive", ".exe" },
                { "PE32 executable (GUI) Intel 80386, for MS Windows, RAR self-extracting archive", ".exe" },
                { "PE32 executable (GUI) Intel 80386, for MS Windows, UPX compressed", ".exe" },
                { "PE32 executable (GUI) Intel 80386, for MS Windows, ZIP self-extracting archive (WinZip)", ".exe" },
                { "PE32 executable (native) Intel 80386 (stripped to external PDB), for MS Windows", ".sys" },
                { "PE32 executable (native) Intel 80386, for MS Windows", ".sys" },
                { "PE32 executable Intel 80386, for MS Windows", ".exe" },
                { "PE32+ executable (console) Aarch64 Mono/.Net assembly, for MS Windows", ".exe" },
                { "PE32+ executable (console) Aarch64, for MS Windows", ".exe" },
                { "PE32+ executable (console) x86-64 (stripped to external PDB), for MS Windows", ".exe" },
                { "PE32+ executable (console) x86-64 Mono/.Net assembly, for MS Windows", ".exe" },
                { "PE32+ executable (console) x86-64, for MS Windows", ".exe" },
                { "PE32+ executable (console) x86-64, for MS Windows, COFF", ".exe" },
                { "PE32+ executable (DLL) (console) Aarch64 Mono/.Net assembly, for MS Windows", ".dll" },
                { "PE32+ executable (DLL) (console) Aarch64, for MS Windows", ".dll" },
                { "PE32+ executable (DLL) (console) x86-64 (stripped to external PDB), for MS Windows", ".dll" },
                { "PE32+ executable (DLL) (console) x86-64 Mono/.Net assembly, for MS Windows", ".dll" },
                { "PE32+ executable (DLL) (console) x86-64, for MS Windows", ".dll" },
                { "PE32+ executable (DLL) (EFI application) x86-64, for MS Windows", ".efi" },
                { "PE32+ executable (DLL) (GUI) Aarch64, for MS Windows", ".dll" },
                { "PE32+ executable (DLL) (GUI) x86-64 (stripped to external PDB), for MS Windows", ".dll" },
                { "PE32+ executable (DLL) (GUI) x86-64 Mono/.Net assembly, for MS Windows", ".dll" },
                { "PE32+ executable (DLL) (GUI) x86-64, for MS Windows", ".dll" },
                { "PE32+ executable (DLL) (native) x86-64, for MS Windows", ".sys" },
                { "PE32+ executable (DLL) x86-64, for MS Windows", ".dll" },
                { "PE32+ executable (GUI) Aarch64, for MS Windows", ".exe" },
                { "PE32+ executable (GUI) Intel Itanium, for MS Windows", ".exe" },
                { "PE32+ executable (GUI) x86-64 (stripped to external PDB), for MS Windows", ".exe" },
                { "PE32+ executable (GUI) x86-64 Mono/.Net assembly, for MS Windows", ".exe" },
                { "PE32+ executable (GUI) x86-64, for MS Windows", ".exe" },
                { "PE32+ executable (native) Aarch64, for MS Windows", ".sys" },
                { "PE32+ executable (native) Intel Itanium, for MS Windows", ".sys" },
                { "PE32+ executable (native) x86-64, for MS Windows", ".sys" },
                { "PE32+ executable x86-64, for MS Windows", ".efi" },
                { "Windows Program Information File for COMMAND.COM", ".pif" },
            };
        }
        // public static string GetExeType(string filename)
        // {
        //     const int IMAGE_FILE_SYSTEM = 0x1000; //Allowed to run in kernel mode.
        //     const int IMAGE_FILE_DLL = 0x2000;
        //     const int IMAGE_FILE_EXECUTABLE_IMAGE = 0x0002;
        //     const int IMAGE_DOS_SIGNATURE = 0x5A4D;  // 'MZ'
        //     const int IMAGE_NT_SIGNATURE = 0x00004550;  // 'PE00'
        //     const int MIN_EXE_SIZE = 1024;
        // 
        //    using (var stream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, FileOptions.SequentialScan))
        //     {
        //         if (stream.Length < MIN_EXE_SIZE) return null;
        //         var reader = new BinaryReader(stream);
        // 
        //         stream.Position = 0;  //starts with struct IMAGE_DOS_HEADER 
        //         if (reader.ReadInt16() != IMAGE_DOS_SIGNATURE) return null;
        // 
        //         stream.Seek(64 - 4, SeekOrigin.Begin); //read last field IMAGE_DOS_HEADER.e_lfanew. This is the offset where the IMAGE_NT_HEADER begins
        //         int offset = reader.ReadInt32();
        //         stream.Seek(offset, SeekOrigin.Begin);
        //         if (offset + 4 + 18 > stream.Length) return ".com"; //must be an old DOS ".com"  executable.
        //         if (reader.ReadInt32() != IMAGE_NT_SIGNATURE) return ".com";
        // 
        //         stream.Seek(18, SeekOrigin.Current); //point to last word of IMAGE_FILE_HEADER
        //         short characteristics = reader.ReadInt16();
        // 
        //         if ((characteristics & IMAGE_FILE_DLL) == IMAGE_FILE_SYSTEM) return ".drv";
        //         if ((characteristics & IMAGE_FILE_DLL) == IMAGE_FILE_DLL) return ".dll";
        //         if ((characteristics & IMAGE_FILE_EXECUTABLE_IMAGE) == IMAGE_FILE_DLL) return ".exe";
        // 
        //         return ".obj";
        //     }
        // }
        #endregion Funcions to get extension by LibMagic description

        /// <summary>
        /// Determine if a specified substring exists within this string.
        /// Functionally equivalant to string.ContainsEx(value), but references the lowest-level function that can perform this action for speed.
        /// </summary>
        /// <param name="content">Source string.</param>
        /// <param name="value">The sub-string to search for within 'content'..</param>
        /// <returns> true if 'value'  occurs within this string</returns>
        private static bool ContainsEx(this string content, string value) => System.Globalization.CultureInfo.InvariantCulture.CompareInfo.IndexOf(content, value, 0, content.Length, CompareOptions.Ordinal) != -1;
        private static bool ContainsExI(this string content, string value) => System.Globalization.CultureInfo.InvariantCulture.CompareInfo.IndexOf(content, value, 0, content.Length, CompareOptions.OrdinalIgnoreCase) != -1;

        /// <summary>
        /// Retrieves text file content for file type detection. 
        /// Strips Comments /*multiline...*/   and  //...\n.  and  #....\n  but not  #!.....\n
        /// Replaces multiple whitespace chars (including CRLF) with a single space char
        /// Replaces all double-quote chars with single-quote chars for easier searching and consistancy. 
        /// </summary>
        /// <param name="filename">Name of text file to retrieve string from.</param>
        /// <param name="maxsize">Maximum size needed for file type detection *after* comment stripping. Default=1024 chars.</param>
        /// <param name="stripSemicolonLineComment">True to also strip semicolon line comments. Default=false, but if first non-whitespace character is a semicolon, this is automatically set to true.</param>
        /// <returns>
        ///   Returns a string used for file type detection or<br/>
        ///    (1) empty < 8 characters left after comments are stripped out or<br/>
        ///    (2) null if this is a binary file.
        /// </returns>
        internal static string ReadText(string filename, int maxsize = 1024, bool stripSemicolonLineComment=false)
        {
            // This is orders of magnitude faster than doing the same thing with:
            //    (1) Reading file into massive string large enough (40*maxsize ?) that after the comments are stripped the resulting string is at least 'maxsize'.
            //    (2) Test if string has any non-text binary file chars (e.g. chars 0-31 except crlf) then skip and return null.
            //    (3) Replace comments with a space char.
            //          Regex(@"(<!--.*?-->)|(/\*.*?\*/)|((?<!https?:)//.*?\n)|(#[^!].*?\n)|(\s+)", RegexOptions.Compiled | RegexOptions.Singleline)
            //    (4) Replace multiple whitespace chars with a single space char.
            //          Regex(@"\s+", RegexOptions.Compiled | RegexOptions.Singleline)
            //    (5) Truncate to 'maxsize'
            //    (6) Replace all double-quote chars with single-quote chars for easier searching.
            //          string.Replace('"', '\'')

            StreamReader sr = null;
            try
            {
                sr = new StreamReader(filename, ReadText_encoding);
                if (sr.BaseStream.Length < 16) return string.Empty;

                var sb = new StringBuilder();

                //Get current char and test if it is valid text char. if not, throw exception. It will be caught in the catch block and ReadText() returns null, as expected.
                Func<int> ReadChar = () =>
                {
                    int c = sr.Read();
                    //Non-printing chars allowed in a text file: 9(TAB), 10(LF), 12 (FF), 13(CR); Not allowed: the rest of the control chars + 127(DEL), 0xFFFD (Invalid Char Replacement)
                    if ((c >= 0 && c <= 8) || c == 11 || (c >= 14 && c <= 31) || c == 127 || c == 0xFFFD) return 0; //could throw Exception() but it is more costly than just returning 0 and detecting it in the caller.
                    return c;
                };

                //Verify previous chars are 'http:' or 'https:' when testing for '//' comment because  https://www.something.com is not a comment.
                Func<bool> IsHttpPrefix = () => 
                {
                    if (sb.Length < 7) return false;
                    var len = sb.Length;
                    if (sb[len - 1] != ':') return false;
                    var h  = sb[len - 6];
                    var t1 = sb[len - 5];
                    var t2 = sb[len - 4];
                    var p  = sb[len - 3];
                    var s  = sb[len - 2];
                    if (s=='p') { h = t1; t1 = t2; t2 = p; p = s; s = 's'; } //shift
                    if (h == 'h' && t1 == 't' && t2 == 't' && p == 'p' && s == 's') return true;
                    return false;
                };

                bool firstchar = true;
                bool maybeHtmlComment = false;
                int ch;
                int prevChar = ' '; //must be set to space detect leading space chars.
                while ((ch = ReadChar()) != -1 && sb.Length <= maxsize)
                {
                    if (ch == 0) return null; //this is a binary file
                    if (firstchar) //Auto-detect if semi-colon comment header is in this file
                    {
                        if (char.IsWhiteSpace((char)ch)) continue;
                        if (ch == ';') stripSemicolonLineComment = true;
                        firstchar = false;
                    }

                    //Strip <!-- xxxx --> comments
                    if (ch == '<' && sr.Peek() == '!')
                    {
                        sb.Append((char)ch);
                        sb.Append((char)ReadChar());
                        prevChar = '!';
                        maybeHtmlComment = true;
                        continue;
                    }
                    if (maybeHtmlComment && ch == '-' && sr.Peek() == '-' )
                    {
                        maybeHtmlComment = false;
                        sb.Length -= 2;
                        sr.Read();
                        while ((ch = ReadChar()) != -1) { if (ch == 0) return null; if (ch == '-' && sr.Read() == '-' && sr.Read() == '>') break; }
                        prevChar = ' ';
                        continue;
                    }
                    if (maybeHtmlComment && ch != '-') maybeHtmlComment = false;

                    //Strip /* xxxx */ comments
                    if (ch == '/' && sr.Peek() == '*')
                    {
                        while ((ch = ReadChar()) != -1) { if (ch == 0) return null; if (ch == '*' && sr.Peek() == '/') break; }
                        ReadChar();
                        continue;
                    }

                    //Strip //xxxx  single-line comments but not url http://xxxx
                    if (ch == '/' && sr.Peek() == '/' && !IsHttpPrefix())
                    {
                        while ((ch = ReadChar()) != -1 && ch != '\n') { if (ch == 0) return null; }
                        continue;
                    }

                    //Strip #xxxx  single-line comments but not unix-like  #!executable
                    if (ch == '#' && sr.Peek() != '!')
                    {
                        while ((ch = ReadChar()) != -1 && ch != '\n') { if (ch == 0) return null; }
                        continue;
                    }

                    //Strip INI-style single-line comments if directed to...
                    if (stripSemicolonLineComment && ch == ';')
                    {
                        while ((ch = ReadChar()) != -1 && ch != '\n') { if (ch == 0) return null; }
                        continue;
                    }

                    //replace all whitespace with a single space
                    if (char.IsWhiteSpace((char)ch)) ch = ' ';
                    if (prevChar == ' ' && ch == ' ') continue;

                    //Replace all double-quotes with single-quotes
                    if (ch == '"') ch = '\'';

                    prevChar = ch;
                    sb.Append((char)ch);
                }

                if (sb.Length < 8) return string.Empty;  //nothing to detect
                if (sb[sb.Length - 1] == ' ') sb.Length--; //truncate any trailing spaces
                return sb.ToString();
            }
            catch
            {
                return null;
            }
            finally
            {
                sr?.Close();
            }
        }
        private static readonly Encoding ReadText_encoding = Encoding.GetEncoding(65001, new EncoderReplacementFallback("\xFFFD"), new DecoderReplacementFallback("\xFFFD")); //65001==UTF-8 codepage
    }
}
