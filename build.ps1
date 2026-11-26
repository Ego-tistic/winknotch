param()
$ErrorActionPreference = 'Stop'
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) {
    throw 'The 64-bit .NET Framework compiler is required. Build on Windows with .NET Framework 4.8 installed.'
}
$metadata = Join-Path $env:WINDIR 'System32\WinMetadata'
$references = @(
    "$framework/System.Runtime.dll",
    "$framework/System.Runtime.WindowsRuntime.dll",
    "$framework/System.Xaml.dll",
    "$framework/System.Drawing.dll",
    "$framework/WPF/WindowsBase.dll",
    "$framework/WPF/PresentationCore.dll",
    "$framework/WPF/PresentationFramework.dll",
    "$framework/WPF/UIAutomationTypes.dll",
    "$framework/WPF/UIAutomationClient.dll"
)
if (Test-Path -LiteralPath "$metadata/Windows.Media.winmd") {
    $references += @("$metadata/Windows.Foundation.winmd", "$metadata/Windows.Media.winmd", "$metadata/Windows.Storage.winmd")
} else {
    $unionMetadata = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\UnionMetadata'
    $windowsMetadata = Get-ChildItem -LiteralPath $unionMetadata -Filter Windows.winmd -Recurse -ErrorAction SilentlyContinue |
        Sort-Object FullName -Descending | Select-Object -First 1
    if (-not $windowsMetadata) { throw 'Windows metadata is missing. Install the Windows 10 or 11 SDK.' }
    $references += $windowsMetadata.FullName
}
$references | ForEach-Object { if (-not (Test-Path -LiteralPath $_)) { throw "Missing build reference: $_" } }
$output = Join-Path $PSScriptRoot 'artifacts'
New-Item -ItemType Directory -Force -Path $output | Out-Null
$compilerArguments = @('/nologo', '/target:winexe', '/optimize+', "/out:$output/WinKnotch.exe")
$compilerArguments += @($references | ForEach-Object { "/r:$_" })
$compilerArguments += Join-Path $PSScriptRoot 'src/BezelPlayer.cs'
& $compiler @compilerArguments
if ($LASTEXITCODE -ne 0) { throw 'Windows build failed.' }
Write-Output "Built $output\WinKnotch.exe"
