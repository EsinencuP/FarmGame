# Автономная проверка синтаксиса C#, принадлежности сборкам и чистой логики строительства.
# Скрипт ничего не устанавливает и не изменяет внутри Unity-проекта.
param(
    # Корень Unity-проекта; по умолчанию вычисляется относительно папки Verification.
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot)
)

# Любая ошибка завершает проверку, а строгий режим обнаруживает опечатки в переменных.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# Используется компилятор, уже встроенный в PowerShell 7: без загрузок и заглушек Unity API.
if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'Run this script with PowerShell 7 (pwsh).' }
# roslyn хранит путь к анализатору синтаксиса C#.
$roslyn = Join-Path $PSHOME 'Microsoft.CodeAnalysis.CSharp.dll'
if (-not (Test-Path -LiteralPath $roslyn)) { throw 'Bundled C# compiler not found.' }
Add-Type -Path (Join-Path $PSHOME 'Microsoft.CodeAnalysis.dll')
Add-Type -Path $roslyn

# gameRoot ограничивает поиск игровым кодом, files содержит найденные C#-скрипты.
$gameRoot = Join-Path $ProjectRoot 'Assets/_Game'
$files = @(Get-ChildItem -LiteralPath $gameRoot -Recurse -Filter '*.cs')
# Каждый новый C#-файл обязан кратко объяснять назначение своего основного типа.
$missingSummaries = @($files | Where-Object {
    [IO.File]::ReadAllText($_.FullName) -notmatch '///\s*<summary>'
})
if ($missingSummaries.Count -gt 0) {
    throw "Scripts without explanatory summary comments: $($missingSummaries.FullName -join ', ')"
}
# errors накапливает все ошибки, чтобы один запуск показал полный результат.
$errors = @()
# options фиксирует ту же версию синтаксиса C# 9, на которую рассчитан проект.
$options = [Microsoft.CodeAnalysis.CSharp.CSharpParseOptions]::Default.WithLanguageVersion(
    [Microsoft.CodeAnalysis.CSharp.LanguageVersion]::CSharp9)
