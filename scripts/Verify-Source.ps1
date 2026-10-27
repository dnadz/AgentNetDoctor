param([string]$RepositoryRoot = '.', [string[]]$CandidateFiles)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath($RepositoryRoot).TrimEnd('\','/')
if (-not $CandidateFiles) {
    $CandidateFiles = @(& git -C $root ls-files --cached --others --exclude-standard)
    if ($LASTEXITCODE -ne 0) { throw 'Cannot enumerate source files' }
}
$failures = 0
foreach ($relative in $CandidateFiles) {
    $path = [IO.Path]::GetFullPath((Join-Path $root $relative))
    $rule = $null
    if (-not $path.StartsWith($root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { $rule = 'OUTSIDE_ROOT' }
    elseif ($relative -match '(^|[/\\])\.env(?:\.[^/\\]*)?$|(^|[/\\])(private|snapshots|backups|artifacts|bin|obj|\.local|\.packages)([/\\]|$)|\.(pfx|key|log)$') { $rule = 'PRIVATE_PATH' }
    elseif (Test-Path -LiteralPath $path -PathType Leaf) {
        $cursor = Get-Item -LiteralPath $path -Force
        while ($cursor -and $cursor.FullName -ne $root) {
            if ($cursor.Attributes -band [IO.FileAttributes]::ReparsePoint) { $rule = 'REPARSE_POINT'; break }
            if ($cursor -is [IO.FileInfo]) { $cursor = $cursor.Directory } else { $cursor = $cursor.Parent }
        }
        if (-not $rule) {
            $content = [IO.File]::ReadAllText($path)
            $matches = [regex]::Matches($content, '(?i)(?:api[_-]?key|access[_-]?token|password)["'']?\s*[:=]\s*["'']([^"''\r\n]{8,})["'']')
            foreach ($match in $matches) {
                if ($relative -match '^tests[/\\]' -and $match.Groups[1].Value -match '^synthetic-') { continue }
                $rule = 'CREDENTIAL_LITERAL'; break
            }
        }
    } else { $rule = 'MISSING_FILE' }
    if ($rule) { Write-Output "$rule : $relative"; $failures++ }
}
if ($failures) { exit 1 }
Write-Output 'PASS: source boundary checks'
exit 0
