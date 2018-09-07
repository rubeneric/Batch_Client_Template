namespace Microsoft.Azure.Batch.Samples.Client
{
    using System;
    using System.Configuration;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Azure.Batch;
    using Microsoft.Azure.Batch.Auth;
    using Microsoft.Azure.Batch.Common;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Blob;

    public class Program
    {
        // Read paramerers from app.config file
        private static readonly string BatchAccountName = ConfigurationManager.AppSettings["BatchAccountName"];     
        private static readonly string BatchAccountKey = ConfigurationManager.AppSettings["BatchAccountKey"];       
        private static readonly string BatchAccountUrl = ConfigurationManager.AppSettings["BatchAccountUrl"];        
        private static readonly string StorageAccountName = ConfigurationManager.AppSettings["StorageAccountName"];
        private static readonly string StorageAccountKey = ConfigurationManager.AppSettings["StorageAccountKey"];
        private static readonly string PoolId = ConfigurationManager.AppSettings["PoolID"];
        private static readonly string JobId = ConfigurationManager.AppSettings["JobID"];
        private static readonly string NodeSize = ConfigurationManager.AppSettings["NodeSize"];
        static int nodeNumberDedicated = int.Parse(ConfigurationManager.AppSettings["PoolSizeDedicated"]);       
        static int nodeNumberLowPriority = int.Parse(ConfigurationManager.AppSettings["PoolSizeLowPriority"]);   
        static int TimeOutLimit = int.Parse(ConfigurationManager.AppSettings["TimeOutLimit"]);
        static int filesperTask = int.Parse(ConfigurationManager.AppSettings["FilesPerTask"]);

        // Create parameters
        static int taskNumber = 0;
        static int skipInputFiles = 0;

        // Starts client and catches basic errors
        public static void Main(string[] args)
        {
            if (String.IsNullOrEmpty(BatchAccountName) || String.IsNullOrEmpty(BatchAccountKey) || String.IsNullOrEmpty(BatchAccountUrl) ||
                String.IsNullOrEmpty(StorageAccountName) || String.IsNullOrEmpty(StorageAccountKey))
            {
                throw new InvalidOperationException("One ore more account credential strings have not been populated. Please ensure that your Batch and Storage account credentials have been specified.");
            }

            try
            {
                // Call the asynchronous version of the Main() method. This is done so that we can await various
                // calls to async methods within the "Main" method of this console application.
                MainAsync().Wait();
            }
            catch (AggregateException ae)
            {
                Console.WriteLine();
                Console.WriteLine("One or more exceptions occurred.");
                Console.WriteLine();

                PrintAggregateException(ae);
            }
            finally
            {
                Console.WriteLine();
                Console.WriteLine("Sample complete, hit ENTER to exit...");
                Console.ReadLine();
            }
        }

        /// <summary>
        /// Provides an asynchronous version of the Main method, allowing for the awaiting of async method calls within.
        /// </summary>
        /// <returns>A <see cref="System.Threading.Tasks.Task"/> object that represents the asynchronous operation.</returns>
        private static async Task MainAsync()
        {
            Console.WriteLine("Sample start: {0}", DateTime.Now);
            Console.WriteLine();
            Stopwatch timer = new Stopwatch();
            timer.Start();

            // Construct the Storage account connection string
            string storageConnectionString = String.Format("DefaultEndpointsProtocol=https;AccountName={0};AccountKey={1}",
                                                            StorageAccountName, StorageAccountKey);

            // Retrieve the storage account
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(storageConnectionString);

            // Create the blob client, for use in obtaining references to blob storage containers
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();

            // Use the blob client to create the containers in Azure Storage if they don't yet exist
            const string appContainerName = "application";
            const string inputContainerName = "input";
            const string outputContainerName = "output";

            await CreateContainerIfNotExistAsync(blobClient, appContainerName);
            await CreateContainerIfNotExistAsync(blobClient, inputContainerName);
            await CreateContainerIfNotExistAsync(blobClient, outputContainerName);

            // Upload the application and its dependencies to Azure Storage. This is the application that will
            // process the data files, and will be executed by each of the tasks on the compute nodes.
            List<ResourceFile> applicationFiles = await ListFilesInContainerAsync(blobClient, appContainerName);

            // Upload the data files. This is the data that will be processed by each of the tasks that are
            // executed on the compute nodes within the pool.
            List<ResourceFile> inputFiles = await ListInputFilesInContainerAsync(blobClient, inputContainerName);

            taskNumber = inputFiles.Count() / filesperTask;

            Console.WriteLine("Number of input files found in input container: {0}", inputFiles.Count());
            Console.WriteLine("Input files per task defined by user: {0}", filesperTask);
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("--> Nuber of tasks: {0}", taskNumber);
            Console.ResetColor();
            Console.WriteLine();



            // Obtain a shared access signature that provides write access to the output container to which
            // the tasks will upload their output.
            string outputContainerSasUrl = GetContainerSasUrl(blobClient, outputContainerName, SharedAccessBlobPermissions.Write);

            // Create a BatchClient. We'll now be interacting with the Batch service in addition to Storage
            BatchSharedKeyCredentials cred = new BatchSharedKeyCredentials(BatchAccountUrl, BatchAccountName, BatchAccountKey);
            using (BatchClient batchClient = BatchClient.Open(cred))
            {
                await CreateJobAsync(batchClient, JobId, PoolId);

                // Add the tasks to the job. We need to supply a container shared access signature for the
                // tasks so that they can upload their output to Azure Storage.
                await AddTasksAsync(batchClient, JobId, inputFiles, taskNumber, outputContainerSasUrl);

                // Create the pool that will contain the compute nodes that will execute the tasks.
                // The ResourceFile collection that we pass in is used for configuring the pool's StartTask
                // which is executed each time a node first joins the pool (or is rebooted or reimaged).
                await CreatePoolAsync(batchClient, PoolId, applicationFiles);

                // Monitor task success/failure, specifying a maximum amount of time to wait for the tasks to complete
                MonitorTasks(batchClient, JobId, TimeSpan.FromMinutes(TimeOutLimit));

                // Print out some timing info
                timer.Stop();
                Console.WriteLine();
                Console.WriteLine("Sample end: {0}", DateTime.Now);
                Console.WriteLine("Elapsed time: {0}", timer.Elapsed);

                // // Clean up Batch resources 
                // Console.WriteLine();
                // Console.WriteLine("Deleting pool [{0}]", PoolId);
                // await batchClient.PoolOperations.DeletePoolAsync(PoolId);
                // Console.WriteLine("Deleting job [{0}]", JobId);
                // await batchClient.JobOperations.DeleteJobAsync(JobId);

                // Clean up Batch resources on prompt
                Console.WriteLine();
                Console.Write("Delete job? [yes] no: ");
                string response = Console.ReadLine().ToLower();
                if (response != "n" && response != "no")
                {
                    batchClient.JobOperations.DeleteJob(JobId);
                }

                Console.Write("Delete pool? [yes] no: ");
                response = Console.ReadLine().ToLower();
                if (response != "n" && response != "no")
                {
                    batchClient.PoolOperations.DeletePool(PoolId);
                } 
     

            }
        }

        /// <summary>
        /// Creates a container with the specified name in Blob storage, unless a container with that name already exists.
        /// </summary>
        /// <param name="blobClient">A <see cref="Microsoft.WindowsAzure.Storage.Blob.CloudBlobClient"/>.</param>
        /// <param name="containerName">The name for the new container.</param>
        /// <returns>A <see cref="System.Threading.Tasks.Task"/> object that represents the asynchronous operation.</returns>
        private static async Task CreateContainerIfNotExistAsync(CloudBlobClient blobClient, string containerName)
        {
            CloudBlobContainer container = blobClient.GetContainerReference(containerName);

            if (await container.CreateIfNotExistsAsync())
            {
                Console.WriteLine("Container [{0}] created.", containerName);
            }
            else
            {
                Console.WriteLine("Container [{0}] exists, skipping creation.", containerName);
            }
        }

        /// <summary>
        /// Returns a shared access signature (SAS) URL providing the specified permissions to the specified container.
        /// </summary>
        /// <param name="blobClient">A <see cref="Microsoft.WindowsAzure.Storage.Blob.CloudBlobClient"/>.</param>
        /// <param name="containerName">The name of the container for which a SAS URL should be obtained.</param>
        /// <param name="permissions">The permissions granted by the SAS URL.</param>
        /// <returns>A SAS URL providing the specified access to the container.</returns>
        /// <remarks>The SAS URL provided is valid for 2 hours from the time this method is called. The container must
        /// already exist within Azure Storage.</remarks>
        private static string GetContainerSasUrl(CloudBlobClient blobClient, string containerName, SharedAccessBlobPermissions permissions)
        {
            // Set the expiry time and permissions for the container access signature. In this case, no start time is specified,
            // so the shared access signature becomes valid immediately
            SharedAccessBlobPolicy sasConstraints = new SharedAccessBlobPolicy
            {
                SharedAccessExpiryTime = DateTime.UtcNow.AddMinutes(TimeOutLimit),
                Permissions = permissions
            };

            // Generate the shared access signature on the container, setting the constraints directly on the signature
            CloudBlobContainer container = blobClient.GetContainerReference(containerName);
            string sasContainerToken = container.GetSharedAccessSignature(sasConstraints);

            // Return the URL string for the container, including the SAS token
            return String.Format("{0}{1}", container.Uri, sasContainerToken);
        }

        /// <summary>
        /// Makes a list of the specified files in the specified Blob container, returning a corresponding
        /// collection of <see cref="ResourceFile"/> objects appropriate for assigning to a task's
        /// <see cref="CloudTask.ResourceFiles"/> property.
        /// </summary>
        /// <param name="blobClient">A <see cref="Microsoft.WindowsAzure.Storage.Blob.CloudBlobClient"/>.</param>
        /// <param name="containerName">The name of the blob storage container to which the files should be uploaded.</param>
        /// <param name="filePaths">A collection of paths of the files to be uploaded to the container.</param>
        /// <returns>A collection of <see cref="ResourceFile"/> objects.</returns>
        private static async Task<List<ResourceFile>> ListFilesInContainerAsync(CloudBlobClient blobClient, string containerName)
        {
            List<ResourceFile> resourceFiles = new List<ResourceFile>();
           
            CloudBlobContainer container = blobClient.GetContainerReference(containerName);
            var filePaths = new List<string>();
            var blobs = container.ListBlobs().OfType<CloudBlockBlob>().ToList();

            foreach (var blob in blobs)
            {
                var blobFileName = blob.Uri.Segments.Last();
                filePaths.Add(blobFileName);
            }

            CloudBlockBlob blockBlob = container.GetBlockBlobReference("BlobName.tex");

            //Gets List of Blobs
            var list = container.ListBlobs();
            List<string> blobNames = list.OfType<CloudBlockBlob>().Select(b => b.Name).ToList();

            Console.WriteLine();
            Console.Write("{0} files found in {1} container.", filePaths.Count(), containerName);

            foreach (string filePath in filePaths)
            {
                resourceFiles.Add(await CreateSASKey(blobClient, containerName, filePath));
            }

            return resourceFiles;
        }
        
        //Same as above, but with the option to skip input files if the job has already been partially executed
        private static async Task<List<ResourceFile>> ListInputFilesInContainerAsync(CloudBlobClient blobClient, string inputContainerName)
        {
            List<ResourceFile> resourceFiles = new List<ResourceFile>();
            
            Console.WriteLine();
            Console.Write("Discovering input files in input container. Please wait...");

           CloudBlobContainer container = blobClient.GetContainerReference(inputContainerName);
            var filePaths = new List<string>();
            var blobs = container.ListBlobs().OfType<CloudBlockBlob>().ToList();

           

            foreach (var blob in blobs)
            {
                var blobFileName = blob.Uri.Segments.Last();
                filePaths.Add(blobFileName);
            }

            CloudBlockBlob blockBlob = container.GetBlockBlobReference("BlobName.tex");

            //Gets List of Blobs
            var list = container.ListBlobs();
            List<string> blobNames = list.OfType<CloudBlockBlob>().Select(b => b.Name).ToList();

            Console.Write("{0} input files found.", filePaths.Count());

            ////The 'Skip' function with the parameter skipInputFiles = x let you start the job after x input files.
            ////This is useful when restarting a job that was only partially successfull.
            ////Using the 'Take' function, you can select a limited range of input files. 
            ////In this example, we only use files 41791 to 41800

            //foreach (string filePath in filePaths.Skip(41790).Take(10))
            //{
            //    resourceFiles.Add(await CreateSASKey(blobClient, inputContainerName, filePath));
            //}

            foreach (string filePath in filePaths.Skip(skipInputFiles))
            {
                resourceFiles.Add(await CreateSASKey(blobClient, inputContainerName, filePath));
            }


            Console.WriteLine();
            Console.WriteLine(); Console.WriteLine("{0} resource files of {1} input files used", resourceFiles.Count, filePaths.Count());
            Console.WriteLine();

            return resourceFiles;

        }

        /// <summary>
        /// Uploads the specified file to the specified Blob container.
        /// </summary>
        /// <param name="inputFilePath">The full path to the file to upload to Storage.</param>
        /// <param name="blobClient">A <see cref="Microsoft.WindowsAzure.Storage.Blob.CloudBlobClient"/>.</param>
        /// <param name="containerName">The name of the blob storage container to which the file should be uploaded.</param>
        /// <returns>A <see cref="Microsoft.Azure.Batch.ResourceFile"/> instance representing the file within blob storage.</returns>
        private async static Task<ResourceFile> CreateSASKey(CloudBlobClient blobClient, string containerName, string filePath)
        {

            string blobName = Path.GetFileName(filePath);

            CloudBlobContainer container = blobClient.GetContainerReference(containerName);
            CloudBlockBlob blobData = container.GetBlockBlobReference(blobName);

            // Set the expiry time and permissions for the blob shared access signature. In this case, no start time is specified,
            // so the shared access signature becomes valid immediately
            SharedAccessBlobPolicy sasConstraints = new SharedAccessBlobPolicy
            {
                SharedAccessExpiryTime = DateTime.UtcNow.AddMinutes(TimeOutLimit),

                Permissions = SharedAccessBlobPermissions.Read
            };

            // Construct the SAS URL for blob
            string sasBlobToken = blobData.GetSharedAccessSignature(sasConstraints);
            string blobSasUri = String.Format("{0}{1}", blobData.Uri, sasBlobToken);

            return new ResourceFile(blobSasUri, blobName);
        }



        /// <summary>
        /// Creates a <see cref="CloudPool"/> with the specified id and configures its StartTask with the
        /// specified <see cref="ResourceFile"/> collection.
        /// </summary>
        /// <param name="batchClient">A <see cref="BatchClient"/>.</param>
        /// <param name="poolId">The id of the <see cref="CloudPool"/> to create.</param>
        /// <param name="resourceFiles">A collection of <see cref="ResourceFile"/> objects representing blobs within
        /// a Storage account container. The StartTask will download these files from Storage prior to execution.</param>
        /// <returns>A <see cref="System.Threading.Tasks.Task"/> object that represents the asynchronous operation.</returns>

        private static async Task CreatePoolAsync(BatchClient batchClient, string poolId, IList<ResourceFile> resourceFiles)
        {

            Console.WriteLine("Creating pool [{0}]...", poolId);

            // Set the OS and version of the VM image
            ImageReference imageReference = new ImageReference(
                    publisher: "MicrosoftWindowsServer",
                    offer: "WindowsServer",
                    sku: "2016-Datacenter",
                    version: "latest");

            VirtualMachineConfiguration virtualMachineConfiguration =
            new VirtualMachineConfiguration(
                imageReference: imageReference,
                nodeAgentSkuId: "batch.node.windows amd64");

            // Create the unbound pool. Until we call CloudPool.Commit() or CommitAsync(), no pool is actually created in the
            // Batch service. This CloudPool instance is therefore considered "unbound," and we can modify its properties.
            CloudPool pool = batchClient.PoolOperations.CreatePool(
                poolId: poolId,
                targetDedicatedComputeNodes: nodeNumberDedicated,           // Number of dedicated compute nodes
                targetLowPriorityComputeNodes: nodeNumberLowPriority,       // Number of low priority compute nodes  
                virtualMachineSize: NodeSize,                               // Node instance size
                virtualMachineConfiguration: virtualMachineConfiguration);                   // Windows Server 2012 R2

            // Create and assign the StartTask that will be executed when compute nodes join the pool.
            // In this case, we copy the StartTask's resource files (that will be automatically downloaded
            // to the node by the StartTask) into the shared directory that all tasks will have access to.
            pool.StartTask = new StartTask

            {
                // Specify a command line for the StartTask that copies the task application files to the
                // node's shared directory. Every compute node in a Batch pool is configured with a number
                // of pre-defined environment variables that can be referenced by commands or applications
                // run by tasks.

                CommandLine = "cmd /c starttask.cmd",

                ResourceFiles = resourceFiles,
                WaitForSuccess = true
            };
            await pool.CommitAsync();
        }

        /// <summary>
        /// Creates a job in the specified pool.
        /// </summary>
        /// <param name="batchClient">A <see cref="BatchClient"/>.</param>
        /// <param name="jobId">The id of the job to be created.</param>
        /// <param name="poolId">The id of the <see cref="CloudPool"/> in which to create the job.</param>
        /// <returns>A <see cref="System.Threading.Tasks.Task"/> object that represents the asynchronous operation.</returns>
        private static async Task CreateJobAsync(BatchClient batchClient, string jobId, string poolId)
        {
            Console.WriteLine("Creating job [{0}]...", jobId);

            CloudJob job = batchClient.JobOperations.CreateJob();
            job.Id = jobId;
            job.PoolInformation = new PoolInformation { PoolId = poolId };

            await job.CommitAsync();
        }

        /// <summary>
        /// Creates tasks to process each of the specified input files, and submits them to the
        /// specified job for execution.
        /// </summary>
        /// <param name="batchClient">A <see cref="BatchClient"/>.</param>
        /// <param name="jobId">The id of the job to which the tasks should be added.</param>
        /// <param name="inputFiles">A collection of <see cref="ResourceFile"/> objects representing the input files to be
        /// processed by the tasks executed on the compute nodes.</param>
        /// <param name="outputContainerSasUrl">The shared access signature URL for the container within Azure Storage that
        /// will receive the output files created by the tasks.</param>
        /// <returns>A collection of the submitted tasks.</returns>
        private static async Task<List<CloudTask>> AddTasksAsync(BatchClient batchClient, string jobId, List<ResourceFile> inputFiles, int taskNumber, string outputContainerSasUrl)
        {
            Console.WriteLine("Adding {0} tasks to job [{1}]...", taskNumber, jobId);

            // Create a collection to hold the tasks that we'll be adding to the job
            List<CloudTask> tasks = new List<CloudTask>();

            // Create each of the tasks. Because we copied the task application to the
            // node's shared directory with the pool's StartTask, we can access it via
            // the shared directory on whichever node each task will run.

            for (int i = 1; i <= taskNumber; i++)
            {
                // Generate taskIDs
                string taskId = "taskNumber" + i.ToString();

                // Define the task command line. Storage credentials are inserted as parameters
                // for the .cmd-file to enable uploading from the node to the output container
                string taskCommandLine = "cmd /c task.cmd" + " " + StorageAccountName + " " + StorageAccountKey;

                CloudTask task = new CloudTask(taskId, taskCommandLine);

                task.ResourceFiles = inputFiles.GetRange(filesperTask * (i - 1), filesperTask);

                tasks.Add(task);

            };


            // Add the tasks as a collection opposed to a separate AddTask call for each. Bulk task submission
            // helps to ensure efficient underlying API calls to the Batch service.
            await batchClient.JobOperations.AddTaskAsync(jobId, tasks);

            return tasks;
        }

        /// <summary>
        /// Monitors the specified tasks for completion and returns a value indicating whether all tasks completed successfully
        /// within the timeout period.
        /// </summary>
        /// <param name="batchClient">A <see cref="BatchClient"/>.</param>
        /// <param name="jobId">The id of the job containing the tasks that should be monitored.</param>
        /// <param name="timeout">The period of time to wait for the tasks to reach the completed state.</param>
        /// <returns><c>true</c> if all tasks in the specified job completed with an exit code of 0 within the specified timeout period, otherwise <c>false</c>.</returns>

        private static void MonitorTasks(BatchClient batchClient, string jobId, TimeSpan timeout)
        {
            bool allTasksSuccessful = true;
            const string successMessage = "All tasks reached state Completed.";
            const string failureMessage = "One or more tasks failed to reach the Completed state within the timeout period.";

            // Obtain the collection of tasks currently managed by the job. 
            // Use a detail level to specify that only the "id" property of each task should be populated. 
            // See https://docs.microsoft.com/en-us/azure/batch/batch-efficient-list-queries

            ODATADetailLevel detail = new ODATADetailLevel(selectClause: "id");

            IEnumerable<CloudTask> addedTasks = batchClient.JobOperations.ListTasks(jobId, detail);

            Console.WriteLine("Monitoring all tasks for 'Completed' state, timeout in {0}...", timeout.ToString());

            // We use a TaskStateMonitor to monitor the state of our tasks. In this case, we will wait for all tasks to
            // reach the Completed state.

            TaskStateMonitor taskStateMonitor = batchClient.Utilities.CreateTaskStateMonitor();
            try
            {
                batchClient.Utilities.CreateTaskStateMonitor().WaitAll(addedTasks, TaskState.Completed, timeout);
            }
            catch (TimeoutException)
            {
                batchClient.JobOperations.TerminateJob(jobId, failureMessage);
                Console.WriteLine(failureMessage);
            }
            batchClient.JobOperations.TerminateJob(jobId, successMessage);

            // All tasks have reached the "Completed" state, however, this does not guarantee all tasks completed successfully.
            // Here we further check each task's ExecutionInformation property to ensure that it did not encounter a scheduling error
            // or return a non-zero exit code.

            // Update the detail level to populate only the task id and executionInfo properties.
            detail.SelectClause = "id, executionInfo";

            IEnumerable<CloudTask> completedTasks = batchClient.JobOperations.ListTasks(jobId, detail);

            foreach (CloudTask task in completedTasks)
            {
                if (task.ExecutionInformation.Result == TaskExecutionResult.Failure)
                {
                    // A task with failure information set indicates there was a problem with the task. It is important to note that
                    // the task's state can be "Completed," yet still have encountered a failure.

                    allTasksSuccessful = false;

                    Console.WriteLine("WARNING: Task [{0}] encountered a failure: {1}", task.Id, task.ExecutionInformation.FailureInformation.Message);
                    if (task.ExecutionInformation.ExitCode != 0)
                    {
                        // A non-zero exit code may indicate that the application executed by the task encountered an error
                        // during execution. As not every application returns non-zero on failure by default (e.g. robocopy),
                        // your implementation of error checking may differ from this example.

                        Console.WriteLine("WARNING: Task [{0}] returned a non-zero exit code - this may indicate task execution or completion failure.", task.Id);
                    }
                }
            }

            if (allTasksSuccessful)
            {
                Console.WriteLine("Success! All tasks completed successfully within the specified timeout period. Output files uploaded to output container.");
            }
        }

        /// <summary>
        /// Processes all exceptions inside an <see cref="AggregateException"/> and writes each inner exception to the console.
        /// </summary>
        /// <param name="aggregateException">The <see cref="AggregateException"/> to process.</param>
        public static void PrintAggregateException(AggregateException aggregateException)
        {
            // Flatten the aggregate and iterate over its inner exceptions, printing each
            foreach (Exception exception in aggregateException.Flatten().InnerExceptions)
            {
                Console.WriteLine(exception.ToString());
                Console.WriteLine();
            }
        }
    }
}
