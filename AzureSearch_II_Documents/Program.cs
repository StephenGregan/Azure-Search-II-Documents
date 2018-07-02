using Microsoft.Azure.Search;
using Microsoft.Azure.Search.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AzureSearch_II_Documents
{
    class Program
    {
        private const string DataSourceName = "";
        private const string IndexName = "";
        private const string IndexerName = "";
        private const string SkillSetName = "";
        private const string SynonymMapName = "";

        private static bool DebugMode = false;
        private static bool ShouldDeployWebsite = false;

        private static ISearchServiceClient _searchServiceClient;
        private static HttpClient _httpClient = new HttpClient();
        private static string _searchServiceEndpoint;
        private static string _azureFunctionHostKey;

        static void Main(string[] args)
        {
            string searchServiceName = ConfigurationManager.AppSettings["SearchSericeName"];
            string apiKey = ConfigurationManager.AppSettings["SearchServicpiKey"];

            _searchServiceClient = new SearchServiceClient(searchServiceName, new SearchCredentials(apiKey));
            _httpClient.DefaultRequestHeaders.Add("api-key", apiKey);
            _searchServiceEndpoint = String.Format("https://{0}.{1}", searchServiceName, _searchServiceClient.SearchDnsSuffix);

            bool result = RunAsync().GetAwaiter().GetResult();
            if (!result && !DebugMode)
            {
                Console.WriteLine("Something went wrong.  Set 'DebugMode' to true to see traces.");
            }
            else if (!result)
            {
                Console.WriteLine("Something went wrong.");
            }
            else
            {
                Console.WriteLine("All operations were successfull.");
            }
            Console.WriteLine("Press any key to exit.");
            Console.ReadKey();
        }

        private static async Task<bool> RunAsync()
        {
            bool result = await DeleteIndexingResources();
            if (!result)
            {
                return result;
            }
            result = await CreateDataSource();
            if (!result)
            {
                return result;
            }
            result = await CreateSkillSet();
            if (!result)
            {
                return result;
            }
            result = await CreateSynonyms();
            if (!result)
            {
                return result;
            }
            result = await CreateIndex();
            if (!result)
            {
                return result;
            }
            result = await CreateIndexer();
            if (!result)
            {
                return result;
            }
            if (ShouldDeployWebsite)
            {
                result = await DeployWebsite();
            }
            result = await CheckIndexerStatus();
            if (!result)
            
                return result;
            result = await QueryIndex();
            return result;
        }

        private static async Task<bool> DeleteIndexingResources()
        {
            Console.WriteLine("Deleting Data Source, Index, Indexer and SynonymMap if they exist.");
            try
            {
                await _searchServiceClient.DataSources.DeleteAsync(DataSourceName);
                await _searchServiceClient.Indexes.DeleteAsync(IndexName);
                await _searchServiceClient.Indexers.DeleteAsync(IndexerName);
                await _searchServiceClient.SynonymMaps.DeleteAsync(SynonymMapName);
            }
            catch (Exception e)
            {
                if (DebugMode)
                {
                    Console.WriteLine("Error deleting resources: {0}", e.Message);
                }
                return false;
            }
            return true;
        }

        private static async Task<bool> CreateDataSource()
        {
            Console.WriteLine("Creating Data Source.");
            try
            {
                DataSource datasource = DataSource.AzureBlobStorage(
                    name: DataSourceName,
                    storageConnectionString: ConfigurationManager.AppSettings[""],
                    containerName: ConfigurationManager.AppSettings[""],
                    description: "Data source for cognitive search example."
                    );
                await _searchServiceClient.DataSources.CreateAsync(datasource);
            }
            catch (Exception e)
            {
                if (DebugMode)
                {
                    Console.WriteLine("Error creating data source: {0}", e.Message);
                }
                return false;
            }
            return true;
        }

        private static async Task<bool> CreateSkillSet()
        {
            Console.WriteLine("Creating skill set.");
            try
            {
                if (_azureFunctionHostKey == null)
                {
                    _azureFunctionHostKey = await KeyHelper.GetAzureFunctionHostKey(_httpClient);
                }
                using (StreamReader r = new StreamReader("skillset.json"))
                {
                    string json = r.ReadToEnd();
                    json = json.Replace("[AzureFunctionEndpointUrl]", String.Format("https://{0}.azurewebsites.net", ConfigurationManager.AppSettings["AzureFunctionSiteName"]));
                    json.Replace("[AzureFunctionDefaultHostKey]", _azureFunctionHostKey);
                    string uri = String.Format("{0}/skillsets/{1}?api-version=2017-11-11-Preview", _searchServiceEndpoint, SkillSetName);
                    HttpContent content = new StringContent(json, Encoding.UTF8, "application/json");
                    HttpResponseMessage response = await _httpClient.PutAsync(uri, content);
                    if (DebugMode)
                    {
                        string responseText = await response.Content.ReadAsStringAsync();
                        Console.WriteLine("Create skiil set response: \n{0}", responseText);
                    }
                    if (!response.IsSuccessStatusCode)
                    {
                        return false;
                    }
                }
            }
            catch (Exception e)
            {
                if (DebugMode)
                {
                    Console.WriteLine("Error creating skill set: {0}", e.Message);
                }
                return false;
            }
            return true;
        }

        private static async Task<bool> CreateSynonyms()
        {
            Console.WriteLine("Create Synonyms Map.");
            try
            {
                SynonymMap synonyms = new SynonymMap(SynonymMapName, SynonymMapFormat.Solr,
                    @"GPFOOR");
                await _searchServiceClient.SynonymMaps.CreateAsync(synonyms);
            }
            catch (Exception e)
            {
                if (DebugMode)
                {
                    Console.WriteLine("Error creating synonym map: {0}", e.Message);
                }
                return false;
            }
            return true;
        }

        private static async Task<bool> CreateIndex()
        {
            Console.WriteLine("Creating Index.");
            try {
                using (StreamReader sr = new StreamReader("index.json"))
                {
                    string json = sr.ReadToEnd();
                    json = json.Replace("[SynonymMapName]", SynonymMapName);
                    string uri = String.Format("{0}/indexes/{1}?api-version=2017-11-11-Preview", _searchServiceEndpoint, IndexName);
                    HttpContent content = new StringContent(json, Encoding.UTF8, "application/json");
                    HttpResponseMessage response = await _httpClient.PutAsync(uri, content);
                    if (DebugMode)
                    {
                        string responseText = await response.Content.ReadAsStringAsync();
                        Console.WriteLine("Create Index response: \n{0}", responseText);
                    }
                    if (!response.IsSuccessStatusCode)
                    {
                        return false;
                    }
                }
            }
            catch (Exception e)
            {
                if (DebugMode)
                {
                    Console.WriteLine("Erroe creating index: {0}", e.Message);
                }
                return false;
            }
            return true;
        }

        private static async Task<bool> CreateIndexer()
        {
            Console.WriteLine("Creating Indexer.");
            try
            {
                using (StreamReader sr = new StreamReader("index.json"))
                {
                    string json = sr.ReadToEnd();
                    json = json.Replace("[IndexerName]", IndexerName);
                    json = json.Replace("[DataSourceName]", DataSourceName);
                    json = json.Replace("[IndexName]", IndexName);
                    json = json.Replace("[SkillSetName]", SkillSetName);
                    string uri = String.Format("{0}/indexers/{1}?api-version=2017-11-11-Preview", _searchServiceEndpoint, IndexerName);
                    HttpContent content = new StringContent(json, Encoding.UTF8, "application/json");
                    HttpResponseMessage response = await _httpClient.PutAsync(uri, content);
                    if (DebugMode)
                    {
                        string responseText = await response.Content.ReadAsStringAsync();
                        Console.WriteLine("Create Indexer response: \n{0}", responseText);
                    }
                    if (!response.IsSuccessStatusCode)
                    {
                        return false;
                    }
                }
            }
            catch (Exception e)
            {
                if (DebugMode)
                {
                    Console.WriteLine("Error creating Indexer: {0}", e.Message);
                }
                return false;
            }
            return true;
        }

        private static async Task<bool> DeployWebsite()
        {
            try
            {
                Console.WriteLine("Setting website keys.");
                string searchQueryKey = ConfigurationManager.AppSettings["SearchServiceQueryKey"];
                if (_azureFunctionHostKey == null)
                {
                    _azureFunctionHostKey = await KeyHelper.GetAzureFunctionHostKey(_httpClient);
                }
                string envText = File.ReadAllText("../../../../frontend/.env");
                envText = envText.Replace("[SearchServiceName]", ConfigurationManager.AppSettings["SearchServiceName"]);
                envText = envText.Replace("[SearchServiceDomain]", _searchServiceClient.SearchDnsSuffix);
                envText = envText.Replace("[IndexName]", IndexName);
                envText = envText.Replace("[SearchServiceApiKey]", searchQueryKey);
                envText = envText.Replace("[AzureFunctionName]", ConfigurationManager.AppSettings["AzureFunctionSiteName"]);
                envText = envText.Replace("[AzureFunctionDefaultHostKey]", _azureFunctionHostKey);
                File.WriteAllText("../../../../frontend/.env", envText);

                Console.WriteLine("Website keys have been set.  Please build the webiste and then return here and press any key to continue");
                Console.ReadKey();

                Console.WriteLine("Deploying Website.");
                if (File.Exists("website.zip"))
                {
                    File.Delete("website.zip");
                }

                //ZipFile.

            }
            catch (Exception e)
            {
                if (DebugMode)
                {
                    Console.WriteLine("Error deploying website {0}", e.Message);
                }
                return false;
            }
            return true;
        }

        private static async Task<bool> CheckIndexerStatus()
        {
            Console.WriteLine("Waiting for indexing to complete.");
            IndexerExecutionStatus requestStatus = IndexerExecutionStatus.InProgress;

            try
            {
                await _searchServiceClient.Indexers.GetAsync(IndexerName);
                while (requestStatus.Equals(IndexerExecutionStatus.InProgress))
                {
                    Thread.Sleep(3000);
                    IndexerExecutionInfo info = await _searchServiceClient.Indexers.GetStatusAsync(IndexerName);
                    requestStatus = info.LastResult.Status;
                    if (DebugMode)
                    {
                        Console.WriteLine("Current indexer status: {0}", requestStatus.ToString());
                    }
                }
            }
            catch (Exception e)
            {
                if (DebugMode)
                {
                    Console.WriteLine("Error retieving indexer status: {0}", e.Message);
                }
                return false;
            }
            return requestStatus.Equals(IndexerExecutionStatus.Success);
        }

        private static async Task<bool> QueryIndex()
        {
            Console.WriteLine("Query index.");
            try
            {
                ISearchIndexClient indexClient = _searchServiceClient.Indexes.GetClient(IndexName);
                DocumentSearchResult searchResult = await indexClient.Documents.SearchAsync(IndexName);
                Console.WriteLine("Query results: ");
                foreach (SearchResult result in searchResult.Results)
                {
                    foreach (string key in result.Document.Keys)
                    {
                        Console.WriteLine("{0}: {1}", key, result.Document[key]);
                    }
                }
            }
            catch (Exception e)
            {
                if (DebugMode)
                {
                    Console.WriteLine("Error querying index: {0}", e.Message);
                }
                return false;
            }
            return true;
        }
    }
}
