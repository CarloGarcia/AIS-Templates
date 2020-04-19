using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using Google.Protobuf.WellKnownTypes;
using TechTalk.SpecFlow;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MockHttpServer;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Customers.Tests
{
    [Binding]
    public class CustomersSteps
    {
        private string restEndpointOutputFolder = "..\\..\\..\\TestOutput\\Messages\\MockHttpServer\\Working";
        private string mockHttpServerSubdomain = "aistemplates";
        private string mockHttpServerPort = "8088";
        private string mockHttpServerLocalUrl = "http://localhost:8888/SendData/";
        private string mockHttpServerExternalHost = "aistemplates.serveo.net";
        private string mockHttpServerExternalUrl = "https://aistemplates.serveo.net/SendData/";
        private string mockHttpServerMockHttpServerPathToSSH = "C:\\Program Files\\OpenSSH\\ssh.exe";

        [Given(@"a local service is exposed on subdomain (.*) and port (.*)")]
        public void GivenALocalServiceIsExposedOnPort(string subdomain, string port)
        {
            string pathToSSH = mockHttpServerMockHttpServerPathToSSH;

            ProcessStartInfo info = new ProcessStartInfo(pathToSSH, $"-R {subdomain}:80:localhost:{port} serveo.net");
            Process cmd = Process.Start(info);
            ScenarioContext.Current["ServeoProcess"] = cmd;
            Thread.Sleep(1000);
        }

        [Given(@"a Rest Endpoint is setup on Url (.*) that responds with HTTP status code (.*) with the following response body (.*)")]
        public void GivenARestEndpointIsSetupOnUrlThatRespondsWithHTTPStatusCodeWithTheFollowingResponseBody(string url, string statusCode, string responseBodyFilePath)
        {
            GivenAnExternalRestEndpointIsSetupOnUrlForHostThatRespondsWithHTTPStatusCodeWithTheFollowingResponseBody(url, "localhost", statusCode, responseBodyFilePath);
        }

        [Given(@"an external Rest Endpoint is setup on Url (.*) for host (.*) that responds with HTTP status code (.*) with the following response body (.*)")]
        public void GivenAnExternalRestEndpointIsSetupOnUrlForHostThatRespondsWithHTTPStatusCodeWithTheFollowingResponseBody(string url, string host, string statusCode, string responseBodyFilePath)
        {
            var actualUrl = url;
            string actualHost = host;
            ScenarioContext.Current["RestEndpointUrl"] = actualUrl;
            ScenarioContext.Current["Guid"] = Guid.NewGuid().ToString();

            if (!Directory.Exists(restEndpointOutputFolder))
            {
                Directory.CreateDirectory(restEndpointOutputFolder);
            }

            DirectoryInfo outputDirectory = new System.IO.DirectoryInfo(restEndpointOutputFolder);
            foreach (FileInfo file in outputDirectory.GetFiles())
            {
                file.Delete();
            }

            var uri = new Uri(actualUrl);

            string responseMessageBody = File.ReadAllText(responseBodyFilePath);

            var requestHandlers = new List<MockHttpHandler>()
            {
                new MockHttpHandler(uri.LocalPath, (req, rsp, _) =>
                {
                    File.WriteAllText($"{restEndpointOutputFolder}\\Request_{ScenarioContext.Current["Guid"]}.json", req.Content());
                    rsp.StatusCode = Convert.ToInt32(statusCode);
                    return responseMessageBody;
                })
            };

            CancellationTokenSource ts = SetupMockHttpServerAsync(actualUrl, requestHandlers, actualHost);

            ScenarioContext.Current["CancellationTokenSource"] = ts;
        }

        [Then(@"the Rest EndPoint will receive (.*) messages within (.*) seconds")]
        public void ThenIWillReceiveMessageInTheRestEndpointWithinSeconds(int numberOfMessages, int waitTimeInSeconds)
        {
            var timer = new Stopwatch();
            timer.Start();

            int fileCount = Directory.GetFiles(restEndpointOutputFolder, $"Request_{ScenarioContext.Current["Guid"]}.json").Length;
            while (fileCount != numberOfMessages && timer.Elapsed <= TimeSpan.FromSeconds(waitTimeInSeconds))
            {
                fileCount = Directory.GetFiles(restEndpointOutputFolder, $"Request_{ScenarioContext.Current["Guid"]}.json").Length;
                Thread.Sleep(1000);
            }

            Assert.AreEqual(numberOfMessages, fileCount);
        }

        [Then(@"the Rest EndPoint received a message with the content as in file ""(.*)"" within (.*) seconds")]
        public void ThenThereIsAMessageInTheRestEndpointWithContentAsWithinSeconds(string filePath, int amountOfSeconds)
        {
            string actualFilePath = $"{restEndpointOutputFolder}\\Response_{ScenarioContext.Current["Guid"]}.json";

            var timer = new Stopwatch();
            timer.Start();
            while (!File.Exists(actualFilePath) && timer.Elapsed <= TimeSpan.FromSeconds(amountOfSeconds))
            {
                Thread.Sleep(1000);
            }

            Assert.IsTrue(File.Exists(actualFilePath));

            string expectedMessageBody = File.ReadAllText(filePath);
            string actualMessageBody = File.ReadAllText(actualFilePath);

            var jResponse = JObject.Parse(actualMessageBody);
            var jExpected = JObject.Parse(expectedMessageBody);

            Assert.AreEqual(jExpected.ToString(), jResponse.ToString());
        }

        [When(@"the following request ""(.*)"" is sent to the Rest Endpoint")]
        public void WhenTheFollowingRequestIsSentToTheRestEndpoint(string requestFilePath)
        {
            var actualUrl = ScenarioContext.Current["RestEndpointUrl"].ToString();
            var uri = new Uri(actualUrl);

            using (var client = new HttpClient())
            {
                client.BaseAddress = uri;

                var requestBody = System.IO.File.ReadAllText(requestFilePath);
                var dataAsString = JsonConvert.SerializeObject(requestBody);
                var content = new StringContent(dataAsString);
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                var result = client.PostAsync(uri.LocalPath, content).GetAwaiter().GetResult();
                if (result.IsSuccessStatusCode)
                {
                    string response = result.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    System.IO.File.WriteAllText($"{restEndpointOutputFolder}\\Response_{ScenarioContext.Current["Guid"]}.json", response);
                }
                else
                {
                    string response = result.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    System.IO.File.WriteAllText($"{restEndpointOutputFolder}\\ErrorResponse_{result.StatusCode.ToString()}_{ScenarioContext.Current["Guid"]}.json", response);
                }
            }
        }

        [When(@"the following request ""(.*)"" is sent to Url ""(.*)""")]
        public void WhenTheFollowingRequestIsSentToUrl(string requestFilePath, string url)
        {
            var actualUrl = url;
            var uri = new Uri(actualUrl);

            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri($"{uri.Scheme}://{uri.Host}:{uri.Port}");

                var requestBody = System.IO.File.ReadAllText(requestFilePath);

                var dataAsString = JsonConvert.SerializeObject(requestBody);
                var content = new StringContent(dataAsString);
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                var result = client.PostAsync(uri.LocalPath, content).GetAwaiter().GetResult();

                string outputFilename = result.IsSuccessStatusCode
                    ? $"{restEndpointOutputFolder}\\Response_{ScenarioContext.Current["Guid"]}.json"
                    : $"{restEndpointOutputFolder}\\ErrorResponse_{result.StatusCode.ToString()}_{ScenarioContext.Current["Guid"]}.json";

                string response = result.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                System.IO.File.WriteAllText(outputFilename, response);

                Assert.IsTrue(result.IsSuccessStatusCode);
            }
        }

        private static CancellationTokenSource SetupMockHttpServerAsync(string url, List<MockHttpHandler> requestHandlers, string host)
        {
            var uri = new Uri(url);

            var ts = new CancellationTokenSource();
            CancellationToken ct = ts.Token;
            Task.Factory.StartNew(
                () =>
                {
                    MockServer mockServer = new MockServer(uri.Port, requestHandlers, host);
                    while (true)
                    {
                        Thread.Sleep(1000);
                        if (ct.IsCancellationRequested)
                        {
                            break;
                        }
                    }
                    mockServer.Dispose();
                }, ct);

            return ts;
        }
    }
}
