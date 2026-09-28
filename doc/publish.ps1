$ErrorActionPreference = "Stop"

$project = "AudioSegmentMatcher.App.csproj"
$output = "publish\win-x64"

Remove-Item $output -Recurse -Force -ErrorAction SilentlyContinue

dotnet publish $project `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $output

Write-Host ""
Write-Host "Publish completed:"
Write-Host $output