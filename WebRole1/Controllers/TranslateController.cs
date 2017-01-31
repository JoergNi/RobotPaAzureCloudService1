using System.Collections.Generic;
using Microsoft.Azure;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using System.Web;
using System.Web.Http;
using TranslationRobot;
using System;
using System.Linq;
using System.Net.Http;
using System.Text;

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
                HttpContext context = HttpContext.Current;
                if (context != null && context.Application != null && context.Application.Contents[TranslatoAccessKey] != null)
                {
                    result = context.Application.Contents[TranslatoAccessKey] as TranslatorAccess;

                }
                else
                {
                    result = new TranslatorAccess();
                    if (context != null && context.Application != null) context.Application.Contents[TranslatoAccessKey] = result;
                }
                return result;
            }
        }


        private CloudTable AddressTranslationTable
        {
            get
            {
                CloudTable result;
                HttpContext context = HttpContext.Current;
                if (context != null && context.Application != null && context.Application.Contents[AddressTranslationTableKey] != null)
                {
                    result = context.Application.Contents[AddressTranslationTableKey] as CloudTable;

                }
                else
                {
                    result = GetTable(TranslatedAddressEntity.TableName);
                    if (context != null && context.Application != null) context.Application.Contents[AddressTranslationTableKey] = result;
                }
                return result;
            }
        }


        [HttpPost ]
        [Route("api/AddTranslation")]
        public string AddTranslation(TranslationInput translationInput)
        {
            translationInput.Translation = Encoding.UTF8.GetString(translationInput.EncodedTranslation);
            string result;
            TranslatedAddressEntity translatedAddressEntity = RetrieveTranslation(translationInput.Input);
            if (translatedAddressEntity==null)
            {
                InsertTranslation(translationInput);
                result = "Translation from "+translationInput.Input+" to "+translationInput.Translation + " added";
            }
            else
            {
                string oldTranslation = translatedAddressEntity.Translation;
                translatedAddressEntity.Translation = translationInput.Translation;
                var replaceOperation
                    = TableOperation.Replace(translatedAddressEntity);
                AddressTranslationTable.Execute(replaceOperation);
                result = "Translation from " + translationInput.Input + " to " + oldTranslation + " replaced with "+translationInput.Translation;
            }
            
            
            

            return result;

        }

        private void InsertTranslation(TranslationInput translationInput)
        {
           InsertTranslation(translationInput.Input,translationInput.Translation);
        }

        [HttpGet]
        [Route("api/Translation/")]
        public HttpResponseMessage Get()
        {
            CloudTable table = GetTable(TranslatedAddressEntity.TableName);
            var query = table.CreateQuery<TranslatedAddressEntity>();
            var queryResult = table.ExecuteQuery(query);
            List<TranslatedAddressEntity> translatedAddressEntities = queryResult.ToList();
          
            IList<TranslationResponse> translationResponses = translatedAddressEntities.Select(x => new TranslationResponse()
            {
                Translation = x.Translation,
                Input = x.RowKey
            }).ToList();

            HttpResponseMessage httpResponseMessage = new HttpResponseMessage()
            {
                Content = new JsonContent(translationResponses)
            };
            return httpResponseMessage;
        }

        [HttpGet]
        [Route("api/Translate/{input}")]
        public string Translate(string input)
        {
            string result;
            TranslatedAddressEntity translatedAddressEntity = RetrieveTranslation(input);
            if (translatedAddressEntity != null)
            {
                result = translatedAddressEntity.Translation;
            }
            else
                try
                {
                    result = LocationInfo.GetLocationInfo(input, TranslatorAccess);

                    InsertTranslation(input, result);
                }
                catch (AddressNotFoundException addressNotFoundException)
                {
                    result = addressNotFoundException.Message;
                }


            return result;
        }

        private void InsertTranslation(string input, string result)
        {
            var translatedAddressEntity = new TranslatedAddressEntity(input) {Translation = result};
            var insertOperation = TableOperation.Insert(translatedAddressEntity);
            AddressTranslationTable.Execute(insertOperation);
        }

        internal TranslatedAddressEntity RetrieveTranslation(string text)
        {
            TranslatedAddressEntity result =null;
            var table = GetTable(TranslatedAddressEntity.TableName);
            TableOperation retrieveOperation = TableOperation.Retrieve<TranslatedAddressEntity>(TranslatedAddressEntity.DefaultPartitionKey, text);

            //TODO lowercase

            // Execute the retrieve operation.
            TableResult retrievedResult = table.Execute(retrieveOperation);
            if (retrievedResult.Result!=null)
            {
                result = (TranslatedAddressEntity)retrievedResult.Result;
            }
            else
            {
                result = RetrieveTranslation(text.ToUpper());
            }

            return result;
        }

        internal string RetrieveTranslationByQuery(string text)
        {
            string result = null;
            var table = GetTable(TranslatedAddressEntity.TableName);
            //var retrieveOperation = TableOperation.Retrieve<TranslatedAddressEntity>(text, TranslatedAddressEntity.DefaultPartitionKey);
            TableQuery<TranslatedAddressEntity> query = table.CreateQuery<TranslatedAddressEntity>();
            query.Where(TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.Equal, text));
            var queryResult = table.ExecuteQuery(query);
            // Execute the retrieve operation.
            //TableResult retrievedResult = table.Execute(retrieveOperation);
            foreach (var item in queryResult)
            {
                result = item.Translation;
            }
            return result;
        }

        public CloudTable GetTable(string tableName)
        {
            // Retrieve the storage account from the connection string.
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(
                CloudConfigurationManager.GetSetting("StorageConnectionString"));

            // Create the table client.
            CloudTableClient tableClient = storageAccount.CreateCloudTableClient();

            // Retrieve a reference to the table.
            CloudTable table = tableClient.GetTableReference(tableName);

            // Create the table if it doesn't exist.
           // table.CreateIfNotExists(); //TODO this is broken in azure

            return table;

        }

    }

    public class TranslationResponse
    {
        public string Translation { get; set; }
        public string Input { get; set; }
    }
}
