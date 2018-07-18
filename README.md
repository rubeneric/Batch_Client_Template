# The Azure Batch client template

### When to use Azure Batch for your compute job?

1. Does your compute job take longer than you want to wait, or do you want to do more in less time?
2. Can you divide your job into many similar but independent tasks?

If the answer to these two questions is yes, then you are in luck.
The Azure Batch service is a very accessible way to scale out by parallelizing any application to virtual machines (VMs) in the cloud. 
Here we describe how to unlock this potential through a stand-alone solution that you can start using without the need for programming. It allows the user to plug in their own software by simply updating a configuration file. Compute jobs that would last several days can be reduced to an hour by deploying a pool of virtual machines in the cloud and running the task collection in parallel, distributed over these machines.

### Introduction

Azure Batch provides scheduling and management services to orchestrate the scaling of a compute job. To understand exactly what this 
means, we must first get the terminology clear. One must perform a compute job that can be divided into individual tasks. Each task 
makes use of the exact same application software, but may use unique input files, leading to a task-specific output. The first stage 
in running your Batch job therefore consists of defining your application, the division of your job into tasks, and the input for 
each task. In Figure 1 the entire process is shown schematically. 

Batch Service deploys a pool of VMs for you--commonly referred to as compute nodes or simply nodes--in Microsoft data centers and 
handles the division of your tasks over these nodes. When a node has finished a task, Azure Batch sends the next task in the queue to 
that node. If an error occurs on any node, the interrupted task is resumed on another node. This way, Azure Batch makes sure that 
your pool is used as efficiently and robustly possible.


![alt text](https://github.com/rubeneric/Batch_Client_Template/blob/master/images/batch_overview.png "Azure Batch schematic")

*Figure 1: Schematic functioning of Azure Batch. The batch pool and job are being defined and deployed from the user's personal computer.*


The number of noes in your pool, and the size of the nodes (i.e. the technical specifications of the VMs that are deployed by Batch), 
are for you to decide. A larger pool gets the job done faster, but may result in a larger number of added compute hours due to the 
overhead time of deploying and decommissioning your nodes. Since you pay per VM, per minute from the time you start your deployment, 
a smaller pool may therefore be cheaper. 

Similarly, choosing large compute nodes with many cores each may prove to be efficient if your application is optimized for 
parallelization on a single machine. In other cases, going with a large number of single-core machines will be advisable. What 
configuration suits the job can typically be estimated from your experience with the application, and when necessary, further optimized 
in a series of small-scale trial runs. More tips and tricks are presented in the section “Optimization and Cost Management” below.

### Getting started

1.	Setup your Azure services.  If you don’t already have one, you will need to set up an Azure subscription. Within that subscription, create an Azure Batch Service with a linked storage account.
2.	Download the Batch client template 
3.	Setup the configuration file. The “app.config” file found in the template folder contains the basic information needed to setup your job. Figure 2 shows which parameters need to be set. We will walk you through them here.


![alt text](https://github.com/rubeneric/Batch_Client_Template/blob/master/images/Config.png "Config file")

*Figure 2: the contents of the “AzureBatchTemplate.exe.config” configuration file that need to be set up.*


*	The first five parameters refer to the account properties of your Azure Batch and Storage account
*	The PoolID and JobID can be chosen at will to uniquely identify your pool and job, respectively
*	The applicationDirectory refers to the local folder that contains the application files, i.e. the files that are copied to every node upon startup since they are needed for each and every task
*	The inputDirectory refers to the local folder that contains the input files, i.e. the files that are needed to to run each specific task
*	The outputDirectory refers to the local folder where the output from each task will be downloaded to
*	The TaskNumber is the number of tasks that your job consists of
*	The PoolSize is the number of nodes Batch will deploy
*	The NodeSize is code (id) defining the technical specifications of each node. See Sizes for Cloud Services for an overview of the available options with ids
*	The TimeOutLimit sets a limit in minutes after which time your job is terminated to prevent it from running indefinitely if it does not finish successfully

### Optimization and cost management

The Azure Batch service itself is free of charge – you simply pay, per minute, for the virtual machines that are running in your pool. You are in full control of the number of virtual machines set up and for how long they run.  Therefore, it is critical to understand how to minimize the time they are running, and to understand the risk of unexpected costs should they not be properly shut down after use. Please mind the following best practices to ensure your Batch job runs as efficiently as possible.
1.	__Take the time to optimize your application.__
When you start running many instances of your application, any of bugs or inefficiencies within it will manifest themselves proportionally. Think carefully about how to optimize your application or compute scenario before getting started with Azure Batch.
2.	__Choose the best node and pool size for your job by running several short test scenarios.__
Depending on your application, faster nodes will not always deliver proportional results. If it is unclear how your application handles multiple processor cores, try running a set of small-scale benchmark runs on a range of node and pool sizes. Mark down the time it takes to deploy the nodes, as well as the total run time: that way, it is easy to extrapolate how long your full-scale job will run on each configuration.
[to be added: choosing the right pool size to fit your job]
3.	__Estimate the running costs of your job and set a timeout limit.__
Once you have a good understanding of how your application performs on the chosen nodes, it is easy to estimate the total running time, and with it the costs of your run. If you intend to leave the job running unattended, make sure to set a timeout limit, but do not to make it too tight.
4.	__Monitor your job at set time points__.
While the Azure Batch service is very robust in its distribution and backing up of tasks, mistakes are easily made when setting up your Batch job. It is therefore advisable to closely monitor the first stages of the job where the pool is deployed and the first tasks initiated. Batch Explorer is a good tool for doing so. This minimizes the impact of any errors.
5.	__Check if your pool is correctly decommissioned after job completion.__
Even if all tasks are performed and your job is completed with the desired output, a pool may remain active if not properly decommissioned. The Batch template presented here does this automatically upon job completion, but always check and confirm that the pool was successfully removed.




