# Extension Detector

A library that can detect the mime type and file extension from the file content.
This uses Windows x64 LibMagic (x86 not supported) and extends the detection of many more windows file extensions.

It is also capable of referencing very long Unicode filenames.

## Available Functions
using ChuckHill2.ExtensionDetector;

static string FileExtension.**ByName**(string path);<br/>
static string FileExtension.**ByMimetype**(string mimeType);<br/>
static string FileExtension.**ByContent**(string filename);

static string MagicDetector.**LibMagic**(string filename, LibMagicOptions option); //low-level

Details for each may be found thru intellisense.

## Build
This has been built with Microsoft Visual Studio 2019 with .Net Framework 4.8