using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Playwright;
using testplaywright1.Models;
using testplaywright1.Services;

namespace testplaywright1.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class VerbaliController : ControllerBase
{
    private readonly VerbaleStore _store;

    public VerbaliController(VerbaleStore store)
    {
        _store = store;
    }

    [HttpGet("VerificaImporto")]
    public async Task<ActionResult<object>> VerificaImporto([FromQuery] string targa, [FromQuery] string numeroVerbale, [FromQuery] string importo)
    {
        var ricevutoIl = DateTimeOffset.Now;
        var response = _store.Save(targa, numeroVerbale, ricevutoIl);

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });

        var page = await browser.NewPageAsync(new BrowserNewPageOptions
        {
            ViewportSize = new ViewportSize { Width = 1440, Height = 1600 }
        });

        await page.GotoAsync("https://tfl.gov.uk/modes/driving/pay-a-pcn", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle
        });

        await CloseCookieBannerAsync(page);

        var pcnInput = page.GetByLabel("PCN number", new() { Exact = false });
        var plateInput = page.GetByLabel("Plate", new() { Exact = false });

        await pcnInput.FillAsync(numeroVerbale);
        await plateInput.FillAsync(targa);

        var continueButton = page.Locator("#pcn-entry-button");
        await continueButton.ClickAsync(new LocatorClickOptions { Force = true });
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var outstandingAmountLocator = page.GetByText("Outstanding amount", new() { Exact = false });
        string? outstandingAmount = null;
        try
        {
            await outstandingAmountLocator.WaitForAsync(new LocatorWaitForOptions { Timeout = 15000 });
            outstandingAmount = await ExtractOutstandingAmountAsync(page);
        }
        catch
        {
        }

        bool? match = null;
        if (outstandingAmount is not null)
        {
            match = NormalizeMoney(importo) == NormalizeMoney(outstandingAmount);
        }

        bool? payable = null;
        string? importoDaPagare = null;

        var addToBasket = page.GetByRole(AriaRole.Button, new() { Name = "Add to basket" });
        try
        {
            if (await addToBasket.CountAsync() > 0)
            {
                var first = addToBasket.First;
                await first.ClickAsync(new LocatorClickOptions { Force = true });
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

                var postClickText = await page.Locator("body").InnerTextAsync();
                payable = true;
                if (!postClickText.Contains("added to basket", StringComparison.OrdinalIgnoreCase) &&
                    !postClickText.Contains("Go to basket", StringComparison.OrdinalIgnoreCase))
                {
                    payable = null;
                }

                var goToBasket = page.GetByRole(AriaRole.Link, new() { Name = "Go to basket" });
                if (await goToBasket.CountAsync() == 0)
                    goToBasket = page.GetByRole(AriaRole.Button, new() { Name = "Go to basket" });

                if (await goToBasket.CountAsync() > 0)
                {
                    await goToBasket.First.ClickAsync(new LocatorClickOptions { Force = true });
                    await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                    importoDaPagare = await ExtractTotalAsync(page);
                }
            }
            else
            {
                payable = false;
            }
        }
        catch
        {
            payable = false;
        }

        var bodyText = await page.Locator("body").InnerTextAsync();

        return Ok(new
        {
            response.Targa,
            response.NumeroVerbale,
            ImportoInIngresso = importo,
            OutstandingAmount = outstandingAmount,
            Match = match,
            Payable = payable,
            ImportoDaPagare = importoDaPagare,
            response.DataRicevimento,
            response.Riassunto,
            ContenutoPagina = bodyText,
            CurrentUrl = page.Url
        });
    }

    private static async Task<string?> ExtractOutstandingAmountAsync(IPage page)
    {
        var text = await page.Locator("body").InnerTextAsync();
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains("Outstanding amount", StringComparison.OrdinalIgnoreCase) && i + 1 < lines.Length)
            {
                return lines[i + 1];
            }
        }
        return null;
    }

    private static async Task<string?> ExtractTotalAsync(IPage page)
    {
        var text = await page.Locator("body").InnerTextAsync();
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].Equals("Total", StringComparison.OrdinalIgnoreCase) && i + 1 < lines.Length)
            {
                return lines[i + 1];
            }
        }
        return null;
    }

    private static string NormalizeMoney(string value)
    {
        var s = value.Trim();
        s = s.Replace("£", "", StringComparison.OrdinalIgnoreCase).Trim();
        if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
            return d.ToString("0.00", CultureInfo.InvariantCulture);
        if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.GetCultureInfo("en-GB"), out d))
            return d.ToString("0.00", CultureInfo.InvariantCulture);
        return s;
    }

    private static async Task CloseCookieBannerAsync(IPage page)
    {
        var selectors = new[]
        {
            "#cb-cookieoverlay",
            "button:has-text('Accept all')",
            "button:has-text('Accept')",
            "button:has-text('I accept')",
            "button:has-text('Agree')",
            "button:has-text('OK')",
            "button:has-text('Close')",
            "[aria-label*='cookie' i]",
            "[id*='cookie' i] button",
        };

        foreach (var selector in selectors)
        {
            try
            {
                var locator = page.Locator(selector).First;
                if (await locator.CountAsync() > 0)
                {
                    if (selector == "#cb-cookieoverlay")
                    {
                        await page.EvaluateAsync("document.querySelector('#cb-cookieoverlay')?.remove();");
                    }
                    else
                    {
                        await locator.ClickAsync(new LocatorClickOptions { Force = true, Timeout = 3000 });
                    }
                    break;
                }
            }
            catch
            {
            }
        }
    }
}
