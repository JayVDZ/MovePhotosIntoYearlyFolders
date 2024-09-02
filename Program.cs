using MetadataExtractor;
using MetadataExtractor.Formats.Exif;

if (!ValidateArgs())
    return;

if (!CheckArgumentsWithUser())
    return;

// create the destination folder if it doesn't already exist.
if (!System.IO.Directory.Exists(DestinationPath))
    System.IO.Directory.CreateDirectory(DestinationPath);

// start!
Console.WriteLine("----------------------------------------------");
EnumerateFiles(SourcePath);

// let the user know we're done
Console.WriteLine("----------------------------------------------");
Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine("All done!");
Console.ResetColor();
Console.WriteLine();

// write out some stats
var fileSize = new FileSize(BytesMovedOrCopied);
Console.WriteLine("Stats:");
Console.WriteLine("\tFiles Moved Or Copied: " + FilesMovedOrCopied);
Console.WriteLine("\tFiles Moved Or Copied By Capture Time: " + FilesMovedOrCopiedByCaptureTime);
Console.WriteLine("\tFiles Moved Or Copied By Creation Time: " + FilesMovedOrCopiedByFileCreationTime);
Console.WriteLine($"\tBytes Moved Or Copied: {Math.Round(fileSize.TeraBytes)} TB / {Math.Round(fileSize.GigaBytes)} GB / {Math.Round(fileSize.MegaBytes)} MB / {Math.Round(fileSize.KilaBytes)} KB");
Console.WriteLine("\tFiles skipped: " + FilesSkipped);
Console.WriteLine("\tSource Folders Deleted: " + SourceFoldersDeleted);
Console.WriteLine("\t.db Files Deleted: " + DbFilesDeleted);
return;

bool ValidateArgs()
{
    // collect & validate command-line arguments
    if (args.Length == 0)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("No arguments supplied. Acceptable ones are:");
        Console.WriteLine("1: string: Source folder path, i.e. \"c:\\my_old_path\"");
        Console.WriteLine("2: string: Destination folder path, i.e. \"c:\\my_new_path\"");
        Console.WriteLine("3: optional bool: Move = true, Copy = false (default: true)");
        Console.WriteLine("4: optional bool: Delete empty source folder when done (default: false)");
        Console.WriteLine("5: optional bool: Delete .db files (default: false)");
        Console.ResetColor();
        return false;
    }
    
    if (args.Length < 2)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("Need a source and destination folder argument at a minimum.");
        Console.ResetColor();
        return false;
    }
    
    SourcePath = args[0];
    if (!System.IO.Directory.Exists(SourcePath))
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Source folder ({SourcePath}) does not exist, or couldn't be accessed!");
        Console.ResetColor();
        return false;
    }
    
    DestinationPath = args[1];

    if (args.Length >= 3)
    {
        if (!bool.TryParse(args[2], out var moveOrCopy))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Third argument (bool: Move = true, Copy = false) could not be parsed.");
            Console.ResetColor();
            return false;
        }
        MoveOrCopy = moveOrCopy;
    }
    
    if (args.Length >= 4)
    {
        if (!bool.TryParse(args[3], out var deleteEmptySourceFolder))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Fourth argument (bool: Delete empty source folder when done) could not be parsed.");
            Console.ResetColor();
            return false;
        }
        DeleteEmptySourceFolder = deleteEmptySourceFolder;
    }
    
    if (args.Length >= 5)
    {
        if (!bool.TryParse(args[4], out var deleteDbFiles))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Firth argument (bool: Delete .db files could not be parsed.");
            Console.ResetColor();
            return false;
        }
        DeleteDbFiles = deleteDbFiles;
    }

    // all arguments check out
    return true;
}

bool CheckArgumentsWithUser()
{
    Console.WriteLine("Checking arguments:");
    Console.WriteLine("\tSource folder: " + SourcePath);
    Console.WriteLine("\tDestination folder: " + DestinationPath);
    Console.WriteLine("\tMove or Copy? " + (MoveOrCopy ? "Move" : "Copy"));
    Console.WriteLine("\tDelete empty source folder? " + DeleteEmptySourceFolder);
    Console.WriteLine("\tDelete .db files? " + DeleteDbFiles);

    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine("Is this right? [y/n]");
    Console.ResetColor();
    var key = Console.ReadKey();
    return key.KeyChar == 'y';
}

