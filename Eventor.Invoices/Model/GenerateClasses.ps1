$namespace = "Eventor.Api.Model"
$outputDir = "."
$outputFileName = "EventorApiModel.cs"

$env:Path += ";C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.8 Tools\"

$fileList = Get-ChildItem -R *.xsd
$fileListSpaceDelimited = ($fileList | Select-Object -ExpandProperty FullName) -join " "

$cmd = "xsd /c /namespace:$namespace /o:$outputDir $fileListSpaceDelimited"

Invoke-Expression $cmd

# Resulting file will be named after the last file in list, but with *.cs extension. Find this name:
$currentFilename = ($fileList | Select-Object -last 1 | Select-Object -ExpandProperty Name ) -replace ".xsd",".cs"

# Move generated file to proper location:
Move-Item -Force $outputDir/$currentFilename $outputDir/$outputFileName