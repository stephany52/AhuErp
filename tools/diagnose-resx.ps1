# tools/diagnose-resx.ps1
# Диагностика: что реально лежит в embedded .resx внутри собранных
# AhuErp.Core.dll, по сравнению с .resx-файлом на диске. Запускать после
# regen-migrations.bat и Rebuild Solution в Visual Studio. Вывод копируй
# целиком в чат.

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$resxPath = Join-Path $repoRoot 'src\AhuErp.Core\Migrations\202604300000000_AddSearchIndex.resx'
$logicalName = 'AhuErp.Core.Migrations.AddSearchIndex.resources'

function Decode-EdmxFromResource([string]$dllPath) {
    if (-not (Test-Path $dllPath)) { return @{ Exists = $false } }
    $info = Get-Item $dllPath
    $asm = [System.Reflection.Assembly]::LoadFile($dllPath)
    $names = $asm.GetManifestResourceNames()
    $matched = $names | Where-Object { $_ -eq $logicalName }
    if (-not $matched) {
        return @{
            Exists       = $true
            Path         = $dllPath
            LastWrite    = $info.LastWriteTime
            ResourceFound = $false
            AllResources = @($names | Where-Object { $_ -like '*Migrations*' })
        }
    }
    $stream = $asm.GetManifestResourceStream($logicalName)
    $reader = New-Object System.Resources.ResourceReader($stream)
    $entries = @{}
    foreach ($e in $reader) { $entries[$e.Key] = $e.Value }
    $reader.Close()
    $stream.Close()
    if (-not $entries.ContainsKey('Target')) {
        return @{
            Exists       = $true
            Path         = $dllPath
            LastWrite    = $info.LastWriteTime
            ResourceFound = $true
            HasTarget    = $false
        }
    }
    $bytes = [Convert]::FromBase64String($entries['Target'])
    $ms = New-Object System.IO.MemoryStream(,$bytes)
    $gz = New-Object System.IO.Compression.GZipStream($ms, [System.IO.Compression.CompressionMode]::Decompress)
    $sr = New-Object System.IO.StreamReader($gz)
    $edmx = $sr.ReadToEnd()
    $sr.Close(); $gz.Close(); $ms.Close()
    $count = ([regex]::Matches($edmx, '<EntityType Name="([^"]+)"')).Count
    return @{
        Exists       = $true
        Path         = $dllPath
        LastWrite    = $info.LastWriteTime
        ResourceFound = $true
        HasTarget    = $true
        EntityTypeRefs = $count
        HasSubstitution = $edmx.Contains('Name="Substitution"')
        HasNotification = $edmx.Contains('Name="Notification"')
        HasDocumentSignature = $edmx.Contains('Name="DocumentSignature"')
        HasAttachmentTextIndex = $edmx.Contains('Name="AttachmentTextIndex"')
        HasIsLockedColumn = $edmx -match '<Property Name="IsLocked"'
        HasEmailColumn = $edmx -match '<Property Name="Email"'
    }
}

Write-Host "=== diagnose-resx.ps1 ==="
Write-Host "Repo root: $repoRoot"
Write-Host ""

if (Test-Path $resxPath) {
    $resxInfo = Get-Item $resxPath
    Write-Host "RESX file on disk:"
    Write-Host "  Path     : $resxPath"
    Write-Host "  Size     : $($resxInfo.Length) bytes"
    Write-Host "  Modified : $($resxInfo.LastWriteTime)"
} else {
    Write-Host "[X] RESX file not found at $resxPath"
}
Write-Host ""

$dllCandidates = @(
    'src\AhuErp.Core\bin\Debug\AhuErp.Core.dll',
    'src\AhuErp.Core\bin\Debug\net48\AhuErp.Core.dll',
    'src\AhuErp.UI\bin\Debug\AhuErp.Core.dll',
    'src\AhuErp.UI\bin\Debug\net48\AhuErp.Core.dll',
    'tools\MigrationGenerator\bin\Debug\AhuErp.Core.dll',
    'tools\MigrationGenerator\bin\Debug\net48\AhuErp.Core.dll'
)

foreach ($rel in $dllCandidates) {
    $full = Join-Path $repoRoot $rel
    Write-Host "DLL candidate: $rel"
    $r = Decode-EdmxFromResource $full
    if (-not $r.Exists) {
        Write-Host "  [missing]"
        Write-Host ""
        continue
    }
    Write-Host "  Modified : $($r.LastWrite)"
    if (-not $r.ResourceFound) {
        Write-Host "  [X] Embedded resource '$logicalName' NOT FOUND"
        Write-Host "  Other Migrations resources:"
        foreach ($n in $r.AllResources) { Write-Host "    $n" }
        Write-Host ""
        continue
    }
    if (-not $r.HasTarget) {
        Write-Host "  [X] Target entry missing inside resource"
        Write-Host ""
        continue
    }
    Write-Host "  EntityType refs in EDMX     : $($r.EntityTypeRefs)"
    Write-Host "  contains Substitution        : $($r.HasSubstitution)"
    Write-Host "  contains Notification        : $($r.HasNotification)"
    Write-Host "  contains DocumentSignature   : $($r.HasDocumentSignature)"
    Write-Host "  contains AttachmentTextIndex : $($r.HasAttachmentTextIndex)"
    Write-Host "  has Documents.IsLocked col   : $($r.HasIsLockedColumn)"
    Write-Host "  has Employees.Email col      : $($r.HasEmailColumn)"
    Write-Host ""
}