void EnumerateFiles(string path)
{
    if (DeleteDbFiles)
    {
        foreach (var dbFile in System.IO.Directory.GetFiles(path, "*.db"))
        {
            File.Delete(dbFile);
            DbFilesDeleted++;
            Console.WriteLine("Deleted: " +  dbFile);
        }
    }
    
    foreach (var filePath in System.IO.Directory.EnumerateFiles(path))
    {
        int year;
        var fileInfo = new FileInfo(filePath);

        IReadOnlyList<MetadataExtractor.Directory> fileMetadata;
        try
        {
            fileMetadata = ImageMetadataReader.ReadMetadata(filePath);
        }
        catch (ImageProcessingException)
        {
            // either this isn't a photo, or the file is corrupt
            Console.WriteLine($"Skipping: {path}");
            FilesSkipped++;
            continue;
        }
        
        var dateTaken = GetImageDateTaken(fileMetadata);
        var sourceFileBytes = fileInfo.Length;
        if (dateTaken.HasValue)
        {
            year = dateTaken.Value.Year;
            FilesMovedOrCopiedByCaptureTime++;
        }
        else
        {
            // fall back to using the last time the file was written, which seems to be the next best way to determine the age of the photo
            year = fileInfo.LastWriteTimeUtc.Year;
            FilesMovedOrCopiedByFileCreationTime++;
        }

        var yearlyDestinationPath = Path.Combine(DestinationPath, year.ToString());
        if (!System.IO.Directory.Exists(yearlyDestinationPath))
            System.IO.Directory.CreateDirectory(yearlyDestinationPath);
        
        // determine the new file path
        var newFilePath = Path.Combine(yearlyDestinationPath, Path.GetFileName(filePath));
        
        // make sure the new file path is unique
        var newFilePathIsUnique = !File.Exists(newFilePath);
        if (!newFilePathIsUnique)
        {
            var instance = 2;
            while (!newFilePathIsUnique)
            {
                var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(newFilePath);
                fileNameWithoutExtension = $"{fileNameWithoutExtension}_{instance}";
                var newFilename = fileNameWithoutExtension + "." + Path.GetExtension(newFilePath);
                newFilePath = Path.Combine(yearlyDestinationPath, newFilename);

                if (Path.Exists(newFilePath))
                    instance++;
                else
                    newFilePathIsUnique = true;
            }    
        }

        if (MoveOrCopy)
        {
            File.Move(filePath, newFilePath);
            Console.WriteLine("Moved file: " + Path.GetFileName(newFilePath));
        }
        else
        {
            File.Copy(filePath, newFilePath);
            Console.WriteLine("Copied file: " + Path.GetFileName(newFilePath));
        }

        FilesMovedOrCopied++;
        BytesMovedOrCopied += sourceFileBytes;
    }
    
    // now recurse into any sub-folders
    foreach (var subFolder in System.IO.Directory.EnumerateDirectories(path))
    {
        EnumerateFiles(subFolder);

        // is sub folder empty now?
        var fileCount = System.IO.Directory.GetFiles(subFolder).Length;
        if (!DeleteEmptySourceFolder || fileCount > 0) continue;

        try
        {
            System.IO.Directory.Delete(subFolder);
            SourceFoldersDeleted++;
            Console.WriteLine("Deleted empty source folder: " + subFolder);
        }
        catch (IOException)
        {
            Console.WriteLine($"Error encountered trying to delete sub folder ({subFolder}). Skipping.");
        }
    }
}

static DateTime? GetImageDateTaken(IEnumerable<MetadataExtractor.Directory> directories)
{
    // obtain the Exif SubIFD directory
    var directory = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
    if (directory == null)
        return null;

    // query the tag's value
    if (directory.TryGetDateTime(ExifDirectoryBase.TagDateTimeOriginal, out var dateTime))
        return dateTime;

    return null;
}

// properties need to be defined this way, and in this location.
// ReSharper disable once UnusedType.Global
internal partial class Program
{
    private static string SourcePath { get; set; } = null!;
    private static string DestinationPath { get; set; } = null!;
    private static bool MoveOrCopy { get; set; } = true; // default is to move
    private static bool DeleteEmptySourceFolder { get; set; } = false;
    private static bool DeleteDbFiles { get; set; } = false; // causes .db files to be deleted when encountered
    private static int FilesMovedOrCopied { get; set; }
    private static long BytesMovedOrCopied { get; set; }
    private static int FilesMovedOrCopiedByCaptureTime { get; set; }
    private static int FilesMovedOrCopiedByFileCreationTime { get; set; }
    private static int FilesSkipped { get; set; }
    private static int SourceFoldersDeleted { get; set; }
    private static int DbFilesDeleted { get; set; }
}