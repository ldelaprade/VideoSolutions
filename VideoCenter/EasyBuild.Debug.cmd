 # Create a single-file, self-contained executable for Windows x64
 dotnet publish -c Debug -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeAllContentForSelfExtract=true
