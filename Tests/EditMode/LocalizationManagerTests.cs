using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Tests for <see cref="LocalizationManager"/> verifying language switching and
/// value retrieval from the JSON tables located under Resources/Localization.
/// </summary>
public class LocalizationManagerTests
{
    [Test]
    public void Get_ReturnsTranslatedValue()
    {
        LocalizationManager.SetLanguage("en");
        Assert.AreEqual("Language", LocalizationManager.Get("settings_language"));

        LocalizationManager.SetLanguage("es");
        Assert.AreEqual("Idioma", LocalizationManager.Get("settings_language"));
    }
}
