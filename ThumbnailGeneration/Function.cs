using Amazon.Lambda.Core;
using Amazon.Lambda.S3Events;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace ThumbnailGeneration;

public class Function
{
    IAmazonS3 S3Client { get; set; }

    /// <summary>
    /// Default constructor. This constructor is used by Lambda to construct the instance. When invoked in a Lambda environment
    /// the AWS credentials will come from the IAM role associated with the function and the AWS region will be set to the
    /// region the Lambda function is executed in.
    /// </summary>
    public Function()
    {
        S3Client = new AmazonS3Client();
    }

    /// <summary>
    /// Constructs an instance with a preconfigured S3 client. This can be used for testing outside of the Lambda environment.
    /// </summary>
    /// <param name="s3Client"></param>
    public Function(IAmazonS3 s3Client)
    {
        this.S3Client = s3Client;
    }

    /// <summary>
    /// This method is called for every Lambda invocation. This method takes in an S3 event object and can be used 
    /// to respond to S3 notifications.
    /// </summary>
    /// <param name="evnt"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    public async Task<string> FunctionHandler(S3Event evnt, ILambdaContext context)
    {
        var s3event = evnt.Records?[0].S3;
        if (s3event == null)
        {
            return null;
        }

        try
        {
            var rs = await this.S3Client.GetObjectMetadataAsync(s3event.Bucket.Name, s3event.Object.Key);
            if (rs.Headers.ContentType.StartsWith("image/"))
            {
                using GetObjectResponse res = await S3Client.GetObjectAsync(s3event.Bucket.Name, s3event.Object.Key);
                using Stream resStream = res.ResponseStream;
                using StreamReader reader = new StreamReader(resStream);
                using MemoryStream memstream = new MemoryStream();
                await resStream.CopyToAsync(memstream);
                var buffer = new byte[512];
                var bytesRead = default(int);
                while ((bytesRead = reader.BaseStream.Read(buffer, 0, buffer.Length)) > 0)
                    memstream.Write(buffer, 0, bytesRead);
                //perform image manipulation
                var transformedImgBase64 = ImagingOperations.GetConvertedImage(memstream.ToArray());
                var transformedImgBytes = Convert.FromBase64String(transformedImgBase64);
                PutObjectRequest putReq = new PutObjectRequest()
                {
                    BucketName = "bucket name here", //add your own bucket here
                    Key = $"thumbnail-{s3event.Object.Key}",
                    ContentType = "image/png",
                    InputStream = new MemoryStream(transformedImgBytes)
                };
                await S3Client.PutObjectAsync(putReq);
            }
            return rs.Headers.ContentType;
        }
        catch (Exception ex)
        {
            context.Logger.Log($"Error: {ex.Message}");
            throw;
        }
    }
}