using Microsoft.Azure;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using System.Web;
using System.Web.Http;
using TranslationRobot;
using TranslationRobot.Entity;

namespace WebRole1.Controllers
{

    public class TranslateController : ApiController
    {

        private TranslatorAccess TranslatorAccess
        {
            get
            {
                TranslatorAccess result;
                HttpContext context = HttpContext.Current;
                if (context != null && context.Application != null && context.Application.Contents["TranslatorAccess"] != null)
                {
                    result = context.Application.Contents["TranslatorAccess"] as TranslatorAccess;

                }
                else
                {
                    result = new TranslatorAccess();
                    if (context != null && context.Application != null) context.Application.Contents["TranslatorAccess"] = result;
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
                if (context != null && context.Application != null && context.Application.Contents["AddressTranslationTable"] != null)
                {
                    result = context.Application.Contents["AddressTranslationTable"] as CloudTable;

                }
                else
                {
                    result = GetTable("AddressTranslation");
                    if (context != null && context.Application != null) context.Application.Contents["AddressTranslationTable"] = result;
                }
                return result;
            }
        }


        [HttpGet]
        [Route("api/Translate/{text}")]
        public string Translate(string text)
        {
            string result=null;
            var table = GetTable("AddressTranslation");
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
            table.CreateIfNotExists();

            return table;

        }

    }
}
