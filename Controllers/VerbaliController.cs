using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Playwright;
using System.Globalization;
using System.Text.RegularExpressions;
using testplaywright1.Models;

namespace testplaywright1.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class VerbaliController : ControllerBase
{


    [HttpGet("VerificaImporto")]
    public async Task<ActionResult<object>> VerificaImporto([FromQuery] VerbaleRequest request)
    {

        // SETUP
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = false,
            Timeout = 10000
        });

        var page = await browser.NewPageAsync(new BrowserNewPageOptions
        {
            ViewportSize = new ViewportSize { Width = 1440, Height = 1600 }
        });

        // CARICO LA PAGINA
        await page.GotoAsync("https://tfl.gov.uk/modes/driving/pay-a-pcn", new PageGotoOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle
        });

        //CHIUDO IL BANNER DEI COOKIES
        await CloseCookieBannerAsync(page);

        //RIEMPIO I CAMPI TARGA E VERBALE E CLICCO CONTINUA
        var pcnInput = page.GetByLabel("PCN number", new() { Exact = false });
        var plateInput = page.GetByLabel("Plate", new() { Exact = false });

        await pcnInput.FillAsync(request.NumeroVerbale);
        await plateInput.FillAsync(request.Targa);

        var goToFine = page.GetByRole(AriaRole.Button, new() { Name = "Continue" });
        await goToFine.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 3000 });
        if (await goToFine.CountAsync() == 0)
            return reportError(request, page, "go to fine (continue) button not found");

        await goToFine.First.ClickAsync(new LocatorClickOptions { Force = true });
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);


        // VERIFICO L'ERRORE PER MULTA NON TROVATA
        var errorFound = page.GetByText("try again");
        if (await errorFound.CountAsync() > 0)
            return reportError(request, page, "fine not found");

        //RECUPERO L'IMPORTO E LO VERIFICO
        var outstandingAmountLocator = page.GetByText("Outstanding amount", new() { Exact = false });
        string? outstandingAmount = null;
        await outstandingAmountLocator.WaitForAsync(new LocatorWaitForOptions { Timeout = 3000 });
        outstandingAmount = await ExtractOutstandingAmountAsync(page);

        if (outstandingAmount is not null)
            if (request.Importo != NormalizeMoney(outstandingAmount) && !request.ForzaPagamento)
            {
                return reportError(request, page, "amount mismatch");
            }

        // AGGIUNGO LA MULTA AL CARRELLO
        var addToBasket = page.GetByRole(AriaRole.Button, new() { Name = "Add to basket" });
        await addToBasket.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 3000 });
        if (await addToBasket.CountAsync() == 0)
            return reportError(request, page, "add to basket button not found (not payable?)");

        var first = addToBasket.First;
        await first.ClickAsync(new LocatorClickOptions { Force = true });
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // VADO AL CARRELLO
        var goToBasket = page.GetByRole(AriaRole.Link, new() { Name = "Go to basket" });
        await goToBasket.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 3000 });

        if (await goToBasket.CountAsync() == 0)
            return reportError(request, page, "go to basket not found");

        await goToBasket.First.ClickAsync(new LocatorClickOptions { Force = true });
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        string importoDaPagare = await ExtractTotalAsync(page);

        // IMPOSTO LA MAIL EPR LA RICEVUTA E PROSEGUO
        await page.Locator("#checkbox-accordion").CheckAsync(new LocatorCheckOptions { Force = true });

        await page.Locator("#EmailAddress").FillAsync(request.EmailNotifica);

        var confirmAndPay = page.GetByRole(AriaRole.Button, new() { Name = "Confirm and pay" });
        await confirmAndPay.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 3000 });
        if (await confirmAndPay.CountAsync() == 0)
            return reportError(request, page, "confirm and pay button not found");

        await confirmAndPay.First.ClickAsync(new LocatorClickOptions { Force = true });
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // IMPOSTO I DATI DELLA CARTA E CLICCO SU CONTINUA
        await page.Locator("#scp_cardPage_cardNumber_input").FillAsync("4293189100000008");

        // 2. Inserimento Data di Scadenza (Mese/Anno)
        await page.Locator("#scp_cardPage_expiryDate_input").FillAsync("12"); // Esempio: Mese
        await page.Locator("#scp_cardPage_expiryDate_input2").FillAsync("27"); // Esempio: Anno
        await page.Locator("#scp_cardPage_csc_input").FillAsync("123");
        await page.Locator("#scp_cardPage_buttonsNoBack_continue_button").ClickAsync();
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // VERIFICO L'ERRORE PER I DATI DELLA CARTA
        errorFound = page.GetByText("Security Code");
        if (await errorFound.CountAsync() > 0)
            return reportError(request, page, "card detail page error");


        // INSERISCO I DETTAGLI SUL POSSESSORE DELLA CARTA
        await page.Locator("#scp_tdsv2AdditionalInfoPage_cardholderName_input").FillAsync("NOME TITOLARE");
        await page.Locator("#scp_tdsv2AdditionalInfoPage_address_1_input").FillAsync("INDIRIZZO RIGA 1");
        await page.Locator("#scp_tdsv2AdditionalInfoPage_city_input").FillAsync("CITTA");
        await page.Locator("#scp_tdsv2AdditionalInfoPage_postcode_input").FillAsync("CAP");
        await page.Locator("#scp_tdsv2AdditionalInfoPage_email_input").FillAsync(request.EmailNotifica);
        await page.Locator("#scp_tdsv2AdditionalInfoPage_buttons_continue_button").ClickAsync();
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // VERIFICO L'ERRORE PER I DATI DEL TITOLARE DELLA CARTA
        errorFound = page.GetByText("Cardholder Billing Information");
        if (await errorFound.CountAsync() > 0)
            return reportError(request, page, "card holder page error");

        // CONFERMO ED EFFETTUO IL PAGAMENTO
        var makePayment = page.GetByRole(AriaRole.Button, new() { Name = "Make Payment" });
        await makePayment.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 3000 });
        if (await makePayment.CountAsync() == 0)
            return reportError(request, page, "make payment button not found");

        await makePayment.First.ClickAsync(new LocatorClickOptions { Force = true });
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // VERIFICO L'ERRORE PER IL 3D SECURE
        errorFound = page.GetByText("Secure authorisation");
        if (await errorFound.CountAsync() > 0)
            return reportError(request, page, "3d secure auth required");

        // VERIFICO L'ERRORE PER CARTA RIFIUTATA
        errorFound = page.GetByText("Card Declined");
        if (await errorFound.CountAsync() > 0)
            return reportError(request, page, "card declined error");



        var bodyText = await page.Locator("body").InnerTextAsync();

        return Ok(new VerbaleResponse
        {
            Targa = request.Targa,
            NumeroVerbale = request.NumeroVerbale,
            Importo = request.Importo,
            ImportoDaPagare = NormalizeMoney(outstandingAmount),
            DataRicevimento = DateTime.Now,
            ContenutoPagina = bodyText,
            CurrentUrl = page.Url
        });
    }

    private ActionResult<object> reportError(VerbaleRequest request, IPage page, string msg)
    {
        var bodyTextTask = page.Locator("body").InnerTextAsync();
        bodyTextTask.Wait(); // Sincronizza per ottenere il risultato in modo sincrono
        var bodyText = bodyTextTask.Result;

        return BadRequest(new VerbaleResponse
        {
            Targa = request.Targa,
            NumeroVerbale = request.NumeroVerbale,
            Importo = request.Importo,
            DataRicevimento = DateTime.Now,
            ContenutoPagina = bodyText,
            CurrentUrl = page.Url,
            Errore = msg
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

    private static decimal? NormalizeMoney(string value)
    {
        var s = value.Trim();
        s = s.Replace("£", "", StringComparison.OrdinalIgnoreCase).Trim();
        if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
            return d;
        if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.GetCultureInfo("en-GB"), out d))
            return d;
        return null;
    }

    private static async Task CloseCookieBannerAsync(IPage page)
    {
        var locator = page.Locator("button:has-text('Accept all')").First;
        if (await locator.CountAsync() > 0)
        {
            await locator.ClickAsync(new LocatorClickOptions { Timeout = 3000 });

            locator = page.Locator("#cb-cookieoverlay");
            if (await locator.CountAsync() > 0)
            {
                await page.EvaluateAsync("document.querySelector('#cb-cookieoverlay')?.remove();");
            }
        }
    }
}
