using System;
using System.Text.RegularExpressions;
using System.Windows;
using MaterialDesignThemes.Wpf;

namespace LiteDbExplorer.Wpf
{
    public class LocalPaletteHelper : PaletteHelper
    {
        public virtual void InitTheme(bool isDark)
        {
            SetLightDark(isDark);
        }

        public virtual void SetLightDark(bool isDark)
        {
            var theme = GetTheme();
            theme.SetBaseTheme(isDark ? BaseTheme.Dark : BaseTheme.Light);
            SetTheme(theme);

            TryFindAndReplaceMergedDictionary(
                @"\/MahApps.Metro;component\/Styles\/Themes\/(Light|Dark)\.Blue\.xaml",
                $"pack://application:,,,/MahApps.Metro;component/Styles/Themes/{(isDark ? "Dark" : "Light")}.Blue.xaml");

            TryFindAndReplaceMergedDictionary(
                @"(\/LiteDbExplorer.Wpf;component\/Themes\/ApplicationColors\.)((Light)|(Dark))",
                $"pack://application:,,,/LiteDbExplorer.Wpf;component/Themes/ApplicationColors.{(isDark ? "Dark" : "Light")}.xaml");

            TryFindAndReplaceMergedDictionary(
                @"(\/MaterialDesignExtensions;component\/Themes\/MaterialDesign((Light)|(Dark))Theme)",
                $"pack://application:,,,/MaterialDesignExtensions;component/Themes/{(isDark ? "MaterialDesignDarkTheme" : "MaterialDesignLightTheme")}.xaml");
        }

        private bool TryFindAndReplaceMergedDictionary(string pattern, string newResourceDictionarySource)
        {
            return TryReplaceMergedDictionary(Application.Current.Resources, pattern, newResourceDictionarySource);
        }

        private static bool TryReplaceMergedDictionary(ResourceDictionary owner, string pattern, string source)
        {
            for (var i = 0; i < owner.MergedDictionaries.Count; i++)
            {
                var dictionary = owner.MergedDictionaries[i];
                if (dictionary.Source != null && Regex.IsMatch(dictionary.Source.OriginalString, pattern, RegexOptions.IgnoreCase))
                {
                    owner.MergedDictionaries[i] = new ResourceDictionary {Source = new Uri(source)};
                    return true;
                }

                if (TryReplaceMergedDictionary(dictionary, pattern, source))
                {
                    return true;
                }
            }

            return false;
        }
    }
}