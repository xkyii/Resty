using System.Collections.Generic;

namespace Kx.Resty.Converters
{
    public class Locale
    {
        public string Name { get; }
        public string Key { get; }

        public Locale(string name, string key) { Name = name; Key = key; }

        public static readonly List<Locale> Supported =
        [
            new Locale("English", "en_US"),
            new Locale("简体中文", "zh_CN"),
        ];
    }
}
