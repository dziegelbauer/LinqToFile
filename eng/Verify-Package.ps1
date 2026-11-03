param(
    [Parameter(Mandatory = $true)][string] $RuntimeIdentifier,
    [string] $PackageDirectory = 'artifacts/feed',
    [string] $PackageVersion = $env:LinqToFileVersion,
    [switch] $KeepWorkspace
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repository = Split-Path $PSScriptRoot -Parent
$feed = (Resolve-Path $PackageDirectory).Path
[xml] $build = Get-Content (Join-Path $repository 'Directory.Build.props') -Raw
$version = if ([string]::IsNullOrWhiteSpace($PackageVersion)) {
    [string] $build.Project.PropertyGroup.Version[0]
} else { $PackageVersion }
$package = Join-Path $feed "LinqToFile.$version.nupkg"

function Invoke-Dotnet {
    & dotnet @args
    if ($LASTEXITCODE -ne 0) { throw "dotnet failed with exit code $LASTEXITCODE" }
}

# Check the shipped artifact rather than the project metadata alone.
$archive = [System.IO.Compression.ZipFile]::OpenRead($package)
try {
    foreach ($entry in @('lib/net10.0/LinqToFile.dll', 'analyzers/dotnet/cs/LinqToFile.Generator.dll', 'LICENSE', 'README.md')) {
        if ($null -eq $archive.GetEntry($entry)) { throw "Missing package asset: $entry" }
    }
    $reader = [System.IO.StreamReader]::new($archive.GetEntry('LinqToFile.nuspec').Open())
    try { [xml] $manifest = $reader.ReadToEnd() } finally { $reader.Dispose() }
    $metadata = $manifest.package.metadata
    if ($metadata.id -ne 'LinqToFile' -or $metadata.version -ne $version) { throw 'Incorrect package identity.' }
    if ($metadata.license.InnerText -ne 'Apache-2.0') { throw 'Incorrect package license.' }
    if ($metadata.repository.url -ne 'https://github.com/dziegelbauer/LinqToFile.git') { throw 'Incorrect repository URL.' }
    if ($manifest.SelectNodes("//*[local-name()='dependency']").Count -ne 0) { throw 'Runtime package dependencies are not empty.' }
    $dlls = @($archive.Entries | Where-Object { $_.FullName.EndsWith('.dll') })
    if ($dlls.Count -ne 2) { throw 'Unexpected DLL payload in package.' }
    $reader = [System.IO.StreamReader]::new($archive.GetEntry('LICENSE').Open())
    try { $license = $reader.ReadToEnd() } finally { $reader.Dispose() }
    if ($license -cne [System.IO.File]::ReadAllText((Join-Path $repository 'LICENSE'))) { throw 'Packaged license differs from repository license.' }
    $reader = [System.IO.StreamReader]::new($archive.GetEntry('README.md').Open())
    try { $readme = $reader.ReadToEnd() } finally { $reader.Dispose() }
    $examples = [regex]::Matches($readme, '(?s)```csharp\r?\n(.*?)```')
    if ($examples.Count -ne 5) { throw 'Expected a model and four runnable API examples in the package README.' }
    if ($readme -cne [System.IO.File]::ReadAllText((Join-Path $repository 'README.md'))) { throw 'The package README differs from the main introduction.' }
} finally { $archive.Dispose() }

# Use an external directory and a fresh cache: no repository build imports or cached package.
$workspace = Join-Path ([System.IO.Path]::GetTempPath()) ('LinqToFile-Consumer-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $workspace | Out-Null
Write-Host "Clean consumer workspace: $workspace"
try {
    $models = Join-Path $workspace 'Models'
    $app = Join-Path $workspace 'Consumer'
    New-Item -ItemType Directory -Path $models, $app | Out-Null
    @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>net10.0</TargetFramework><Nullable>enable</Nullable><IsAotCompatible>true</IsAotCompatible></PropertyGroup>
  <ItemGroup><PackageReference Include="LinqToFile" Version="$version" /></ItemGroup>
</Project>
"@ | Set-Content (Join-Path $models 'Models.csproj')
    $examples[0].Groups[1].Value.Replace('namespace SampleModels;', 'namespace ConsumerModels;') | Set-Content (Join-Path $models 'Person.cs')
    # Compile the XLSX models in the external model assembly with the packaged generator.
    foreach ($name in @('WorkbookEntry', 'WorkbookCells', 'ScalarRecord', 'Product', 'ConvertedStock', 'Appointment', 'InventoryItem', 'HeaderlessEntry')) {
        $model = [System.IO.File]::ReadAllText((Join-Path $repository "samples/Models/$name.cs"))
        ("using System;`n" + $model.Replace('namespace SampleModels;', 'namespace ConsumerModels;')) |
            Set-Content (Join-Path $models "$name.cs")
    }
    foreach ($name in @('WorkbookSample', 'WorkbookCellsSample', 'WorkbookAsyncSample', 'CsvWritingSample', 'QuickStartSample', 'ConverterSample', 'TemporalFormatSample', 'TokenSample', 'StreamWritingSample', 'XlsxWritingSample', 'XlsxMutationSample', 'HeaderlessCsvSample')) {
        $sample = [System.IO.File]::ReadAllText((Join-Path $repository "samples/NativeAot/$name.cs"))
        $sample.Replace('using SampleModels;', 'using ConsumerModels;') | Set-Content (Join-Path $app "$name.cs")
    }
    @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable><PublishAot>true</PublishAot></PropertyGroup>
  <ItemGroup><ProjectReference Include="../Models/Models.csproj" /></ItemGroup>
</Project>
'@ | Set-Content (Join-Path $app 'Consumer.csproj')
    $program = @'
using System.Text;
using ConsumerModels;
using LinqToFile;
byte[] bytes = Encoding.Unicode.GetBytes("person_id,full_name,Age,Balance\n42,Ada,36,12.50\n7,Grace,17,\n");
var source = TabularFile.Csv<Person>(() => new MemoryStream(bytes)).WithEncoding(Encoding.Unicode);
var rows = source.ToArray();
if (rows.Length != 2 || rows.Sum(row => row.Id) != 49 || rows[0].Name != "Ada")
    throw new InvalidOperationException("Sync consumer failed.");
var dictionary = await source.ToDictionaryAsync(row => row.Id);
if (dictionary.Count != 2 || dictionary[7].Name != "Grace")
    throw new InvalidOperationException("Async consumer failed.");
int count = 0;
await foreach (var row in source.AsAsyncEnumerable().Where(row => row.Id > 10)) count++;
if (count != 1) throw new InvalidOperationException("Async LINQ consumer failed.");
string path = "people.csv", outputPath = "new-people.csv", workbookPath = "people.xlsx";
File.WriteAllText(path, "person_id,full_name,Age,Balance\n1,Ada,36,12.50\n2,Grace,17,\n");
File.Delete(outputPath);
QuickStartSample.CreateWorkbook(workbookPath);
'@
    foreach ($example in @($examples | Select-Object -Skip 1)) { $program += "`n" + $example.Groups[1].Value }
    $program += @'

if (adults.Count != 1 || adults[0].Name != "Ada" || peopleById.Count != 2 ||
    !file.Select(person => person.Id).SequenceEqual(new[] { 1, 3, 2 }) ||
    workbookAdults.Count != 1 || workbookAdults[0].Name != "Ada")
    throw new InvalidOperationException("README examples failed.");
WorkbookSample.Run();
await WorkbookAsyncSample.RunAsync();
await CsvWritingSample.RunAsync();
await ConverterSample.RunAsync();
await TemporalFormatSample.RunAsync();
await TokenSample.RunAsync();
await StreamWritingSample.RunAsync();
await XlsxWritingSample.RunAsync();
await XlsxMutationSample.RunAsync();
await HeaderlessCsvSample.RunAsync();
Console.WriteLine("Clean package consumer passed: README, CSV/XLSX sync/async, non-seekable input, and cross-assembly mapping.");
'@
    $program | Set-Content (Join-Path $app 'Program.cs')
    $escapedFeed = [System.Security.SecurityElement]::Escape($feed)
    @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources><clear /><add key="local" value="$escapedFeed" /><add key="nuget.org" value="https://api.nuget.org/v3/index.json" /></packageSources>
  <packageSourceMapping><packageSource key="local"><package pattern="LinqToFile" /></packageSource><packageSource key="nuget.org"><package pattern="*" /></packageSource></packageSourceMapping>
</configuration>
"@ | Set-Content (Join-Path $workspace 'NuGet.Config')
    $project = 'Consumer/Consumer.csproj'
    $cache = Join-Path $workspace 'packages'
    Push-Location $workspace
    try {
    Invoke-Dotnet restore $project -r $RuntimeIdentifier --configfile (Join-Path $workspace 'NuGet.Config') "-p:RestorePackagesPath=$cache" '-p:TreatWarningsAsErrors=true'
    Invoke-Dotnet run --project $project -c Release --no-restore "-p:RestorePackagesPath=$cache" '-p:TreatWarningsAsErrors=true'
    $publish = Join-Path $workspace 'publish'
    Invoke-Dotnet publish $project -c Release -r $RuntimeIdentifier --no-restore "-p:RestorePackagesPath=$cache" '-p:TreatWarningsAsErrors=true' -o $publish
    $executable = if ($IsWindows) { Join-Path $publish 'Consumer.exe' } else { Join-Path $publish 'Consumer' }
    & $executable
    if ($LASTEXITCODE -ne 0) { throw "Native AOT consumer failed with exit code $LASTEXITCODE" }
    Write-Host "Package validation passed: LinqToFile $version ($RuntimeIdentifier)"
    } finally { Pop-Location }
} finally { if (-not $KeepWorkspace) { Remove-Item $workspace -Recurse -Force } }
