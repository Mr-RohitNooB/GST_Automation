using GST_Suite_AutomationCafe.Models;
using Microsoft.Playwright;
using System.Diagnostics;
using System.Text.Json;

namespace GST_Suite_AutomationCafe.Services
{
    public class AutomationEngine
    {
        #region Core Execution Engine
        /// <summary>
        /// Parses the JSON script and executes the Playwright commands.
        /// </summary>
        public async Task RunScriptAsync(string jsonContent, Dictionary<string, string> userInputs)
        {
            // Context: Deserialize the JSON string into our C# objects
            var script = JsonSerializer.Deserialize<AutomationScript>(jsonContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (script == null || script.Steps == null)
                throw new Exception("Failed to parse the automation script.");

            // Context: Initialize Playwright Engine. Headless = false shows the browser to the user.
            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = false });
            var page = await browser.NewPageAsync();

            // Context: The Interpreter Loop that reads the JSON steps one by one
            foreach (var step in script.Steps)
            {
                Debug.WriteLine($"Executing Action: {step.Action}");

                switch (step.Action.ToLower())
                {
                    case "goto":
                        if (!string.IsNullOrEmpty(step.Value))
                            await page.GotoAsync(step.Value);
                        break;

                    case "type":
                        if (!string.IsNullOrEmpty(step.Selector) && !string.IsNullOrEmpty(step.Value))
                        {
                            // Context: Replaces placeholders like {GST_USERNAME} with actual data
                            string finalValue = ReplacePlaceholders(step.Value, userInputs);
                            await page.Locator(step.Selector).FillAsync(finalValue);
                        }
                        break;

                    case "click":
                        if (!string.IsNullOrEmpty(step.Selector))
                            await page.Locator(step.Selector).ClickAsync();
                        break;

                    case "wait":
                        if (int.TryParse(step.Value, out int waitTime))
                            await Task.Delay(waitTime);
                        break;

                    default:
                        Debug.WriteLine($"Unknown action skipped: {step.Action}");
                        break;
                }
            }

            // Context: Keep browser open briefly at the end so the user can verify success
            await Task.Delay(3000);
        }
        #endregion

        #region Helper Methods
        // Context: Helper method to swap dynamic text markers with real user input
        private string ReplacePlaceholders(string template, Dictionary<string, string> inputs)
        {
            string result = template;
            foreach (var input in inputs)
            {
                result = result.Replace($"{{{input.Key}}}", input.Value);
            }
            return result;
        }
        #endregion
    }
}