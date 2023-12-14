using Amazon.Lambda.Core;
using Amazon.Lambda.S3Events;
using Amazon.Rekognition;
using Amazon.Rekognition.Model;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Amazon.S3;


[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace LabelDetection;

public class Function
{
    IAmazonRekognition rekognitionClient = new AmazonRekognitionClient();
    IAmazonDynamoDB dynamoClient = new AmazonDynamoDBClient();

    public async Task FunctionHandler(S3Event s3Event, ILambdaContext context)
    {
        if(s3Event == null)
        {
            context.Logger.LogLine("s3Event is null");
        }


        if(s3Event.Records == null || !s3Event.Records.Any()) 
        {
            context.Logger.LogLine("s3event.Records is null or empty");
        }
        foreach (var record in s3Event.Records)
        {
            // Check if any of the record's properties are null
            if (record?.S3?.Bucket?.Name == null || record?.S3?.Object?.Key == null)
            {
                context.Logger.LogLine("Bucket name or Object key is null");
                continue;
            }
            var bucket = record.S3.Bucket.Name;
            var key = record.S3.Object.Key;

            string imageUrl = $"https://{bucket}.s3.amazonaws.com/{key}";
            try {
                var s3client = new AmazonS3Client();
                var objMetaData = await s3client.GetObjectMetadataAsync(bucket, key);


                //detect labels
                var labels = await DetectLabels(bucket, key);

                if (labels == null || objMetaData == null)
                {
                    context.Logger.LogLine("Labels detection or object metadata retrieval failed");
                    return;
                }

                long size = objMetaData.ContentLength;
                string format = key.Split('.').Last();

                //store in DynamoDB
                await StoreLabelsInDB(key, labels, imageUrl, bucket, size, format);
            }
            catch (Exception ex)
            {
                context.Logger.LogLine($"Error processing recordS: {ex.Message}");
                throw;
            }
            }
    }

    private async Task<List<Label>> DetectLabels(string bucket, string key)
    {
        var detectLabelsReq = new DetectLabelsRequest
        {
            Image = new Image
            {
                S3Object = new S3Object
                {
                    Bucket = bucket,
                    Name = key
                }
            },
            MinConfidence = 90
        };
        var res = await rekognitionClient.DetectLabelsAsync(detectLabelsReq);
        return res.Labels;
    }

    private async Task StoreLabelsInDB(string key, List<Label> labels, string imageUrl, string sourceBucket, long size, string format)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            { "imageId", new AttributeValue { S = key } },
            { "imageUrl", new AttributeValue { S = imageUrl} },
            { "labels", new AttributeValue { S = JsonConvert.SerializeObject(labels) } },
            { "timestamp", new AttributeValue { S = DateTime.UtcNow.ToString("o")} },
            { "sourceBucket", new AttributeValue { S = sourceBucket } },
            { "size" , new AttributeValue { N = size.ToString()} },
            {"format", new AttributeValue { S = format } },
            {"processedFlag", new AttributeValue { BOOL = true } },

        };

        var req = new PutItemRequest
        {
            TableName = "Images",
            Item = item
        };

        await dynamoClient.PutItemAsync(req);
    }

}
