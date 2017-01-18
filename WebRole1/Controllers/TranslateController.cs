using Microsoft.Azure;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using System.Web;
using System.Web.Http;
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


        [HttpGet]
        [Route("api/Translate/{text}")]
        public string Translate(string text)
        {
            string result=null;
            result = RetrieveTranslation(text);
            if (result == null)
            {


                try
                {
                    result = LocationInfo.GetLocationInfo(text, TranslatorAccess);

                    var translatedAddressEntity = new TranslatedAddressEntity(text);
                    translatedAddressEntity.Translation = result;

                    var insertOperation = TableOperation.Insert(translatedAddressEntity);
                    AddressTranslationTable.Execute(insertOperation);

                }
                catch (AddressNotFoundException addressNotFoundException)
                {
                    result = addressNotFoundException.Message;
                }
            }
          

            return result;
        }

        internal string RetrieveTranslation(string text)
        {
            string result=null;
            var table = GetTable(TranslatedAddressEntity.TableName);
            TableOperation retrieveOperation = TableOperation.Retrieve<TranslatedAddressEntity>(TranslatedAddressEntity.DefaultPartitionKey, text);

            // Execute the retrieve operation.
            TableResult retrievedResult = table.Execute(retrieveOperation);
            if (retrievedResult.Result!=null)
            {
                result = ((TranslatedAddressEntity)retrievedResult.Result).Translation;
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
}
