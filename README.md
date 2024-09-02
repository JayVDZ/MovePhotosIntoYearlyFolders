# MovePhotosIntoYearlyFolders
A tool that analyses photo capture time and moves photos into folders for the year they were taken.

## Arguments
1. string: Source folder path, i.e. `"c:\my_old_path"`
2. string: Destination folder path, i.e. `"c:\my_new_path"`
3. optional bool: Move = true, Copy = false (default: true)
4. optional bool: Delete empty source folder when done (default: false)

## Example
Open a command prompt and CD to the program location.

`.\MovePhotosIntoYearlyFolders.exe "C:\Temp\SourceFolder" "C:\Temp\DestinationFolder" true true true`



## References
Uses https://www.nuget.org/packages/MetadataExtractor to determine what the capture photo dates are.

Uses https://www.nuget.org/packages/FileSize for prettifying bytes moved or copied.