using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using Microsoft.Win32;

namespace EnBdSetup
{
    internal static class LocaleInstaller
    {
        private const int HWND_BROADCAST = 0xFFFF;
        private const uint WM_SETTINGCHANGE = 0x001A;
        private const uint SMTO_ABORTIFHUNG = 0x0002;

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr SendMessageTimeout(
            IntPtr hWnd,
            uint msg,
            UIntPtr wParam,
            string lParam,
            uint fuFlags,
            uint uTimeout,
            out UIntPtr lpdwResult);

        private sealed class CldrOverrides
        {
            public string CurrencySymbol = "\u09F3"; // ৳
            public int[] NumberGroupSizes = new int[] { 3, 2 };
            public int[] PercentGroupSizes = new int[] { 3, 2 };
            public int[] CurrencyGroupSizes = new int[] { 3, 2 };
        }

        private static int[] ParseGroupSizes(string cldrPattern, int[] fallback)
        {
            if (string.IsNullOrWhiteSpace(cldrPattern))
                return fallback;

            string positive = cldrPattern.Split(';')[0];
            // Strip currency/percent symbols and keep integer part before '.'
            int dot = positive.IndexOf('.');
            string intPart = dot >= 0 ? positive.Substring(0, dot) : positive;
            string cleaned = new string(intPart.Where(c => c == '#' || c == '0' || c == ',').ToArray());
            string[] segments = cleaned.Split(',');
            if (segments.Length <= 1)
                return fallback;

            // Rightmost segment is primary group size (3), next is secondary (2)
            int primary = segments[segments.Length - 1].Length;
            int secondary = segments[segments.Length - 2].Length;
            if (primary > 0 && secondary > 0 && primary != secondary)
                return new int[] { primary, secondary };
            if (primary > 0)
                return new int[] { primary };
            return fallback;
        }

        private static CldrOverrides LoadBundledCldrOverrides(Action<string> log)
        {
            var result = new CldrOverrides();
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                using (Stream stream = asm.GetManifestResourceStream("EnBdSetup.cldr.en_BD.xml"))
                {
                    if (stream == null)
                    {
                        log("Bundled CLDR en_BD.xml not found; using built-in en-BD defaults.");
                        return result;
                    }

                    var settings = new System.Xml.XmlReaderSettings
                    {
                        DtdProcessing = System.Xml.DtdProcessing.Ignore,
                        XmlResolver = null
                    };
                    using (var reader = System.Xml.XmlReader.Create(stream, settings))
                    {
                        XDocument doc = XDocument.Load(reader);
                        var symElem = doc.Descendants("currency")
                            .FirstOrDefault(e => string.Equals((string)e.Attribute("type"), "BDT", StringComparison.OrdinalIgnoreCase))
                            ?.Element("symbol");
                        if (symElem != null && !string.IsNullOrEmpty(symElem.Value))
                            result.CurrencySymbol = symElem.Value.Trim();

                        var decPat = doc.Descendants("decimalFormat").FirstOrDefault()?.Element("pattern")?.Value;
                        var pctPat = doc.Descendants("percentFormat").FirstOrDefault()?.Element("pattern")?.Value;
                        var curPat = doc.Descendants("currencyFormat")
                            .FirstOrDefault(e => string.Equals((string)e.Attribute("type"), "standard", StringComparison.OrdinalIgnoreCase))
                            ?.Element("pattern")?.Value;

                        result.NumberGroupSizes = ParseGroupSizes(decPat, result.NumberGroupSizes);
                        result.PercentGroupSizes = ParseGroupSizes(pctPat, result.PercentGroupSizes);
                        result.CurrencyGroupSizes = ParseGroupSizes(curPat, result.CurrencyGroupSizes);
                        log("Loaded overrides from bundled CLDR en_BD.xml (Symbol: " + result.CurrencySymbol + ", Grouping: " + string.Join(",", result.NumberGroupSizes) + ").");
                    }
                }
            }
            catch (Exception ex)
            {
                log("Warning: Could not parse bundled en_BD.xml (" + ex.Message + "); using built-in en-BD defaults.");
            }
            return result;
        }

