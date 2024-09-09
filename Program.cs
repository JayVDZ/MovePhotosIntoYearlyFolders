using System.Text.RegularExpressions;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.FileSystem;

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
Console.WriteLine("\tFiles Moved Or Copied By File Modified Date: " + FilesMovedOrCopiedByFileModifiedDate);
Console.WriteLine("\tFiles Moved Or Copied By Inference: " + FilesMovedOrCopiedByInference);
Console.WriteLine($"\tBytes Moved Or Copied: {Math.Round(fileSize.TeraBytes)} TB / {Math.Round(fileSize.GigaBytes)} GB / {Math.Round(fileSize.MegaBytes)} MB / {Math.Round(fileSize.KilaBytes)} KB");
Console.WriteLine("\tFiles skipped: " + FilesSkipped);
Console.WriteLine("\tSource Folders Deleted: " + SourceFoldersDeleted);
Console.WriteLine("\t.db Files Deleted: " + RedundantFilesDeleted);
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
        Console.WriteLine("4: optional bool: Delete empty source folder when done? (default: false)");
        Console.WriteLine("5: optional bool: Delete .db and .xmp files? (default: false)");
        Console.WriteLine("6: optional bool: Attempt to determine photo year when there's no metadata? (default: true)");
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
            Console.WriteLine("Third argument: Move = true, Copy = false) could not be parsed.");
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
            Console.WriteLine("Fourth argument: Delete empty source folder when done) could not be parsed.");
            Console.ResetColor();
            return false;
        }
        DeleteEmptySourceFolder = deleteEmptySourceFolder;
    }
    
    if (args.Length >= 5)
    {
        if (!bool.TryParse(args[4], out var deleteRedundantFiles))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Fifth argument: Delete .db and .xmp files could not be parsed.");
            Console.ResetColor();
            return false;
        }
        DeleteRedundantFiles = deleteRedundantFiles;
    }
    
    if (args.Length >= 6)
    {
        if (!bool.TryParse(args[5], out var attemptNoMetadataYearDetermination))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Sixth argument: Attempt to determine photo year when there's no metadata? Could not be parsed.");
            Console.ResetColor();
            return false;
        }
        AttemptNoMetadataYearDetermination = attemptNoMetadataYearDetermination;
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
    Console.WriteLine("\tDelete .db and .xmp files? " + DeleteRedundantFiles);
    Console.WriteLine("\tAttempt to determine photo year when there's no metadata? " + AttemptNoMetadataYearDetermination);

    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine("Is this right? [y/n]");
    Console.ResetColor();
    var key = Console.ReadKey();
    return key.KeyChar == 'y';
}

