namespace ActuarialTranslationEngine.PoC
{
    using Xunit;
    using ClosedXML.Excel;
    using System;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Net.Http.Json;
    using System.Text.Json;
    using System.Threading.Tasks;

    public class DualHypothesisSpike
    {
        [Fact]
        public async Task Phase1_Must_Validate_Extraction_Fidelity_And_Semantic_Accuracy()
        {
            // --- HYPOTHESIS A: .NET EXTRACTION FIDELITY ---
            const string filePath = "edu-2012-c13-01.xlsx";
            using var workbook = new XLWorkbook(filePath);
            var sheet = workbook.Worksheet("Table 13.4");

            // Extract Target Cell (Row 6, Column M)
            var targetCell = sheet.Cell(6, 13);
            string extractedFormula = targetCell.FormulaA1; // ClosedXML excludes the leading '=' character
            string evaluatedValue = targetCell.Value.ToString();

            // Strict Assertion: Ensure the formula token is parsed and contains structural components
            Assert.True(targetCell.HasFormula, "ClosedXML failed to recognize the formula token context.");
            Assert.False(string.IsNullOrWhiteSpace(extractedFormula), "Formula extraction returned empty string.");
            Assert.Contains("K6", extractedFormula); // Verifies components exist

            // Numeric tolerance guard: Avoid runtime string variations across OS/precision layers
            double numericValue = double.Parse(evaluatedValue);
            Assert.InRange(numericValue, 953.6558, 953.6560);

            // --- HYPOTHESIS B: LLM SEMANTIC ACCURACY ---
            var rawPayload = new
            {
                TargetCell = "Table_13.4!M6",
                Formula = extractedFormula,
                EvaluatedValue = evaluatedValue,
                SurroundingContext = new[] {
                    new { Cell = "K6", Definition = "Fund Value After Monthly Deduction", Value = sheet.Cell(6, 11).Value.ToString() },
                    new { Cell = "L6", Definition = "Interest On Fund Value", Value = sheet.Cell(6, 12).Value.ToString() }
                }
            };

            string systemPrompt = "You are a Senior Actuary. Translate the provided spreadsheet formula string into its core financial rule. Identify the product framework and map cell coordinates to standard actuarial terms. Output text only.";

            // Endpoint URL and API key are resolved via environment variables or a local secrets configuration.
            string endpointUrl = Environment.GetEnvironmentVariable("ACTUARIAL_LLM_ENDPOINT")
                ?? "https://api.openai.com/v1/chat/completions";
            string apiKey = Environment.GetEnvironmentVariable("ACTUARIAL_LLM_API_KEY")
                ?? throw new InvalidOperationException("ACTUARIAL_LLM_API_KEY environment variable is not set. Cannot execute Hypothesis B.");

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            string modelName = Environment.GetEnvironmentVariable("ACTUARIAL_LLM_MODEL") ?? "codestral-latest";

            // Using standard completions format
            var response = await client.PostAsJsonAsync(endpointUrl, new {
                model = modelName,
                messages = new[] {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = JsonSerializer.Serialize(rawPayload) }
                }
            });

            string responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"LLM API Request failed with status code {response.StatusCode}: {responseContent}");
            }

            using var document = JsonDocument.Parse(responseContent);
            var root = document.RootElement;
            string actualContent = root.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();

            // Assert: Confirm semantic recognition boundaries
            Assert.Contains("fund value after monthly deduction", actualContent, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("interest on the fund value", actualContent, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("pension or retirement", actualContent, StringComparison.OrdinalIgnoreCase);
        }
    }
}
