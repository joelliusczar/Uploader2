using Amazon;
using Amazon.Runtime;
using Amazon.Runtime.Internal;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Uploader2
{
    public partial class Form1 : Form
    {
        public BindingList<UploadableItem> ListItems { get; set; }
        public ICollection<string> Uploaded { get; set; }
        public bool SpinnerRunning { get; set; }
        public bool PercentUpdaterRunning { get; set; }
        public int CurrentUploadPercentDone { get; set; }
        public long TotalSize { get; set; }
        private const string BUCKET = "";
        private const string PARENT_FOLDER = "pb";
        public Form1()
        {
            
            InitializeComponent();
        }


        private async void Form1_Load(object sender, EventArgs e)
        {
            ListItems = new BindingList<UploadableItem> {};
            dataGridView1.DataSource = ListItems;
            dataGridView1.AutoGenerateColumns = true;

            var spinner = RunSpinner();
            var uploaded = await UploadedObjects();
            this.Uploaded = uploaded;
            SpinnerRunning = false;
            await spinner;
            button1.BeginInvoke(new Action(() => {
                button1.Enabled = true;
            }));

        }

        private async Task<ICollection<string>> UploadedObjects()
        {
            var result = new HashSet<string>();
            var request = new ListObjectsV2Request
            {
                BucketName = BUCKET,
            };

            var bucketRegion = RegionEndpoint.USEast2;

            AWSConfigsS3.UseSignatureVersion4 = true;

            var client = new AmazonS3Client(bucketRegion);
            

            try
            {
                ListObjectsV2Response response;
                do
                {

                    response = await client.ListObjectsV2Async(request);
                    var items = response.S3Objects
                        .Where(o => o.Key.StartsWith(PARENT_FOLDER))
                        .Select(o => Path.GetFileName(o.Key))
                        .Where(o => o != null);

                    foreach(var item in items)
                    {
                        result.Add(item);
                    }

                    



                    // If the response is truncated, set the request ContinuationToken
                    // from the NextContinuationToken property of the response.
                    request.ContinuationToken = response.NextContinuationToken;
                }
                while (response.IsTruncated);

            }
            catch (AmazonS3Exception ex)
            {
                System.Diagnostics.Debug.Write(ex);
            }
            return result;
        }

        private string CalculateMD5(string filename)
        {
            System.Diagnostics.Debug.WriteLine("Calculating Digest");
            System.Diagnostics.Debug.WriteLine(Thread.CurrentThread.ManagedThreadId);
            using (var md5 = MD5.Create())
            {
               
                using (var stream = File.OpenRead(filename))
                {
                    var hash = md5.ComputeHash(stream);
                    return Convert.ToBase64String(hash);
                }
            }
        }

        private async Task<bool> UploadItem(UploadableItem item)
        {
            CurrentUploadPercentDone = 0;
            if (Uploaded?.Contains(item.AWSKey) ?? false)
            {
                item.Status = "Skipped!";
                return true;
            }
            item.Status = "Started";


            var request = new PutObjectRequest
            {
                BucketName = BUCKET,
                Key = $"{PARENT_FOLDER}/{item.AWSKey}",
                FilePath = item.Path,
            };
            request.MD5Digest = await Task.Run(() => {
                return CalculateMD5(item.Path);
            });
            request.StorageClass = S3StorageClass.GlacierInstantRetrieval;

            System.Diagnostics.Debug.WriteLine(request.MD5Digest);
            System.Diagnostics.Debug.WriteLine(Thread.CurrentThread.ManagedThreadId);

            var bucketRegion = RegionEndpoint.USEast2;

            request.StreamTransferProgress = OnUploadProgress;


            AWSConfigsS3.UseSignatureVersion4 = true;

            var client = new AmazonS3Client(bucketRegion);

            Task updaterTask = null;

            try
            { 
            
                var responseTask = client.PutObjectAsync(request);
                System.Diagnostics.Debug.WriteLine("Started");
                updaterTask = RunProgressUpdater(item);
                var response = await responseTask;

                

                if (response.HttpStatusCode == System.Net.HttpStatusCode.OK)
                {
                    Uploaded.Add(item.AWSKey);
                    item.Uploaded = "Yes";
                    item.Status = "Done!";
                    item.PercentDone = 100;
                    return true;
                }
                else
                {
                    item.Status = "Failed";
                    return false;
                }
            }
            catch (AmazonS3Exception)
            {
                item.Status = "Failed";
                return false;
            }
            finally
            {
                PercentUpdaterRunning = false;
                await updaterTask;
                request.StreamTransferProgress = null;
            }


        }

        private async void button1_Click(object sender, EventArgs e)
        {
            button1.Enabled = false;
            button1.Text = "Running";
            System.Diagnostics.Debug.WriteLine(Thread.CurrentThread.ManagedThreadId);
            var failureCount = 0;
            foreach (var item in ListItems)
            {
                System.Diagnostics.Debug.WriteLine("Next");
                System.Diagnostics.Debug.WriteLine(Thread.CurrentThread.ManagedThreadId);
                var result = await UploadItem(item);
                if (!result)
                {

                    failureCount++;
                }
            }

            if (failureCount == 0)
            {
                MessageBox.Show("Yay! Batch finished. No Errors");
            }
            else
            {
                MessageBox.Show($"Batch finished with {failureCount} Errors");
            }
        }

        public void OnUploadProgress(object sender, StreamTransferProgressArgs args)
        {
            CurrentUploadPercentDone = args.PercentDone;
        }

        private async Task RunProgressUpdater(UploadableItem item)
        {
            PercentUpdaterRunning = true;

            while (PercentUpdaterRunning)
            {
                item.PercentDone = CurrentUploadPercentDone;
                await Task.Delay(500);
            }

        }


        private IEnumerable<string> RecursiveFileAdd(string file)
        {
            var attr = File.GetAttributes(file);
            if (attr.HasFlag(FileAttributes.Directory))
            {
                foreach (var nestedItem in Directory.EnumerateFileSystemEntries(file))
                {
                    var nestedResults = RecursiveFileAdd(nestedItem);
                    foreach (var result in nestedResults)
                    {
                        yield return result;
                    }

                }
            }
            else 
            {
                yield return file;
            }
        }

        private void dataGridView1_DragDrop(object sender, DragEventArgs e)
        {

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                ListItems.RaiseListChangedEvents = false;
                foreach (var item in files)
                {
                    foreach (var file in RecursiveFileAdd(item))
                    {
                        ListItems.Add(new UploadableItem {
                            Path = file,
                            RootPath = item,
                            Uploaded = Uploaded?.Contains(Path.GetFileName(file)) ?? false ? "Yes" : "No",
                            Status = "Pending",
                            PercentDone = 0
                        });
                    }
                }
                ListItems.RaiseListChangedEvents = true;
                ListItems.ResetBindings();

                countLabel.Text = ListItems.Count().ToString();

                foreach (var item in ListItems)
                {
                    try
                    {
                        item.SizeMB = (new System.IO.FileInfo(item.Path).Length) / (1024 * 1000);
                        TotalSize += item.SizeMB;
                    }
                    catch (Exception)
                    { }
                }

                totalSizeLabel.Text = TotalSize.ToString() + "MB";

            }
        }

        private void dataGridView1_DragEnter_1(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Link;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        private async void dataGridView1_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex == 0)
            {
                await UploadItem(ListItems[e.RowIndex]);

            }
        }

        private async Task RunSpinner()
        {
            SpinnerRunning = true;
            var phase = 0;
            while(SpinnerRunning)
            {
                if(waitingLbl.InvokeRequired)
                {
                    waitingLbl.Invoke(new Action(() => {
                        if (phase == 0 % 4) waitingLbl.Text = "-";
                        if (phase == 1 % 4) waitingLbl.Text = "\\";
                        if (phase == 2 % 4) waitingLbl.Text = "|";
                        if (phase == 3 % 4) waitingLbl.Text = "/";
                    }));
                }
                else
                {
                    if (phase == 0 % 4) waitingLbl.Text = "-";
                    if (phase == 1 % 4) waitingLbl.Text = "\\";
                    if (phase == 2 % 4) waitingLbl.Text = "|";
                    if (phase == 3 % 4) waitingLbl.Text = "/";
                }
                phase++;
                System.Diagnostics.Debug.WriteLine($"loop {phase} {SpinnerRunning}");
                await Task.Delay(200);
            }

            waitingLbl.BeginInvoke(new Action(() =>
            {

                waitingLbl.Text = "Done";
            }));
        }

        private void clearButton_Click(object sender, EventArgs e)
        {
            ListItems.Clear();
            button1.Enabled = true;
            button1.Text = "Start";
            TotalSize = 0;
            totalSizeLabel.Text = "";
            countLabel.Text = ListItems.Count().ToString();
        }
    }
}
