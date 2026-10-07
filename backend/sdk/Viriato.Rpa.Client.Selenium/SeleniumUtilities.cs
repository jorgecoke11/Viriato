using System.Diagnostics;
using OpenQA.Selenium;
using OpenQA.Selenium.Edge;
using OpenQA.Selenium.Interactions;
using OpenQA.Selenium.Support.UI;

namespace Viriato.Rpa.Client.Selenium;

public enum TipoWait
{
    Displayed,
    Enabled,
}

/// <summary>The Selenium helpers the original robot's pages were written against (waits, retrying clicks,
/// JS scrolling), with the same timings, so the page logic ports over unchanged.</summary>
public sealed class SeleniumUtilities(NavegadorOptions opciones) : IDisposable
{
    public const int EsperaDinamicaSegundos = 120;
    public const int EsperaEstaticaSegundos = 1;
    public const int Reintentos = 3;

    private IWebDriver? _driver;

    public IWebDriver Driver => _driver ?? throw new InvalidOperationException("El navegador aún no está abierto.");

    public bool Abierto => _driver is not null;

    public void StartDriver()
    {
        var descargas = opciones.CarpetaDescargasEfectiva();
        Directory.CreateDirectory(descargas);

        var edge = new EdgeOptions();
        edge.AddUserProfilePreference("download.default_directory", descargas);
        edge.AddUserProfilePreference("download.prompt_for_download", false);

        try
        {
            _driver = new EdgeDriver(edge);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"No se ha podido iniciar Edge: {ex.Message}", ex);
        }
    }

    public void StopDriver()
    {
        if (_driver is null) return;

        try { _driver.Quit(); } catch (Exception) { /* already gone */ }
        _driver = null;

        if (opciones.MatarProcesosEdge) ProcesosNavegador.MatarEdge();
    }

    public void Dispose() => StopDriver();

    public void GoToUrl(string url) => Driver.Navigate().GoToUrl(url);

    public WebDriverWait NewWait(double segundos)
    {
        var espera = new WebDriverWait(Driver, TimeSpan.FromSeconds(segundos));
        espera.IgnoreExceptionTypes(typeof(ElementNotInteractableException));
        espera.IgnoreExceptionTypes(typeof(ElementClickInterceptedException));
        espera.IgnoreExceptionTypes(typeof(InvalidSelectorException));
        return espera;
    }

    public void Wait(By locator, TipoWait tipo = TipoWait.Displayed)
    {
        Func<IWebDriver, bool> condicion = tipo switch
        {
            TipoWait.Displayed => d => d.FindElement(locator).Displayed,
            TipoWait.Enabled => d => d.FindElement(locator).Enabled,
            _ => throw new ArgumentException("Tipo de espera no válido", nameof(tipo)),
        };
        NewWait(EsperaDinamicaSegundos).Until(condicion);
    }

    public void Wait(IWebElement elemento, TipoWait tipo = TipoWait.Displayed)
    {
        Func<IWebDriver, bool> condicion = tipo switch
        {
            TipoWait.Displayed => _ => elemento.Displayed,
            TipoWait.Enabled => _ => elemento.Enabled,
            _ => throw new ArgumentException("Tipo de espera no válido", nameof(tipo)),
        };
        NewWait(EsperaDinamicaSegundos).Until(condicion);
    }

    public bool ElementExists(By locator, int segundos)
    {
        try
        {
            NewWait(segundos).Until(d => d.FindElement(locator).Displayed);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool ElementExists(IWebElement elemento, int segundos)
    {
        try
        {
            NewWait(segundos).Until(_ => elemento.Displayed);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public IEnumerable<IWebElement> GetElements(By locator)
    {
        Wait(locator);
        return Driver.FindElements(locator);
    }

    public string GetTextElement(By locator)
    {
        NewWait(EsperaDinamicaSegundos).Until(d => d.FindElement(locator).Displayed);
        return Driver.FindElement(locator).Text.Trim();
    }

    public void Type(By locator, string texto)
    {
        Wait(locator);
        Driver.FindElement(locator).SendKeys(texto);
    }

    public void Click(By locator, TipoWait tipo = TipoWait.Displayed) =>
        ConReintentos(locator.ToString(), () =>
        {
            Wait(locator, tipo);
            Thread.Sleep(EsperaEstaticaSegundos * 1000);
            Driver.FindElement(locator).Click();
        });

    public void Click(IWebElement elemento, TipoWait tipo = TipoWait.Displayed)
    {
        Wait(elemento, tipo);
        Thread.Sleep(EsperaEstaticaSegundos * 1000);
        elemento.Click();
    }

    public void ClickJs(By locator, TipoWait tipo = TipoWait.Displayed) =>
        ConReintentos(locator.ToString(), () =>
        {
            Wait(locator, tipo);
            var elemento = Driver.FindElement(locator);
            Thread.Sleep(EsperaEstaticaSegundos * 1000);
            Js("arguments[0].click();", elemento);
        });

    public void ClickJs(IWebElement elemento) =>
        ConReintentos("elemento", () =>
        {
            Thread.Sleep(EsperaEstaticaSegundos * 1000);
            Js("arguments[0].click();", elemento);
        });

    public void ClickIfExists(By locator, int segundos)
    {
        if (ElementExists(locator, segundos)) Click(locator);
    }

    public void ClickIfExists(IWebElement elemento, int segundos)
    {
        if (ElementExists(elemento, segundos)) Click(elemento);
    }

    /// <summary>Waits for the element to be visible and clicks it, failing outright if it never is.</summary>
    public void ClickCuandoVisible(By locator, int segundos = 30)
    {
        if (!ElementExists(locator, segundos))
        {
            throw new InvalidOperationException($"Elemento no cliclable -> {locator}");
        }

        try
        {
            Driver.FindElement(locator).Click();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Elemento no cliclable -> {locator}", ex);
        }
    }

    public void MoveToElementJs(By locator)
    {
        Wait(locator);
        Js("arguments[0].scrollIntoView()", Driver.FindElement(locator));
    }

    public void MoveToElementJs(IWebElement elemento)
    {
        Wait(elemento);
        Js("arguments[0].scrollIntoView()", elemento);
    }

    public void MoveToElementByIdJs(string id)
    {
        Wait(By.Id(id));
        Js($"document.getElementById('{id}').scrollIntoView();");
    }

    public void ScrollIntoViewJs(By locator) => Js("arguments[0].scrollIntoView(true);", Driver.FindElement(locator));

    public void ScrollIntoViewJs(IWebElement elemento) => Js("arguments[0].scrollIntoView(true);", elemento);

    public void SwitchToIframe(By locator)
    {
        NewWait(EsperaDinamicaSegundos).Until(d => d.FindElement(locator).Displayed);
        Driver.SwitchTo().Frame(Driver.FindElement(locator));
    }

    public void PageUp() => new Actions(Driver).SendKeys(Keys.PageUp).Build().Perform();

    public void PageEnd() => new Actions(Driver).SendKeys(Keys.End).Build().Perform();

    public void PageInit() => new Actions(Driver).SendKeys(Keys.Home).Build().Perform();

    public byte[]? CapturarPantalla()
    {
        if (_driver is not ITakesScreenshot camara) return null;
        return camara.GetScreenshot().AsByteArray;
    }

    private void Js(string script, params object[] argumentos) => ((IJavaScriptExecutor)Driver).ExecuteScript(script, argumentos);

    private static void ConReintentos(string descripcion, Action accion)
    {
        for (var intento = 0; intento < Reintentos; intento++)
        {
            try
            {
                accion();
                return;
            }
            catch
            {
                // retry: the page is often still settling when the first attempt lands
            }
        }

        throw new InvalidOperationException($"No se ha podido hacer click en el elemento {descripcion}");
    }
}

public static class ProcesosNavegador
{
    public static void MatarEdge()
    {
        foreach (var nombre in new[] { "msedgewebview2", "msedge" })
        {
            foreach (var proceso in Process.GetProcessesByName(nombre))
            {
                try { proceso.Kill(); } catch (Exception) { /* already exited */ }
            }
        }
    }
}
