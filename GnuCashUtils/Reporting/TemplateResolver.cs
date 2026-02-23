using System;
using System.IO;
using GnuCashUtils.Core;

namespace GnuCashUtils.Reporting;

public static class TemplateResolver
{
    /// <summary>
    /// Finds the path to the template for a given template name.
    /// Checks ~/.config/GnuCashUtils/templates/{name} first, then falls back to the app output directory.
    /// </summary>
    public static string Resolve(string templateName)
    {
        var userTemplatePath = Path.Combine(ConfigService.ResolveConfigDir(), "templates", templateName);
        if (File.Exists(userTemplatePath))
            return userTemplatePath;

        var appTemplatePath = Path.Combine(AppContext.BaseDirectory, templateName);
        if (File.Exists(appTemplatePath))
            return appTemplatePath;

        throw new FileNotFoundException($"Template '{templateName}' not found in user templates or app directory.", templateName);
    }
}
