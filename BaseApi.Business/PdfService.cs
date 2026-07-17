using PuppeteerSharp;
using PuppeteerSharp.Media;
using System.IO;
using System.Threading.Tasks;

namespace BaseApi.Business
{
    public class PdfService
    {
        public async Task<byte[]> GeneratePdfFromHtmlAsync(string htmlContent)
        {
            // Download the Chromium executable if not already present
            var browserFetcher = new BrowserFetcher();
            await browserFetcher.DownloadAsync();

            // Launch the Chromium browser in headless mode
            using (var browser = await Puppeteer.LaunchAsync(new LaunchOptions { Headless = true }))
            {
                // Create a new page and set its content
                var page = await browser.NewPageAsync();
                await page.SetContentAsync(htmlContent);

                // Generate PDF as bytes
                var pdfBytes = await page.PdfDataAsync(new PdfOptions
                {
                    Format = PaperFormat.A4,
                    PrintBackground = true
                });

                return pdfBytes;
            }
        }
    }
}