foreach ($file in $files) {
    # Каждый файл разбирается отдельно с исходным путём для понятной диагностики.
    $tree = [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText(
        [IO.File]::ReadAllText($file.FullName), $options, $file.FullName)
    $errors += @($tree.GetDiagnostics() | Where-Object Severity -eq 'Error')
    # Ближайший asmdef вверх по дереву определяет владельца скрипта по правилам Unity.
    $directory = $file.Directory
    $assembly = @()
    while ($directory -and $directory.FullName.StartsWith($gameRoot, [StringComparison]::OrdinalIgnoreCase)) {
        $assembly = @(Get-ChildItem -LiteralPath $directory.FullName -Filter '*.asmdef')
        if ($assembly.Count -gt 0) { break }
        $directory = $directory.Parent
    }
    if ($assembly.Count -ne 1) { throw "No unique assembly owner: $($file.FullName)" }
    $definition = Get-Content -LiteralPath $assembly[0].FullName -Raw | ConvertFrom-Json
    # Ожидаемая сборка зависит от расположения runtime, EditMode или PlayMode-файла.
    $expected = if ($file.FullName -match '[\\/]Editor[\\/]') { 'MyLittleFarm.Editor' }
        elseif ($file.FullName -match '[\\/]Tests[\\/]EditMode[\\/]') { 'MyLittleFarm.EditModeTests' }
        elseif ($file.FullName -match '[\\/]Tests[\\/]PlayMode[\\/]') { 'MyLittleFarm.PlayModeTests' }
        else { 'MyLittleFarm.Runtime' }
    if ($definition.name -ne $expected) { throw "Wrong assembly for $($file.Name): $($definition.name)" }
}
if ($errors.Count -gt 0) { $errors | ForEach-Object { Write-Output $_ }; throw 'C# syntax errors found.' }
Write-Output "PASS: C# 9 syntax and assembly ownership for $($files.Count) scripts."
Write-Output "PASS: all $($files.Count) scripts contain explanatory summary comments."

# Рабочий bootstrap не должен снова получить автоматический runtime-генератор всей сцены.
$bootstrapSource = [IO.File]::ReadAllText((Join-Path $gameRoot 'Core/GameBootstrap.cs'))
if ($bootstrapSource -match 'RuntimeInitializeOnLoadMethod\(RuntimeInitializeLoadType\.AfterSceneLoad\)') {
    throw 'GameBootstrap must bind baked objects instead of generating the scene after load.'
}
$bakerPath = Join-Path $gameRoot 'Editor/PrototypeSceneBaker.cs'
if (-not (Test-Path -LiteralPath $bakerPath)) {
    throw 'Prototype scene baker is missing.'
}
Write-Output 'PASS: runtime scene auto-generation is disabled and the Edit Mode baker is present.'

# GridSystem v2 обязан жить в Core/Grid и выбирать поверхность без аллокаций через RaycastNonAlloc.
$gridPath = Join-Path $gameRoot 'Core/Grid/GridSystem.cs'
$selectorPath = Join-Path $gameRoot 'Core/Grid/CellSelector.cs'
$chunkPath = Join-Path $gameRoot 'Core/Grid/TilemapChunk.cs'
foreach ($requiredPath in @($gridPath, $selectorPath, $chunkPath)) {
    if (-not (Test-Path -LiteralPath $requiredPath)) { throw "GridSystem v2 script is missing: $requiredPath" }
}
$gridSource = [IO.File]::ReadAllText($gridPath)
$selectorSource = [IO.File]::ReadAllText($selectorPath)
if ($gridSource -notmatch 'Dictionary<Vector2Int, ChunkData>' -or $gridSource -notmatch 'GridToLocalCell') {
    throw 'GridSystem v2 chunk storage or negative-coordinate conversion is missing.'
}
if ($selectorSource -notmatch 'RaycastNonAlloc' -or $selectorSource -match 'new Plane\(') {
    throw 'CellSelector must raycast baked TilemapChunk colliders without per-frame allocations.'
}
Write-Output 'PASS: world-scale chunk grid and non-allocating terrain selection contracts are present.'

# Параметры движения и камеры должны оставаться настройками Inspector, а не скрытыми константами.
$playerPath = Join-Path $gameRoot 'Gameplay/World/PlayerController.cs'
$cameraPath = Join-Path $gameRoot 'Gameplay/World/IsometricCameraController.cs'
$inputPath = Join-Path $gameRoot 'Core/InputReader.cs'
$playerSource = [IO.File]::ReadAllText($playerPath)
$cameraSource = [IO.File]::ReadAllText($cameraPath)
$inputSource = [IO.File]::ReadAllText($inputPath)
foreach ($setting in @('walkSpeed', 'runSpeed', 'jumpHeight', 'gravity', 'groundedVelocity', 'rotationSpeed')) {
    if ($playerSource -notmatch "\[SerializeField[^\]]*\][^;]*\b$setting\b") {
        throw "Player movement setting is not exposed in Inspector: $setting"
    }
}
foreach ($setting in @('initialYaw', 'initialDistance', 'pitch', 'lookHeight', 'orbitSensitivity',
        'zoomSensitivity', 'minDistance', 'maxDistance')) {
    if ($cameraSource -notmatch "\[SerializeField[^\]]*\][^;]*\b$setting\b") {
        throw "Camera setting is not exposed in Inspector: $setting"
    }
}
foreach ($setting in @('moveForwardKey', 'moveBackwardKey', 'moveLeftKey', 'moveRightKey', 'sprintKey',
        'jumpKey', 'interactKey', 'plantTreeKey', 'buildToggleKey', 'saveKey', 'loadKey')) {
    if ($inputSource -notmatch "\[SerializeField[^\]]*\][^;]*\b$setting\b") {
        throw "Input binding is not exposed in Inspector: $setting"
    }
}
if (($inputSource -notmatch 'SprintHeld = shift') -or
    ($inputSource -notmatch 'JumpPressed = WasPressed\(keyboard, jumpKey\)') -or
    ($playerSource -notmatch 'Mathf\.Sqrt\(jumpHeight \* -2f \* gravity\)')) {
    throw 'Run or jump movement contract is missing.'
}
Write-Output 'PASS: inspector-backed player/camera settings and run/jump contracts are present.'

# Эти файлы не зависят от UnityEngine и компилируются как настоящая доменная реализация.
$domain = @('Gameplay/Building/BuildingDefinition.cs', 'Gameplay/Building/BuildingRuntimeState.cs',
    'Gameplay/Building/BuildingLayout.cs', 'Tests/EditMode/BuildingContractChecks.cs') |
    ForEach-Object { Join-Path $gameRoot $_ }
Add-Type -Path $domain
# assertions подтверждает объём реально выполненных контрактных проверок.
$assertions = [MyLittleFarm.Tests.EditMode.BuildingContractChecks]::Run()
Write-Output "PASS: actual building domain compiled; $assertions assertions, including 1000 deterministic random operations."

# Экономическая модель также компилируется и выполняется автономно на настоящем C# коде проекта.
$economyDomain = @('Gameplay/Economy/TransactionMath.cs',
    'Tests/EditMode/TransactionMathContractChecks.cs') |
    ForEach-Object { Join-Path $gameRoot $_ }
Add-Type -Path $economyDomain
$economyAssertions = [MyLittleFarm.Tests.EditMode.TransactionMathContractChecks]::Run()
Write-Output "PASS: actual economy calculations compiled; $economyAssertions assertions, including 1000 deterministic random transactions."

# Минимальная заглушка заменяет только Unity Vector2Int; ChunkData и GridMath берутся из игры без копий.
$gridDomain = @('Verification/UnityVector2IntStub.cs',
    'Assets/_Game/Core/Grid/CellType.cs', 'Assets/_Game/Core/Grid/CellData.cs',
    'Assets/_Game/Core/Grid/TerrainCell.cs', 'Assets/_Game/Core/Grid/FarmingCell.cs',
    'Assets/_Game/Core/Grid/BuildingCell.cs', 'Assets/_Game/Core/Grid/GridMath.cs',
    'Assets/_Game/Core/Grid/ChunkData.cs',
    'Assets/_Game/Tests/EditMode/GridArchitectureContractChecks.cs') |
    ForEach-Object { Join-Path $ProjectRoot $_ }
Add-Type -Path $gridDomain
$gridAssertions = [MyLittleFarm.Tests.EditMode.GridArchitectureContractChecks]::Run()
Write-Output "PASS: layered chunk model compiled; $gridAssertions assertions, including all coordinates from -4096 to 4096."

# Первый блок Этапа 2 обязан оставаться data-driven: новые культуры не получают отдельные системы.
$catalogSource = [IO.File]::ReadAllText((Join-Path $gameRoot 'Gameplay/Farming/FarmCatalog.cs'))
$inputSource = [IO.File]::ReadAllText((Join-Path $gameRoot 'Core/InputReader.cs'))
$quickSlotSource = [IO.File]::ReadAllText((Join-Path $gameRoot 'Gameplay/Economy/QuickSlotSystem.cs'))
foreach ($requiredCrop in @('"carrot"', '"potato"', '"wheat"', '"tomato"', '"corn"', '"cucumber"')) {
    if ($catalogSource -notmatch $requiredCrop) { throw "Stage 2 crop is missing: $requiredCrop" }
}
foreach ($requiredToken in @('seedSlot5Key', 'seedSlot6Key', 'SeedSelection', 'Range(0, 5)')) {
    if (($inputSource + $quickSlotSource) -notmatch [regex]::Escape($requiredToken)) {
        throw "Six seed slots contract is missing: $requiredToken"
    }
}
Write-Output 'PASS: Stage 2 content block has six data-driven crops and six Inspector-backed seed slots.'

# Вертикальный срез Этапа 2 должен содержать повторный урожай дерева и цикл курицы.
$orchardSource = [IO.File]::ReadAllText((Join-Path $gameRoot 'Gameplay/Farming/OrchardSystem.cs'))
$animalSource = [IO.File]::ReadAllText((Join-Path $gameRoot 'Gameplay/Animals/AnimalSystem.cs'))
$saveSource = [IO.File]::ReadAllText((Join-Path $gameRoot 'Core/SaveSystem.cs'))
foreach ($requiredToken in @('PlantApple', 'TryHarvest', 'RegrowSeconds', 'TreeOccupantId')) {
    if ($orchardSource -notmatch [regex]::Escape($requiredToken)) {
        throw "Apple tree loop contract is missing: $requiredToken"
    }
}
foreach ($requiredToken in @('AddChicken', 'Feed', 'TryCollect', 'productIntervalSeconds', 'MaxAnimals')) {
    if ($animalSource -notmatch [regex]::Escape($requiredToken)) {
        throw "Chicken loop contract is missing: $requiredToken"
    }
}
if (($saveSource -notmatch 'List<AnimalRuntimeState> animals') -or ($saveSource -notmatch 'ValidateAnimals')) {
    throw 'Animal save and validation contract is missing.'
}
Write-Output 'PASS: Stage 2 orchard repeat-harvest and chicken feed/egg/save contracts are present.'
Write-Output 'NOT RUN: Unity assembly compilation, EditMode/PlayMode tests, rendering, input and 20-30 minute playtest. These require Unity Editor.'