void EnumerateFiles(string path)
{
    // do we have any files in the source folder we want to delete, i.e. ones that aren't relevant anymore?
    // this only applies to Move (not Copy) where we are making changes to the source folder.
    if (DeleteRedundantFiles && MoveOrCopy)
    {
        foreach (var dbFile in System.IO.Directory.GetFiles(path, "*.db"))
        {
            File.Delete(dbFile);
            RedundantFilesDeleted++;
            Console.WriteLine("Deleted: " +  dbFile);
        }
        
        foreach (var dbFile in System.IO.Directory.GetFiles(path, "*.xmp"))
        {
            File.Delete(dbFile);
            RedundantFilesDeleted++;
            Console.WriteLine("Deleted: " +  dbFile);
        }
    }
    
    foreach (var sourceFilePath in System.IO.Directory.EnumerateFiles(path))
    {
        int? year;
        var sourceFileInfo = new FileInfo(sourceFilePath);
        IReadOnlyList<MetadataExtractor.Directory> fileMetadata;
        
        try
        {
            fileMetadata = ImageMetadataReader.ReadMetadata(sourceFilePath);
        }
        catch (ImageProcessingException)
        {
            // either this isn't a photo, or the file is corrupt
            Console.WriteLine($"Skipping: {path}");
            FilesSkipped++;
            continue;
        }
        
        var dateTaken = GetImageDateTaken(fileMetadata, out var dateMethod);
        var sourceFileBytes = sourceFileInfo.Length;
        if (dateTaken.HasValue)
        {
            year = dateTaken.Value.Year;
        }
        else
        {
            if (!AttemptNoMetadataYearDetermination)
            {
                Console.WriteLine($"Skipping: {sourceFilePath} as there is no metadata and params don't allow inference.");
                FilesSkipped++;
                continue;
            }

            year = InferPhotoYear(sourceFilePath, sourceFileInfo);
            if (year.HasValue)
            {
                dateMethod = DateMethod.Inference;
            }
            else
            {
                Console.WriteLine($"Skipping: {sourceFilePath} as the photo's year of capture could not be determined as there's no metadata and it couldn't be inferred.");
                FilesSkipped++;
                continue;
            }
        }

        var yearlyDestinationPath = Path.Combine(DestinationPath, year.ToString() ?? throw new InvalidOperationException("year is null"));
        if (!System.IO.Directory.Exists(yearlyDestinationPath))
            System.IO.Directory.CreateDirectory(yearlyDestinationPath);
        
        // determine the new file path
        var destinationFilePath = Path.Combine(yearlyDestinationPath, Path.GetFileName(sourceFilePath));
        var destinationFilename = Path.GetFileName(destinationFilePath);
        
        // make sure the new file path is unique
        var destinationFilePathIsUnique = !File.Exists(destinationFilePath);
        if (!destinationFilePathIsUnique)
        {
            // are both files the same size and name? this suggests they're a duplicate. do not process.
            var destinationFileInfo = new FileInfo(destinationFilePath);
            if (destinationFileInfo.Length == sourceFileInfo.Length)
            {
                // stop processing this file and move on to the next
                Console.WriteLine($"Skipped {destinationFilename} ({year}) as it seems to be a duplicate.");
                FilesSkipped++;
                continue;
            }
            
            var instance = 2;
            while (!destinationFilePathIsUnique)
            {
                var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(destinationFilePath);
                fileNameWithoutExtension = $"{fileNameWithoutExtension}_{instance}";
                var newFilename = fileNameWithoutExtension + "." + Path.GetExtension(destinationFilePath);
                destinationFilePath = Path.Combine(yearlyDestinationPath, newFilename);

                if (Path.Exists(destinationFilePath))
                    instance++;
                else
                    destinationFilePathIsUnique = true;
            }    
        }

        if (MoveOrCopy)
        {
            File.Move(sourceFilePath, destinationFilePath);
            Console.WriteLine($"Moved file: {destinationFilename} to {year}");
        }
        else
        {
            File.Copy(sourceFilePath, destinationFilePath);
            Console.WriteLine($"Copied file: {destinationFilename} to {year}");
        }
        
        switch (dateMethod)
        {
            case DateMethod.CaptureTime:
                FilesMovedOrCopiedByCaptureTime++;
                break;
            case DateMethod.FileModifiedDate:
                FilesMovedOrCopiedByFileModifiedDate++;
                break;
            case DateMethod.Inference:
                FilesMovedOrCopiedByInference++;
                break;
        }

        FilesMovedOrCopied++;
        BytesMovedOrCopied += sourceFileBytes;
    }
    
    // now recurse into any sub-folders
    foreach (var subFolder in System.IO.Directory.EnumerateDirectories(path))
    {
        EnumerateFiles(subFolder);

        // only delete sub-folders if we're told to in a Move configuration (if Copy, we must leave source alone)
        if (MoveOrCopy && DeleteEmptySourceFolder)
        {
            // is sub folder empty now?
            var fileCount = System.IO.Directory.GetFiles(subFolder).Length;
            if (fileCount > 0) 
                continue;

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
}

// there's no metadata, try to work out the photo capture date another way
static int? InferPhotoYear(string path, FileInfo fileInfo)
{
    int? year = null;
    
    // can we extract the year from the filename?
    // i.e. IMG_20150703_200006_1.JPG
    var imgMatch = RegexImg().Match(path);
    if (imgMatch.Success)
    {
        var date = imgMatch.Groups[1].Value;
        year = int.Parse(date[..4]);
    }

    if (!year.HasValue)
    {
        // i.e. 2011-11-03 18.02.38.jpg
        var yearMatch = RegexFirstFourDigits().Match(path);
        if (yearMatch.Success)
            year = int.Parse(yearMatch.Groups[1].Value);
    }

    if (!year.HasValue)
    {
        // fall back to using the last time the file was written, which seems to be the next best way to determine the age of the photo
        year = fileInfo.LastWriteTimeUtc.Year;    
    }
    return year;
}

static DateTime? GetImageDateTaken(IEnumerable<MetadataExtractor.Directory> directories, out Program.DateMethod dateMethod)
{
    // find the Exif SubIFD directory that has the date time original tag in.
    // there can be multiple directories and the tag only exists in one.
    var enumerableDirectories = directories as MetadataExtractor.Directory[] ?? directories.ToArray();
    var exifSubIfDirectory = enumerableDirectories.OfType<ExifSubIfdDirectory>().FirstOrDefault(q => q.ContainsTag(ExifDirectoryBase.TagDateTimeOriginal));
    
    // query the tag's value
    if (exifSubIfDirectory != null && exifSubIfDirectory.TryGetDateTime(ExifDirectoryBase.TagDateTimeOriginal, out var dateTime))
    {
        dateMethod = DateMethod.CaptureTime;
        return dateTime;
    }

    // okay, we haven't got the preferred tag, what about others?
    var fileMetadataDirectory = enumerableDirectories.OfType<FileMetadataDirectory>().FirstOrDefault(q => q.ContainsTag(FileMetadataDirectory.TagFileModifiedDate));
    if (fileMetadataDirectory != null && fileMetadataDirectory.TryGetDateTime(FileMetadataDirectory.TagFileModifiedDate, out var modifiedDate))
    {
        dateMethod = DateMethod.FileModifiedDate;
        return modifiedDate;
    }

    dateMethod = DateMethod.NotSet;
    return null;
}

// properties need to be defined this way, and in this location.
// ReSharper disable once UnusedType.Global
internal partial class Program
{
    private static string SourcePath { get; set; } = null!;
    private static string DestinationPath { get; set; } = null!;
    private static bool MoveOrCopy { get; set; } = true; // default is to move
    private static bool DeleteEmptySourceFolder { get; set; }
    private static bool DeleteRedundantFiles { get; set; } // causes .db and .xmp files to be deleted when encountered
    private static bool AttemptNoMetadataYearDetermination { get; set; } = true;
    private static int FilesMovedOrCopied { get; set; }
    private static long BytesMovedOrCopied { get; set; }
    private static int FilesMovedOrCopiedByCaptureTime { get; set; }
    private static int FilesMovedOrCopiedByFileModifiedDate { get; set; }
    private static int FilesMovedOrCopiedByInference { get; set; }
    private static int FilesSkipped { get; set; }
    private static int SourceFoldersDeleted { get; set; }
    private static int RedundantFilesDeleted { get; set; }
    
    [GeneratedRegex("IMG_(\\d*)_")]
    private static partial Regex RegexImg();

    [GeneratedRegex("(\\d{4})-(\\d{2})-(\\d{2}).*")]
    private static partial Regex RegexFirstFourDigits();

    /// <summary>
    /// How was a file dated?
    /// </summary>
    private enum DateMethod
    {
        NotSet,
        CaptureTime,
        FileModifiedDate,
        Inference
    }
}