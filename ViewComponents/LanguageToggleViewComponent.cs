using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using ShiftManager.Resources;
using System.Globalization;

namespace ShiftManager.ViewComponents;

public class LanguageToggleViewComponent : ViewComponent
{
    private readonly IStringLocalizer<SharedResources> _localizer;

    public LanguageToggleViewComponent(IStringLocalizer<SharedResources> localizer)
    {
        _localizer = localizer;
    }

    public IViewComponentResult Invoke()
    {
        var currentCulture = CultureInfo.CurrentUICulture.Name;
        var isHebrew = currentCulture.StartsWith("he");

        var model = new LanguageToggleViewModel
        {
            CurrentLanguage = isHebrew ? "he-IL" : "en-US",
            IsHebrew = isHebrew
        };

        return View(model);
    }
}

public class LanguageToggleViewModel
{
    public string CurrentLanguage { get; set; } = "en-US";
    public bool IsHebrew { get; set; }
}