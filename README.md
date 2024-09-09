# MovePhotosIntoYearlyFolders
A command-line tool that analyses capture time from photos and videos and moves them into folders for the year they were taken, i.e. `c:\output\2004`, `c:\output\2005`, etc. Useful for bringing order to huge photo archives.

## Arguments
1. String: Source folder path, i.e. `"c:\my_old_path"`
2. String: Destination folder path, i.e. `"c:\my_new_path"`
3. Optional bool: Move = true, Copy = false (default: true)
4. Optional bool: Delete empty source folder when done (default: false)
5. Optional bool: Delete any redundant files (.db/.xmp) (default: false)
6. Optional bool: Attempt to determine photo year when there's no metadata (default: true)

## Example
Open a command prompt and CD to the program location.

`.\MovePhotosIntoYearlyFolders.exe "C:\Temp\SourceFolder" "C:\Temp\DestinationFolder" true true true false`

## References
Uses https://www.nuget.org/packages/MetadataExtractor to determine what the capture photo dates are.

Uses https://www.nuget.org/packages/FileSize for prettifying bytes moved or copied.