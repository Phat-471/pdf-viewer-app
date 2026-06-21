using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace TestApp
{
    class Program
    {
        static async Task Main()
        {
            try
            {
                using var client = new HttpClient();
                var payload = new System.Collections.Generic.Dictionary<string, string>
                {
                    { "license_key", "PDFPROPHAT" },
                    { "machine_id", "DCA6-8A6C-9FD7-CDFD" }
                };
                string jsonPayload = JsonSerializer.Serialize(payload);
                Console.WriteLine("Sending payload: " + jsonPayload);

                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                var response = await client.PostAsync("https://hongmien.vn/wp-json/pdfpro/v1/check", content);

                Console.WriteLine("Status Code: " + (int)response.StatusCode + " " + response.StatusCode);
                string responseBody = await response.Content.ReadAsStringAsync();
                Console.WriteLine("Response Body: " + responseBody);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.ToString());
            }
        }
    }
}
