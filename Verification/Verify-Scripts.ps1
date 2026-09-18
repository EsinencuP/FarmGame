param([string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot))
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# Uses the compiler already bundled with PowerShell 7. No downloads or Unity API stubs.
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'Run this script with PowerShell 7 (pwsh).' }
$roslyn = Join-Path $PSHOME 'Microsoft.CodeAnalysis.CSharp.dll'
if (-not (Test-Path -LiteralPath $roslyn)) { throw 'Bundled C# compiler not found.' }
Add-Type -Path (Join-Path $PSHOME 'Microsoft.CodeAnalysis.dll')
Add-Type -Path $roslyn

$gameRoot = Join-Path $ProjectRoot 'Assets/_Game'
$files = @(Get-ChildItem -LiteralPath $gameRoot -Recurse -Filter '*.cs')
$errors = @()
$options = [Microsoft.CodeAnalysis.CSharp.CSharpParseOptions]::Default.WithLanguageVersion(
    [Microsoft.CodeAnalysis.CSharp.LanguageVersion]::CSharp9)
foreach ($file in $files) {
    $tree = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText(
        [IO.File]::ReadAllText($file.FullName), $options, $file.FullName)
    $errors += @($tree.GetDiagnostics() | Where-Object Severity -eq 'Error')
    $directory = $file.Directory
    $assembly = @()
    while ($directory -and $directory.FullName.StartsWith($gameRoot, [StringComparison]::OrdinalIgnoreCase)) {
        $assembly = @(Get-ChildItem -LiteralPath $directory.FullName -Filter '*.asmdef')
        if ($assembly.Count -gt 0) { break }
        $directory = $directory.Parent
    }
    if ($assembly.Count -ne 1) { throw "No unique assembly owner: $($file.FullName)" }
    $definition = Get-Content -LiteralPath $assembly[0].FullName -Raw | ConvertFrom-Json
    $expected = if ($file.FullName -match '[\\/]Tests[\\/]EditMode[\\/]') { 'MyLittleFarm.EditModeTests' }
        elseif ($file.FullName -match '[\\/]Tests[\\/]PlayMode[\\/]') { 'MyLittleFarm.PlayModeTests' }
        else { 'MyLittleFarm.Runtime' }
    if ($definition.name -ne $expected) { throw "Wrong assembly for $($file.Name): $($definition.name)" }
}
if ($errors.Count -gt 0) { $errors | ForEach-Object { Write-Output $_ }; throw 'C# syntax errors found.' }
Write-Output "PASS: C# 9 syntax and assembly ownership for $($files.Count) scripts."

$domain = @('Gameplay/Building/BuildingDefinition.cs', 'Gameplay/Building/BuildingRuntimeState.cs',
    'Gameplay/Building/BuildingLayout.cs', 'Tests/EditMode/BuildingContractChecks.cs') |
    ForEach-Object { Join-Path $gameRoot $_ }
Add-Type -Path $domain
$assertions = [MyLittleFarm.Tests.EditMode.BuildingContractChecks]::Run()
Write-Output "PASS: actual building domain compiled; $assertions assertions, including 1000 deterministic random operations."
Write-Output 'NOT RUN: Unity assembly compilation, EditMode/PlayMode tests, rendering and input. These require Unity Editor.'
