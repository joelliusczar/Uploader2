using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Uploader2
{
    public partial class S3Tree : Form
    {
        public S3Tree()
        {
            Task.Run(async () => { 
                await LoadTree();
            });

            InitializeComponent();
        }

        private async Task LoadTree()
        {
            var request = new ListObjectsV2Request
            {
                BucketName = "enjoymentp",
                MaxKeys = 5,
            };

            var bucketRegion = RegionEndpoint.USEast2;

            AWSConfigsS3.UseSignatureVersion4 = true;

            var client = new AmazonS3Client(bucketRegion);

            try 
            {
                var response = await client.ListObjectsV2Async(request);
                do
                {

                    var items = response.S3Objects
                        .Where(o => o.Key.StartsWith("ftv"));


                    // If the response is truncated, set the request ContinuationToken
                    // from the NextContinuationToken property of the response.
                    request.ContinuationToken = response.NextContinuationToken;
                }
                while (response.IsTruncated);
            }
            catch(AmazonS3Exception ex)
            {
                System.Diagnostics.Debug.Write(ex);
            }
        }
    }
}
