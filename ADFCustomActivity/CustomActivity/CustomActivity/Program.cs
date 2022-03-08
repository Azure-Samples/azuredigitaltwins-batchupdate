using Azure;
using Azure.DigitalTwins.Core;
using Azure.Identity;
using Azure.Storage.Blobs;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;

namespace CustomActivity
{
    class Program
    {
        static void Main(string[] args)
        {
            Logger.WriteLine("Custom Activity running...");

            bool local = false;

            //The name of the custom activity configuration file.
            string activityFile = "activity.json";

            //Get the current runtime path.
            string path = Directory.GetCurrentDirectory();

            //If we are debugging locally, those environment variables will not be set. Lets tweak the names of the files.
            if (Environment.GetEnvironmentVariable("AZ_BATCH_TASK_WORKING_DIR") == null)
            {
                Logger.WriteLine("Environment variable not found.  Assuming debug and running locally.");
                //Get the current directory
                local = true;
                
                activityFile = Path.GetFullPath(Path.Combine(path, @"..\..\..\")) + "activity-local.json";
            }

            //Read in the custom activity configuration.
            dynamic activity = JsonConvert.DeserializeObject<ExpandoObject>(File.ReadAllText(activityFile), new ExpandoObjectConverter());

            
            //Read the mapping file name that was passed in.  If we're debugging, tweak the file path.
            var mappingFile = activity.typeProperties.extendedProperties.Mapping;
            if (local)
            {
                mappingFile = Path.GetFullPath(Path.Combine(path, @"..\..\..\")) + mappingFile;
            }
            //Load the data to twin mapping file.
            Logger.WriteLine("Loading data conversion mapping file.");
            DataMapping mapping = JsonConvert.DeserializeObject<DataMapping>(File.ReadAllText(mappingFile));

            //Get a credential to run as when connecting to Azure.
            var credential = new DefaultAzureCredential();
            

            Logger.WriteLine($"File to process: {activity.typeProperties.extendedProperties.Storage} | {activity.typeProperties.extendedProperties.Container} | {activity.typeProperties.extendedProperties.File}");

            // Download the twin updates contents and load it into a dictionary so we can 
            Logger.WriteLine("Downloading the data file locally.");
            BlobServiceClient blobServiceClient = new BlobServiceClient(new Uri(activity.typeProperties.extendedProperties.Storage), credential);
            BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(activity.typeProperties.extendedProperties.Container);
            BlobClient blobClient = containerClient.GetBlobClient(activity.typeProperties.extendedProperties.File);
            blobClient.DownloadTo(activity.typeProperties.extendedProperties.File);

            //Open the file and load it into a dictionary so we can retrieve name/value pairs.
            dynamic twinUpdateFile = JsonConvert.DeserializeObject<ExpandoObject>(File.ReadAllText(activity.typeProperties.extendedProperties.File), new ExpandoObjectConverter());
            IDictionary<string, object> twinUpdateDict = twinUpdateFile;

            Logger.WriteLine("Building json patch for twin updates");
            var updateTwinData = new JsonPatchDocument();


            //The Mapping Data Flow that extracts the data is configured to save each record as a file with the twin id as the file name.
            string twinId = activity.typeProperties.extendedProperties.File;
            //Loop through each data element, get the mapping attribute, convert the data, and add it to the json update payload.
            //foreach (string key in mapping.Mapping..Keys)
            foreach (DataAttribute dataAttribute in mapping.Mapping)
            {
                var adtData = "";
                if (dataAttribute.Source.Equals("$FILENAME"))
                {
                    adtData = activity.typeProperties.extendedProperties.File;
                }
                else
                {
                    adtData = Convert.ToString(twinUpdateDict[dataAttribute.Source]);
                }

                updateTwinData.AppendAdd(dataAttribute.Target, ConvertToADTDataType(dataAttribute.TargetType, adtData));
            }

            //Update digital twin.
            Logger.WriteLine("Updating ADT");
            var adtURI = activity.typeProperties.extendedProperties.ADT;
            Logger.WriteLine($"Azure Digital Twins URI {adtURI}");
            DigitalTwinsClient client = new DigitalTwinsClient(new Uri(adtURI), credential);
            client.UpdateDigitalTwin(twinId, updateTwinData);

        }

        private static object ConvertToADTDataType(string schema, string val)
        {
            switch (schema)
            {
                case "boolean":
                    return bool.Parse(val);
                case "double":
                    return double.Parse(val);
                case "float":
                    return float.Parse(val);
                case "integer":
                case "int":
                    return int.Parse(val);
                case "dateTime":
                    return DateTime.Parse(val);
                case "date":
                    return DateTime.Parse(val).Date;
                case "duration":
                    return int.Parse(val);
                case "string":
                default:
                    return val;
            }
        }
    }

    class DataMapping
    {
        public List<DataAttribute> Mapping { get; set; }
    }

    class DataAttribute
    {
        public string Source { get; set; }
        public string Target { get; set; }
        public string TargetType { get; set; }
    }
}
