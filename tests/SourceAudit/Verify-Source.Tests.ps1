$ErrorActionPreference='Stop'
$repoRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$scanner=Join-Path $repoRoot 'scripts/Verify-Source.ps1'
if(-not (Test-Path -LiteralPath $scanner)){throw 'RED: source scanner missing'}
$tempRoot=Join-Path $repoRoot ('.local/source-audit-test-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $tempRoot -Force | Out-Null
# Fixtures contain synthetic values only; no user data.
[IO.File]::WriteAllText((Join-Path $tempRoot 'safe.cs'),'namespace Safe;')
[IO.File]::WriteAllText((Join-Path $tempRoot '.env'),'SYNTHETIC=1')
& $scanner -RepositoryRoot $tempRoot -CandidateFiles @('safe.cs')
if($LASTEXITCODE -ne 0){throw 'safe source rejected'}
& $scanner -RepositoryRoot $tempRoot -CandidateFiles @('.env')
if($LASTEXITCODE -eq 0){throw 'tracked env file not rejected'}
[IO.File]::WriteAllText((Join-Path $tempRoot 'bad.cs'),'api_key = "synthetic-sensitive-value"')
& $scanner -RepositoryRoot $tempRoot -CandidateFiles @('bad.cs')
if($LASTEXITCODE -eq 0){throw 'secret-shaped source not rejected'}
Write-Output 'PASS: 3 source-boundary checks'
foreach($name in @('.env.local', '.env.production')) {
    [IO.File]::WriteAllText((Join-Path $tempRoot $name), 'SYNTHETIC=1')
    & $scanner -RepositoryRoot $tempRoot -CandidateFiles @($name)
    if($LASTEXITCODE -eq 0){throw 'suffixed env file not rejected'}
}
[IO.File]::WriteAllText((Join-Path $tempRoot 'bad.json'), '{"api_key":"synthetic-sensitive-value"}')
& $scanner -RepositoryRoot $tempRoot -CandidateFiles @('bad.json')
if($LASTEXITCODE -eq 0){throw 'JSON secret-shaped source not rejected'}
Write-Output 'PASS: 3 additional source-boundary checks'
