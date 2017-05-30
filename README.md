# The Azure Batch client template

**Introduction**

The Azure Batch service is a very accessible way to scale out by parallelizing any application to virtual machines (VMs) in the cloud. 
Here we describe how to unlock this potential through a stand-alone solution that does not require any programming. It allows the user 
to plug in their own software by simply updating a configuration file. Compute jobs that would last several days can be reduced to an 
hour by deploying a pool of virtual machines in the cloud and running the task collection in parallel, distributed over these machines.


Azure Batch provides scheduling and management services to orchestrate the scaling of a compute job. To understand exactly what this 
means, we must first get the terminology clear. One must perform a compute job that can be divided into individual tasks. Each task 
makes use of the exact same application software, but may use unique input files, leading to a task-specific output. The first stage 
in running your Batch job therefore consists of defining your application, the division of your job into tasks, and the input for 
each task. In Figure 1 the entire process is shown schematically. 


Batch Service deploys a pool of VMs for you--commonly referred to as compute nodes or simply nodes--in Microsoft data centers and 
handles the division of your tasks over these nodes. When a node has finished a task, Azure Batch sends the next task in the queue to 
that node. If an error occurs on any node, the interrupted task is resumed on another node. This way, Azure Batch makes sure that 
your pool is used as efficiently and robustly possible.

*Figure 1: Schematic functioning of Azure Batch. The batch pool and job are being defined and deployed from the user's personal computer.*

The number of noes in your pool, and the size of the nodes (i.e. the technical specifications of the VMs that are deployed by Batch), 
are for you to decide. A larger pool gets the job done faster, but may result in a larger number of added compute hours due to the 
overhead time of deploying and decommissioning your nodes. Since you pay per VM, per minute from the time you start your deployment, 
a smaller pool may therefore be cheaper. 

Similarly, choosing large compute nodes with many cores each may prove to be efficient if your application is optimized for 
parallelization on a single machine. In other cases, going with a large number of single-core machines will be advisable. What 
configuration suits the job can typically be estimated from your experience with the application, and when necessary, further optimized 
in a series of small-scale trial runs. More tips and tricks are presented in the section “Optimization and Cost Management” below.



