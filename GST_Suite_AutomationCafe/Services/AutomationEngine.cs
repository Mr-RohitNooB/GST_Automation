using Microsoft.Playwright;
using System.Text.Json;
using System.Threading.Tasks;

public class AutomationEngine
{
    public async Task ExecuteScriptAsync(string jsonScript, string gstUser, string gstPass)
    {
        using JsonDocument document = JsonDocument.Parse(jsonScript);
        var steps = document.RootElement.GetProperty("steps");

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = false,
            SlowMo = 500 // Slows down clicks so the GST portal can keep up
        });

        var context = await browser.NewContextAsync();
        var page = await context.NewPageAsync();

        foreach (var step in steps.EnumerateArray())
        {
            string action = step.GetProperty("action").GetString();

            if (action == "goto")
            {
                await page.GotoAsync(step.GetProperty("value").GetString());
            }
            else if (action == "type")
            {
                string selector = step.GetProperty("selector").GetString();
                string value = step.GetProperty("value").GetString();

                if (value == "{GST_USERNAME}") value = gstUser;
                if (value == "{GST_PASSWORD}") value = gstPass;

                await page.FillAsync(selector, value);
            }
            // --- NEW ACTION: CLICK ---
            else if (action == "click")
            {
                string selector = step.GetProperty("selector").GetString();
                await page.ClickAsync(selector);
            }
            // -------------------------
            else if (action == "wait")
            {
                int delay = int.Parse(step.GetProperty("value").GetString());
                await Task.Delay(delay);
            }
        }
    }
}