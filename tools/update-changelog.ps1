<#
.SYNOPSIS
Tops CHANGELOG.md up with any releases it does not already list.

.DESCRIPTION
Help > About lists every version from CHANGELOG.md, which is compiled into the
assembly - so a build made before a release is published has no notes for it.
Run this after publishing, then build.

Only the missing releases are fetched. The file already holds the rest, and
re-downloading forty-odd release bodies to add one is work done for nothing.
Pass -Full to rebuild from scratch, which is only needed if the file is lost
or an old release's notes were edited on GitHub.

Each release body is written with its own "##" sections demoted to "###", so
that "## " marks a version and nothing else. Without that a release section
called "## Playlists" would be read back as a version named Playlists.
ChangelogTests holds both ends of that contract.

.EXAMPLE
pwsh tools/update-changelog.ps1
#>

[CmdletBinding()]
param(
    [switch]$Full,
    [string]$Repo = 'gouryellas/MPC-HC-Video-Editor',
    [string]$Path
)

$ErrorActionPreference = 'Stop'

# Windows PowerShell decodes a native command's output using the console's
# encoding, which is not UTF-8 by default - so an em dash or an arrow in a
# release body arrives as mojibake. gh emits UTF-8, so say so.
$priorOutputEncoding = [Console]::OutputEncoding
[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding $false

# Resolved here rather than as a parameter default: $PSScriptRoot is not
# reliably bound while the param block is being evaluated.
if (-not $Path) {
    $root = Split-Path -Parent $MyInvocation.MyCommand.Path
    $Path = Join-Path $root '..\CHANGELOG.md'
}

# The CLI is not on PATH for every shell on this machine.
$gh = (Get-Command gh -ErrorAction SilentlyContinue).Source
if (-not $gh) { $gh = 'C:\Program Files\GitHub CLI\gh.exe' }
if (-not (Test-Path $gh)) { throw "GitHub CLI not found. Looked for 'gh' on PATH and at $gh." }

$Path = [System.IO.Path]::GetFullPath($Path)
$exists = Test-Path $Path

function Convert-Body {
    <#
    Brings a release body into the house style: headings one level down, one
    bullet marker, and every paragraph a bullet on a single line.

    Releases up to 5.7 are written as prose hard-wrapped at about eighty
    columns. Those paragraphs become bullets, and wrapped lines are rejoined
    so each bullet is one line, which is how every release since is written.
    No words are changed - a paragraph gains "- " and loses its line breaks.

    Must match ChangelogCache.Normalise, which does the same to notes arriving
    from the update check. ChangelogCacheTests holds the two together.
    #>
    param([string]$Body)

    $lines = ($Body -replace "`r`n", "`n").Split("`n")

    $out = New-Object System.Collections.Generic.List[string]
    $run = New-Object System.Collections.Generic.List[string]
    $fenced = $false

    function Flush {
        if ($run.Count -eq 0) { return }
        $text = ($run -join ' ').Trim()
        $run.Clear()
        if (-not $text) { return }
        # Already a bullet, or make it one.
        if ($text -match '^[-*]\s') { $out.Add('- ' + ($text -replace '^[-*]\s+', '')) }
        else { $out.Add('- ' + $text) }
    }

    foreach ($raw in $lines) {
        $line = $raw.TrimEnd()
        $trim = $line.TrimStart()

        # Fenced blocks are the one place line breaks carry meaning.
        if ($trim.StartsWith('```')) {
            Flush
            $out.Add($line)
            $fenced = -not $fenced
            continue
        }
        if ($fenced) { $out.Add($line); continue }

        # The body's own title; the version heading replaces it.
        if ($line -match '^#\s*(MPC-HC|MPC-BE)\s+Video\s+Editor') { continue }

        if ($trim -match '^#{1,6}\s') {
            Flush
            if ($line.StartsWith('## ')) { $out.Add('#' + $line) }
            elseif ($line.StartsWith('# ')) { $out.Add('##' + $line) }
            else { $out.Add($line) }
            continue
        }

        if (-not $trim) { Flush; $out.Add(''); continue }

        # A new bullet ends the one before it; anything else continues the
        # paragraph or bullet already open, which is how the wrapped ones in
        # the older notes were written.
        if ($trim -match '^[-*]\s') { Flush }
        $run.Add($trim)
    }

    Flush

    # Collapse the runs of blank lines the rejoining leaves behind.
    $text = ($out -join "`n") -replace "`n{3,}", "`n`n"
    $text.Trim("`n")
}

function Format-Entry {
    param($Release)

    $version = $Release.tag_name -replace '^[vV]', ''
    $date = ([string]$Release.published_at).Substring(0, 10)
    $notes = Convert-Body ([string]$Release.body)
    if (-not $notes) { $notes = '_No notes were published for this release._' }

    "## $version $([char]0x2014) $date`n`n$notes`n"
}

# Read as UTF-8 explicitly. Get-Content -Raw and Select-String both decode a
# BOM-less file as ANSI in Windows PowerShell, which turns every em dash and
# arrow in the part being kept into mojibake on the way back out.
$existingText = if ($exists) {
    [System.IO.File]::ReadAllText($Path, (New-Object System.Text.UTF8Encoding $false)) -replace "`r`n", "`n"
} else { '' }

# What the file already has. The newest is the first "## " heading in it.
$known = @()
if ($exists -and -not $Full) {
    $known = [regex]::Matches($existingText, '(?m)^##\s+(\S+)') |
             ForEach-Object { $_.Groups[1].Value }
}

if ($Full -or -not $exists) {
    Write-Host 'Rebuilding from every published release...'
    $releases = & $gh api --paginate "repos/$Repo/releases" | ConvertFrom-Json
} else {
    # One page is plenty: this runs per release, so at most a handful can be
    # missing. If the newest page is somehow all new, say so rather than
    # silently leaving a gap.
    Write-Host "Checking for releases newer than $($known[0])..."
    $releases = & $gh api "repos/$Repo/releases?per_page=30" | ConvertFrom-Json
}

$releases = @($releases | Where-Object { -not $_.draft })
$missing = @($releases | Where-Object { ($_.tag_name -replace '^[vV]', '') -notin $known })

if ($missing.Count -eq 0) {
    Write-Host 'Already up to date. Nothing fetched.'
    return
}

if (-not $Full -and $exists -and $missing.Count -eq $releases.Count) {
    throw "Every release on the first page is missing from $([System.IO.Path]::GetFileName($Path)). " +
          "The file may be truncated or in the wrong format - rerun with -Full."
}

$missing = @($missing | Sort-Object { [datetime]$_.published_at } -Descending)

if ($Full -or -not $exists) {
    # The arrow by code point, so this script stays ASCII and cannot be
    # broken by whatever encoding an editor saves it as.
    $arrow = [char]0x25B8

    $header = @(
        '# Changelog'
        ''
        'Every released version, newest first. Generated from the published'
        "GitHub releases; Help $arrow About shows this same file."
        ''
    ) -join "`n"

    $body = ($missing | ForEach-Object { Format-Entry $_ }) -join "`n"
    $text = "$header`n$body"
} else {
    # Prepend, so the newest stays at the top without rewriting what is below.
    $old = $existingText
    $split = [regex]::Match($old, '(?m)^##\s+\S+')
    if (-not $split.Success) { throw "No version headings found in $Path. Rerun with -Full." }

    $head = $old.Substring(0, $split.Index).TrimEnd("`n")
    $rest = $old.Substring($split.Index)
    $body = ($missing | ForEach-Object { Format-Entry $_ }) -join "`n"
    $text = "$head`n`n$body`n$rest"
}

# UTF-8 without a BOM and with Unix endings, matching what the generator
# produced and what the resource reader expects.
[System.IO.File]::WriteAllText($Path, ($text.TrimEnd("`n") + "`n"),
                               (New-Object System.Text.UTF8Encoding $false))

[Console]::OutputEncoding = $priorOutputEncoding

Write-Host "Added $($missing.Count) release(s): $(($missing | ForEach-Object { $_.tag_name }) -join ', ')"
Write-Host "Wrote $Path"
