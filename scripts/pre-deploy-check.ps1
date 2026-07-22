<#
.SYNOPSIS
    Pre-deployment smoke test for Peer eCommOrderProcessing.

.DESCRIPTION
    Run this after ANY code change, in ANY module (a controller, a service, the background
    job, a model), before pushing or deploying, to quickly confirm the whole application
    still works as expected.

    Steps:
      1. Build            dotnet build the whole solution. Catches compile-time breakage in
                           any project/module immediately, before running anything.

      2. Full test suite   dotnet test, covering:
                             - Unit tests for every controller and service (business rules,
                               validation, the cancellation guard, password hashing).
                             - The background job's sweep logic (OrderStatusUpdateServiceTests),
                               run directly with no real timer/wait involved.
                             - Integration tests (Integration/*.cs) that boot the REAL
                               ASP.NET Core pipeline via WebApplicationFactory<Program> - real
                               routing, DI, JSON, CORS - and drive it purely over HTTP against
                               an isolated in-memory database. Includes a full end-to-end
                               checkout flow (register -> login -> browse -> cart -> checkout
                               -> background sweep -> order history) tying every module
                               together in one test.
                           None of this touches the real eCommDB.db file.

      3. Live boot check   (-LiveSmoke only) starts the actual API process against the real
         (optional)        configured database/connection string and makes a couple of
                            READ-ONLY calls (GET /products) to confirm it actually boots and
                            serves traffic with today's real configuration and ports. Never
                            writes data, so it's safe to run against the real seeded database.

    Exits non-zero the moment any step fails, so this can gate a CI/CD deploy step.

.PARAMETER LiveSmoke
    Also boot the real API process and hit a read-only endpoint against the real configured
    database, in addition to the build + test suite.

.PARAMETER Configuration
    Build/test configuration. Defaults to Debug.

.EXAMPLE
    powershell ./scripts/pre-deploy-check.ps1

.EXAMPLE
    powershell ./scripts/pre-deploy-check.ps1 -LiveSmoke
#>
param(
    [switch]$LiveSmoke,
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

$repoRoot   = Split-Path -Parent $PSScriptRoot
$solution   = Join-Path $repoRoot "src\peer_ecomm_ms\peer_ecomm_ms.slnx"
$apiProject = Join-Path $repoRoot "src\peer_ecomm_ms\peer_ecomm_ms\peer_ecomm_ms.csproj"

$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
$failed = $false

function Write-Step  { param($text) Write-Host ""; Write-Host "==> $text" -ForegroundColor Cyan }
function Write-Ok    { param($text) Write-Host "    [PASS] $text" -ForegroundColor Green }
function Write-Fail  { param($text) Write-Host "    [FAIL] $text" -ForegroundColor Red }

# ---------------------------------------------------------------------------
# 1. Build - catches compile errors in any module before wasting time on tests
# ---------------------------------------------------------------------------
Write-Step "Building solution ($Configuration)"
dotnet build $solution --configuration $Configuration --nologo
if ($LASTEXITCODE -ne 0) {
    Write-Fail "Build failed - fix compile errors before continuing."
    exit 1
}
Write-Ok "Solution builds cleanly."

# ---------------------------------------------------------------------------
# 2. Full test suite - unit + background-job + integration (real HTTP pipeline)
# ---------------------------------------------------------------------------
Write-Step "Running full test suite (unit, background job, integration)"
dotnet test $solution --configuration $Configuration --nologo --logger "console;verbosity=normal"
if ($LASTEXITCODE -ne 0) {
    Write-Fail "One or more tests failed - see output above. Do not deploy."
    $failed = $true
} else {
    Write-Ok "All unit, background-job, and integration tests passed."
}

# ---------------------------------------------------------------------------
# 3. Optional live boot check against the real configuration
# ---------------------------------------------------------------------------
if ($LiveSmoke -and -not $failed) {
    Write-Step "Booting the real API for a read-only live check"

    $proc = Start-Process -FilePath "dotnet" `
        -ArgumentList "run --project `"$apiProject`" --configuration $Configuration --urls http://localhost:5296" `
        -PassThru -WindowStyle Hidden

    $baseUrl = "http://localhost:5296"
    $healthy = $false
    try {
        for ($i = 0; $i -lt 20; $i++) {
            Start-Sleep -Seconds 1
            try {
                $response = Invoke-WebRequest -Uri "$baseUrl/products" -UseBasicParsing -TimeoutSec 3
                if ($response.StatusCode -eq 200) { $healthy = $true; break }
            } catch {
                # API likely still starting up - keep polling until the timeout below.
            }
        }

        if ($healthy) {
            Write-Ok "API booted and responded to GET /products with 200 (real config, real DB - read-only)."
        } else {
            Write-Fail "API did not respond on $baseUrl within 20 seconds - check the connection string / port config in appsettings/Program.cs."
            $failed = $true
        }
    }
    finally {
        if ($proc -and -not $proc.HasExited) {
            Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
        }
    }
}

# ---------------------------------------------------------------------------
# Summary
# ---------------------------------------------------------------------------
$stopwatch.Stop()
$elapsed = [math]::Round($stopwatch.Elapsed.TotalSeconds, 1)
Write-Host ""
if ($failed) {
    Write-Host "RESULT: FAILED  (${elapsed}s) - do not deploy." -ForegroundColor Red
    exit 1
} else {
    Write-Host "RESULT: PASSED  (${elapsed}s) - safe to deploy." -ForegroundColor Green
    exit 0
}
