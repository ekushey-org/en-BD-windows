using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32;

namespace EnBdSetup
{
    internal static class LanguageManager
    {
        private const uint MOVEFILE_DELAY_UNTIL_REBOOT = 0x00000004;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool MoveFileEx(
            string lpExistingFileName,
            string lpNewFileName,
            uint dwFlags);

        private static RegistryKey OpenNativeLocalMachine()
        {
            return RegistryKey.OpenBaseKey(
                RegistryHive.LocalMachine,
                Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Registry32);
        }

        private static string GetNativeProgramFilesPath()
        {
            if (Environment.Is64BitOperatingSystem)
            {
                string pf64 = Environment.GetEnvironmentVariable("ProgramW6432");
                if (!string.IsNullOrWhiteSpace(pf64))
                    return pf64;
            }
            return Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        }

        private static readonly object _logFileLock = new object();

        public static string GetSetupLogPath()
        {
            string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            return Path.Combine(programData, "ekushey.org", "EnBdSetup", "setup.log");
        }

        public static void AppendSetupLogLine(string line)
        {
            try
            {
                string logPath = GetSetupLogPath();
                Directory.CreateDirectory(Path.GetDirectoryName(logPath));
                lock (_logFileLock)
                {
                    File.AppendAllText(logPath, line + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch { }
        }

        private static string GetBackupFilePath()
        {
            string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            return Path.Combine(programData, "ekushey.org", "EnBdSetup", "backup.json");
        }

        private static string GetNativePowerShellPath()
        {
            string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string sys = (Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess) ? "Sysnative" : "System32";
            return Path.Combine(winDir, sys, "WindowsPowerShell", "v1.0", "powershell.exe");
        }

        private static int RunPowerShellScript(string scriptContent, Action<string> log, string requiredSentinel = null)
        {
            string tempScript = Path.Combine(Path.GetTempPath(), "EnBdSetup_" + Guid.NewGuid().ToString("N") + ".ps1");
            Timer heartbeat = null;
            int sentinelSeen = 0;
            try
            {
                File.WriteAllText(tempScript, scriptContent, new UTF8Encoding(true));

                var psi = new ProcessStartInfo
                {
                    FileName = GetNativePowerShellPath(),
                    Arguments = "-NoProfile -ExecutionPolicy Bypass -File \"" + tempScript + "\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                    CreateNoWindow = true
                };

                var totalSw = Stopwatch.StartNew();
                long lastActivityMs = 0;

                using (Process proc = new Process { StartInfo = psi })
                {
                    proc.OutputDataReceived += (s, e) =>
                    {
                        if (!string.IsNullOrWhiteSpace(e.Data))
                        {
                            Interlocked.Exchange(ref lastActivityMs, totalSw.ElapsedMilliseconds);
                            if (!string.IsNullOrEmpty(requiredSentinel) &&
                                string.Equals(e.Data.Trim(), requiredSentinel, StringComparison.Ordinal))
                            {
                                Interlocked.Exchange(ref sentinelSeen, 1);
                                return;
                            }
                            log(e.Data);
                        }
                    };
                    proc.ErrorDataReceived += (s, e) =>
                    {
                        if (!string.IsNullOrWhiteSpace(e.Data))
                        {
                            Interlocked.Exchange(ref lastActivityMs, totalSw.ElapsedMilliseconds);
                            log("[PS] " + e.Data);
                        }
                    };
                    proc.Start();
                    proc.BeginOutputReadLine();
                    proc.BeginErrorReadLine();

                    heartbeat = new Timer(_ =>
                    {
                        long elapsed = totalSw.ElapsedMilliseconds;
                        long idle = elapsed - Interlocked.Read(ref lastActivityMs);
                        if (idle >= 14000)
                        {
                            TimeSpan ts = TimeSpan.FromMilliseconds(elapsed);
                            log(string.Format("Still working... ({0}m {1:00}s elapsed)", (int)ts.TotalMinutes, ts.Seconds));
                        }
                    }, null, 15000, 15000);

                    proc.WaitForExit();
                    if (proc.ExitCode == 0 && !string.IsNullOrEmpty(requiredSentinel) && Volatile.Read(ref sentinelSeen) == 0)
                    {
                        return 99;
                    }
                    return proc.ExitCode;
                }
            }
            finally
            {
                if (heartbeat != null)
                {
                    try { heartbeat.Dispose(); } catch { }
                }
                try { if (File.Exists(tempScript)) File.Delete(tempScript); } catch { }
            }
        }

        public static void EnsureEnGbLanguagePack(Action<string> log)
        {
            string ps = @"
$ErrorActionPreference = 'Continue'
$ProgressPreference    = 'SilentlyContinue'

function Get-EnGbPackStatus {
    try {
        if (Get-Command Get-InstalledLanguage -ErrorAction SilentlyContinue) {
            # Materialize full array without truncating the pipeline, avoiding
            # PipelineStoppedException in LanguagePackManagement on Windows 11.
            $langs = @(Get-InstalledLanguage)
            foreach ($item in $langs) {
                if ($item -and ($item.LanguageId -ieq 'en-GB')) {
                    return [string]$item.LanguagePacks
                }
            }
            return 'Absent'
        }
    } catch { }
    if (Test-Path 'HKLM:\SYSTEM\CurrentControlSet\Control\MUI\UILanguages\en-GB') {
        return 'RegistryMUI'
    }
    return 'Absent'
}

$cv = Get-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion' -ErrorAction SilentlyContinue
$isSingleLang = ($cv -and (($cv.EditionID -match 'SingleLanguage') -or ($cv.ProductName -match 'Single Language')))

if ($isSingleLang) {
    Write-Output 'Windows Single Language edition detected; skipping display language pack download.'
    Write-Output '[ENBD_PREREQ_OK]'
    exit 0
}

$packStatus = Get-EnGbPackStatus
Write-Output ('Checking English (United Kingdom) [en-GB] language pack status: ' + $packStatus)
if ($packStatus -match 'LpCab|LXP|RegistryMUI') {
    Write-Output ('English (United Kingdom) [en-GB] display language pack is already installed (' + $packStatus + ').')
    Write-Output '[ENBD_PREREQ_OK]'
    exit 0
}

if (Get-Command Install-Language -ErrorAction SilentlyContinue) {
    for ($attempt = 1; $attempt -le 2; $attempt++) {
        if ($attempt -eq 1) {
            Write-Output 'Downloading English (United Kingdom) [en-GB] display language pack for en-BD...'
        } else {
            Write-Output 'Retrying English (United Kingdom) [en-GB] language pack download in 10 seconds...'
            Start-Sleep -Seconds 10
        }
        try {
            Install-Language -Language 'en-GB' -ExcludeFeatures -ErrorAction Stop | Out-Null
        } catch {
            Write-Output ('Install-Language attempt ' + $attempt + ' error: ' + $_.Exception.Message)
        }
        $packStatus = Get-EnGbPackStatus
        Write-Output ('Post-attempt ' + $attempt + ' en-GB language pack status: ' + $packStatus)
        if ($packStatus -match 'LpCab|LXP|RegistryMUI') {
            Write-Output ('Installed English (United Kingdom) [en-GB] display language pack (' + $packStatus + ').')
            if (Get-Command Add-WindowsCapability -ErrorAction SilentlyContinue) {
                Write-Output 'Adding lightweight en-GB basic typing / spellcheck capability...'
                try {
                    Add-WindowsCapability -Online -Name 'Language.Basic~~~en-GB~0.0.1.0' -ErrorAction Stop | Out-Null
                } catch {
                    Write-Output ('Note: Basic typing capability skipped (' + $_.Exception.Message + ').')
                }
            }
            Write-Output '[ENBD_PREREQ_OK]'
            exit 0
        }
    }
    Write-Output 'ERROR: Could not download English (United Kingdom) [en-GB] display language pack.'
    exit 2
}

if (Get-Command Add-WindowsCapability -ErrorAction SilentlyContinue) {
    Write-Output 'Downloading en-GB basic typing capability for en-BD...'
    $basicOk = $false
    try {
        Add-WindowsCapability -Online -Name 'Language.Basic~~~en-GB~0.0.1.0' -ErrorAction Stop | Out-Null
        $basicOk = $true
    } catch {
        Write-Output ('Capability Language.Basic~~~en-GB~0.0.1.0 download error: ' + $_.Exception.Message)
    }
    if (-not $basicOk) {
        Write-Output 'ERROR: Could not download required en-GB language capabilities.'
        exit 2
    }
    Write-Output '[ENBD_PREREQ_OK]'
    exit 0
}

Write-Output 'Note: Neither Install-Language nor Add-WindowsCapability is available on this OS build.'
Write-Output '[ENBD_PREREQ_OK]'
exit 0
";
            int exitCode = RunPowerShellScript(ps, log, "[ENBD_PREREQ_OK]");
            if (exitCode == 2)
            {
                throw new InvalidOperationException(
                    "Could not download the English (United Kingdom) [en-GB] display language pack required for English (Bangladesh) [en-BD]. " +
                    "Please verify your internet connection and Windows Update service, then run the installer again. " +
                    "No system settings were changed.");
            }
            if (exitCode == 99)
            {
                throw new InvalidOperationException("Language pack prerequisite script stopped unexpectedly before completing.");
            }
            if (exitCode != 0)
            {
                throw new InvalidOperationException("Language pack prerequisite check exited with code " + exitCode + ".");
            }
        }

        public static void ConfigureLanguagesAndDisplay(string klid, Action<string> log)
        {
            string backupPath = GetBackupFilePath();
            Directory.CreateDirectory(Path.GetDirectoryName(backupPath));

            string ps = @"
$ErrorActionPreference = 'Continue'
$ProgressPreference    = 'SilentlyContinue'
$backupPath = '" + backupPath.Replace("'", "''") + @"'
$klidLower  = '" + klid.ToLowerInvariant() + @"'
$klidUpper  = '" + klid.ToUpperInvariant() + @"'

$preloadPath    = 'HKCU:\Keyboard Layout\Preload'
$substPath      = 'HKCU:\Keyboard Layout\Substitutes'
$defPreloadPath = 'Registry::HKEY_USERS\.DEFAULT\Keyboard Layout\Preload'
$defSubstPath   = 'Registry::HKEY_USERS\.DEFAULT\Keyboard Layout\Substitutes'
$userProfRoot   = 'HKCU:\Control Panel\International\User Profile'
$userProfBak    = 'HKCU:\Control Panel\International\User Profile System Backup'
$userProfEnBd   = Join-Path $userProfRoot 'en-BD'
$userProfBakBd  = Join-Path $userProfBak  'en-BD'

if (-not (Test-Path $preloadPath)) { New-Item -Path $preloadPath -Force | Out-Null }
if (-not (Test-Path $substPath))   { New-Item -Path $substPath -Force | Out-Null }

# 1. Back up current international, display language & keyboard settings once before first install
$existingList = @()
try { $existingList = @(Get-WinUserLanguageList) } catch { }

if (-not (Test-Path $backupPath)) {
    try {
        $origCulture = (Get-Culture).Name
        $origGeo     = (Get-WinHomeLocation).GeoId
        $origSysUi   = ''
        try {
            if (Get-Command Get-SystemPreferredUILanguage -ErrorAction SilentlyContinue) {
                $origSysUi = [string](Get-SystemPreferredUILanguage)
            }
        } catch { }
        $origUiOver  = ''
        try {
            $uObj = Get-WinUILanguageOverride
            if ($uObj) { $origUiOver = [string]$uObj.Name }
        } catch { }
        $origPrefUi  = ''
        try {
            $dObj = Get-ItemProperty -Path 'HKCU:\Control Panel\Desktop' -Name 'PreferredUILanguages' -ErrorAction SilentlyContinue
            if ($dObj -and $dObj.PreferredUILanguages) {
                $origPrefUi = [string](@($dObj.PreferredUILanguages)[0])
            }
        } catch { }

        $preloadObj  = Get-ItemProperty -Path $preloadPath -ErrorAction SilentlyContinue
        $origPreload = [ordered]@{}
        if ($preloadObj) {
            foreach ($prop in $preloadObj.PSObject.Properties) {
                if ($prop.Name -match '^[0-9]+$') {
                    $origPreload[$prop.Name] = [string]$prop.Value
                }
            }
        }
        $substObj  = Get-ItemProperty -Path $substPath -ErrorAction SilentlyContinue
        $origSubst = [ordered]@{}
        if ($substObj) {
            foreach ($prop in $substObj.PSObject.Properties) {
                if ($prop.Name -notlike 'PS*') {
                    $origSubst[$prop.Name] = [string]$prop.Value
                }
            }
        }
        $origList = @()
        foreach ($lang in $existingList) {
            $origList += [PSCustomObject]@{
                LanguageTag = $lang.LanguageTag
                Tips        = @($lang.InputMethodTips)
            }
        }
        $backup = [PSCustomObject]@{
            Culture       = $origCulture
            GeoId         = $origGeo
            SystemUiLang  = $origSysUi
            UiOverride    = $origUiOver
            PreferredUi   = $origPrefUi
            Languages     = $origList
            Preload       = $origPreload
            Subst         = $origSubst
        }
        $backup | ConvertTo-Json -Depth 6 | Set-Content -Path $backupPath -Encoding UTF8
        Write-Output 'Backed up previous language, region, and keyboard settings.'
    } catch {
        Write-Output ('Warning: Could not save backup: ' + $_.Exception.Message)
    }
}

# 2. Verify English (United Kingdom) [en-GB] display language pack presence (already ensured before install)
function Get-EnGbPackStatus {
    try {
        if (Get-Command Get-InstalledLanguage -ErrorAction SilentlyContinue) {
            $langs = @(Get-InstalledLanguage)
            foreach ($item in $langs) {
                if ($item -and ($item.LanguageId -ieq 'en-GB')) {
                    return [string]$item.LanguagePacks
                }
            }
            return 'Absent'
        }
    } catch { }
    if (Test-Path 'HKLM:\SYSTEM\CurrentControlSet\Control\MUI\UILanguages\en-GB') {
        return 'RegistryMUI'
    }
    return 'Absent'
}

$cv = Get-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion' -ErrorAction SilentlyContinue
$isSingleLang = ($cv -and (($cv.EditionID -match 'SingleLanguage') -or ($cv.ProductName -match 'Single Language')))
$hasEnGb = $false

if ($isSingleLang) {
    Write-Output 'Windows Single Language edition detected; keeping built-in display language untouched.'
} else {
    $packStatus = Get-EnGbPackStatus
    $hasEnGb = ($packStatus -match 'LpCab|LXP|RegistryMUI')
    if (-not $hasEnGb -and (Get-Command Install-Language -ErrorAction SilentlyContinue)) {
        Write-Output 'ERROR: en-GB display language pack is missing.'
        exit 2
    }
}

function Set-EnGbDisplayLanguage {
    if ($isSingleLang -or (-not $hasEnGb)) { return }

    # 1. System-wide preferred UI language (HKLM\...\MUI\Settings)
    if (Get-Command Set-SystemPreferredUILanguage -ErrorAction SilentlyContinue) {
        try {
            Set-SystemPreferredUILanguage -Language 'en-GB' -ErrorAction Stop
        } catch {
            Write-Output ('Note setting system UI language to en-GB: ' + $_.Exception.Message)
        }
    }

    # 2. User display language override (activates en-GB / shown as English (en-BD) immediately on next sign-in)
    try {
        Set-WinUILanguageOverride -Language 'en-GB'
    } catch {
        Write-Output ('Note setting user UI language override to en-GB: ' + $_.Exception.Message)
    }

    # 3. Also ensure HKCU PreferredUILanguagesPending = @('en-GB') matches native Windows 11 Settings behavior
    try {
        New-ItemProperty -Path 'HKCU:\Control Panel\Desktop' -Name 'PreferredUILanguagesPending' -Value ([string[]]@('en-GB')) -PropertyType MultiString -Force | Out-Null
    } catch {
        Write-Output ('Note writing PreferredUILanguagesPending: ' + $_.Exception.Message)
    }

    # Read back final display language state into the log for verification
    $sysNow = ''
    try { if (Get-Command Get-SystemPreferredUILanguage -ErrorAction SilentlyContinue) { $sysNow = [string](Get-SystemPreferredUILanguage) } } catch { }
    $ovNow = ''
    try { $ovObj = Get-WinUILanguageOverride; if ($ovObj) { $ovNow = [string]$ovObj.Name } } catch { }
    Write-Output ('Activated en-GB display language for English (en-BD) [SystemUI=' + $sysNow + ', Override=' + $ovNow + '].')
}

# 3. Activate en-BD Regional Format & Bangladesh Home Location (GeoId 23)
Write-Output 'Activating English (Bangladesh) [en-BD] regional format...'
Set-Culture -CultureInfo 'en-BD'
Set-WinHomeLocation -GeoId 23

# Clear any stale default input method override and stale substitutes
try { Set-WinDefaultInputMethodOverride } catch { }
$staleSubst = Get-ItemProperty -Path $substPath -ErrorAction SilentlyContinue
if ($staleSubst) {
    foreach ($p in $staleSubst.PSObject.Properties) {
        if ($p.Name -notlike 'PS*' -and ([string]$p.Value) -match '^a0[0-9a-fA-F]{2}0409$') {
            Remove-ItemProperty -Path $substPath -Name $p.Name -ErrorAction SilentlyContinue
        }
    }
}
Remove-ItemProperty -Path $substPath -Name '00000409' -ErrorAction SilentlyContinue
Remove-ItemProperty -Path $substPath -Name 'd0000409' -ErrorAction SilentlyContinue
Remove-ItemProperty -Path $substPath -Name '00000809' -ErrorAction SilentlyContinue
Remove-ItemProperty -Path $substPath -Name 'd0000809' -ErrorAction SilentlyContinue
Remove-ItemProperty -Path $substPath -Name '00004009' -ErrorAction SilentlyContinue

# Helper: build Pass-1 user language list with untouched en-BD first + existing non-English languages
function Invoke-Pass1EnBd {
    $enBdInit  = New-WinUserLanguageList -Language 'en-BD'
    $pass1List = New-Object 'System.Collections.Generic.List[Microsoft.InternationalSettings.Commands.WinUserLanguage]'
    $pass1List.Add($enBdInit[0])
    foreach ($lang in $existingList) {
        if ($lang.LanguageTag -notlike 'en*') {
            $pass1List.Add($lang)
        }
    }
    Set-WinUserLanguageList -LanguageList $pass1List -Force
}

# Helper: discover Windows' real TransientLangId for en-BD (e.g. 2000, 2400, ...)
function Get-EnBdTransientLangId {
    if (Test-Path $userProfEnBd) {
        $prof = Get-ItemProperty -Path $userProfEnBd -ErrorAction SilentlyContinue
        if ($prof -and ($null -ne $prof.TransientLangId)) {
            $tid = [int]$prof.TransientLangId
            if ($tid -gt 0) {
                return ('{0:X4}' -f $tid)
            }
        }
    }
    try {
        $live = $null
        foreach ($entry in @(Get-WinUserLanguageList)) {
            if ($entry -and ($entry.LanguageTag -ieq 'en-BD')) { $live = $entry; break }
        }
        if ($live -and $live.InputMethodTips -and $live.InputMethodTips.Count -gt 0) {
            $first = [string]$live.InputMethodTips[0]
            if ($first -match '^([0-9A-Fa-f]{4}):') {
                return $Matches[1].ToUpperInvariant()
            }
        }
    } catch { }
    return $null
}

# 4. Pass 1: Register en-BD untouched so Windows allocates its TransientLangId cleanly
try {
    Invoke-Pass1EnBd
} catch {
    Write-Output ('Note: User language list not updated (e.g. Server Core): ' + $_.Exception.Message)
    Set-EnGbDisplayLanguage
    Write-Output '[ENBD_CONFIG_OK]'
    exit 0
}

$transientHex = Get-EnBdTransientLangId
$liveList     = @(Get-WinUserLanguageList)
$enBdLive     = $null; foreach ($entry in $liveList) { if ($entry -and ($entry.LanguageTag -ieq 'en-BD')) { $enBdLive = $entry; break } }
$pass1Tips    = if ($enBdLive) { @($enBdLive.InputMethodTips) } else { @() }
Write-Output ('Pass 1 en-BD TransientLangId: ' + $(if ($transientHex) { $transientHex } else { 'none' }) + ', tips: ' + ($pass1Tips -join ', '))

if (-not $transientHex) {
    Write-Output 'Could not determine TransientLangId for en-BD; keeping Pass 1 regional configuration.'
    Set-EnGbDisplayLanguage
    Write-Output '[ENBD_CONFIG_OK]'
    exit 0
}

# 5. Pass 2: Attach US with Taka sign (<TransientLangId>:<KLID>) to the live en-BD language entry
$targetTip = $transientHex + ':' + $klidUpper
$accepted  = $false

if ($enBdLive) {
    try {
        Write-Output ('Pass 2 attaching keyboard tip ' + $targetTip + ' (US with Taka sign)...')
        $enBdLive.InputMethodTips.Clear()
        $enBdLive.InputMethodTips.Add($targetTip)

        $pass2List = New-Object 'System.Collections.Generic.List[Microsoft.InternationalSettings.Commands.WinUserLanguage]'
        foreach ($l in $liveList) { $pass2List.Add($l) }
        Set-WinUserLanguageList -LanguageList $pass2List -Force

        $verifyEntry = $null; foreach ($entry in @(Get-WinUserLanguageList)) { if ($entry -and ($entry.LanguageTag -ieq 'en-BD')) { $verifyEntry = $entry; break } }
        $verifyTips  = if ($verifyEntry) { @($verifyEntry.InputMethodTips) } else { @() }
        Write-Output ('Pass 2 read-back tips for en-BD: ' + ($verifyTips -join ', '))

        foreach ($t in $verifyTips) {
            if ([string]$t -ieq $targetTip) {
                $accepted = $true
                break
            }
        }
    } catch {
        Write-Output ('Pass 2 API note: ' + $_.Exception.Message)
    }
}

$tidLower  = $transientHex.ToLowerInvariant()
$substSlot = 'd000' + $tidLower

if ($accepted) {
    Write-Output ('Configured en-BD with US with Taka sign [' + $targetTip + '].')
} else {
    Write-Output ('Synchronizing en-BD TransientLangId (' + $transientHex + ') keyboard tip directly...')
    try { Invoke-Pass1EnBd } catch { }

    $recheckedTid = Get-EnBdTransientLangId
    if ($recheckedTid) {
        $transientHex = $recheckedTid
        $tidLower     = $transientHex.ToLowerInvariant()
        $targetTip    = $transientHex + ':' + $klidUpper
        $substSlot    = 'd000' + $tidLower
    }

    foreach ($profKey in @($userProfEnBd, $userProfBakBd)) {
        if (Test-Path $profKey) {
            $props = Get-ItemProperty -Path $profKey -ErrorAction SilentlyContinue
            if ($props) {
                foreach ($p in $props.PSObject.Properties) {
                    if ($p.Name -imatch ('^' + [regex]::Escape($transientHex) + ':[0-9A-Fa-f]{8}$')) {
                        Remove-ItemProperty -Path $profKey -Name $p.Name -ErrorAction SilentlyContinue
                    }
                }
            }
            New-ItemProperty -Path $profKey -Name $targetTip -Value 1 -PropertyType DWord -Force | Out-Null
        }
    }
}

# 6. Purge phantom US (00000409) / UK (00000809) keyboards from HKCU Preload & stale User Profile subkeys
#    so ONLY en-BD (US with Taka sign) + any non-English languages appear in Win+Spacebar.
$preloadObj = Get-ItemProperty -Path $preloadPath -ErrorAction SilentlyContinue
$keptPreload = New-Object 'System.Collections.Generic.List[string]'
$foundEnBdSlot = $null

if ($preloadObj) {
    $orderedProps = $preloadObj.PSObject.Properties |
        Where-Object { $_.Name -match '^[0-9]+$' } |
        Sort-Object { [int]$_.Name }

    foreach ($p in $orderedProps) {
        $val = ([string]$p.Value).Trim().ToLowerInvariant()
        if ([string]::IsNullOrWhiteSpace($val)) { continue }

        # Drop phantom US/UK base layouts and any stale a0xx0409 direct entries
        if ($val -in @('00000409', 'd0000409', '00000809', 'd0000809', '00004009')) { continue }
        if ($val -match '^a0[0-9a-f]{2}0409$') { continue }

        if ($val -match ('^d0[0-9a-f]{2}' + [regex]::Escape($tidLower) + '$')) {
            if (-not $foundEnBdSlot) { $foundEnBdSlot = $val }
            continue
        }
        if ($val -eq ('0000' + $tidLower)) {
            continue
        }
        if (-not $keptPreload.Contains($val)) {
            $keptPreload.Add($val)
        }
    }
}

if ($foundEnBdSlot) { $substSlot = $foundEnBdSlot }

# Rewrite HKCU\Keyboard Layout\Preload cleanly with en-BD transient slot at position 1
if ($preloadObj) {
    foreach ($p in $preloadObj.PSObject.Properties) {
        if ($p.Name -match '^[0-9]+$') {
            Remove-ItemProperty -Path $preloadPath -Name $p.Name -ErrorAction SilentlyContinue
        }
    }
}
Set-ItemProperty -Path $preloadPath -Name '1' -Value $substSlot
$idx = 2
foreach ($otherVal in $keptPreload) {
    Set-ItemProperty -Path $preloadPath -Name ([string]$idx) -Value $otherVal
    $idx++
}
Set-ItemProperty -Path $substPath -Name $substSlot -Value $klidLower

# Remove any leftover en-US / en-GB subkeys in User Profile so CTFMON does not resurrect ghost US/UK layouts
foreach ($rootKey in @($userProfRoot, $userProfBak)) {
    foreach ($staleLang in @('en-US', 'en-GB')) {
        $stalePath = Join-Path $rootKey $staleLang
        if (Test-Path $stalePath) {
            Remove-Item -Path $stalePath -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}

# Set default input method override to en-BD's US with Taka sign tip
try {
    Set-WinDefaultInputMethodOverride -InputTip $targetTip
} catch { }

Write-Output ('Cleaned Preload (' + $substSlot + ' -> ' + $klidLower + ') and set default keyboard to ' + $targetTip + '.')

# 7. Sync settings to Welcome screen & New user accounts so HKEY_USERS\.DEFAULT never re-injects ghost US keyboard
$copiedToSystem = $false
if (Get-Command Copy-UserInternationalSettingsToSystem -ErrorAction SilentlyContinue) {
    try {
        Copy-UserInternationalSettingsToSystem -WelcomeScreen $true -NewUser $true -ErrorAction Stop
        $copiedToSystem = $true
        Write-Output 'Copied en-BD and US with Taka sign settings to Welcome screen and new user accounts.'
    } catch {
        Write-Output ('Note copying settings to Welcome screen: ' + $_.Exception.Message)
    }
}

if (-not $copiedToSystem) {
    # Fallback for Windows 10 / LTSC 2021 / Windows Server 2019-2022 without Copy-UserInternationalSettingsToSystem
    try {
        if (-not (Test-Path $defPreloadPath)) { New-Item -Path $defPreloadPath -Force | Out-Null }
        if (-not (Test-Path $defSubstPath))   { New-Item -Path $defSubstPath -Force | Out-Null }

        $defObj = Get-ItemProperty -Path $defPreloadPath -ErrorAction SilentlyContinue
        if ($defObj) {
            foreach ($p in $defObj.PSObject.Properties) {
                if ($p.Name -match '^[0-9]+$') {
                    Remove-ItemProperty -Path $defPreloadPath -Name $p.Name -ErrorAction SilentlyContinue
                }
            }
        }
        Set-ItemProperty -Path $defPreloadPath -Name '1' -Value $substSlot
        Set-ItemProperty -Path $defSubstPath -Name $substSlot -Value $klidLower
        Write-Output 'Synchronized Welcome screen (.DEFAULT) keyboard layout to US with Taka sign.'
    } catch {
        Write-Output ('Note updating .DEFAULT keyboard preload: ' + $_.Exception.Message)
    }
}

# 8. Activate en-GB display language LAST (after Set-WinUserLanguageList & Copy-UserInternationalSettingsToSystem)
#    so neither the language list update nor Copy-UserInternationalSettingsToSystem can revert it to en-US.
Set-EnGbDisplayLanguage
Write-Output '[ENBD_CONFIG_OK]'
";

            int exitCode = RunPowerShellScript(ps, log, "[ENBD_CONFIG_OK]");
            if (exitCode == 99)
                throw new InvalidOperationException("Language configuration script stopped unexpectedly before completing.");
            if (exitCode != 0)
                throw new InvalidOperationException("Language configuration exited with code " + exitCode + ".");
        }

        public static void RestorePreviousLanguages(Action<string> log)
        {
            string backupPath = GetBackupFilePath();
            string ps = @"
$ErrorActionPreference = 'Continue'
$ProgressPreference    = 'SilentlyContinue'
$backupPath     = '" + backupPath.Replace("'", "''") + @"'
$preloadPath    = 'HKCU:\Keyboard Layout\Preload'
$substPath      = 'HKCU:\Keyboard Layout\Substitutes'
$defPreloadPath = 'Registry::HKEY_USERS\.DEFAULT\Keyboard Layout\Preload'
$defSubstPath   = 'Registry::HKEY_USERS\.DEFAULT\Keyboard Layout\Substitutes'
$userProfEnBd   = 'HKCU:\Control Panel\International\User Profile\en-BD'
$userProfBackup = 'HKCU:\Control Panel\International\User Profile System Backup\en-BD'

function Remove-EnBdSubstitutes([string]$path) {
    if (Test-Path $path) {
        $obj = Get-ItemProperty -Path $path -ErrorAction SilentlyContinue
        if ($obj) {
            foreach ($p in $obj.PSObject.Properties) {
                if ($p.Name -notlike 'PS*' -and (([string]$p.Value) -match '^a0[0-9a-fA-F]{2}0409$' -or $p.Name -ieq '00000409' -or $p.Name -ieq 'd0000409')) {
                    Remove-ItemProperty -Path $path -Name $p.Name -ErrorAction SilentlyContinue
                }
            }
        }
    }
}

if (Test-Path $backupPath) {
    try {
        $b = Get-Content -Path $backupPath -Raw -Encoding UTF8 | ConvertFrom-Json
        if ($b.Culture -and ($b.Culture -ine 'en-BD')) {
            Set-Culture -CultureInfo $b.Culture
        } else {
            Set-Culture -CultureInfo 'en-US'
        }
        if ($b.GeoId) {
            Set-WinHomeLocation -GeoId ([int]$b.GeoId)
        }

        try { Set-WinDefaultInputMethodOverride } catch { }

        $restored = New-Object 'System.Collections.Generic.List[Microsoft.InternationalSettings.Commands.WinUserLanguage]'
        foreach ($item in $b.Languages) {
            if ($item.LanguageTag -ieq 'en-BD') { continue }
            $tmp = New-WinUserLanguageList -Language $item.LanguageTag
            if ($tmp -and $tmp.Count -gt 0) {
                $lang = $tmp[0]
                if ($item.Tips -and $item.Tips.Count -gt 0) {
                    $lang.InputMethodTips.Clear()
                    foreach ($t in $item.Tips) {
                        if ([string]$t -notmatch ':A0[0-9A-Fa-f]{2}0409$') {
                            $lang.InputMethodTips.Add([string]$t)
                        }
                    }
                    if ($lang.InputMethodTips.Count -eq 0 -and $item.LanguageTag -like 'en*') {
                        $lang.InputMethodTips.Add('0409:00000409')
                    }
                }
                $restored.Add($lang)
            }
        }
        if ($restored.Count -eq 0) {
            $restored = New-WinUserLanguageList -Language 'en-US'
        }
        try { Set-WinUserLanguageList -LanguageList $restored -Force } catch { }

        Remove-EnBdSubstitutes $substPath
        Remove-EnBdSubstitutes $defSubstPath
        if (Test-Path $substPath) {
            if ($b.Subst) {
                foreach ($p in $b.Subst.PSObject.Properties) {
                    if ($p.Name -notlike 'PS*' -and ([string]$p.Value) -notmatch '^a0[0-9a-fA-F]{2}0409$') {
                        Set-ItemProperty -Path $substPath -Name $p.Name -Value ([string]$p.Value)
                    }
                }
            }
        }

        if (Test-Path $preloadPath) {
            if ($b.Preload) {
                $validPreload = @()
                foreach ($p in $b.Preload.PSObject.Properties) {
                    if ($p.Name -match '^[0-9]+$' -and ([string]$p.Value) -notmatch '^a0[0-9a-fA-F]{2}0409$') {
                        $validPreload += $p
                    }
                }
                if ($validPreload.Count -gt 0) {
                    Get-ItemProperty -Path $preloadPath -ErrorAction SilentlyContinue |
                        ForEach-Object {
                            foreach ($p in $_.PSObject.Properties) {
                                if ($p.Name -match '^[0-9]+$') {
                                    Remove-ItemProperty -Path $preloadPath -Name $p.Name -ErrorAction SilentlyContinue
                                }
                            }
                        }
                    foreach ($p in $validPreload) {
                        Set-ItemProperty -Path $preloadPath -Name $p.Name -Value ([string]$p.Value)
                    }
                }
            }
        }

        Remove-Item -Path $userProfEnBd   -Recurse -Force -ErrorAction SilentlyContinue
        Remove-Item -Path $userProfBackup -Recurse -Force -ErrorAction SilentlyContinue

        if (Get-Command Copy-UserInternationalSettingsToSystem -ErrorAction SilentlyContinue) {
            try { Copy-UserInternationalSettingsToSystem -WelcomeScreen $true -NewUser $true } catch { }
        } else {
            try {
                if (Test-Path $defPreloadPath) {
                    Set-ItemProperty -Path $defPreloadPath -Name '1' -Value '00000409'
                }
            } catch { }
        }

        if ($b.SystemUiLang -and (Get-Command Set-SystemPreferredUILanguage -ErrorAction SilentlyContinue)) {
            try { Set-SystemPreferredUILanguage -Language ([string]$b.SystemUiLang) } catch { }
        }
        if ($b.UiOverride) {
            try { Set-WinUILanguageOverride -Language ([string]$b.UiOverride) } catch { }
        } else {
            try { Set-WinUILanguageOverride } catch { }
        }
        $restoreUi = ''
        if ($b.PreferredUi) { $restoreUi = [string]$b.PreferredUi }
        elseif ($b.SystemUiLang) { $restoreUi = [string]$b.SystemUiLang }
        if ($restoreUi) {
            try {
                New-ItemProperty -Path 'HKCU:\Control Panel\Desktop' -Name 'PreferredUILanguagesPending' -Value ([string[]]@($restoreUi)) -PropertyType MultiString -Force | Out-Null
            } catch { }
        }

        Remove-Item -Path $backupPath -Force -ErrorAction SilentlyContinue
        Write-Output 'Restored previous Windows language, region, and keyboard settings.'
    } catch {
        Write-Output ('Warning restoring backup: ' + $_.Exception.Message)
    }
} else {
    Write-Output 'No backup found; resetting regional format cleanly to en-US.'
    try {
        Set-Culture -CultureInfo 'en-US'
        try { Set-WinUILanguageOverride } catch { }
        try { Set-WinDefaultInputMethodOverride } catch { }
        Remove-EnBdSubstitutes $substPath
        Remove-EnBdSubstitutes $defSubstPath
        $fallback = New-WinUserLanguageList -Language 'en-US'
        Set-WinUserLanguageList -LanguageList $fallback -Force
        Remove-Item -Path $userProfEnBd   -Recurse -Force -ErrorAction SilentlyContinue
        Remove-Item -Path $userProfBackup -Recurse -Force -ErrorAction SilentlyContinue
        if (Get-Command Copy-UserInternationalSettingsToSystem -ErrorAction SilentlyContinue) {
            try { Copy-UserInternationalSettingsToSystem -WelcomeScreen $true -NewUser $true } catch { }
        } elseif (Test-Path $defPreloadPath) {
            Set-ItemProperty -Path $defPreloadPath -Name '1' -Value '00000409'
        }
        if (Get-Command Set-SystemPreferredUILanguage -ErrorAction SilentlyContinue) {
            try { Set-SystemPreferredUILanguage -Language 'en-US' } catch { }
        }
        try {
            New-ItemProperty -Path 'HKCU:\Control Panel\Desktop' -Name 'PreferredUILanguagesPending' -Value ([string[]]@('en-US')) -PropertyType MultiString -Force | Out-Null
        } catch { }
    } catch { }
}
";
            RunPowerShellScript(ps, log);
        }

        public static void RegisterUninstaller(Action<string> log)
        {
            string progFiles = GetNativeProgramFilesPath();
            string setupDir = Path.Combine(progFiles, "ekushey.org", "EnBdSetup");
            Directory.CreateDirectory(setupDir);

            string installedExe = Path.Combine(setupDir, "en-BD-Setup.exe");
            string currentExe = Process.GetCurrentProcess().MainModule.FileName;
            if (!string.Equals(Path.GetFullPath(currentExe), Path.GetFullPath(installedExe), StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(currentExe, installedExe, true);
            }

            string regPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\" + Constants.UninstallKeyName;
            using (RegistryKey hklm = OpenNativeLocalMachine())
            using (RegistryKey key = hklm.CreateSubKey(regPath))
            {
                if (key != null)
                {
                    key.SetValue("DisplayName", Constants.ProductDisplayName);
                    key.SetValue("Publisher", Constants.Publisher);
                    key.SetValue("DisplayVersion", Constants.ProductVersion);
                    key.SetValue("DisplayIcon", installedExe);
                    key.SetValue("InstallLocation", setupDir);
                    key.SetValue("UninstallString", "\"" + installedExe + "\" /uninstall-ui");
                    key.SetValue("QuietUninstallString", "\"" + installedExe + "\" /uninstall");
                    key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                    key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                }
            }
            log("Registered uninstaller in Windows Installed Apps.");
        }

        public static void UnregisterUninstaller(Action<string> log)
        {
            string regPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\" + Constants.UninstallKeyName;
            try
            {
                using (RegistryKey hklm = OpenNativeLocalMachine())
                {
                    hklm.DeleteSubKeyTree(regPath, false);
                }
                log("Removed entry from Installed Apps.");
                string progFiles = GetNativeProgramFilesPath();
                string setupDir = Path.Combine(progFiles, "ekushey.org", "EnBdSetup");
                string installedExe = Path.Combine(setupDir, "en-BD-Setup.exe");
                if (File.Exists(installedExe)) { MoveFileEx(installedExe, null, MOVEFILE_DELAY_UNTIL_REBOOT); }
                if (Directory.Exists(setupDir)) { MoveFileEx(setupDir, null, MOVEFILE_DELAY_UNTIL_REBOOT); }
            }
            catch (Exception ex)
            {
                log("Note removing uninstall key: " + ex.Message);
            }
        }
    }
}
