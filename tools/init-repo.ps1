# Initialise the git repository for the Card Game project.
#
# Why this exists:
# The repo had a half-initialised .git/ left over from a prior run, with
# zero-byte index.lock / config.lock files whose ACLs blocked deletion from
# the WSL/Linux sandbox. Run this once from PowerShell to wipe the broken
# .git/ and create a clean initial commit.
#
# Usage (from inside the Card Game folder):
#   powershell -ExecutionPolicy Bypass -File tools\init-repo.ps1

$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path "$PSScriptRoot\..").Path
Set-Location $repoRoot

Write-Host "=== Card Game repo init ==="
Write-Host "Working in: $repoRoot"

if (Test-Path '.git') {
    Write-Host "Removing stale .git/ ..."
    # -Force handles read-only files; cmd /c rd handles long paths cleanly.
    cmd /c "rd /s /q .git" | Out-Null
}

Write-Host "Running git init ..."
git init -b main | Out-Null

git config user.email 'boris.kuslitskiy@gmail.com'
git config user.name  'Boris'

Write-Host "Staging files (.gitignore handles Library/, Logs/, .vs/, node_modules/, etc) ..."
git add -A

Write-Host "Creating initial commit ..."
git commit -m "Initial commit: Unity scaffold + JS rewrite attempt + Playwright test spec" | Out-Null

Write-Host ""
Write-Host "Done. Try:" -ForegroundColor Green
Write-Host "  git log --oneline"
Write-Host "  git status"