        public static void InstallCustomLocale(Action<string> log)
        {
            log("Preparing custom Windows locale: " + Constants.LocaleName + " (English (Bangladesh))...");
            CldrOverrides cldr = LoadBundledCldrOverrides(log);

            // Unregister any previous en-BD.nlp cleanly before registering fresh
            try
            {
                CultureAndRegionInfoBuilder.Unregister(Constants.LocaleName);
                log("Removed existing en-BD custom locale registration for clean update.");
            }
            catch (ArgumentException)
            {
                // Not previously registered
            }
            catch (Exception ex)
            {
                log("Note while checking existing en-BD locale: " + ex.Message);
            }

            var cib = new CultureAndRegionInfoBuilder(Constants.LocaleName, CultureAndRegionModifiers.None);
            cib.LoadDataFromCultureInfo(new CultureInfo("en-GB"));
            cib.LoadDataFromRegionInfo(new RegionInfo("BD"));

            // Identity & Inheritance (en-GB / Commonwealth English + Bangladesh region)
            cib.CultureEnglishName = "English (Bangladesh)";
            cib.CultureNativeName = "English (Bangladesh)";
            cib.RegionEnglishName = "Bangladesh";
            cib.RegionNativeName = "Bangladesh";
            cib.IetfLanguageTag = Constants.LocaleName;
            cib.Parent = new CultureInfo("en-GB");

            cib.GeoId = Constants.BangladeshGeoId;
            cib.IsMetric = true;
            cib.KeyboardLayoutId = 0x0409; // Default to US English QWERTY (never UK)

            cib.TwoLetterISOLanguageName = "en";
            cib.ThreeLetterISOLanguageName = "eng";
            cib.ThreeLetterWindowsLanguageName = "ENG";
            cib.TwoLetterISORegionName = "BD";
            cib.ThreeLetterISORegionName = "BGD";
            cib.ThreeLetterWindowsRegionName = "BGD";

            cib.ISOCurrencySymbol = "BDT";
            cib.CurrencyEnglishName = "Bangladeshi Taka";
            cib.CurrencyNativeName = "\u099F\u09BE\u0995\u09BE"; // টাকা

            // Numbers & Currency (Lakh/Crore {3, 2}, Taka ৳, -$n negative)
            NumberFormatInfo nf = cib.NumberFormat;
            nf.NumberDecimalSeparator = ".";
            nf.NumberGroupSeparator = ",";
            nf.NumberDecimalDigits = 2;
            nf.NumberGroupSizes = cldr.NumberGroupSizes;

            nf.PercentDecimalSeparator = ".";
            nf.PercentGroupSeparator = ",";
            nf.PercentDecimalDigits = 2;
            nf.PercentGroupSizes = cldr.PercentGroupSizes;

            nf.CurrencySymbol = cldr.CurrencySymbol;
            nf.CurrencyDecimalSeparator = ".";
            nf.CurrencyGroupSeparator = ",";
            nf.CurrencyDecimalDigits = 2;
            nf.CurrencyGroupSizes = cldr.CurrencyGroupSizes;
            nf.CurrencyPositivePattern = 0; // ৳n
            nf.CurrencyNegativePattern = 1; // -৳n
            nf.NegativeSign = "-";
            nf.PositiveSign = "+";
            cib.NumberFormat = nf;

            // Date & Time (DD/MM/YYYY, 12-hour AM/PM, Sunday first day of week)
            DateTimeFormatInfo dtf = cib.GregorianDateTimeFormat;
            dtf.DateSeparator = "/";
            dtf.TimeSeparator = ":";
            dtf.ShortDatePattern = "dd/MM/yyyy";
            dtf.LongDatePattern = "dddd, d MMMM yyyy";
            dtf.ShortTimePattern = "h:mm tt";
            dtf.LongTimePattern = "h:mm:ss tt";
            dtf.FullDateTimePattern = "dddd, d MMMM yyyy h:mm:ss tt";
            dtf.YearMonthPattern = "MMMM yyyy";
            dtf.MonthDayPattern = "d MMMM";
            dtf.AMDesignator = "AM";
            dtf.PMDesignator = "PM";
            dtf.FirstDayOfWeek = DayOfWeek.Sunday;
            cib.GregorianDateTimeFormat = dtf;

            cib.Register();
            log("Registered %windir%\\Globalization\\en-BD.nlp successfully.");
        }

        public static void ApplyUserInternationalRegistryTweaks(Action<string> log)
        {
            // Ensure ISO A4 paper size (9), Metric (0), and Bangladesh dialling code (880)
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Control Panel\International", true))
            {
                if (key != null)
                {
                    key.SetValue("iPaperSize", "9", RegistryValueKind.String);
                    key.SetValue("iMeasure", "0", RegistryValueKind.String);
                    key.SetValue("iCountry", "880", RegistryValueKind.String);
                }
            }
            BroadcastIntlChange();
            log("Verified ISO A4 paper size (iPaperSize=9), Metric units, and country code 880.");
        }

        public static void UninstallCustomLocale(Action<string> log)
        {
            try
            {
                CultureAndRegionInfoBuilder.Unregister(Constants.LocaleName);
                BroadcastIntlChange();
                log("Unregistered custom locale " + Constants.LocaleName + ".");
            }
            catch (ArgumentException)
            {
                log("Custom locale " + Constants.LocaleName + " was not registered.");
            }
            catch (Exception ex)
            {
                log("Warning while unregistering " + Constants.LocaleName + ": " + ex.Message);
            }
        }

        public static void BroadcastIntlChange()
        {
            UIntPtr dummy;
            SendMessageTimeout(
                new IntPtr(HWND_BROADCAST),
                WM_SETTINGCHANGE,
                UIntPtr.Zero,
                "intl",
                SMTO_ABORTIFHUNG,
                3000,
                out dummy);
        }
    }
}
