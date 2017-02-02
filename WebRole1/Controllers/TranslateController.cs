using System.Linq;
using System.Net.Http;
using System.Text;
using System.Web;
using System.Web.Http;
using Microsoft.Azure;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;
using TranslationRobot;

namespace WebRole1.Controllers
{
    public class TranslateController : ApiController
    {
        private const string TranslatoAccessKey = "TranslatorAccess";
        private const string AddressTranslationTableKey = "AddressTranslationTable";

        private TranslatorAccess TranslatorAccess
        {
            get
            {
                TranslatorAccess result;
                var context = HttpContext.Current;
                if (context != null && context.Application != null &&
                    context.Application.Contents[TranslatoAccessKey] != null)
                {
                    result = context.Application.Contents[TranslatoAccessKey] as TranslatorAccess;
                }
                else
                {
                    result = new TranslatorAccess();
                    if (context != null && context.Application != null)
                        context.Application.Contents[TranslatoAccessKey] = result;
                }
                return result;
            }
        }


        private CloudTable AddressTranslationTable
        {
            get
            {
                CloudTable result;
                var context = HttpContext.Current;
                if (context != null && context.Application != null &&
                    context.Application.Contents[AddressTranslationTableKey] != null)
                {
                    result = context.Application.Contents[AddressTranslationTableKey] as CloudTable;
                }
                else
                {
                    result = GetTable(TranslatedAddressEntity.TableName);
                    if (context != null && context.Application != null)
                        context.Application.Contents[AddressTranslationTableKey] = result;
                }
                return result;
            }
        }


        [HttpPost]
        [Route("api/AddTranslation")]
        public string AddTranslation(HttpRequestMessage httpRequestMessage)
        {
            string translationInputString = httpRequestMessage.Content.ReadAsStringAsync().Result;
            TranslationInput translationInput = JsonConvert.DeserializeObject<TranslationInput>(translationInputString);

            translationInput.Translation = Encoding.UTF8.GetString(translationInput.EncodedTranslation);
            string result;
            var translatedAddressEntity = RetrieveTranslation(translationInput.Input);
            if (translatedAddressEntity == null)
            {
                translatedAddressEntity = LocationInfo.GetLocationInfo(translationInput.Translation, TranslatorAccess);
                translatedAddressEntity.Input = translationInput.Input;
                translatedAddressEntity.OriginalAddress = translationInput.Translation;
                translatedAddressEntity.Translation = translationInput.Translation;
                InsertTranslation(translatedAddressEntity);
                result = "Translation from " + translationInput.Input + " to " + translationInput.Translation + " added";
            }
            else
            {
                var oldTranslation = translatedAddressEntity.Translation;
                translatedAddressEntity.Translation = translationInput.Translation;
                translatedAddressEntity.OriginalAddress = translationInput.Translation;
                translatedAddressEntity.Translation = translationInput.Translation;
                var determindedLocation = LocationInfo.GetLocationInfo(translationInput.Translation, TranslatorAccess);
                translatedAddressEntity.CountryCode = determindedLocation.CountryCode;
                translatedAddressEntity.Lattitude = determindedLocation.Lattitude;
                translatedAddressEntity.Longitude = determindedLocation.Longitude;
                var replaceOperation
                    = TableOperation.Replace(translatedAddressEntity);
                AddressTranslationTable.Execute(replaceOperation);
                result = "Translation from " + translationInput.Input + " to " + oldTranslation + " replaced with " +
                         translationInput.Translation;
            }


            return result;
        }


        [HttpGet]
        [Route("api/Translation/")]
        public HttpResponseMessage Get()
        {
            var table = GetTable(TranslatedAddressEntity.TableName);
            var query = table.CreateQuery<TranslatedAddressEntity>();
            var queryResult = table.ExecuteQuery(query);
            var translatedAddressEntities = queryResult.ToList();


            var httpResponseMessage = new HttpResponseMessage
            {
                Content = new JsonContent(translatedAddressEntities)
            };
            return httpResponseMessage;
        }

        [HttpGet]
        [Route("api/Translate/{input}")]
        public string Translate(string input)
        {
            string result;
            var translatedAddressEntity = RetrieveTranslation(input);
            if (translatedAddressEntity != null)
                result = translatedAddressEntity.Translation;
            else
                try
                {
                    translatedAddressEntity = LocationInfo.GetLocationInfo(input, TranslatorAccess);

                    InsertTranslation(translatedAddressEntity);
                    result = translatedAddressEntity.Translation;
                }
                catch (AddressNotFoundException addressNotFoundException)
                {
                    result = addressNotFoundException.Message;
                }


            return result;
        }

        [HttpGet]
        [Route("api/TranslateDetails/{input}")]
        public HttpResponseMessage TranslateDetails(string input)
        {
            var translatedAddressEntity = RetrieveTranslation(input);
            if (translatedAddressEntity == null)
            {
                translatedAddressEntity = LocationInfo.GetLocationInfo(input, TranslatorAccess);
                InsertTranslation(translatedAddressEntity);
            }

            var httpResponseMessage = new HttpResponseMessage
            {
                Content = new JsonContent(translatedAddressEntity)
            };


            return httpResponseMessage;
        }

        private void InsertTranslation(TranslatedAddressEntity translatedAddressEntity)
        {
            var insertOperation = TableOperation.Insert(translatedAddressEntity);
            AddressTranslationTable.Execute(insertOperation);
        }

        internal TranslatedAddressEntity RetrieveTranslation(string text)
        {
            TranslatedAddressEntity result = null;
            var table = GetTable(TranslatedAddressEntity.TableName);
            var retrieveOperation =
                TableOperation.Retrieve<TranslatedAddressEntity>(TranslatedAddressEntity.DefaultPartitionKey, text);

            //TODO lowercase

            // Execute the retrieve operation.
            var retrievedResult = table.Execute(retrieveOperation);
            if (retrievedResult.Result != null)
                result = (TranslatedAddressEntity) retrievedResult.Result;
            else if (text.ToUpper() != text)
                result = RetrieveTranslation(text.ToUpper());

            return result;
        }

        internal string RetrieveTranslationByQuery(string text)
        {
            string result = null;
            var table = GetTable(TranslatedAddressEntity.TableName);
            //var retrieveOperation = TableOperation.Retrieve<TranslatedAddressEntity>(text, TranslatedAddressEntity.DefaultPartitionKey);
            var query = table.CreateQuery<TranslatedAddressEntity>();
            query.Where(TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.Equal, text));
            var queryResult = table.ExecuteQuery(query);
            // Execute the retrieve operation.
            //TableResult retrievedResult = table.Execute(retrieveOperation);
            foreach (var item in queryResult)
                result = item.Translation;
            return result;
        }

        public CloudTable GetTable(string tableName)
        {
            // Retrieve the storage account from the connection string.
            var storageAccount = CloudStorageAccount.Parse(
                CloudConfigurationManager.GetSetting("StorageConnectionString"));

            // Create the table client.
            var tableClient = storageAccount.CreateCloudTableClient();

            // Retrieve a reference to the table.
            var table = tableClient.GetTableReference(tableName);

            // Create the table if it doesn't exist.
            // table.CreateIfNotExists(); //TODO this is broken in azure

            return table;
        }
    }
}