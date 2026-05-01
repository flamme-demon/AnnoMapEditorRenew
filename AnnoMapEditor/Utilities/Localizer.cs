using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace AnnoMapEditor.Utilities
{
    /// <summary>
    /// Tiny string-table-based i18n. Loads en.json + fr.json from embedded resources, exposes
    /// a hot-swappable current language, and supports {0}-style positional placeholders.
    ///
    /// Used both from code (Localizer.Current["main.save_mod"]) and from XAML via the
    /// {l:L Key=...} markup extension (see L.cs).
    /// </summary>
    public class Localizer : INotifyPropertyChanged
    {
        // Static fields are initialized top-to-bottom — keep SupportedLanguages BEFORE Current,
        // because Current's initializer (new Localizer()) reads SupportedLanguages in its ctor.
        private static readonly string[] SupportedLanguages = { "en", "fr" };
        private const string FallbackLanguage = "en";

        public static Localizer Current { get; } = new();

        private readonly Dictionary<string, Dictionary<string, string>> _tables = new();
        private string _language = "en";

        public event PropertyChangedEventHandler? PropertyChanged;

        private Localizer()
        {
            foreach (string lang in SupportedLanguages)
                _tables[lang] = LoadTable(lang);

            // Default to system language if supported, else English.
            string sys = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();
            _language = Array.IndexOf(SupportedLanguages, sys) >= 0 ? sys : FallbackLanguage;
        }

        public string Language
        {
            get => _language;
            set
            {
                if (_language == value || Array.IndexOf(SupportedLanguages, value) < 0) return;
                _language = value;
                _version++;
                // {l:L} bindings listen to Version (a plain int property) — when it changes,
                // the converter re-runs and re-resolves the localized string. This is more
                // reliable than relying on indexer change notifications, which Avalonia does
                // not auto-track.
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Version)));
                LanguageChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private int _version;
        public int Version => _version;

        /// <summary>Plain event for code-behind subscribers (windows that need to re-render).</summary>
        public event EventHandler? LanguageChanged;

        public IReadOnlyList<string> AvailableLanguages => SupportedLanguages;

        /// <summary>Indexer used by XAML bindings: {Binding [main.save_mod], Source={x:Static l:Localizer.Current}}.</summary>
        public string this[string key] => Get(key);

        public string Get(string key)
        {
            if (_tables.TryGetValue(_language, out var table) && table.TryGetValue(key, out var v))
                return v;
            if (_language != FallbackLanguage
                && _tables.TryGetValue(FallbackLanguage, out var fb)
                && fb.TryGetValue(key, out var fv))
                return fv;
            return key;
        }

        public string Format(string key, params object?[] args)
        {
            try { return string.Format(Get(key), args); }
            catch (FormatException) { return Get(key); }
        }

        private static Dictionary<string, string> LoadTable(string lang)
        {
            var asm = Assembly.GetExecutingAssembly();
            // Resource names use '.' separators: AnnoMapEditor.Resources.i18n.en.json
            string resName = $"AnnoMapEditor.Resources.i18n.{lang}.json";
            using Stream? stream = asm.GetManifestResourceStream(resName);
            if (stream is null)
                return new Dictionary<string, string>();

            using var sr = new StreamReader(stream);
            string raw = sr.ReadToEnd();
            var dict = new Dictionary<string, string>();
            try
            {
                JObject obj = JObject.Parse(raw);
                foreach (var prop in obj.Properties())
                    dict[prop.Name] = prop.Value.ToString();
            }
            catch { /* keep dict empty on parse failure — fallback path will catch */ }
            return dict;
        }
    }
}
