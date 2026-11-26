# English (Bangladesh) [`en-BD`] for Windows

🌐 **Language / ভাষা:** **English (Bangladesh)** | [বাংলা (README_BN.md)](README_BN.md)

A lightweight, open-source Windows installer by **[ekushey.org](https://ekushey.org)** that installs the **English (Bangladesh) [`en-BD`]** regional locale and sets **US with Taka sign** as the default keyboard layout on **Windows 10, Windows 11, and Windows Server** (**x86, x64, and ARM64**).

---

## What It Configures

| Setting | Value Applied |
| :--- | :--- |
| **Regional Format** | **English (Bangladesh) [`en-BD`]** |
| **Currency** | **Bangladeshi Taka (`৳` / `BDT`)** - `৳1,00,000.00` (negative: `-৳1,00,000.00`) |
| **Number Grouping** | **Lakh & Crore** (`00,00,00,000.00`) |
| **Date & Time** | **`DD/MM/YYYY`**, **12-hour `AM`/`PM`**, **Sunday** as first day of the week |
| **Units & Paper** | **Metric** (`Metre`, `Kilometre`, `Celsius`) and **ISO A4** |
| **Default Keyboard** | **US with Taka sign** - **`Shift + 4` = `৳`**, **`AltGr + 4` (`Ctrl+Alt+4`) = `$`** |

---

## Supported Operating Systems & Requirements

Single universal binary (`en-BD-Setup.exe`) supporting **x86 (32-bit), x64 (64-bit), and ARM64** architectures.

| OS Family | Supported Versions & Minimum Builds | Notes |
| :--- | :--- | :--- |
| **Windows 10** | **Version 1903 (OS Build 18362)** through **Version 22H2 (OS Build 19045)** | `.NET Framework 4.8` is preinstalled. Older builds (1607-1809) require installing `.NET Framework 4.8` first. |
| **Windows 11** | **All versions** - **21H2, 22H2, 23H2, 24H2, 25H2, 26H1, 26H2** (x64 & ARM64) | Fully supported out of the box. |
| **Windows LTSC** | **Windows 10 Enterprise LTSC 2021** (Build 19044)<br>**Windows 11 Enterprise LTSC 2024** (Build 26100) | Supported out of the box. *(Windows 10 LTSC 2019 / Build 17763 requires installing `.NET Framework 4.8` first.)* |
| **Windows Server** | **Windows Server 2022** and **Windows Server 2025** | Supported out of the box (GUI & Server Core via `/install`). *(Server 2016 & 2019 require `.NET Framework 4.8` first.)* |

* **Permissions:** Administrator access (prompted automatically).
* **Runtime:** Uses **`.NET Framework 4.8`**, which is already built into Windows 10 (1903+) and Windows 11. If you are on an older Windows 10, LTSC 2019, or Windows Server 2016/2019 build, install **[Microsoft .NET Framework 4.8 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet-framework/net48)** first.

---

## Important Notices

> **Note for Windows Home Single Language Users:**
> Windows Single Language editions lock the OS display language to a single built-in Microsoft language pack. This installer **does not replace or remove** your Windows display language or system locale - it safely adds the **`en-BD`** regional format (`৳`, Lakh/Crore, `DD/MM/YYYY`, Metric, A4) and the **US with Taka sign** keyboard layout on top of your existing edition.

> **Note for Users Outside Bangladesh:**
> This package enforces Bangladesh-specific regional standards: Bangladeshi Taka (`৳` / `BDT`), South Asian **Lakh/Crore** (`1,00,000.00`) digit grouping, **`DD/MM/YYYY`** dates, **Sunday** week start, **A4** paper size, and **`Shift + 4` = `৳`** (moving `$` to `AltGr + 4`). If you live outside Bangladesh or need standard `$` on `Shift + 4` and Western `100,000.00` number grouping, stick with your country's built-in Windows locale (such as `en-US`, `en-GB`, `en-CA`, or `en-AU`).

---

## How to Install

1. Download **`en-BD-Setup.exe`** from **[Releases](../../releases)**.
2. Run **`en-BD-Setup.exe`** and approve administrator access.
3. Click **Install**.
4. **Restart Windows** (or sign out and back in) when prompted so all apps use the new format and default keyboard layout.

---

## How to Uninstall

1. Open **Settings -> Apps -> Installed apps** (or **Control Panel -> Programs and Features**).
2. Select **English (Bangladesh) Setup** and click **Uninstall**.
3. Restart or sign out of Windows to restore your previous settings.

*(Silent IT deployment switches: `en-BD-Setup.exe /install` and `en-BD-Setup.exe /uninstall`.)*

---

## Security & License

* **Code Signing:** See [CODE_SIGNING_POLICY.md](CODE_SIGNING_POLICY.md). Verify any release with PowerShell:
  ```powershell
  Get-FileHash .\en-BD-Setup.exe -Algorithm SHA256
  ```
* **License:** [MIT License](LICENSE) - Maintained by **[ekushey.org](https://ekushey.org)**.
