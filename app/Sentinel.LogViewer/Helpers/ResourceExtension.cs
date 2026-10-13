using System;
using System.ComponentModel;
using System.Globalization;
using System.Resources;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Markup;
using Sentinel.LogViewer.Services;

namespace Sentinel.LogViewer.Helpers
{
    /// <summary>
    /// Markup extension for accessing localized resources in XAML
    /// </summary>
    public class ResourceExtension : MarkupExtension
    {
        private static ResourceManager? _designTimeResourceManager;

        /// <summary>
        /// The resource key to look up
        /// </summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// Default value if resource is not found
        /// </summary>
        public string DefaultValue { get; set; } = string.Empty;

        public ResourceExtension()
        {
        }

        public ResourceExtension(string key)
        {
            Key = key;
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            if (string.IsNullOrEmpty(Key))
                return DefaultValue;

            // Check if we're in design mode
            bool isDesignTime = false;
            if (serviceProvider?.GetService(typeof(IProvideValueTarget)) is IProvideValueTarget target)
            {
                if (target.TargetObject is DependencyObject dependencyObject)
                {
                    isDesignTime = DesignerProperties.GetIsInDesignMode(dependencyObject);
                }
            }

            // At design time, load resources directly from RESX files
            if (isDesignTime)
            {
                try
                {
                    // Initialize ResourceManager if not already done
                    if (_designTimeResourceManager == null)
                    {
                        _designTimeResourceManager = new ResourceManager(
                            "Sentinel.LogViewer.Resources.Resources",
                            typeof(ResourceExtension).Assembly);
                    }

                    // Use current UI culture or default to "en"
                    var culture = CultureInfo.CurrentUICulture;
                    if (string.IsNullOrEmpty(culture.Name))
                    {
                        culture = new CultureInfo("en");
                    }

                    // Try to load the resource
                    var resourceValue = _designTimeResourceManager.GetString(Key, culture);
                    if (!string.IsNullOrEmpty(resourceValue))
                    {
                        return resourceValue;
                    }
                }
                catch
                {
                    // If resource loading fails, fall through to DefaultValue
                }
            }
            else
            {
                // At runtime, get LocalizationService from DI container
                var localizationService = App.ServiceProvider?.GetService<LocalizationService>();
                
                if (localizationService != null)
                {
                    return localizationService.GetString(Key, DefaultValue);
                }
            }
            
            // Fallback if DI is not available or resource not found
            return DefaultValue;
        }
    }
}

